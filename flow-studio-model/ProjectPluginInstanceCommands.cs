namespace Flow.Studio.Model;

/// <summary>Independent device instances reuse immutable accepted output while
/// owning distinct source, output binding, parameter and automation identities.</summary>
public static class ProjectPluginInstanceCommands
{
    /// <summary>Remove an unassigned instance and its automation in one action.
    /// Routing and clip references must be explicitly reassigned or removed first.</summary>
    public static void Remove(ProjectDocument document, Guid sourceId)
    {
        ArgumentNullException.ThrowIfNull(document);
        var before = document.Snapshot;
        var source = before.Sources[sourceId];
        if (source.Plugin is null) throw new InvalidOperationException("Source is not a declared plugin");
        var bindings = source.Bindings.Select(b => b.Id).ToHashSet();
        if ((before.Routing.GraphBinding is { } graph && bindings.Contains(graph)) ||
            before.Routing.Effects.Any(e => bindings.Contains(e.Binding)) ||
            before.Routing.Tracks.Any(t => t.InstrumentBinding is { } instrument && bindings.Contains(instrument)) ||
            before.Arrangement.ScoreClips.Any(c => c.SourceId == sourceId) ||
            before.Arrangement.AudioClips.Any(c => c.SourceId == sourceId))
            throw new InvalidOperationException("Reassign or remove device references before deleting the instance");
        var after = new ProjectSnapshot(before.Arrangement, before.Context,
            before.Sources.Values.Where(s => s.Descriptor.SourceId != sourceId), before.Routing, before.Assets,
            before.Automation.Where(l => !bindings.Contains(l.GraphBinding)), before.RenderSettings);
        document.Edit("Remove plugin instance", _ => after);
    }
    /// <summary>Duplicate the accepted device, optionally assigning an instrument
    /// to a track in the same action. Automation is copied by default. No Flow
    /// evaluation occurs; undo/redo restore the captured instance and IDs.</summary>
    public static Guid Duplicate(ProjectDocument document, Guid sourceId,
        Guid? instrumentTrack = null, bool copyAutomation = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        var before = document.Snapshot;
        var source = before.Sources[sourceId];
        var plugin = source.Plugin ?? throw new InvalidOperationException("Source is not a declared plugin");
        if (plugin.Manifest.Kind is not (FlowPluginKind.Instrument or FlowPluginKind.AudioEffect))
            throw new NotSupportedException("Only accepted instrument and effect instances can be duplicated");
        if (instrumentTrack.HasValue && (plugin.Manifest.Kind != FlowPluginKind.Instrument ||
            !before.Routing.Tracks.Any(t => t.Id == instrumentTrack.Value)))
            throw new ArgumentException("Instrument assignment requires an existing track and an instrument plugin");
        Guid id = Guid.NewGuid();
        var bindings = source.Bindings.ToDictionary(b => b.Id, b => b with { Id = Guid.NewGuid() });
        var result = source.Result;
        var copiedResult = new GeneratedSourceOutput(id, result.SourceRevision, result.Context,
            result.ScoreLayers, result.AudioLayers, result.GraphLayers, result.InstrumentLayers);
        var copied = ProjectSource.Restore(new(source.Descriptor.ApiVersion, id, source.Descriptor.EntryPoint),
            source.Code, copiedResult, bindings.Values.ToArray(), plugin: plugin, pluginValues: source.PluginValues);
        Guid? assignedBinding = null;
        if (instrumentTrack.HasValue)
            assignedBinding = copied.Bindings.Single(b => b.Available && b.Output.Role == GeneratedRole.Instrument &&
                b.Output.LayerId == plugin.OutputLayer).Id;
        var routing = instrumentTrack.HasValue
            ? new ProjectRouting(before.Routing.Tracks.Select(t => t.Id == instrumentTrack.Value
                ? t with { InstrumentBinding = assignedBinding } : t), before.Routing.GraphBinding, before.Routing.Effects)
            : before.Routing;
        var automation = copyAutomation
            ? before.Automation.Where(l => bindings.ContainsKey(l.GraphBinding)).Select(l =>
                new ProjectAutomationLane(Guid.NewGuid(), bindings[l.GraphBinding].Id, l.NodeId,
                    l.ParameterId, l.Points, l.Shape, l.TargetKind)).ToArray()
            : [];
        // Construct and validate the whole state before creating the history action.
        var after = new ProjectSnapshot(before.Arrangement, before.Context, before.Sources.Values.Append(copied),
            routing, before.Assets, before.Automation.Concat(automation), before.RenderSettings);
        document.Edit("Duplicate plugin instance", _ => after);
        return id;
    }
}
