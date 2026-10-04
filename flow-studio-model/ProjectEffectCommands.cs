namespace Flow.Studio.Model;

public static class ProjectEffectCommands
{
    /// <summary>Replace ordered insert references atomically; omission removes an
    /// insert, order changes reorder it, and Bypassed changes bypass. Sources and
    /// automation remain available for reinsertion and undo.</summary>
    public static void Set(ProjectDocument document, IEnumerable<ProjectEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(effects);
        var next = Replace(document.Snapshot, effects);
        document.Edit("Edit effect chains", _ => next);
    }
    public static ProjectSnapshot Replace(ProjectSnapshot project, IEnumerable<ProjectEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(effects);
        var routing = new ProjectRouting(project.Routing.Tracks, project.Routing.GraphBinding, effects);
        foreach (var effect in routing.Effects)
        {
            var source = project.Sources.Values.SingleOrDefault(s => s.Bindings.Any(b => b.Id == effect.Binding && b.Available && b.Output.Role == GeneratedRole.Graph));
            if (source?.Plugin is not { } plugin || plugin.Manifest.Kind != FlowPluginKind.AudioEffect || plugin.Manifest.AudioInputs != 1 ||
                !source.Bindings.Any(b => b.Id == effect.Binding && b.Output.LayerId == plugin.OutputLayer))
                throw new ArgumentException("Insert requires an available single-input packaged effect");
        }
        if (project.Routing.Effects.SequenceEqual(routing.Effects)) return project;
        return new(project.Arrangement, project.Context, project.Sources.Values, routing, project.Assets, project.Automation, project.RenderSettings);
    }
}
