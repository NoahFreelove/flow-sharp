using System.Text;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// Pins the type relations and overload resolution that moving music types behind
/// bindings must preserve (roadmap 4.3: "preserve overload ranking and strict-mode
/// behavior before attempting to simplify them").
/// </summary>
[Collection("FlowScripts")]
public class TypeSystemCharacterizationTests
{
    /// <summary>One representative per type family, primitive and musical.</summary>
    internal static readonly (string Label, FlowType Type)[] Representatives =
    [
        ("Int", IntType.Instance), ("Long", LongType.Instance), ("Float", FloatType.Instance),
        ("Double", DoubleType.Instance), ("Number", NumberType.Instance), ("String", StringType.Instance),
        ("Bool", BoolType.Instance), ("Symbol", SymbolType.Instance), ("Void", VoidType.Instance),
        ("Function", FunctionType.Instance), ("Lazy<Void>", new LazyType(VoidType.Instance)),
        ("Int[]", new ArrayType(IntType.Instance)), ("String[]", new ArrayType(StringType.Instance)),
        ("Void[]", new ArrayType(VoidType.Instance)), ("Note[]", new ArrayType(NoteType.Instance)),
        ("Tuple<<Int,Int>>", new TupleType([IntType.Instance, IntType.Instance])),
        ("Dict<String,Int>", new DictType(StringType.Instance, IntType.Instance)),
        ("Buffer", BufferType.Instance), ("Note", NoteType.Instance), ("Chord", ChordType.Instance),
        ("Sequence", SequenceType.Instance), ("Bar", BarType.Instance), ("Section", SectionType.Instance),
        ("Song", SongType.Instance), ("Semitone", SemitoneType.Instance), ("Cent", CentType.Instance),
        ("Millisecond", MillisecondType.Instance), ("Second", SecondType.Instance),
        ("Decibel", DecibelType.Instance), ("Hertz", HertzType.Instance), ("Beat", BeatType.Instance),
        ("NoteValue", NoteValueType.Instance), ("TimeSignature", TimeSignatureType.Instance),
    ];

    [Fact]
    public void TypeRelationsAreUnchanged()
    {
        var sb = new StringBuilder();
        sb.Append("# from -> to: compatible/convertible/equal; specificity(from)\n");
        foreach (var (fromLabel, from) in Representatives)
        {
            sb.Append(fromLabel).Append(" (").Append(from.Name).Append(", specificity ")
              .Append(from.GetSpecificity()).Append(")\n");
            foreach (var (toLabel, to) in Representatives)
            {
                var flags = $"{(from.IsCompatibleWith(to) ? 'C' : '-')}{(from.CanConvertTo(to) ? 'V' : '-')}{(from.Equals(to) ? '=' : '-')}";
                if (flags != "---") sb.Append("  -> ").Append(toLabel).Append(": ").Append(flags).Append('\n');
            }
        }
        Snapshot.Verify("types/relations.txt", sb.ToString());
    }

    [Fact]
    public void OverloadResolutionIsUnchanged()
    {
        using var engine = LoadAllModules();
        var frame = engine.Context.GlobalFrame;
        var sb = new StringBuilder();
        foreach (var name in frame.GetFunctionNames().Order(StringComparer.Ordinal))
        {
            var overloads = frame.GetFunctionOverloads(name);
            var signatures = overloads.Select(o => o.Signature).Distinct().ToList();
            sb.Append("## ").Append(name).Append(" (").Append(signatures.Count).Append(" overloads)\n");
            foreach (var signature in signatures.OrderBy(s => s.ToString(), StringComparer.Ordinal))
            {
                sb.Append(signature).Append('\n');
                Probe(sb, name, overloads, signature.InputTypes.ToList(), "exact", strict: false);
                for (int slot = 0; slot < signature.InputTypes.Count; slot++)
                {
                    sb.Append("  #").Append(slot).Append(':');
                    foreach (var (label, type) in Representatives)
                    {
                        var args = signature.InputTypes.ToList();
                        args[slot] = type;
                        var charitable = Resolve(name, overloads, args, strict: false);
                        var strict = Resolve(name, overloads, args, strict: true);
                        // Record only the probes that resolve to something other than
                        // this signature, plus any strict-mode difference.
                        if (charitable == signature.ToString() && strict == charitable) continue;
                        // Unresolvable in both modes is the common case; omitting it keeps the
                        // snapshot readable (a regression to "none" still shows as a removal).
                        if (charitable == "none" && strict == "none") continue;
                        sb.Append(' ').Append(label).Append('=').Append(Short(charitable, signature));
                        if (strict != charitable) sb.Append("|strict:").Append(Short(strict, signature));
                    }
                    sb.Append('\n');
                }
            }
        }
        Snapshot.Verify("types/overloads.txt", sb.ToString());
    }

    private static void Probe(StringBuilder sb, string name, IReadOnlyList<FunctionOverload> overloads,
        List<FlowType> args, string label, bool strict)
    {
        var result = Resolve(name, overloads, args, strict);
        var expected = overloads.FirstOrDefault(o => o.Signature.InputTypes.SequenceEqual(args))?.Signature.ToString();
        if (result != expected) sb.Append("  ").Append(label).Append(" -> ").Append(result).Append('\n');
    }

    private static string Short(string result, FunctionSignature self) =>
        result == self.ToString() ? "self" : result;

    private static string Resolve(string name, IReadOnlyList<FunctionOverload> overloads, List<FlowType> args, bool strict)
    {
        var reporter = new ErrorReporter();
        var resolver = new OverloadResolver(reporter);
        var winner = resolver.Resolve(name, overloads, args, strictMode: strict);
        if (winner is not null) return winner.Signature.ToString();
        var message = reporter.Errors.FirstOrDefault()?.Message ?? "";
        return message.StartsWith("Ambiguous", StringComparison.Ordinal) ? "ambiguous" : "none";
    }

    /// <summary>An engine with every shipped module imported, so all builtins are visible.</summary>
    internal static FlowEngine LoadAllModules()
    {
        var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var ok = engine.Execute("""
            use "@std"
            use "@audio"
            use "@notation"
            use "@composition"
            use "@patterns"
            use "@generative"
            use "@improv"
            use "@notation-io"
            use "@test"
            use "@sfz"
            use "@osc"
            use "@midi"
            use "@jack"
            """, "<all-modules>");
        Assert.True(ok, engine.ErrorReporter.FormatAll(engine.SourceMap, useColor: false));
        return engine;
    }
}
