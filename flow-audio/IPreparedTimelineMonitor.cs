namespace Flow.Audio;

/// <summary>Prepared monitoring-only audio following the transport clock without
/// changing finite project duration. Exclusively owned by its prepared playback.</summary>
public interface IPreparedTimelineMonitor
{
    int SampleRate { get; }
    int MaxBlockFrames { get; }
    void ReadAt(Span<float> output, long projectFrame, bool advancing = true);
    void Reset(long frame);
}
