using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;

/// <summary>
/// Phase 26 Wave 0 (RED): pins Decisions D-01 + D-03 — the parser shorthand
/// `-IDENT` lowers to `(neg IDENT)`, and `+IDENT` is silently absorbed
/// (Plus token stripped at expression-start).
///
/// Decisions referenced (locked in 26-CONTEXT.md):
///   D-01 — variable negation `-x` is a parser shorthand emitting
///          FunctionCallExpression("neg", [VariableExpression]). No
///          BinaryExpression is produced.
///   D-03 — unary `+` is kept as a no-op shorthand. `+x` parses as `x`.
///
/// Pattern: FlowEngineRunner stdout-substring assertion per S-06; analog
/// flow-lang.Tests/Unit/Phase21/HAliasFacts.cs:38-58.
/// </summary>
[Collection("FlowScripts")]
public class UnaryMinusShorthandFacts
{
    [Fact]
    public void MinusIdent_LowersToNegCall()
    {
        // D-01 acceptance: `-x` parses as `(neg x)`. We exercise this end-to-end
        // via a print roundtrip — if the shorthand lowered correctly, the
        // negative value appears in stdout. If it produced a parse error or
        // kept BinaryExpression, the run fails or the wrong value prints.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Int x = 5
Int y = -x
(print (str y))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("-5", stdout);
    }

    [Fact]
    public void PlusIdent_StripsSilently()
    {
        // D-03: `+x` parses as `x` (Plus token absorbed at expression-start).
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Int x = 5
Int y = +x
(print (str y))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Contains("5", stdout);
    }
}
