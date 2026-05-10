using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;

/// <summary>
/// Phase 26 Wave 0 (RED): pins Decision D-05 flexible (convertible-scoring) path.
/// Six Facts via FlowEngineRunner exercise mixed-type arithmetic end-to-end —
/// the parser, OverloadResolver convertible-scoring tier (+100), and the new
/// Wave 1 coercion fix in EvaluateFunctionCall must all cooperate to widen
/// the narrower operand to the wider operand's same-type fast path.
///
/// Decisions referenced (locked in 26-CONTEXT.md):
///   D-05 — flexible path: mixed-type calls fall through OverloadResolver
///          convertible scoring; narrower operand widens to wider via existing
///          Value.ConvertTo machinery.
///   D-06 — wider operand wins: Int &lt; Long &lt; Float &lt; Double &lt; Number.
///
/// IMPORTANT: these Facts are RED until Wave 1 (plan 26-02) lands BOTH the
/// new same-type fast paths AND the EvaluateFunctionCall coercion fix
/// (RESEARCH §"Mixed-Type Coercion Boundary"). Without coercion, the
/// implementation lambda hits an InvalidCastException on argValues[i].As&lt;T&gt;().
///
/// Pattern: FlowEngineRunner end-to-end per S-06; analog
/// flow-lang.Tests/Unit/Phase21/HAliasFacts.cs:38-58.
/// </summary>
[Collection("FlowScripts")]
public class MixedTypeArithmeticFacts
{
    [Fact]
    public void AddIntDouble_WidensToDouble()
    {
        // (add 5 3.0) — Int widens to Double, hits (add Double Double) fast path.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Double r = (add 5 3.0)
(print (str r))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("8", stdout);
    }

    [Fact]
    public void AddFloatDouble_WidensToDouble()
    {
        // Float widens to Double via the existing Int→Long→Float→Double chain.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Float a = 1.5
Double b = 2.5
Double r = (add a b)
(print (str r))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("4", stdout);
    }

    [Fact]
    public void MulIntLong_WidensToLong()
    {
        // Int 5 widens to Long, hits (mul Long Long) fast path; result is Long 30.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Long six = 6
Long r = (mul 5 six)
(print (str r))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("30", stdout);
    }

    [Fact]
    public void MulFloatDouble_WidensToDouble()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Float a = 2.0
Double b = 3.5
Double r = (mul a b)
(print (str r))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("7", stdout);
    }

    [Fact]
    public void AddLongNumber_WidensToNumber()
    {
        // Long widens to Number; hits (add Number Number) fast path.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Number n = 1000000000000
Long m = 1000000000000
Number r = (add m n)
(print (str r))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("2000000000000", stdout);
    }

    [Fact]
    public void SubLongNumber_WidensToNumber()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"
use ""@std""
Number n = 5000000000000
Long m = 2000000000000
Number r = (sub n m)
(print (str r))
");
        Assert.True(ok, $"stderr: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("3000000000000", stdout);
    }
}
