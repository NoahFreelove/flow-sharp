using Flow.Platform.Linux;
using Flow.Studio.Engine;

namespace Flow.Studio.Host;

/// <summary>Control-owner monitor subscription on the session's shared MIDI input.
/// Poll rebinds only acknowledged endpoints. Native callbacks never touch document,
/// preparation or DSP cursors. Recording uses a separate subscription on the same port.</summary>
public sealed class ProjectMidiMonitoringHost : IDisposable
{
    private readonly ProjectPlaybackSession _playback;
    private IMidiInputConnection? _input;
    private PlaybackMonitorEndpoint? _endpoint;
    private Exception? _error;
    private bool _disconnecting, _disposed;
    public bool DeviceSubscriptionOwned => _input is not null;
    public bool IsConnected => !_disconnecting && _input?.IsRunning == true;
    public Exception? Error => Volatile.Read(ref _error) ?? _input?.Error;

    public ProjectMidiMonitoringHost(ProjectPlaybackSession playback)
    { ArgumentNullException.ThrowIfNull(playback); _playback = playback; }

    public void Connect(string portName)
    {
        ThrowIfDisposed();
        if (_input is not null) throw new InvalidOperationException("Disconnect the previous monitor input first.");
        if (_playback.OutputState != PlaybackOutputState.Running || !_playback.Playback.TryGetMonitor(out var endpoint))
            throw new InvalidOperationException("Wait for running audio and an acknowledged monitor before connecting input.");
        _error = null; _disconnecting = false;
        Volatile.Write(ref _endpoint, endpoint);
        try { _input = _playback.MidiInputHub.Subscribe(portName, Receive); }
        catch (Exception error) { _error = error; Interlocked.Exchange(ref _endpoint, null)?.Close(); throw; }
    }

    private void Receive(ReadOnlySpan<byte> packet, long timestamp)
    {
        try
        {
            if (!MidiChannelPacket.TryDecode(packet, out var message)) return;
            var endpoint = Volatile.Read(ref _endpoint);
            if (endpoint is null) return;
            if (!endpoint.TryWrite(message.Status, message.Data1, message.Data2) && endpoint.Faulted)
                throw new InvalidOperationException("Monitoring MIDI queue overflow; recording input is independent.");
        }
        catch (Exception error)
        {
            Interlocked.CompareExchange(ref _error, error, null);
            Volatile.Read(ref _endpoint)?.Close();
        }
    }

    public void Poll()
    {
        ThrowIfDisposed();
        if (_input is null) return;
        if (_disconnecting) { Disconnect(TimeSpan.Zero); return; }
        if (Error is not null || !_input.IsRunning)
        {
            Interlocked.CompareExchange(ref _error, Error ?? new IOException("MIDI input stopped unexpectedly."), null);
            Disconnect(TimeSpan.Zero); return;
        }
        if (_playback.OutputState != PlaybackOutputState.Running || !_playback.Playback.RequestedMonitorTrack.HasValue)
        { Disconnect(TimeSpan.Zero); return; }
        _playback.Playback.TryGetMonitor(out var next);
        var previous = Volatile.Read(ref _endpoint);
        if (ReferenceEquals(previous, next)) return;
        previous?.Close();
        Volatile.Write(ref _endpoint, next); // Null while preparing/awaiting acknowledgment.
    }

    public bool Disconnect(TimeSpan timeout)
    {
        ThrowIfDisposed();
        _disconnecting = true;
        Interlocked.Exchange(ref _endpoint, null)?.Close();
        if (_input is null) { _disconnecting = false; return true; }
        if (!_input.TryStop(timeout)) return false;
        if (_input.Error is { } error) Interlocked.CompareExchange(ref _error, error, null);
        _input = null; _disconnecting = false; return true;
    }
    public void Dispose()
    {
        if (_disposed) return;
        if (!Disconnect(TimeSpan.FromSeconds(2))) throw new TimeoutException("Monitor input still owns its callback; retry disposal.");
        _disposed = true;
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
