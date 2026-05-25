using System;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-01 Task 1 — Facts pinning the two new bool fields on
/// <see cref="ExecutionContext"/> per D-02 + D-05: <c>StrictMode</c> +
/// <c>CallerStrictMode</c>. Both default <c>false</c> and are auto-property
/// settable. <c>StrictMode</c> is the per-declaring-file bit that FlowEngine
/// + ModuleLoader write at file-load boundaries (Plan 44-01 Task 2);
/// <c>CallerStrictMode</c> is the call-dispatch snapshot Plan 44-02 wires
/// for the stdlib clamp / advisory leaf-site read path (NOT StrictMode — see
/// Anti-Pattern 1 in 44-PATTERNS.md).
///
/// <para>
/// CallerStrictMode lands here in Plan 44-01 (not Plan 44-02) to avoid an
/// extra ExecutionContext edit cycle: the field shape is identical, the
/// down-stream wiring is the only thing Plan 44-02 adds. The field stays
/// unread until Plan 44-02.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class ExecutionContextStrictModeTests : IDisposable
{
    public ExecutionContextStrictModeTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    private static ExecutionContext NewContext()
    {
        var reporter = new ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new ExecutionContext(reporter, registry);
    }

    [Fact]
    public void Fact_StrictMode_DefaultsFalse()
    {
        var ctx = NewContext();
        Assert.False(ctx.StrictMode,
            "ExecutionContext.StrictMode must default to false per Phase 44 D-02.");
    }

    [Fact]
    public void Fact_CallerStrictMode_DefaultsFalse()
    {
        var ctx = NewContext();
        Assert.False(ctx.CallerStrictMode,
            "ExecutionContext.CallerStrictMode must default to false per Phase 44 D-05.");
    }

    [Fact]
    public void Fact_StrictMode_Settable()
    {
        var ctx = NewContext();
        ctx.StrictMode = true;
        Assert.True(ctx.StrictMode);
        ctx.StrictMode = false;
        Assert.False(ctx.StrictMode);
    }

    [Fact]
    public void Fact_CallerStrictMode_Settable()
    {
        var ctx = NewContext();
        ctx.CallerStrictMode = true;
        Assert.True(ctx.CallerStrictMode);
        ctx.CallerStrictMode = false;
        Assert.False(ctx.CallerStrictMode);
    }
}
