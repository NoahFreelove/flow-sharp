using Flow.Audio.Graph;
namespace Flow.Studio.Model;

public enum AutomationTargetKind { GraphNode, PluginParameter }

public sealed record MusicalAutomationPoint(double Quarter, double Value);
public sealed class ProjectAutomationLane
{
    public AutomationTargetKind TargetKind { get; }
    public Guid Id { get; }
    public Guid GraphBinding { get; }
    public string NodeId { get; }
    public string ParameterId { get; }
    public AutomationShape Shape { get; }
    public IReadOnlyList<MusicalAutomationPoint> Points { get; }
    public ProjectAutomationLane(Guid id, Guid graphBinding, string nodeId, string parameterId,
        IEnumerable<MusicalAutomationPoint> points, AutomationShape shape = AutomationShape.Linear, AutomationTargetKind targetKind = AutomationTargetKind.GraphNode)
    {
        Id = Validate.Id(id, nameof(id)); GraphBinding = Validate.Id(graphBinding, nameof(graphBinding));
        if (!Enum.IsDefined(targetKind)) throw new ArgumentOutOfRangeException(nameof(targetKind));
        if (targetKind == AutomationTargetKind.GraphNode) ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        else if (nodeId != "") throw new ArgumentException("Public plugin lanes do not address internal node IDs");
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);
        TargetKind = targetKind;
        var copy = points.Take(100001).ToArray();
        if (copy.Length is < 1 or > 100000 || !Enum.IsDefined(shape)) throw new ArgumentException("Invalid automation lane");
        double previous = -1;
        foreach (var p in copy)
        {
            if (p is null || !double.IsFinite(p.Quarter) || p.Quarter < 0 || p.Quarter <= previous || !double.IsFinite(p.Value))
                throw new ArgumentException("Musical automation points must be finite and strictly ordered");
            previous = p.Quarter;
        }
        NodeId = nodeId; ParameterId = parameterId; Shape = shape; Points = Array.AsReadOnly(copy);
    }
}
public static class ProjectAutomationCommands
{
    public static void Set(ProjectDocument document, ProjectAutomationLane lane) => document.Edit("Edit automation", p =>
        new(p.Arrangement, p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation.Where(a => a.Id != lane.Id).Append(lane), p.RenderSettings));
    public static void Remove(ProjectDocument document, Guid id) => document.Edit("Remove automation", p =>
        p.Automation.Any(a => a.Id == id) ? new(p.Arrangement, p.Context, p.Sources.Values, p.Routing, p.Assets,
            p.Automation.Where(a => a.Id != id), p.RenderSettings) : p);
}
