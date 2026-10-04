using System.Collections.ObjectModel;
namespace Flow.Studio.Model;

public sealed record ProjectTrack(Guid Id, string Name, Guid? InstrumentBinding = null, int? InputBus = null,
    bool Muted = false, bool Solo = false, int? ColorRgb = null);
/// <summary>Ordered insert reference; null TrackId means after the master mixer.</summary>
public sealed record ProjectEffect(Guid Binding, Guid? TrackId = null, bool Bypassed = false);
/// <summary>Persisted display order, stable input buses and explicit output bindings. Missing bindings are
/// retained for repair and diagnosed during playback preparation.</summary>
public sealed class ProjectRouting
{
    public IReadOnlyList<ProjectTrack> Tracks { get; }
    public Guid? GraphBinding { get; }
    public IReadOnlyList<ProjectEffect> Effects { get; }
    public ProjectRouting(IEnumerable<ProjectTrack> tracks, Guid? graphBinding, IEnumerable<ProjectEffect>? effects = null)
    {
        var copy = tracks.ToArray();
        if (copy.Length > 64 || copy.Any(t => t is null || t.Id == Guid.Empty || string.IsNullOrWhiteSpace(t.Name) ||
            t.Name.Length > 1024 || t.InstrumentBinding == Guid.Empty || t.ColorRgb is < 0 or > 0xFFFFFF) || copy.Select(t => t.Id).Distinct().Count() != copy.Length || graphBinding == Guid.Empty)
            throw new ArgumentException("Invalid project routing");
        // Older projects and the three-argument Flow constructor imply list order.
        copy = copy.Select((track, index) => track with { InputBus = track.InputBus ?? index }).ToArray();
        if (copy.Any(t => t.InputBus is < 0 or > 63) || copy.Select(t => t.InputBus).Distinct().Count() != copy.Length)
            throw new ArgumentException("Track input buses must be distinct and within 0–63");
        Tracks = Array.AsReadOnly(copy); GraphBinding = graphBinding;
        var inserts = (effects ?? []).Take(257).ToArray();
        if (inserts.Length > 256 || inserts.Any(e => e is null || e.Binding == Guid.Empty || e.Binding == graphBinding ||
            (e.TrackId.HasValue && !copy.Any(t => t.Id == e.TrackId.Value))) || inserts.Select(e => e.Binding).Distinct().Count() != inserts.Length)
            throw new ArgumentException("Effect inserts require unique bindings and existing tracks within the 256-instance limit");
        Effects = Array.AsReadOnly(inserts);
    }
}
