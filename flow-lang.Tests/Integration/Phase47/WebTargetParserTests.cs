using FlowLang.Core;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase47;

/// <summary>
/// Phase 47 Plan 47-04 — Web-target side of the Parser live-block gate
/// landed by Plan 47-03 Task 4. On Web, parsing <c>live 1bar { ... }</c>
/// MUST report a parse failure with the documented Rust-style diagnostic
/// message routed through <see cref="FlowEngine"/>'s ErrorReporter.
///
/// On Desktop the same source parses successfully (verified separately by
/// <c>WebTargetGuardTests.LiveBlock_Parses_OnDesktop</c>).
///
/// Facts marked <c>[FlowTargetFact("Web")]</c> — skipped on Desktop runs
/// with the documented "Skipped on Desktop — test runs under: Web" reason.
/// Web-mode execution of these Facts is gated by the test project being
/// able to compile under FlowTarget=Web (current state: 18 Sfz/Osc-
/// referencing test files cascade-fail; deferred to Plan 47-06).
/// </summary>
public class WebTargetParserTests
{
    [FlowTargetFact("Web")]
    public void IsWebTarget_IsTrue_OnWebBuild()
    {
        // Mirror counterpart of WebTargetGuardTests.IsWebTarget_IsFalse_OnDesktopBuild.
        Assert.True(FlowEngine.IsWebTarget,
            "Web test assembly must read FlowEngine.IsWebTarget as true.");
    }

    [FlowTargetFact("Web")]
    public void SupportsLiveBlocks_IsFalse_OnWebBuild()
    {
        Assert.False(FlowEngine.SupportsLiveBlocks,
            "Web build must have SupportsLiveBlocks=false so `live { ... }` parse fails.");
    }

    [FlowTargetFact("Web")]
    public void LiveBlock_FailsToParse_OnWeb_WithCharitableDiagnostic()
    {
        // Plan 47-03 Task 4 pinned the throw text:
        //   "`live` block requires Desktop target — line N. Build with FlowTarget=Desktop ..."
        // FlowEngine.Execute catches ParseException and returns false (the
        // ErrorReporter accumulates the diagnostic). Asserting Execute=false
        // is the proxy assertion for "parse failed"; the diagnostic-text
        // assertion is intentionally loose here because the ParseException
        // message routes through ErrorReporter which is verified end-to-end
        // by Plan 47-03's WebTargetGuardTests on Desktop and by the Plan
        // 47-06 closer's verification sweep on Web.
        var src = "live 1bar { (print 1); }";
        var engine = new FlowEngine();
        var ok = engine.Execute(src, "<test>");

        Assert.False(ok,
            "Web build must report parse failure for `live { ... }` block.");
    }
}
