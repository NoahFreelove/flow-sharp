namespace Flow.Studio.Model;

/// <summary>Half-open selection in output sample frames, including any desired
/// tail region. No events beyond the range are synthesized into extra output.</summary>
public sealed record ProjectExportRange
{
    public long StartFrame { get; }
    public long EndFrame { get; }
    public ProjectExportRange(long startFrame, long endFrame)
    {
        if (startFrame < 0 || endFrame <= startFrame) throw new ArgumentException("Export range must be positive and ordered");
        StartFrame = startFrame; EndFrame = endFrame;
    }

    public static ProjectExportRange FromQuarters(ProjectTempoMap tempo, double start, double end, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(tempo);
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start || sampleRate is < 1 or > 384000)
            throw new ArgumentException("Invalid musical export range");
        long Frame(double quarter) => checked((long)Math.Round(tempo.SecondsAt(quarter) * sampleRate, MidpointRounding.AwayFromZero));
        return new(Frame(start), Frame(end));
    }
}
