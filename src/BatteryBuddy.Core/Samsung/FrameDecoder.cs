namespace BatteryBuddy.Core.Samsung;

/// <summary>Incremental decoder: feed arbitrary chunks, get complete, CRC-checked frames.</summary>
public sealed class FrameDecoder
{
    const byte StartOfMessage = 0xFD;
    const byte EndOfMessage = 0xDD;
    const int MinBodyLength = 3; // id + crc

    readonly List<byte> _buffer = new();

    public int RejectedFrames { get; private set; }

    public IReadOnlyList<SamsungFrame> Push(ReadOnlySpan<byte> chunk)
    {
        _buffer.AddRange(chunk.ToArray());
        var frames = new List<SamsungFrame>();
        while (true)
        {
            int start = _buffer.IndexOf(StartOfMessage);
            if (start < 0) { _buffer.Clear(); break; }
            if (start > 0) _buffer.RemoveRange(0, start);
            if (_buffer.Count < 3) break;

            int header = _buffer[1] | (_buffer[2] << 8);
            int length = header & 0x3FF;
            int total = 3 + length + 1;
            if (length < MinBodyLength) { Reject(); continue; }
            if (_buffer.Count < total)
            {
                // A stray 0xFD can declare up to 1023 bytes; don't wait for them if a real frame follows.
                if (ValidFrameStartsAfter(0)) { Reject(); continue; }
                break;
            }
            if (_buffer[total - 1] != EndOfMessage) { Reject(); continue; }

            byte[] body = _buffer.GetRange(3, length).ToArray();
            ushort expected = (ushort)(body[^2] | (body[^1] << 8));
            if (Crc16.Compute(body.AsSpan(0, length - 2)) != expected) { Reject(); continue; }

            frames.Add(new SamsungFrame(body[0], body[1..^2], (ushort)(header & 0xFC00)));
            _buffer.RemoveRange(0, total);
        }
        return frames;
    }

    bool ValidFrameStartsAfter(int position)
    {
        for (int i = _buffer.IndexOf(StartOfMessage, position + 1); i >= 0 && i + 3 <= _buffer.Count; i = _buffer.IndexOf(StartOfMessage, i + 1))
        {
            int length = (_buffer[i + 1] | (_buffer[i + 2] << 8)) & 0x3FF;
            int total = 3 + length + 1;
            if (length < MinBodyLength || i + total > _buffer.Count || _buffer[i + total - 1] != EndOfMessage) continue;
            byte[] body = _buffer.GetRange(i + 3, length).ToArray();
            if (Crc16.Compute(body.AsSpan(0, length - 2)) == (ushort)(body[^2] | (body[^1] << 8))) return true;
        }
        return false;
    }

    void Reject()
    {
        RejectedFrames++;
        _buffer.RemoveAt(0); // drop this start byte and resync on the next one
    }
}
