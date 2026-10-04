using System.Globalization;
using System.Text;
using Flow.Audio.Graph;

namespace FlowLang.Hosting;

/// <summary>Explicit executable construction code for the supported catalog.
/// Exports connections/IDs/version/parameters/bypass without rewriting user code.</summary>
public static class FlowGraphExporter
{
    public static string Export(AudioGraphDefinition graph) => Export(graph, "graphNode");
    internal static string Export(AudioGraphDefinition graph, string prefix)
    {
        var text = new StringBuilder("use \"@flowDaw\"\n");
        var variables = new Dictionary<string, string>();
        int index = 0;
        foreach (var node in graph.GetProcessingOrder())
        {
            string variable = prefix + index++;
            variables.Add(node.Id, variable);
            if (node.DeviceId == "flow.sample")
                text.AppendLine($"AudioGraph {variable} = (dawSample \"{node.Id}\" {variables[node.Inputs[0]]} {variables[node.Inputs[1]]} {(int)node.Parameters["asset"]} {Number(node.Parameters["rootHz"])})");
            else if (node.DeviceId == "flow.sum")
            {
                string array = variable + "Inputs";
                text.AppendLine($"AudioGraphs {array} = (list {string.Join(' ', node.Inputs.Select(i => variables[i]))})");
                text.AppendLine($"AudioGraph {variable} = (dawSum \"{node.Id}\" {array})");
            }
            else if (node.DeviceId == "flow.input")
                text.AppendLine($"AudioGraph {variable} = (dawInput \"{node.Id}\" {(int)node.Parameters["bus"]})");
            else if (node.DeviceId == "flow.value")
                text.AppendLine($"AudioGraph {variable} = (dawValue \"{node.Id}\" {Number(node.Parameters["value"])})");
            else if (node.DeviceId == "flow.multiply")
                text.AppendLine($"AudioGraph {variable} = (dawMultiply \"{node.Id}\" {variables[node.Inputs[0]]} {variables[node.Inputs[1]]})");
            else if (node.DeviceId == "flow.tanh")
                text.AppendLine($"AudioGraph {variable} = (dawTanh \"{node.Id}\" {variables[node.Inputs[0]]})");
            else if (node.DeviceId == "flow.sineOsc")
                text.AppendLine($"AudioGraph {variable} = (dawSineOsc \"{node.Id}\" {variables[node.Inputs[0]]})");
            else if (node.DeviceId == "flow.lowPass")
                text.AppendLine($"AudioGraph {variable} = (dawLowPass \"{node.Id}\" {variables[node.Inputs[0]]} {variables[node.Inputs[1]]})");
            else
            {
                string values = string.Join(' ', node.Parameters.Select(p => $"\"{p.Key}\" {Number(p.Value)}"));
                text.AppendLine($"Dict<String, Double> {variable}Params = (dict {values})");
                text.AppendLine($"AudioGraph {variable} = (dawDevice \"{node.Id}\" \"{node.DeviceId}\" {node.Version} {variables[node.Inputs[0]]} {variable}Params {node.Bypassed.ToString().ToLowerInvariant()})");
            }
        }
        text.AppendLine(variables[graph.OutputId]);
        return text.ToString();
    }
    internal static string Number(double value)
    {
        // Flow uses (neg x), and its numeric lexer has no scientific notation.
        if (value < 0) return "(neg " + Number(-value) + ")";
        string result = value.ToString("R", CultureInfo.InvariantCulture);
        int exponentAt = result.IndexOf('E');
        if (exponentAt >= 0)
        {
            string mantissa = result[..exponentAt];
            int point = mantissa.IndexOf('.');
            if (point < 0) point = mantissa.Length;
            string digits = mantissa.Replace(".", "");
            point += int.Parse(result[(exponentAt + 1)..], CultureInfo.InvariantCulture);
            result = point <= 0 ? "0." + new string('0', -point) + digits :
                point >= digits.Length ? digits + new string('0', point - digits.Length) + ".0" :
                digits.Insert(point, ".");
        }
        return result.Contains('.') ? result : result + ".0";
    }
}
