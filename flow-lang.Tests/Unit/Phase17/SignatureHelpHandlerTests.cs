using FlowLsp.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 plan 06 Task 2 Facts — SignatureHelpHandler.DetectCall pure parser.
/// Verifies comma-count active-parameter detection and correct depth tracking
/// for nested parens.
/// </summary>
public class SignatureHelpHandlerTests
{
    [Fact]
    public void NoArgs_ActiveZero()
    {
        var ctx = SignatureHelpHandler.DetectCall("print(", new Position(0, 6));
        Assert.NotNull(ctx);
        Assert.Equal("print", ctx!.FunctionName);
        Assert.Equal(0, ctx.ActiveParameter);
    }

    [Fact]
    public void AfterOneComma_ActiveOne()
    {
        var ctx = SignatureHelpHandler.DetectCall("concat(\"a\", ", new Position(0, 12));
        Assert.NotNull(ctx);
        Assert.Equal("concat", ctx!.FunctionName);
        Assert.Equal(1, ctx.ActiveParameter);
    }

    [Fact]
    public void NoParens_ReturnsNull()
    {
        Assert.Null(SignatureHelpHandler.DetectCall("proc foo", new Position(0, 8)));
    }

    [Fact]
    public void NestedParens_OnlyOuterDepthCounts()
    {
        // cursor after `mul(a, b),` inside outer call
        var ctx = SignatureHelpHandler.DetectCall("outer(mul(a, b), ", new Position(0, 17));
        Assert.NotNull(ctx);
        Assert.Equal("outer", ctx!.FunctionName);
        Assert.Equal(1, ctx.ActiveParameter);
    }
}
