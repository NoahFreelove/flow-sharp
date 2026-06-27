using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase33;

/// <summary>
/// Phase 33 Plan 33-05 Task 2 — SPEC-1 acceptance facts. <c>loadSfz</c> +
/// <c>sampler:NAME</c> require <c>use "@sfz"</c> to activate; without the
/// import, <c>(loadSfz #violin)</c> errors at call time with a message
/// containing <c>use "@sfz"</c>.
///
/// <para>CONTEXT D-10 — the C# builtins are registered unconditionally at
/// FlowEngine startup (no parser changes for SFZ). The gate is a runtime
/// check on <see cref="ExecutionContext.SfzEnabled"/>; the
/// <c>__enableSfzModule</c> marker called from <c>sfz.flow</c> flips that
/// flag during the <c>use</c> import. This test class exercises both the
/// gated-off (no import) and the gated-on (import + happy-path-elided) sides
/// of the contract.</para>
///
/// <para>The <c>SamplerDispatch_WithoutImport_Errors</c> fact deliberately
/// lives in Plan 33-07's SfzBindingTests (locked single-location ownership)
/// because the <c>SongRenderer</c> dispatch only exists after Plan 33-07
/// wires it in. Plan 33-05's job is to gate the <c>loadSfz</c> surface; the
/// downstream <c>sampler:NAME</c> dispatch is gated by Plan 33-07.</para>
///
/// <para>[Collection("FlowScripts")] serializes the entire SFZ test suite —
/// every Plan 33-04/05/06 test class joins this collection so that the
/// shared <see cref="RenderingDiagnostics"/> sentinel set + the shared
/// <see cref="FlowConfig.Active"/> singleton do not leak state across
/// parallel test workers. Combined with <see cref="RenderingDiagnostics.ResetForTesting"/>
/// in ctor + Dispose, this keeps each fact independently re-runnable.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzGatingTests : IDisposable
{
    public SfzGatingTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// SPEC-1 acceptance — without <c>use "@sfz"</c>, calling
    /// <c>(loadSfz #violin)</c> raises an error whose message contains
    /// <c>use "@sfz"</c>. The composer-facing diagnostic is the entire
    /// point of the gate: the error message must guide them to the fix.
    ///
    /// <para>Implementation detail: the runtime check happens inside
    /// <see cref="FlowLang.StandardLibrary.Audio.Sfz.SfzBuiltins.LoadSfzSymbol"/>
    /// before any dict lookup or file-read; FlowEngineRunner surfaces the
    /// thrown <see cref="InvalidOperationException"/> via the
    /// <see cref="FlowLang.Core.FlowEngine.Execute"/> catch block as an
    /// ErrorReporter entry, which flushes to stderr.</para>
    /// </summary>
    [Fact]
    public void LoadSfz_WithoutImport_Errors()
    {
        using var runner = new FlowEngineRunner();
        // `use "@std"` is necessary so the parser-resolution layer sees the
        // loadSfz forward-decl (declared in std.flow per CONTEXT D-10's
        // "registered unconditionally" contract — the SfzEnabled gate is the
        // RUNTIME check, not the parse-time check). Without it the test would
        // hit "Function not found" before reaching the gate, which is not
        // the SPEC-1 surface we are validating.
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@std""
Sfz v = (loadSfz #violin)
");
        Assert.False(ok, "expected non-zero exit when loadSfz is called without `use \"@sfz\"`");
        Assert.Contains("use \"@sfz\"", stderr);
    }

    /// <summary>
    /// Companion to the symbol-overload fact: the String overload is gated
    /// the same way. Both bodies share the SfzEnabled check; this fact
    /// pins the contract for the absolute-path entry point too.
    /// </summary>
    [Fact]
    public void LoadSfzString_WithoutImport_Errors()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@std""
Sfz v = (loadSfz ""/tmp/anything.sfz"")
");
        Assert.False(ok, "expected non-zero exit when loadSfz(String) is called without `use \"@sfz\"`");
        Assert.Contains("use \"@sfz\"", stderr);
    }

    /// <summary>
    /// Positive control: with <c>use "@sfz"</c> in effect AND sfz_root
    /// unconfigured, the gate has flipped (no "use @sfz" error) but the
    /// downstream missing-config error fires. Asserting the absence of the
    /// gate error here pins the contract — without this control, a regression
    /// that mis-fires the gate error everywhere would silently pass the
    /// negative facts above.
    /// </summary>
    [Fact]
    public void LoadSfz_WithImport_NoGateError_ButMissingRootError()
    {
        // FlowConfig.Reset already cleared SfzRoot; explicit assert for clarity.
        Assert.Null(FlowConfig.Active.SfzRoot);
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@sfz""
Sfz v = (loadSfz #violin)
");
        Assert.False(ok, "expected non-zero exit since sfz_root is not configured");
        // The gate has flipped — stderr must NOT contain the gate error.
        Assert.DoesNotContain("loadSfz requires 'use \"@sfz\"'", stderr);
        // The downstream missing-config error fires (asserted in detail in SfzConfigTests).
        Assert.Contains("sfz_root", stderr);
    }
}
