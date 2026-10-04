namespace Flow.Audio;

/// <summary>Exclusively owned, preallocated stereo playback cursor. Implementations
/// must keep valid Read/Seek/Reset calls allocation-free, nonblocking and IO-free.
/// Read fills the requested stereo span, zeroing any remainder after EOF.
/// Host publication validates matching sample rate/block size before transfer.</summary>
public interface IPreparedAudioPlayback
{
    int SampleRate { get; }
    int MaxBlockFrames { get; }
    long TotalFrames { get; }
    long PositionFrames { get; }
    int Read(Span<float> output);
    void Seek(long frame);
    void Reset();
}
