using Flow.Audio;

namespace Flow.Studio.Engine;

/// <summary>Admission-only handle for an acknowledged monitor generation. One input
/// producer writes MIDI. No DSP cursor or native device ownership is exposed.</summary>
public sealed class PlaybackMonitorEndpoint
{
    private readonly PreparedLiveInstrument _instrument;
    private readonly QueuedSinePlayback _playback;
    private readonly Flow.Audio.Graph.PreparedGraphPlayback _source;
    private int _closed;
    public Guid TrackId { get; }
    public long Generation { get; }
    public bool Faulted => _instrument.Faulted;
    public long DroppedMessages => _instrument.DroppedMessages;
    public long StolenVoices => _instrument.StolenVoices;
    public bool IsActive => Volatile.Read(ref _closed) == 0 && !_instrument.AdmissionClosed &&
        _playback.Generation == Generation && !_playback.ReplacementPending;

    internal PlaybackMonitorEndpoint(Guid trackId, long generation, PreparedLiveInstrument instrument, QueuedSinePlayback playback, Flow.Audio.Graph.PreparedGraphPlayback source)
    { TrackId = trackId; Generation = generation; _instrument = instrument; _playback = playback; _source = source; }

    /// <summary>False means stale, closed, or overflowed. A publication racing this
    /// call can only reach the old instrument, never a differently selected track.</summary>
    public bool TryWrite(byte status, byte data1, byte data2 = 0) =>
        IsActive && _instrument.TryWrite(status, data1, data2);

    /// <summary>Stops further admission and clears live voices at their next render.
    /// Frozen-transport audition stops too. During ordinary playback, the shared mixer
    /// may still have finite effect tails. Reprepare to reopen.</summary>
    public void Close()
    {
        Interlocked.Exchange(ref _closed, 1);
        _instrument.CloseAdmission();
        _source.DisableMonitoring();
    }
}
