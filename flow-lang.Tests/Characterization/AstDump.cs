using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.TypeSystem;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// Canonical, location-free text form of a parsed program. Type annotations print
/// by name, so the dump is stable when the parser's type representation changes
/// but shows any change in what was parsed.
/// </summary>
internal static class AstDump
{
    private static readonly HashSet<string> SkippedProperties = new(StringComparer.Ordinal)
    {
        "Location", "Span", "EqualityContract", "ResolvedType", "EffectiveSpan",
    };

    /// <summary>Parses <paramref name="source"/> the way <c>FlowEngine</c> does and dumps it.</summary>
    public static string Parse(string source, string fileName)
    {
        var reporter = new ErrorReporter();
        var (pragmas, transformed) = PragmaScanner.Scan(source, fileName, reporter);
        var sb = new StringBuilder();
        if (!reporter.HasErrors)
        {
            var tokens = new SimpleLexer(transformed, reporter, fileName, pragmas).Tokenize();
            if (!reporter.HasErrors)
            {
                try
                {
                    var program = new Parser(tokens, reporter, pragmas).Parse();
                    Write(sb, program, 0);
                }
                catch (Exception ex)
                {
                    sb.Append("<parser threw ").Append(ex.GetType().Name).Append(": ").Append(ex.Message).Append('>');
                }
            }
        }
        foreach (var error in reporter.Errors)
            sb.Append("\n! ").Append(error.Message);
        foreach (var diagnostic in reporter.Diagnostics)
            sb.Append("\n! ").Append(diagnostic.Message);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, object? value, int depth)
    {
        if (depth > 400) { sb.Append("<depth>"); return; }
        switch (value)
        {
            case null: sb.Append("null"); return;
            case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")).Append('"'); return;
            case char c: sb.Append('\'').Append(c).Append('\''); return;
            case bool b: sb.Append(b ? "true" : "false"); return;
            case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
            case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return;
            case BigInteger bi: sb.Append(bi.ToString(CultureInfo.InvariantCulture)).Append('n'); return;
            case IFormattable n when value.GetType().IsPrimitive || value is decimal:
                sb.Append(n.ToString(null, CultureInfo.InvariantCulture)); return;
            case Enum e: sb.Append(e.GetType().Name).Append('.').Append(e); return;
            case FlowType t: sb.Append("type ").Append(t.Name); return;
            case Core.SourceLocation or Core.Span: sb.Append("<loc>"); return;
            case PragmaSet p: sb.Append("pragmas[").Append(string.Join(",", p.Enabled.Order(StringComparer.Ordinal))).Append(']'); return;
            case IDictionary dict:
                sb.Append('{');
                foreach (DictionaryEntry entry in dict)
                {
                    sb.Append(' ');
                    Write(sb, entry.Key, depth + 1);
                    sb.Append(": ");
                    Write(sb, entry.Value, depth + 1);
                }
                sb.Append(" }");
                return;
            case IEnumerable list:
                sb.Append('[');
                bool first = true;
                foreach (var item in list)
                {
                    sb.Append(first ? "" : ",").Append('\n').Append(' ', (depth + 1) * 2);
                    Write(sb, item, depth + 1);
                    first = false;
                }
                sb.Append(']');
                return;
        }

        var type = value.GetType();
        if (type.Namespace?.StartsWith("FlowLang", StringComparison.Ordinal) != true)
        {
            sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            return;
        }

        sb.Append(type.Name).Append(" {");
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.GetIndexParameters().Length == 0 && !SkippedProperties.Contains(p.Name))
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            sb.Append('\n').Append(' ', (depth + 1) * 2).Append(prop.Name).Append(": ");
            object? propValue;
            try { propValue = prop.GetValue(value); }
            catch (TargetInvocationException ex) { propValue = "<" + ex.InnerException?.GetType().Name + ">"; }
            Write(sb, propValue, depth + 1);
        }
        sb.Append(" }");
    }
}
