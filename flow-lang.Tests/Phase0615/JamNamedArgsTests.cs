using FlowLang.Runtime;
using FlowLang.TypeSystem.SpecialTypes;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Phase0615;

/// <summary>
/// Feature-addition 0615 (#8 jam-named-args) — jam's documented SPARSE
/// named-arg surface now resolves via OverloadResolver per-parameter
/// default-fill against a single collapsed 6-param signature
/// <c>jam(over, style, length, key, seed, order)</c>.
///
/// <para>
/// Before this feature these two documented call forms FAILED "No matching
/// overload" because the named-arg resolver required the supplied names to
/// EXACTLY cover one of jam's six contiguous-prefix arity overloads — it did
/// not default-fill skipped middle slots:
/// <code>
///   (jam over=chords style=#jazz seed=42)          // skips length + key
///   (jam over=chords key="Cmajor" length=4)        // out-of-order + key= label
/// </code>
/// The second form ALSO tripped a parse-time defect: <c>key</c> lexes as
/// <c>TokenType.Key</c> (a musical-context keyword), not <c>Identifier</c>, so
/// the named-arg label detector never saw it.
/// </para>
///
/// <para>
/// These tests fail before the feature (resolution error / parse error) and
/// pass after. They also pin that positional jam is unchanged and that
/// default-fill produces the SAME output as the equivalent fully-positional
/// call — proving the defaults are wired through to the handler correctly.
/// </para>
/// </summary>
[Collection("FlowScripts")]
public class JamNamedArgsTests
{
    private const string OverDecl =
        "use \"@std\"\n" +
        "use \"@improv\"\n" +
        "Sequence over = | Cmaj7 Am7 Dm7 G7 |\n";

    [Fact]
    public void SparseNamedArgs_SkipLengthAndKey_Resolves()
    {
        // THE headline form: (jam over=chords style=#jazz seed=42) — length and
        // key are skipped and must default-fill (length=8, key=context/Cmajor).
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource(
            OverDecl + "Sequence improvised = (jam over=over style=#jazz seed=42)\n");

        Assert.True(success, $"Sparse named-arg jam failed; stderr:\n{stderr}");
        var seq = runner.GetVariable("improvised").As<SequenceData>();
        // length defaulted to 8.
        Assert.Equal(8, seq.Bars.Count);
    }

    [Fact]
    public void SparseNamedArgs_KeyLabel_OutOfOrder_Resolves()
    {
        // (jam over=chords key="Cmajor" length=4) — exercises BOTH the key=
        // keyword-token label AND out-of-source-order named args (key before
        // length) with style + seed + order default-filled.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource(
            OverDecl + "Sequence improvised = (jam over=over key=\"Cmajor\" length=4)\n");

        Assert.True(success, $"key= named-arg jam failed; stderr:\n{stderr}");
        var seq = runner.GetVariable("improvised").As<SequenceData>();
        Assert.Equal(4, seq.Bars.Count);
    }

    [Fact]
    public void PositionalJam_StillResolves_AllArities()
    {
        // The collapse to a single defaulted signature must NOT break the
        // existing positional surface: 1-arg, partial, and fully-positional.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource(
            OverDecl +
            "Sequence one = (jam over)\n" +                       // 1-arg
            "Sequence three = (jam over #jazz 4)\n" +              // 3-arg partial
            "Sequence six = (jam over #jazz 4 \"Cmajor\" 42 2)\n"); // full positional

        Assert.True(success, $"Positional jam regressed; stderr:\n{stderr}");
        Assert.Equal(8, runner.GetVariable("one").As<SequenceData>().Bars.Count);
        Assert.Equal(4, runner.GetVariable("three").As<SequenceData>().Bars.Count);
        Assert.Equal(4, runner.GetVariable("six").As<SequenceData>().Bars.Count);
    }

    [Fact]
    public void DefaultFill_MatchesEquivalentPositionalCall()
    {
        // (jam over=over style=#jazz length=4 seed=42 order=2) default-fills
        // key, and MUST produce byte-identical notes to the fully-positional
        // call with key="Cmajor" (the charitable default when no context key is
        // set). This proves defaults reach the handler in the right slots.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource(
            OverDecl +
            "Sequence named = (jam over=over style=#jazz length=4 seed=42 order=2)\n" +
            "Sequence positional = (jam over #jazz 4 \"Cmajor\" 42 2)\n");

        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        var named = runner.GetVariable("named").As<SequenceData>();
        var positional = runner.GetVariable("positional").As<SequenceData>();
        AssertSequenceEqual(named, positional);
    }

    [Fact]
    public void OtherOverloadedBuiltins_StillResolveIdentically()
    {
        // REGRESSION GUARD: the default-fill machinery is gated on
        // FunctionSignature.HasParameterDefaults, so it must be inert for every
        // builtin that lacks defaults. Exercise a spread of overloaded builtins
        // by both positional and named-arg paths and confirm correct results.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, _) = runner.RunSource("""
            use "@std"
            Int sum = (add 5 3)
            Double dsum = (add 2.5 1.5)
            Sequence seq = | C4q D4q E4q |
            Sequence up = (transpose seq amount=+2st)
            Sequence shifted = (transpose seq +50c)
            Int idx = (length (list 1 2 3))
            (print (str sum))
            (print (str dsum))
            (print (str idx))
            """);

        Assert.True(success, $"Other-builtin resolution regressed; stderr:\n{stderr}");
        Assert.Contains("8", stdout);     // add Int
        Assert.Contains("4", stdout);     // add Double → 4.0
        Assert.Contains("3", stdout);     // length
        // transpose by named Semitone and positional Cent both resolved.
        Assert.Equal(3, runner.GetVariable("up").As<SequenceData>().Bars[0].MusicalNotes.Count);
        Assert.Equal(3, runner.GetVariable("shifted").As<SequenceData>().Bars[0].MusicalNotes.Count);
    }

    [Fact]
    public void UnknownNamedArg_StillReportsHelpfully()
    {
        // Default-fill must NOT swallow genuinely-unknown parameter names — a
        // typo like `tempo=` (not a jam parameter) must still error.
        using var runner = new FlowEngineRunner();
        var (success, _, _, errorCount) = runner.RunSource(
            OverDecl + "Sequence bad = (jam over=over nonexistentParam=42)\n");

        Assert.False(success, "Expected jam with an unknown named arg to fail");
        Assert.True(errorCount > 0);
    }

    private static void AssertSequenceEqual(SequenceData a, SequenceData b)
    {
        Assert.Equal(a.Bars.Count, b.Bars.Count);
        for (int barIdx = 0; barIdx < a.Bars.Count; barIdx++)
        {
            var na = a.Bars[barIdx].MusicalNotes;
            var nb = b.Bars[barIdx].MusicalNotes;
            Assert.Equal(na.Count, nb.Count);
            for (int i = 0; i < na.Count; i++)
            {
                int midiA = NoteType.ToMidiNote(na[i].NoteName, na[i].Octave, na[i].Alteration);
                int midiB = NoteType.ToMidiNote(nb[i].NoteName, nb[i].Octave, nb[i].Alteration);
                Assert.True(midiA == midiB,
                    $"bar {barIdx} slot {i}: MIDI mismatch ({midiA} vs {midiB})");
                Assert.Equal(na[i].DurationValue, nb[i].DurationValue);
            }
        }
    }
}
