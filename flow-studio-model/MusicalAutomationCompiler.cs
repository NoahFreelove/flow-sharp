using Flow.Audio.Graph;
using Flow.Studio.Model;
namespace Flow.Studio.Model;

public static class MusicalAutomationCompiler
{
    public static GraphAutomationLane Lower(ProjectAutomationLane lane, ProjectTempoMap tempo, int sampleRate)
    {
        if (lane.TargetKind != AutomationTargetKind.GraphNode)
            throw new InvalidOperationException("Resolve public plugin targets before lowering automation");
        if (sampleRate is < 1 or > 384000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        var positions = lane.Points.Select(p => p.Quarter);
        // Musical linear slopes change in seconds at tempo boundaries. Insert those
        // knots before frame conversion to preserve the authored musical curve.
        if (lane.Shape == AutomationShape.Linear)
            positions = positions.Concat(tempo.Changes.Where(t => t.Quarter > lane.Points[0].Quarter && t.Quarter < lane.Points[^1].Quarter).Select(t => t.Quarter));
        var quarters = positions.Distinct().Order().Take(100001).ToArray();
        if (quarters.Length > 100000) throw new ArgumentException("Lowered automation exceeds point budget");
        var result = new List<AutomationPoint>(); int previous = 0;
        foreach (double quarter in quarters)
        {
            while (previous + 1 < lane.Points.Count && lane.Points[previous + 1].Quarter <= quarter) previous++;
            double value = lane.Points[previous].Value;
            if (lane.Shape == AutomationShape.Linear && previous + 1 < lane.Points.Count)
            {
                var left = lane.Points[previous]; var right = lane.Points[previous + 1];
                double fraction = (quarter - left.Quarter) / (right.Quarter - left.Quarter);
                value = left.Value * (1 - fraction) + right.Value * fraction;
            }
            double position = Math.Round(tempo.SecondsAt(quarter) * sampleRate, MidpointRounding.AwayFromZero);
            if (!double.IsFinite(position) || position >= long.MaxValue) throw new ArgumentException("Automation position exceeds frame range");
            var point = new AutomationPoint((long)position, value);
            // Last authored value wins if sub-frame points round onto one frame.
            if (result.Count > 0 && result[^1].Frame == point.Frame) result[^1] = point; else result.Add(point);
        }
        return new(lane.NodeId, lane.ParameterId, result, lane.Shape);
    }
}
