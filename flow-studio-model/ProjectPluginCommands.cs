namespace Flow.Studio.Model;

/// <summary>Public plugin control edits; values belong to the source instance,
/// while the pinned definition and default graph remain unchanged.</summary>
public static class ProjectPluginCommands
{
    /// <summary>Replace all public values in one undoable edit. Automation-owned
    /// changes reject the entire preset before mutating the document.</summary>
    public static bool ApplyPreset(ProjectDocument document, Guid sourceId, PluginPreset preset)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(preset);
        var source = document.Snapshot.Sources[sourceId];
        var plugin = source.Plugin ?? throw new InvalidOperationException("Source is not a declared plugin");
        RequireLiveDevice(plugin);
        preset.ValidateFor(plugin);
        var changedParameters = plugin.Manifest.Parameters.Where(p =>
            source.PluginValues.GetValueOrDefault(p.Id, p.Default) != preset.Values[p.Id]).ToArray();
        if (changedParameters.Length == 0) return false;
        foreach (var parameter in changedParameters) CheckAutomation(document, source, parameter.Id);
        var values = preset.Values.Where(p => p.Value != plugin.Manifest.Parameters.Single(parameter => parameter.Id == p.Key).Default)
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var changed = ProjectSource.Restore(source.Descriptor, source.Code, source.Result, source.Bindings.ToArray(),
            source.EditableOrigin, source.IsManagedGraph, plugin, values);
        document.Edit("Apply plugin preset", p => new(p.Arrangement, p.Context,
            p.Sources.Values.Select(s => s.Descriptor.SourceId == sourceId ? changed : s), p.Routing, p.Assets, p.Automation, p.RenderSettings));
        return true;
    }
    public static void SetParameter(ProjectDocument document, Guid sourceId, string parameterId, double value)
        => Edit(document, sourceId, parameterId, value);
    public static void ResetParameter(ProjectDocument document, Guid sourceId, string parameterId)
        => Edit(document, sourceId, parameterId, null);
    private static void Edit(ProjectDocument document, Guid sourceId, string parameterId, double? value)
    {
        var source = document.Snapshot.Sources[sourceId];
        var plugin = source.Plugin ?? throw new InvalidOperationException("Source is not a declared plugin");
        RequireLiveDevice(plugin);
        var parameter = plugin.Manifest.Parameters.SingleOrDefault(p => p.Id == parameterId)
            ?? throw new ArgumentException("Unknown public plugin parameter");
        if (value is { } typed) _ = parameter.ToNormalized(typed);
        CheckAutomation(document, source, parameterId);
        var values = new Dictionary<string, double>(source.PluginValues);
        if (value is { } v)
        {
            if (values.TryGetValue(parameterId, out double old) && old == v) return;
            values[parameterId] = v;
        }
        else if (!values.Remove(parameterId)) return;
        var changed = ProjectSource.Restore(source.Descriptor, source.Code, source.Result, source.Bindings.ToArray(),
            source.EditableOrigin, source.IsManagedGraph, plugin, values);
        document.Edit(value is null ? "Reset plugin parameter" : "Set plugin parameter", p => new(p.Arrangement, p.Context,
            p.Sources.Values.Select(s => s.Descriptor.SourceId == sourceId ? changed : s), p.Routing, p.Assets, p.Automation, p.RenderSettings));
    }
    private static void CheckAutomation(ProjectDocument document, ProjectSource source, string parameterId)
    {
        var targets = source.Plugin!.Targets.Where(t => t.ParameterId == parameterId).ToArray();
        var bindings = source.Bindings.Select(b => b.Id).ToHashSet();
        if (document.Snapshot.Automation.Any(l => bindings.Contains(l.GraphBinding) && (l.TargetKind == AutomationTargetKind.PluginParameter ? l.ParameterId == parameterId : targets.Any(t => t.NodeId == l.NodeId && t.DeviceParameterId == l.ParameterId))))
            throw new InvalidOperationException("Automation owns a target of this plugin parameter");
    }
    private static void RequireLiveDevice(PluginPackage plugin)
    {
        if (plugin.Manifest.Kind is not (FlowPluginKind.AudioEffect or FlowPluginKind.Instrument))
            throw new InvalidOperationException("Offline processor parameters require reprocessing the captured input");
    }
}
