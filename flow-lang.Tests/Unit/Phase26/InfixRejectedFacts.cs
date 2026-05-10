using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;

/// <summary>
/// Phase 26 Wave 0 (RED): pins Decision D-15 — bare infix arithmetic
/// (`1 + 2`, `5 - 3`, `4 * 2`, `10 / 5`, `x + 1`) MUST produce a parse error
/// post-Wave-1. Generic 'unexpected token' is sufficient (no charitable
/// migration hint shipped per D-16; Flow is pre-public).
///
/// Decisions referenced (locked in 26-CONTEXT.md):
///   D-15 — generic 'unexpected token' parse error. The error message text is
///          NOT asserted — only the existence of at least one error.
///   D-16 — rationale: Flow is pre-public, the migration script handles every
///          existing .flow file in one sweep, no legacy users.
///
/// IMPORTANT: pre-Wave-1 these Facts are RED — infix is currently ACCEPTED by
/// the parser (ParseAdditive/ParseMultiplicative are still in the codebase).
/// The Theory will see errors==0 and fail. Wave 1 (plan 26-02) deletes those
/// methods and turns these Facts GREEN.
///
/// Pattern: FlowEngineRunner errors-greater-than-zero assertion per S-06+S-07;
/// analog flow-lang.Tests/Unit/Phase21/PragmaScannerFacts.cs:107-116.
/// </summary>
[Collection("FlowScripts")]
public class InfixRejectedFacts
{
    [Theory]
    [InlineData("Int x = 1 + 2")]
    [InlineData("Int x = 5 - 3")]
    [InlineData("Int x = 4 * 2")]
    [InlineData("Int x = 10 / 5")]
    [InlineData("Int a = 1\nInt y = (add a 1)\nInt z = a + 1")]
    public void BareInfix_ProducesParseError(string source)
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errors) = runner.RunSource("use \"@std\"\n" + source);
        Assert.True(errors > 0,
            $"expected parse error from infix arithmetic, got success. source: {source}\nstderr: {stderr}");
    }
}
