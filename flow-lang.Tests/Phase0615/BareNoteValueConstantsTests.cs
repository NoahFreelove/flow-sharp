using FlowLang.Core;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase0615;

/// <summary>
/// Feature-addition 0615 (#6 bare-notevalues) — the canonical single-letter
/// duration short-forms (w/h/q/e/s + finer t/x/y) resolve to predefined GLOBAL
/// <see cref="NoteValueType"/> constants when used as bare identifiers in
/// expression position, so the documented <c>(quantize seq e 1.0 0.0)</c> call
/// form works WITHOUT reserving e/q/h/w/s as keywords.
///
/// <para>
/// Least-breaking path: the constants are NOT seeded into the global frame
/// (that would trip <c>DeclareVariable</c>'s "already declared" guard on a
/// top-level <c>Int e = 5</c>). Instead
/// <c>ExpressionEvaluator.EvaluateVariable</c> resolves them as a FALLBACK,
/// only after the variable + function frame-chain lookup misses — so a
/// composer's <c>Int e = 5</c> (or a proc param named <c>q</c>) shadows the
/// constant naturally. Single source of truth:
/// <see cref="NoteValueType.TryGetPredefinedConstant"/>, mirroring
/// NoteStreamCompiler's duration-suffix table.
/// </para>
/// </summary>
public class BareNoteValueConstantsTests
{
    private static FlowEngine Run(string source, out bool ok)
    {
        var engine = new FlowEngine(verbose: false);
        ok = engine.Execute(source + "\n");
        return engine;
    }

    [Theory]
    [InlineData("w", NoteValueType.Value.WHOLE)]
    [InlineData("h", NoteValueType.Value.HALF)]
    [InlineData("q", NoteValueType.Value.QUARTER)]
    [InlineData("e", NoteValueType.Value.EIGHTH)]
    [InlineData("s", NoteValueType.Value.SIXTEENTH)]
    [InlineData("t", NoteValueType.Value.THIRTYSECOND)]
    [InlineData("x", NoteValueType.Value.SIXTYFOURTH)]
    [InlineData("y", NoteValueType.Value.ONETWENTYEIGHTH)]
    public void BareShortForm_ResolvesToCorrectNoteValueEnum(string name, NoteValueType.Value expected)
    {
        using var engine = Run($"NoteValue nv = {name};", out var ok);
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Empty(engine.ErrorReporter.Errors);

        var v = engine.Context.GetVariable("nv");
        Assert.NotNull(v);
        Assert.IsType<NoteValueType>(v!.Type);
        Assert.Equal((int)expected, v.As<int>());
    }

    [Fact]
    public void DocumentedQuantizeCallForm_WithBareE_Works()
    {
        // The exact documented form: (quantize seq e 1.0 0.0).
        using var engine = Run(
            "Sequence seq = | C4 D4 E4 F4 |\n" +
            "Sequence r = (quantize seq e 1.0 0.0)",
            out var ok);
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Empty(engine.ErrorReporter.Errors);

        var r = engine.Context.GetVariable("r");
        Assert.NotNull(r);
        Assert.IsType<SequenceType>(r!.Type);
    }

    [Fact]
    public void IntE_Equals5_StillCompilesAndShadowsTheConstant()
    {
        // The owner requirement: `Int e = 5` must keep working. The local binding
        // shadows the predefined NoteValue constant (frame-chain lookup hits first).
        using var engine = Run("Int e = 5", out var ok);
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Empty(engine.ErrorReporter.Errors);

        var v = engine.Context.GetVariable("e");
        Assert.NotNull(v);
        Assert.IsType<FlowLang.TypeSystem.PrimitiveTypes.IntType>(v!.Type);
        Assert.Equal(5, v.As<int>());
    }

    [Fact]
    public void LocalShadow_OfQ_WinsOverConstant_WithinScope()
    {
        // Bind q to an Int, then read it back — the constant must NOT leak through.
        using var engine = Run("Int q = 42; Int copy = q", out var ok);
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Empty(engine.ErrorReporter.Errors);

        var copy = engine.Context.GetVariable("copy");
        Assert.NotNull(copy);
        Assert.IsType<FlowLang.TypeSystem.PrimitiveTypes.IntType>(copy!.Type);
        Assert.Equal(42, copy.As<int>());
    }

    [Fact]
    public void ProcParamNamedH_ShadowsConstant_InsideBody()
    {
        // A proc parameter named `h` shadows the half-note constant inside the body
        // (params/locals shadow globals per Flow's lexical scope). The bare `h`
        // outside any binding still resolves to the constant.
        using var engine = Run(
            "proc doubleIt (Int: h)\n" +
            "    Int doubled = (mul h 2)\n" +
            "    return doubled\n" +
            "end proc\n" +
            "Int result = (doubleIt 7)\n" +
            "NoteValue outside = h",
            out var ok);
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Empty(engine.ErrorReporter.Errors);

        var result = engine.Context.GetVariable("result");
        Assert.NotNull(result);
        Assert.Equal(14, result!.As<int>());

        // The bare `h` at file scope still resolves to the HALF constant.
        var outside = engine.Context.GetVariable("outside");
        Assert.NotNull(outside);
        Assert.IsType<NoteValueType>(outside!.Type);
        Assert.Equal((int)NoteValueType.Value.HALF, outside.As<int>());
    }

    [Fact]
    public void TryGetPredefinedConstant_TableMatchesNoteStreamSuffixes()
    {
        // Single-source-of-truth sanity: every canonical short-form maps, and a
        // non-short-form (e.g. an arbitrary identifier) does NOT.
        Assert.True(NoteValueType.TryGetPredefinedConstant("e", out var e));
        Assert.Equal(NoteValueType.Value.EIGHTH, e);
        Assert.True(NoteValueType.TryGetPredefinedConstant("q", out var q));
        Assert.Equal(NoteValueType.Value.QUARTER, q);

        Assert.False(NoteValueType.TryGetPredefinedConstant("foo", out _));
        Assert.False(NoteValueType.TryGetPredefinedConstant("E", out _)); // case-sensitive
    }
}
