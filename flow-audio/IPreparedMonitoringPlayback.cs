namespace Flow.Audio;

/// <summary>Optional audio-owner path for monitoring through the same prepared mixer
/// while arrangement cursors are frozen or at EOF. It never advances source position.</summary>
public interface IPreparedMonitoringPlayback
{
    bool MonitoringEnabled { get; }
    void ReadMonitoring(Span<float> output, long projectFrame, bool advanceTimeline);
}
