using System.Text;
using Flow.Audio.Graph;
using Flow.Studio.Model;
namespace FlowLang.Hosting;

public static class FlowAutomationExporter
{
    /// <summary>Executable construction of musical curves. Returns DawAutomations.
    /// Preserves stable identities/targets and authored values, not editor formatting.</summary>
    public static string Export(IEnumerable<ProjectAutomationLane> lanes)
    {
        var copy = lanes.Take(257).ToArray();
        if (copy.Length is < 1 or > 256 || copy.Sum(l => (long)l.Points.Count) > 100000) throw new ArgumentException("Curve export budget exceeded");
        var text = new StringBuilder("use \"@flowDaw\"\n");
        for (int i = 0; i < copy.Length; i++)
        {
            var lane = copy[i];
            string points = string.Join(' ', lane.Points.Select(p => FlowGraphExporter.Number(p.Quarter) + " " + FlowGraphExporter.Number(p.Value)));
            text.AppendLine($"Dict<Double, Double> curve{i}Points = (dict {points})");
            string constructor = lane.TargetKind == AutomationTargetKind.PluginParameter ? "dawPluginCurve" : "dawCurve";
            string node = lane.TargetKind == AutomationTargetKind.PluginParameter ? "" : Quote(lane.NodeId) + " ";
            text.AppendLine($"DawAutomation curve{i} = ({constructor} \"{lane.Id}\" \"{lane.GraphBinding}\" {node}{Quote(lane.ParameterId)} curve{i}Points {(lane.Shape == AutomationShape.Linear ? "true" : "false")})");
        }
        text.AppendLine("(list " + string.Join(' ', Enumerable.Range(0, copy.Length).Select(i => "curve" + i)) + ")");
        return text.ToString();
    }
    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
}
