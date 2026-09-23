using BatteryBuddy.Core.Samsung;

namespace BatteryBuddy.Core.Tests;

public class SamsungProtocolTests
{
    // Captured from the Galaxy Buds QuickControls log on 2026-09-23.
    const string Status = "FD-0D-00-60-02-63-63-01-00-11-5C-00-00-00-EA-8C-DD";
    const string Extended = "FD-40-00-61-04-08-64-64-01-00-11-5C-00-00-FF-22-01-00-54-01-54-01-03-00-04-99-00-04-04-10-00-01-00-00-11-02-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-01-00-02-00-02-00-00-FF-01-01-00-00-00-00-FC-39-DD";
    const string ExtendedCaseUnknown = "FD-40-00-61-04-08-5E-5D-01-00-11-00-00-00-FF-22-01-00-54-01-54-01-03-00-04-99-00-04-04-10-00-01-00-00-11-02-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-01-00-02-00-02-00-00-FF-01-01-00-00-00-00-0D-AD-DD";

    static byte[] Hex(string s) => Convert.FromHexString(s.Replace("-", ""));

    [Fact]
    public void Crc16_matches_captured_frame() =>
        Assert.Equal(0x8CEA, Crc16.Compute(Hex("60-02-63-63-01-00-11-5C-00-00-00")));

    [Fact]
    public void Decoder_reads_single_frame()
    {
        var frame = Assert.Single(new FrameDecoder().Push(Hex(Status)));
        Assert.Equal(0x60, frame.MessageId);
        Assert.Equal(10, frame.Payload.Length);
    }

    [Fact]
    public void Decoder_handles_frame_split_across_chunks()
    {
        var bytes = Hex(Status);
        var decoder = new FrameDecoder();
        Assert.Empty(decoder.Push(bytes.AsSpan(0, 5)));
        Assert.Single(decoder.Push(bytes.AsSpan(5)));
    }

    [Fact]
    public void Decoder_handles_two_frames_in_one_chunk()
    {
        var frames = new FrameDecoder().Push(Hex(Status).Concat(Hex(Extended)).ToArray());
        Assert.Equal(new byte[] { 0x60, 0x61 }, frames.Select(f => f.MessageId));
    }

    [Fact]
    public void Decoder_skips_garbage_before_frame() =>
        Assert.Single(new FrameDecoder().Push(Hex("00-11-22-" + Status)));

    [Fact]
    public void Decoder_rejects_bad_crc_and_recovers()
    {
        var corrupt = Hex(Status);
        corrupt[^2] ^= 0xFF;
        var decoder = new FrameDecoder();
        var frames = decoder.Push(corrupt.Concat(Hex(Status)).ToArray());
        Assert.Single(frames);
        Assert.Equal(1, decoder.RejectedFrames);
    }

    [Fact]
    public void Encoder_builds_extended_status_ack_seen_in_logs() =>
        Assert.Equal(Hex("FD-04-10-61-00-1B-38-DD"), FrameEncoder.ExtendedStatusAck());

    [Fact]
    public void Encoder_output_round_trips_through_decoder()
    {
        var frame = Assert.Single(new FrameDecoder().Push(FrameEncoder.Encode(0x42, new byte[] { 1, 2, 3 })));
        Assert.Equal(0x42, frame.MessageId);
        Assert.Equal(new byte[] { 1, 2, 3 }, frame.Payload);
    }

    [Fact]
    public void StatusUpdated_parses_levels_and_wear()
    {
        var detail = StatusParser.TryParse(new FrameDecoder().Push(Hex(Status))[0]);
        Assert.Equal(new BatteryBuddy.Core.Devices.BudsDetail(99, 99, 92, true, true), detail);
    }

    [Fact]
    public void ExtendedStatus_parses_levels()
    {
        var detail = StatusParser.TryParse(new FrameDecoder().Push(Hex(Extended))[0]);
        Assert.Equal(new BatteryBuddy.Core.Devices.BudsDetail(100, 100, 92, true, true), detail);
    }

    [Fact]
    public void ExtendedStatus_case_zero_is_unknown()
    {
        var detail = StatusParser.TryParse(new FrameDecoder().Push(Hex(ExtendedCaseUnknown))[0])!;
        Assert.Equal(94, detail.Left);
        Assert.Equal(93, detail.Right);
        Assert.Null(detail.Case);
    }

    [Fact]
    public void Unknown_message_returns_null() =>
        Assert.Null(StatusParser.TryParse(new SamsungFrame(0x42, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0)));

    [Fact]
    public void Short_payload_returns_null() =>
        Assert.Null(StatusParser.TryParse(new SamsungFrame(0x60, new byte[] { 2, 99 }, 0)));

    [Theory]
    [InlineData("Buds3 Pro de Roberto", true)]
    [InlineData("Galaxy Buds2", true)]
    [InlineData("WF-C500", false)]
    public void Recognizes_galaxy_buds_names(string name, bool expected) =>
        Assert.Equal(expected, SamsungBuds.IsGalaxyBudsName(name));
}
