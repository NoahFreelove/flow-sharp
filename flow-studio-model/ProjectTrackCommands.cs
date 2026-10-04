namespace Flow.Studio.Model;

/// <summary>Captured track gestures. Display order is independent of graph bus
/// identity; removing tracks also removes their clips but retains reusable sources.</summary>
public static class ProjectTrackCommands
{
    /// <summary>Opaque sRGB 0xRRGGBB; null selects the frontend's automatic color.</summary>
    public static void SetColor(ProjectDocument document, Guid trackId, int? colorRgb) => document.Edit("Color track", p =>
    {
        var track = Find(p, trackId);
        return track.ColorRgb == colorRgb ? p : Replace(p, p.Routing.Tracks.Select(t => t.Id == trackId ? t with { ColorRgb = colorRgb } : t));
    });
    public static void SetMuted(ProjectDocument document, Guid trackId, bool muted) => document.Edit("Mute track", p =>
    {
        var track = Find(p, trackId);
        return track.Muted == muted ? p : Replace(p, p.Routing.Tracks.Select(t => t.Id == trackId ? t with { Muted = muted } : t));
    });
    public static void SetSolo(ProjectDocument document, Guid trackId, bool solo) => document.Edit("Solo track", p =>
    {
        var track = Find(p, trackId);
        return track.Solo == solo ? p : Replace(p, p.Routing.Tracks.Select(t => t.Id == trackId ? t with { Solo = solo } : t));
    });
    public static void Rename(ProjectDocument document, Guid trackId, string name) => document.Edit("Rename track", p =>
    {
        Find(p, trackId);
        return Replace(p, p.Routing.Tracks.Select(t => t.Id == trackId ? t with { Name = name } : t));
    });

    public static void Reorder(ProjectDocument document, IReadOnlyList<Guid> order) => document.Edit("Reorder tracks", p =>
    {
        if (order.Count != p.Routing.Tracks.Count || order.Distinct().Count() != order.Count)
            throw new ArgumentException("Supply every track exactly once");
        var tracks = p.Routing.Tracks.ToDictionary(t => t.Id);
        if (order.Any(id => !tracks.ContainsKey(id))) throw new ArgumentException("Unknown track");
        return Replace(p, order.Select(id => tracks[id]));
    });

    public static void SetInstrument(ProjectDocument document, Guid trackId, Guid? binding) => document.Edit("Assign track instrument", p =>
    {
        Find(p, trackId); ValidateInstrument(p, binding);
        return Replace(p, p.Routing.Tracks.Select(t => t.Id == trackId ? t with { InstrumentBinding = binding } : t));
    });

    /// <summary>Add to an unused input exposed by the selected Flow graph. Expanding
    /// a graph's routing is a separate explicit graph-authoring operation.</summary>
    public static void Add(ProjectDocument document, ProjectTrack track) => document.Edit("Add track", p =>
    {
        ArgumentNullException.ThrowIfNull(track);
        if (track.InputBus is null) throw new ArgumentException("A new track requires an explicit graph input bus");
        ValidateInstrument(p, track.InstrumentBinding);
        var source = p.Sources.Values.SingleOrDefault(s => s.Bindings.Any(b => b.Id == p.Routing.GraphBinding && b.Available && b.Output.Role == GeneratedRole.Graph))
            ?? throw new InvalidOperationException("Selected graph is unavailable");
        var binding = source.Bindings.Single(b => b.Id == p.Routing.GraphBinding);
        var graph = source.Result.GraphLayers.Single(l => l.Id == binding.Output.LayerId).Graph;
        if (!graph.Nodes.Any(n => n.DeviceId == "flow.input" && n.Parameters["bus"] == track.InputBus.Value))
            throw new ArgumentException("The selected graph does not expose that input bus");
        return Replace(p, p.Routing.Tracks.Append(track));
    });

    public static void Remove(ProjectDocument document, IReadOnlyList<Guid> trackIds) => document.Edit("Remove tracks", p =>
    {
        if (trackIds.Count is < 1 or > 64 || trackIds.Distinct().Count() != trackIds.Count)
            throw new ArgumentException("Select distinct tracks");
        foreach (var id in trackIds) Find(p, id);
        var ids = trackIds.ToHashSet();
        return new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
            p.Arrangement.ScoreClips.Where(c => !ids.Contains(c.TrackId)), p.Arrangement.AudioClips.Where(c => !ids.Contains(c.TrackId))),
            p.Context, p.Sources.Values, new(p.Routing.Tracks.Where(t => !ids.Contains(t.Id)), p.Routing.GraphBinding,
                p.Routing.Effects.Where(e => !e.TrackId.HasValue || !ids.Contains(e.TrackId.Value))), p.Assets, p.Automation, p.RenderSettings);
    });
    private static ProjectTrack Find(ProjectSnapshot project, Guid id) =>
        project.Routing.Tracks.SingleOrDefault(t => t.Id == id) ?? throw new ArgumentException("Unknown track");
    private static ProjectSnapshot Replace(ProjectSnapshot p, IEnumerable<ProjectTrack> tracks) =>
        new(p.Arrangement, p.Context, p.Sources.Values, new(tracks, p.Routing.GraphBinding, p.Routing.Effects), p.Assets, p.Automation, p.RenderSettings);
    private static void ValidateInstrument(ProjectSnapshot p, Guid? binding)
    {
        if (binding is not null && !p.Sources.Values.Any(s => s.Bindings.Any(b => b.Id == binding && b.Available && b.Output.Role == GeneratedRole.Instrument)))
            throw new ArgumentException("Instrument output is unavailable");
    }
}
