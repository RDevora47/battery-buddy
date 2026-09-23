using BatteryBuddy.Backends.Logitech.Hidpp;

namespace BatteryBuddy.Backends.Logitech.Hid;

/// <summary>
/// An open HID++ collection. One read loop owns the handle's input reports: each is offered to the pending
/// requests first, and anything unclaimed (events, other programs' replies) goes to <see cref="Report"/>.
/// Never abandon a read mid-flight: a timed-out read would swallow the next report.
/// </summary>
public sealed class HidChannel : IDisposable
{
    const uint GENERIC_READ_WRITE = 0xC0000000, FILE_SHARE_READ_WRITE = 0x03, OPEN_EXISTING = 3;
    const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    sealed record Pending(byte DeviceIndex, byte FeatureIndex, byte Function, TaskCompletionSource<byte[]?> Reply);

    readonly FileStream _stream;
    readonly CancellationTokenSource _cts = new();
    readonly List<Pending> _pending = new();
    readonly SemaphoreSlim _writeGate = new(1, 1);
    readonly Task _readLoop;

    HidChannel(HidInterface iface, FileStream stream)
    {
        Interface = iface;
        _stream = stream;
        _readLoop = Task.Run(ReadLoopAsync);
    }

    public HidInterface Interface { get; }

    /// <summary>Input reports no request claimed. Raised on the read loop's thread.</summary>
    public event Action<byte[]>? Report;

    /// <summary>The read loop ended: the device went away or the channel was disposed. Raised once, on the read loop's thread.</summary>
    public event Action? Closed;

    /// <summary>False once the device went away (read failed) or the channel was disposed.</summary>
    public bool IsAlive => !_readLoop.IsCompleted;

    /// <summary>Shared read/write, so it works alongside Options+. Null if the collection can't be opened.</summary>
    public static HidChannel? Open(HidInterface iface)
    {
        var handle = HidInterface.CreateFileW(iface.Path, GENERIC_READ_WRITE, FILE_SHARE_READ_WRITE, IntPtr.Zero,
            OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (handle.IsInvalid) return null;
        // bufferSize 0: reports must reach the driver whole, never coalesced or split.
        return new HidChannel(iface, new FileStream(handle, FileAccess.ReadWrite, 0, isAsync: true));
    }

    /// <summary>Sends a long HID++ request and waits for its reply; null on an error reply, timeout or dead channel.</summary>
    public async Task<byte[]?> RequestAsync(byte[] frame, TimeSpan timeout, CancellationToken ct)
    {
        if (!IsAlive) return null;
        var pending = new Pending(frame[1], frame[2], (byte)(frame[3] >> 4),
            new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously));
        lock (_pending) _pending.Add(pending);
        try
        {
            if (!await WriteAsync(frame, ct)) return null;
            var done = await Task.WhenAny(pending.Reply.Task, Task.Delay(timeout, ct));
            return done == pending.Reply.Task ? await pending.Reply.Task : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        finally { lock (_pending) _pending.Remove(pending); }
    }

    async Task<bool> WriteAsync(byte[] frame, CancellationToken ct)
    {
        var report = new byte[Interface.OutputLength];
        Array.Copy(frame, report, Math.Min(frame.Length, report.Length));
        await _writeGate.WaitAsync(ct);
        try
        {
            await _stream.WriteAsync(report, ct);
            return true;
        }
        // A collection rejects report ids it doesn't declare, and a sleeping device can fail writes: not fatal.
        catch (IOException) { return false; }
        catch (ObjectDisposedException) { return false; }
        finally { _writeGate.Release(); }
    }

    async Task ReadLoopAsync()
    {
        var buffer = new byte[Math.Max(Interface.InputLength, HidppProtocol.LongLength)];
        while (!_cts.IsCancellationRequested)
        {
            int read;
            try { read = await _stream.ReadAsync(buffer, _cts.Token); }
            catch (Exception) { break; }   // device removed, handle closed, or cancelled
            if (read <= 0) break;
            Dispatch(buffer.AsSpan(0, read).ToArray());
        }
        lock (_pending)
            foreach (var pending in _pending) pending.Reply.TrySetResult(null);
        Closed?.Invoke();
    }

    void Dispatch(byte[] report)
    {
        lock (_pending)
            foreach (var pending in _pending)
                switch (HidppProtocol.Match(report, pending.DeviceIndex, pending.FeatureIndex, pending.Function))
                {
                    case HidppMatch.Response:
                        pending.Reply.TrySetResult(report);
                        return;
                    case HidppMatch.Error:
                        pending.Reply.TrySetResult(null);
                        return;
                }
        Report?.Invoke(report);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _stream.Dispose();
    }
}
