namespace Flow.Studio.Model;

public static class ProjectAssetGrantCommands
{
    public static void Set(ProjectDocument document, Guid sourceId, IEnumerable<Guid> assetIds)
    {
        var ids = assetIds.Take(33).ToArray();
        document.Edit("Change generator sample access", p => WithGrants(p, sourceId, ids));
    }
    public static ProjectSnapshot WithGrants(ProjectSnapshot p, Guid sourceId, IEnumerable<Guid> assetIds)
    {
        var ids = assetIds.Take(33).Order().ToArray();
        if (!p.Sources.TryGetValue(sourceId, out var source)) throw new ArgumentException("Unknown generator source");
        if (ids.Any(id => !p.Assets.Any(a => a.Id == id))) throw new ArgumentException("Sample grant must identify a project asset");
        var changed = ProjectSource.Restore(source.Descriptor, source.Code, source.Result, source.Bindings.ToArray(),
            source.EditableOrigin, source.IsManagedGraph, source.Plugin, source.PluginValues, ids);
        if (source.AssetGrants.SequenceEqual(ids)) return p;
        return new(p.Arrangement, p.Context, p.Sources.Values.Select(s => s.Descriptor.SourceId == sourceId ? changed : s),
            p.Routing, p.Assets, p.Automation, p.RenderSettings);
    }
}
