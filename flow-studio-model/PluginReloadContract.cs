using Flow.Audio.Graph;

namespace Flow.Studio.Model;

/// <summary>Compatibility for replacing an accepted packaged device in place.
/// Breaking changes require explicit migration or a new independently assigned instance.</summary>
public static class PluginReloadContract
{
    public static void Validate(ProjectSource previous, PluginPackage next, GeneratedSourceOutput result,
        IEnumerable<ProjectAutomationLane> automation)
    {
        ArgumentNullException.ThrowIfNull(previous); ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(result); ArgumentNullException.ThrowIfNull(automation);
        var oldPackage = previous.Plugin ?? throw new ArgumentException("Previous source is not a plugin");
        var old = oldPackage.Manifest; var updated = next.Manifest;
        if (old.Id != updated.Id || old.Kind != updated.Kind || old.StateSchema != updated.StateSchema ||
            old.StatePolicy != updated.StatePolicy || old.AudioInputs != updated.AudioInputs || old.AudioOutputs != updated.AudioOutputs ||
            old.NoteInput != updated.NoteInput || old.NoteOutput != updated.NoteOutput || oldPackage.OutputLayer != next.OutputLayer)
            throw new InvalidOperationException("Incompatible plugin identity, state schema or ports; create a new instance or explicitly migrate");
        foreach (var parameter in old.Parameters)
        {
            var replacement = updated.Parameters.SingleOrDefault(p => p.Id == parameter.Id);
            if (replacement is null || parameter.Unit != replacement.Unit || parameter.Scale != replacement.Scale ||
                parameter.Minimum != replacement.Minimum || parameter.Maximum != replacement.Maximum ||
                parameter.RequiresRebuild != replacement.RequiresRebuild || parameter.SmoothingMilliseconds != replacement.SmoothingMilliseconds ||
                !parameter.Labels.SequenceEqual(replacement.Labels))
                throw new InvalidOperationException($"Incompatible public parameter '{parameter.Id}'; explicit migration is required");
        }
        // Public lanes follow stable public IDs through new target mappings. Direct
        // node lanes deliberately address implementation details and cannot silently
        // change kernel semantics or lose their target during reload.
        foreach (var lane in automation.Where(l => l.TargetKind == AutomationTargetKind.GraphNode))
        {
            var binding = previous.Bindings.SingleOrDefault(b => b.Id == lane.GraphBinding);
            if (binding is null) continue;
            var oldNode = Graph(previous.Result, binding.Output).Nodes.SingleOrDefault(n => n.Id == lane.NodeId);
            var newNode = Graph(result, binding.Output).Nodes.SingleOrDefault(n => n.Id == lane.NodeId);
            if (oldNode is null || newNode is null || oldNode.DeviceId != newNode.DeviceId || oldNode.Version != newNode.Version ||
                !newNode.Parameters.ContainsKey(lane.ParameterId))
                throw new InvalidOperationException($"Reload would invalidate direct automation for '{lane.NodeId}.{lane.ParameterId}'");
        }
    }
    private static AudioGraphDefinition Graph(GeneratedSourceOutput result, OutputKey key) => key.Role switch
    {
        GeneratedRole.Graph => result.GraphLayers.SingleOrDefault(l => l.Id == key.LayerId)?.Graph
            ?? throw new InvalidOperationException("Reload removed an automated graph"),
        GeneratedRole.Instrument => result.InstrumentLayers.SingleOrDefault(l => l.Id == key.LayerId)?.Instrument.VoiceGraph
            ?? throw new InvalidOperationException("Reload removed an automated instrument graph"),
        _ => throw new InvalidOperationException("Automation does not target a device graph")
    };
}
