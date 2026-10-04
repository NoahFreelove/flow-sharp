namespace Flow.Audio.Graph;

public enum AutomationShape { Step, Linear }
public sealed record AutomationPoint(long Frame, double Value);
/// <summary>Immutable frame-domain automation. Before the first point, use the
/// authored device value; after the last point, hold. Shape applies between points.</summary>
public sealed class GraphAutomationLane
{
    public string NodeId { get; }
    public string ParameterId { get; }
    public AutomationShape Shape { get; }
    public IReadOnlyList<AutomationPoint> Points { get; }
    public GraphAutomationLane(string nodeId, string parameterId, IEnumerable<AutomationPoint> points,
        AutomationShape shape = AutomationShape.Linear)
    {
        AudioGraphNode.CheckId(nodeId); ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);
        var copy = points.Take(100001).ToArray();
        if (copy.Length is < 1 or > 100000 || !Enum.IsDefined(shape)) throw new ArgumentException("Invalid automation lane");
        long previous = -1;
        foreach (var point in copy)
        {
            if (point is null || point.Frame < 0 || point.Frame <= previous || !double.IsFinite(point.Value))
                throw new ArgumentException("Automation points must be finite and strictly ordered");
            previous = point.Frame;
        }
        NodeId = nodeId; ParameterId = parameterId; Shape = shape; Points = Array.AsReadOnly(copy);
    }
    public double ValueAt(long frame, double initialValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        int low = 0, high = Points.Count;
        while (low < high) { int mid = low + (high - low) / 2; if (Points[mid].Frame <= frame) low = mid + 1; else high = mid; }
        return ValueAtIndex(frame, low - 1, initialValue);
    }
    internal double ValueAtIndex(long frame, int previous, double initialValue)
    {
        if (previous < 0) return initialValue;
        var left = Points[previous];
        if (Shape == AutomationShape.Step || previous == Points.Count - 1) return left.Value;
        var right = Points[previous + 1];
        double fraction = (frame - left.Frame) / (double)(right.Frame - left.Frame);
        return left.Value * (1 - fraction) + right.Value * fraction;
    }
}
