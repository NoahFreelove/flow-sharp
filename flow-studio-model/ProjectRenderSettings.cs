namespace Flow.Studio.Model;

/// <summary>Portable offline rendering preferences. Device sessions still negotiate
/// their actual format explicitly. Current exports are stereo float32 WAVE.</summary>
public sealed record ProjectRenderSettings
{
    public int SampleRate { get; }
    public int BlockFrames { get; }
    public ProjectRenderSettings(int sampleRate = 48000, int blockFrames = 256)
    {
        if (sampleRate is < 1 or > 384000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (blockFrames is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(blockFrames));
        SampleRate = sampleRate; BlockFrames = blockFrames;
    }
}

public static class ProjectRenderCommands
{
    public static void Set(ProjectDocument document, ProjectRenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        document.Edit("Change render settings", p => p.RenderSettings == settings ? p :
            new(p.Arrangement, p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation, settings));
    }
}
