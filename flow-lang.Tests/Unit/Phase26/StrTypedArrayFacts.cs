using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;

/// <summary>
/// Phase 26 fix-omissions Blocker 1: pins the Strategy A coercion fix in
/// <see cref="FlowLang.Interpreter.ExpressionEvaluator.EvaluateFunctionCall"/>.
///
/// The (str X[]) wildcard overload is registered with parameter type
/// ArrayType(VoidType) — a true pass-through wildcard. Pre-fix, the D-05/D-06
/// coercion loop saw the typed argument's CanConvertTo(Void[]) return true
/// (per ArrayType's array-to-array compatibility rule) and called
/// Value.ConvertTo on the underlying List&lt;Value&gt; storage. No Value-level
/// path exists for typed-array → Void[] target, so the call crashed with
/// "Cannot convert Flow type 'Int[]' with underlying CLR type 'List`1' to
/// Flow target type 'Void[]'". This is exactly RESEARCH.md "Pitfall 2".
///
/// Post-fix the loop early-continues when the signature parameter is
/// ArrayType(VoidType) — typed arrays pass through to the StrArray impl
/// with their original storage and the wildcard's role (accepting the call)
/// is preserved without transforming the runtime value.
///
/// Pre-fix: RED on all three theories (typed-array → Void[] crash).
/// Post-fix: GREEN — (str Int[]) / (str String[]) / (str Float[]) all
/// resolve and produce a printable string.
///
/// Pattern matches InfixRejectedFacts.cs (same FlowEngineRunner.RunSource
/// fixture, same [Collection("FlowScripts")] serialization), but inverts
/// the assertion to errors == 0 since this Fact pins SUCCESS, not failure.
/// </summary>
[Collection("FlowScripts")]
public class StrTypedArrayFacts
{
    // Source-string notes: Flow's array-literal type inference has pre-existing
    // quirks that are orthogonal to Blocker 1. ["a","b"] lexes as a Note[]
    // (note-letter ambiguity) so String[] is constructed via variable refs.
    // Float vs. Double: floating-point literals lex as Double, so the wide
    // floating path is exercised via Double[] (Float[] has no convenient
    // literal form — both Double and Float share the (str X[]) Void[]
    // wildcard path, so Double[] is a faithful regression guard for the fix).
    [Theory]
    [InlineData("Int[] xs = [1, 2, 3]\nString s = (str xs)\n(print s)")]
    [InlineData("String a = \"x\"\nString b = \"y\"\nString[] ys = [a, b]\nString s = (str ys)\n(print s)")]
    [InlineData("Double[] zs = [1.0, 2.0, 3.0]\nString s = (str zs)\n(print s)")]
    public void StrTypedArray_ResolvesAndCoerces(string source)
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errors) = runner.RunSource("use \"@std\"\n" + source);
        Assert.True(errors == 0,
            $"expected (str X[]) to succeed via Void[] wildcard pass-through, got {errors} error(s).\nsource: {source}\nstderr: {stderr}");
    }
}
