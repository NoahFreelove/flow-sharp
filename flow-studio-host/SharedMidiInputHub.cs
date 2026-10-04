using System.Diagnostics;
using Flow.Platform.Linux;

namespace Flow.Studio.Host;

/// <summary>One control owner manages at most two subscribers on one native port.
/// The native input thread fans borrowed packets out synchronously, without allocation.
/// Failed subscriber/native joins retain ownership and block new subscriptions.</summary>
public sealed class SharedMidiInputHub : IDisposable
{
    private readonly Func<string, MidiPacketReceiver, IMidiInputConnection> _open;
    private readonly List<Subscription> _subscriptions = [];
    private Subscription[] _published = [];
    private IMidiInputConnection? _source;
    private string? _port;
    private bool _disposed;
    public bool DeviceOwned => _source is not null;
    public int SubscriberCount => _subscriptions.Count;

    public SharedMidiInputHub() : this(MidiInputConnection.Open) { }
    internal SharedMidiInputHub(Func<string, MidiPacketReceiver, IMidiInputConnection> open)
    { ArgumentNullException.ThrowIfNull(open); _open = open; }

    public IMidiInputConnection Subscribe(string portName, MidiPacketReceiver receiver)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName); ArgumentNullException.ThrowIfNull(receiver);
        if (_subscriptions.Any(s => s.Closed)) throw new InvalidOperationException("Previous MIDI subscriber is still stopping.");
        if (_subscriptions.Count == 2) throw new InvalidOperationException("MIDI input supports one monitor and one recorder.");
        if (_source is not null && (!string.Equals(_port, portName, StringComparison.Ordinal) || _source.Error is not null || !_source.IsRunning))
            throw new InvalidOperationException("Disconnect the current MIDI input before selecting another or recovering a failed device.");
        var subscription = new Subscription(this, receiver);
        _subscriptions.Add(subscription); Publish();
        try
        {
            if (_source is null) { _source = _open(portName, Dispatch); _port = portName; }
            subscription.Bind(_source);
            return subscription;
        }
        catch
        {
            subscription.CloseAdmission(); _subscriptions.Remove(subscription); Publish(); throw;
        }
    }
    private void Publish() => Volatile.Write(ref _published, _subscriptions.Where(s => !s.Closed).ToArray());
    private void Dispatch(ReadOnlySpan<byte> packet, long timestamp)
    {
        foreach (var subscription in Volatile.Read(ref _published)) subscription.Deliver(packet, timestamp);
    }
    private bool Stop(Subscription subscription, TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (subscription.Stopped) return true;
        if (subscription.IsCallbackThread) throw new InvalidOperationException("Stop MIDI subscriptions from the control owner.");
        long start = Stopwatch.GetTimestamp();
        subscription.CloseAdmission(); Publish();
        while (subscription.InFlight != 0)
        {
            if (Stopwatch.GetElapsedTime(start) >= timeout) return false;
            Thread.Sleep(1);
        }
        if (_subscriptions.Any(s => !s.Closed))
        {
            _subscriptions.Remove(subscription); subscription.Stopped = true; return true;
        }
        var remaining = timeout - Stopwatch.GetElapsedTime(start);
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        // Join the entire native dispatch before clearing even another failed-close
        // subscriber. Its callback may still be running from an older published array.
        if (_source is not null && !_source.TryStop(remaining)) return false;
        foreach (var closing in _subscriptions) closing.Stopped = true;
        _subscriptions.Clear(); _source = null; _port = null; Publish();
        return true;
    }
    public void Dispose()
    {
        if (_disposed) return;
        foreach (var subscription in _subscriptions) subscription.CloseAdmission();
        Publish();
        foreach (var subscription in _subscriptions.ToArray())
            if (!Stop(subscription, TimeSpan.FromSeconds(2)))
                throw new TimeoutException("MIDI input still owns callbacks; retry disposal.");
        _disposed = true;
    }

    private sealed class Subscription(SharedMidiInputHub owner, MidiPacketReceiver receiver) : IMidiInputConnection
    {
        private IMidiInputConnection? _source;
        private Exception? _error;
        private int _closed, _inFlight, _callbackThread;
        internal bool Stopped;
        internal bool Closed => Volatile.Read(ref _closed) != 0;
        internal int InFlight => Volatile.Read(ref _inFlight);
        internal bool IsCallbackThread => Volatile.Read(ref _callbackThread) == Environment.CurrentManagedThreadId;
        public Exception? Error => Volatile.Read(ref _error) ?? _source?.Error;
        public bool IsRunning => !Closed && Error is null && _source?.IsRunning == true;
        internal void Bind(IMidiInputConnection source) => _source = source;
        internal void CloseAdmission() => Volatile.Write(ref _closed, 1);
        internal void Deliver(ReadOnlySpan<byte> packet, long timestamp)
        {
            Interlocked.Increment(ref _inFlight);
            try
            {
                if (Closed) return;
                Volatile.Write(ref _callbackThread, Environment.CurrentManagedThreadId);
                try { receiver(packet, timestamp); }
                catch (Exception error) { Interlocked.CompareExchange(ref _error, error, null); CloseAdmission(); }
            }
            finally { Volatile.Write(ref _callbackThread, 0); Interlocked.Decrement(ref _inFlight); }
        }
        public bool TryStop(TimeSpan timeout) => owner.Stop(this, timeout);
        public void Dispose()
        {
            if (!TryStop(TimeSpan.FromSeconds(2))) throw new TimeoutException("MIDI subscriber still owns its callback; retry disposal.");
        }
    }
}
