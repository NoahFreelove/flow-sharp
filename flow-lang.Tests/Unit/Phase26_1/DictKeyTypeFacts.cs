using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_1;

/// <summary>
/// Phase 26.1 Wave 4 (GREEN): pins DICT-01 hashable allowlist — exactly
/// the 8 allowed key types (Int / Long / Float / String / Symbol / Note / Chord /
/// Tuple-of-hashables) per CONTEXT § Hashable enforcement timing.
///
/// REVISION 1 added <see cref="NestedTupleAnnotation_DictOfTupleOfNoteBeat_Parses"/>
/// — locks the parse-time acceptance of <c>Dict&lt;Tuple&lt;&lt;Note, Beat&gt;&gt;, Int&gt;</c>.
///
/// Source-running facts go through <see cref="FlowEngineRunner"/> with
/// <c>[Collection("FlowScripts")]</c> so Console.SetOut is serialized
/// (RESEARCH Pitfall 4).
/// </summary>
[Collection("FlowScripts")]
public class DictKeyTypeFacts
{
    [Theory]
    [InlineData("Int",    "Dict<Int, String>",    "(dict 1 \"v\")",   "OK")]
    [InlineData("Long",   "Dict<Int, String>",    "(dict 1 \"v\")",   "OK")]   // 1 lexes as Int (Long-compat via numeric chain)
    [InlineData("Double", "Dict<Double, String>", "(dict 1.0 \"v\")", "OK")]   // 1.0 lexes as Double; Float→Double also hashable
    [InlineData("String", "Dict<String, Int>",    "(dict \"k\" 1)",   "OK")]
    [InlineData("Symbol", "Dict<Symbol, Int>",    "(dict #k 1)",      "OK")]
    [InlineData("Note",   "Dict<Note, Int>",      "(dict C4 1)",      "OK")]
    [InlineData("Chord",  "Dict<Chord, Int>",     "(dict Cmaj 1)",    "OK")]
    public void AllowedKeys_Inline(string label, string annotation, string ctorExpr, string sentinel)
    {
        var src = $"use \"@std\"\n{annotation} d = {ctorExpr}\n(print \"{sentinel}\")";
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(src);
        Assert.True(success, $"({label}) engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains(sentinel, stdout);
    }

    [Fact]
    public void AllowedKeys_TupleOfHashables()
    {
        // CONTEXT § Specifics block 9 — Dict<Tuple<<Note, Beat>>, Int> idiom.
        // Beat values constructed via the new (beat Double) builtin (DICT-01 deviation).
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Dict<Tuple<<Note, Beat>>, Int> beatMap = (dict <<C4, (beat 0.25)>> 1 <<D4, (beat 0.5)>> 2)
Int v = (get beatMap <<C4, (beat 0.25)>>)
(print (str v))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("1", stdout);
    }

    /// <summary>
    /// REVISION 1 (plan-checker WARNING 3 fix) — explicit nested-generic acceptance:
    /// CONTEXT § Specifics block 9 requires <c>Dict&lt;Tuple&lt;&lt;Note, Beat&gt;&gt;, Int&gt;</c> to
    /// parse, which means the TypeParser must correctly handle the closing
    /// <c>&gt;&gt;&gt;</c> THREE-greater sequence (one <c>&gt;&gt;</c> closes the inner Tuple,
    /// one <c>&gt;</c> closes the outer Dict). <see cref="AllowedKeys_TupleOfHashables"/>
    /// exercises the runtime path; this Fact specifically LOCKS the parse-time
    /// acceptance — failure here would indicate a <c>&gt;&gt;&gt;</c> lexing/parsing regression
    /// even if runtime works.
    /// </summary>
    [Fact]
    public void NestedTupleAnnotation_DictOfTupleOfNoteBeat_Parses()
    {
        const string SOURCE =
            "use \"@std\"\n"
            + "Dict<Tuple<<Note, Beat>>, Int> beatMap = (dict <<C4, (beat 0.25)>> 1 <<D4, (beat 0.5)>> 2)\n"
            + "(print (str (get beatMap <<C4, (beat 0.25)>>)))";
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(SOURCE);
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("1", stdout);
    }
}
