namespace Flow.Studio.Model;

/// <summary>Control-thread asset commits. Prepared files are kept across undo/redo;
/// garbage collection requires separate reachability analysis of project/history.</summary>
public static class ProjectAssetCommands
{
    public static void Place(ProjectDocument document, AudioAssetReference asset, Guid clipId, Guid trackId,
        double anchorQuarters, TimeOffset nudge = default)
    {
        document.Edit("Import audio clip", p =>
        {
            if (!p.Routing.Tracks.Any(t => t.Id == trackId)) throw new ArgumentException("Unknown target track");
            var existing = p.Assets.FirstOrDefault(a => a.Id == asset.Id);
            if (existing is not null && existing != asset) throw new ArgumentException("Asset ID already has different content");
            var clip = new AudioClip(clipId, trackId, asset.Id, anchorQuarters, 0, asset.Frames, asset.SampleRate, nudge);
            var arrangement = new ArrangementSnapshot(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips, p.Arrangement.AudioClips.Append(clip));
            return new(arrangement, p.Context, p.Sources.Values, p.Routing,
                existing is null ? p.Assets.Append(asset) : p.Assets, p.Automation, p.RenderSettings);
        });
    }
    public static void Relink(ProjectDocument document, AudioAssetReference replacement)
    {
        document.Edit("Relink audio asset", p =>
        {
            var previous = p.Assets.SingleOrDefault(a => a.Id == replacement.Id) ?? throw new ArgumentException("Unknown asset");
            if (previous == replacement) return p;
            if (previous.SampleRate != replacement.SampleRate)
                throw new ArgumentException("Relink requires the original sample rate; convert the replacement explicitly");
            // Window lengths remain unchanged even when replacement audio is shorter.
            return new(p.Arrangement, p.Context, p.Sources.Values, p.Routing,
                p.Assets.Select(a => a.Id == replacement.Id ? replacement : a), p.Automation, p.RenderSettings);
        });
    }
}
