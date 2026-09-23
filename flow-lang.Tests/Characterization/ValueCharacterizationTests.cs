using System.Reflection;
using System.Text;
using FlowLang.Core;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using Xunit;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// Pins value behavior that moves behind domain bindings in the extraction:
/// formatting, equality and hashing, Value.ConvertTo, and member access
/// (explicit members plus the reflection fallback that exposes CLR properties).
/// </summary>
[Collection("FlowScripts")]
public class ValueCharacterizationTests
{
    /// <summary>Flow expressions producing one representative value per type.</summary>
    private static readonly string[] Representatives =
    [
        "3", "3000000000", "99999999999999999999", "1.5", "(float 1.5)", "\"text\"", "true", "#tag",
        "C4", "F4+50c", "Cmaj7", "| C4 D4 E4 F4 |", "+2st", "+50c", "100ms", "2.5s", "-6dB", "440Hz",
        "1b", "q", "[1, 2, 3]", "(list \"a\" \"b\")", "(list)", "<<1, \"a\">>", "(dict \"a\" 1)",
        "(createSineTone 440Hz 0.01 0.5)", "(Nothing)", "fn Int n => (mul n 2)", "lazy ((add 1 1))",
    ];

    private static (FlowEngine Engine, List<(string Source, Value Value)> Values) Bind()
    {
        var engine = TypeSystemCharacterizationTests.LoadAllModules();
        var values = new List<(string, Value)>();
        foreach (var source in Representatives)
        {
            var result = engine.Evaluate(source, "<value>");
            Assert.True(result.Succeeded, $"{source}: {engine.ErrorReporter.FormatAll(engine.SourceMap, useColor: false)}");
            values.Add((source, result.LastValue ?? Value.Void()));
        }
        return (engine, values);
    }

    [Fact]
    public void FormattingEqualityAndHashingAreUnchanged()
    {
        var (engine, values) = Bind();
        using var _ = engine;
        var sb = new StringBuilder();
        foreach (var (source, value) in values)
        {
            sb.Append(source).Append(" : ").Append(value.Type.Name).Append('\n');
            sb.Append("  autostr: ").Append(StdLib.AutoStr(value)).Append('\n');
            sb.Append("  tostring: ").Append(value.ToString()).Append('\n');
            var loose = values.Where(o => Utils.LooseEquals(value, o.Value)).Select(o => o.Source);
            var strict = values.Where(o => Utils.StrictEquals(value, o.Value)).Select(o => o.Source);
            var clr = values.Where(o => value.Equals(o.Value)).Select(o => o.Source);
            sb.Append("  loose-equals: ").Append(string.Join(" | ", loose)).Append('\n');
            sb.Append("  strict-equals: ").Append(string.Join(" | ", strict)).Append('\n');
            sb.Append("  value-equals: ").Append(string.Join(" | ", clr)).Append('\n');
            foreach (var (otherSource, other) in values)
            {
                if (value.Equals(other) && value.GetHashCode() != other.GetHashCode())
                    sb.Append("  HASH MISMATCH with ").Append(otherSource).Append('\n');
            }
        }
        Snapshot.Verify("values/formatting-equality.txt", sb.ToString());
    }

    [Fact]
    public void ValueConversionsAreUnchanged()
    {
        var (engine, values) = Bind();
        using var _ = engine;
        var sb = new StringBuilder();
        foreach (var (source, value) in values)
        {
            sb.Append(source).Append(" : ").Append(value.Type.Name).Append('\n');
            foreach (var (label, type) in TypeSystemCharacterizationTests.Representatives)
            {
                if (!value.Type.CanConvertTo(type) && !value.Type.IsCompatibleWith(type)) continue;
                string outcome;
                try
                {
                    var converted = value.ConvertTo(type);
                    outcome = $"{converted.Type.Name} {StdLib.AutoStr(converted)}";
                }
                catch (Exception ex)
                {
                    outcome = $"throws {ex.GetType().Name}";
                }
                sb.Append("  -> ").Append(label).Append(": ").Append(outcome).Append('\n');
            }
        }
        Snapshot.Verify("values/conversions.txt", sb.ToString());
    }

    [Fact]
    public void MemberAccessIsUnchanged()
    {
        var sb = new StringBuilder();
        var explicitMembers = new[]
        {
            "Root", "Quality", "Octave", "NoteNames", "TimeSignature", "Count", "Name", "SequenceCount",
            "SectionCount", "OffsetBeats", "Gain", "Pan", "SampleRate", "Channels",
        };
        var extra = new[]
        {
            "section verse { Sequence s = | C4 | }", "Song song = [verse verse]",
        };
        foreach (var source in Representatives.Concat(["(getSections song)@0", "song"]))
        {
            var output = new StringWriter();
            using var engine = new FlowEngine(new EngineOptions { Output = output, Diagnostics = TextWriter.Null });
            engine.Execute("use \"@std\"\nuse \"@audio\"\n" + string.Join("\n", extra) + $"\nVoid v = {source}", "<members>");
            if (!engine.Context.GlobalFrame.TryGetVariable("v", out var value))
            {
                sb.Append(source).Append(" : <not bindable>\n");
                continue;
            }
            var names = new SortedSet<string>(explicitMembers, StringComparer.Ordinal);
            if (value.Data is not null)
                foreach (var p in value.Data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    if (p.GetIndexParameters().Length == 0) names.Add(p.Name);

            sb.Append(source).Append(" : ").Append(value.Type.Name).Append('\n');
            foreach (var name in names)
            {
                output.GetStringBuilder().Clear();
                var result = engine.Evaluate($"(print $\"{{v.{name}}}\")", "<member>");
                var text = result.Succeeded
                    ? output.ToString().TrimEnd('\n')
                    : "error: " + (result.Errors.FirstOrDefault()?.Message ?? result.Diagnostics.FirstOrDefault()?.Message);
                if (text.StartsWith("error: Type '", StringComparison.Ordinal)) continue;   // no such member
                text = System.Text.RegularExpressions.Regex.Replace(text, "__lambda_[0-9a-f]{32}", "__lambda_<id>");
                if (text.Length > 120) text = text[..120] + "…";
                sb.Append("  .").Append(name).Append(" = ").Append(text).Append('\n');
            }
        }
        Snapshot.Verify("values/members.txt", sb.ToString());
    }
}
