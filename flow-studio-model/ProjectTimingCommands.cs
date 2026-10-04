namespace Flow.Studio.Model;

/// <summary>Step tempo and bar-boundary meter edits preserve authored quarter/frame
/// windows. Captured source contexts stay historical; only current context changes.</summary>
public static class ProjectTimingCommands
{
    public static void Set(ProjectDocument document, ProjectTempoMap tempo, ProjectMeterMap meter) =>
        document.Edit("Change project timing", p => WithTiming(p, tempo, meter));
    public static ProjectSnapshot WithTiming(ProjectSnapshot p, ProjectTempoMap tempo, ProjectMeterMap meter)
    {
        ArgumentNullException.ThrowIfNull(tempo); ArgumentNullException.ThrowIfNull(meter);
        if (p.Arrangement.Tempo.Changes.SequenceEqual(tempo.Changes) && p.Arrangement.Meter.Changes.SequenceEqual(meter.Changes)) return p;
        return new(new(p.Arrangement.Id, tempo, meter, p.Arrangement.ScoreClips, p.Arrangement.AudioClips),
            new(checked(p.Context.Revision + 1), p.Context.Seed, tempo, meter, p.Context.Parameters, p.Context.Tuning),
            p.Sources.Values, p.Routing, p.Assets, p.Automation, p.RenderSettings);
    }
}
