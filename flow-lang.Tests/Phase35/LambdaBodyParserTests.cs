using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// sweep-0614 regression — a multi-statement lambda body must parse regardless
/// of whether its first statement is a type declaration or an expression.
///
/// <para>
/// Previously ParseLambdaExpression chose the multi-statement branch ONLY when
/// the token after <c>=&gt;</c> was <c>(</c> immediately followed by a type
/// keyword (i.e. a variable declaration). An expression-first body such as
/// <c>fn Int x =&gt; ((print "side") x)</c> mis-parsed as a single parenthesized
/// expression and left the trailing <c>x</c> as an unexpected token. The fix
/// replaces the heuristic with a structural lookahead over the matching
/// parenthesized region.
/// </para>
/// </summary>
[Collection("FlowScripts")]
public class LambdaBodyParserTests
{
    [Fact]
    public void ExpressionFirstMultiStatementBody_Parses()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Function f = fn Int x => ((print ""side"") x)
(print (str (f 5)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("side", stdout);
        Assert.Contains("5", stdout);
    }

    [Fact]
    public void DeclarationFirstMultiStatementBody_StillParses()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Function f = fn Int x => (Int y = (mul x 2); (print ""side""); y)
(print (str (f 5)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("side", stdout);
        Assert.Contains("10", stdout);
    }

    [Fact]
    public void SingleFunctionCallBody_StillParses()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Function f = fn Int x => (mul x 2)
(print (str (f 5)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("10", stdout);
    }

    [Fact]
    public void SingleParenthesizedExpressionBody_StillParses()
    {
        // `((add x 1))` is a single regular parenthesized expression (one inner
        // unit) and must NOT be promoted to a multi-statement block.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
Function f = fn Int x => ((add x 1))
(print (str (f 5)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("6", stdout);
    }

    [Fact]
    public void SideEffectFirstLambdaComposesWithEach()
    {
        // The natural side-effect-first form passed to `each`.
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errCount) = runner.RunSource(@"
use ""@std""
(each (list 1 2 3) (fn Int x => ((print (str x)) x)))
");
        Assert.True(success, $"engine reported failure. stderr={stderr}");
        Assert.Equal(0, errCount);
        Assert.Contains("1", stdout);
        Assert.Contains("2", stdout);
        Assert.Contains("3", stdout);
    }
}
