using System.Diagnostics;

namespace Flow.Platform.Linux;

/// <summary>The packet is borrowed for this call only. Timestamp is Stopwatch ticks at receipt.</summary>
public delegate void MidiPacketReceiver(ReadOnlySpan<byte> packet, long timestamp);

public interface IMidiInputConnection : IDisposable
{
    Exception? Error { get; }
    bool IsRunning { get; }
    bool TryStop(TimeSpan timeout);
}

internal interface IMidiInputPort : IDisposable
{
    int Read(byte[] buffer);
}

/// <summary>
/// Owns one polling thread and its port. Stop/Dispose belong to one control owner;
/// the receiver runs on the input thread, never on the audio callback.
/// </summary>
public sealed class MidiInputConnection : IMidiInputConnection
{
    private readonly IMidiInputPort _port;
    private readonly MidiPacketReceiver _receive;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stop = new(false);
    private Exception? _error;
    private bool _disposed;

    internal MidiInputConnection(IMidiInputPort port, MidiPacketReceiver receive)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(receive);
        _port = port;
        _receive = receive;
        _thread = new Thread(Run) { IsBackground = true, Name = "Flow MIDI input" };
        try { _thread.Start(); }
        catch { _port.Dispose(); _stop.Dispose(); throw; }
    }

    public Exception? Error => Volatile.Read(ref _error);
    public bool IsRunning => _thread.IsAlive;

    /// <summary>Enumerates exact names. Native library/device failures are surfaced to the caller.</summary>
    public static IReadOnlyList<string> ListPorts() => RtMidiInputPort.ListPorts();

    /// <summary>Requires exactly one matching name; ambiguous or missing devices are rejected.</summary>
    public static MidiInputConnection Open(string portName, MidiPacketReceiver receive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        ArgumentNullException.ThrowIfNull(receive);
        return new(RtMidiInputPort.Open(portName), receive);
    }

    private void Run()
    {
        var buffer = new byte[512];
        try
        {
            while (!_stop.IsSet)
            {
                int length = _port.Read(buffer);
                if ((uint)length > buffer.Length)
                    throw new IOException("MIDI input returned an invalid or oversized packet.");
                if (length == 0) { _stop.Wait(1); continue; }
                // A read already in flight at stop is discarded; host closes admission first.
                if (!_stop.IsSet) _receive(buffer.AsSpan(0, length), Stopwatch.GetTimestamp());
            }
        }
        catch (Exception error) { Interlocked.CompareExchange(ref _error, error, null); }
    }

    /// <summary>
    /// On timeout the port remains owned and MUST NOT be freed. Retry after the reader exits.
    /// Error remains available even after a successful stop. No callbacks run after success.
    /// </summary>
    public bool TryStop(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (Thread.CurrentThread == _thread)
            throw new InvalidOperationException("Stop MIDI input from its control owner, not its receiver.");
        if (_disposed) return true;
        _stop.Set();
        if (!_thread.Join(timeout)) return false;
        _port.Dispose();
        _stop.Dispose();
        _disposed = true;
        return true;
    }

    public void Dispose()
    {
        if (!TryStop(TimeSpan.FromSeconds(2)))
            throw new TimeoutException("MIDI input has not stopped. Device ownership is retained; retry disposal.");
    }
}
