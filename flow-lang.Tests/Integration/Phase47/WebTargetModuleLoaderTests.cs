using FlowLang.Core;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase47;

/// <summary>
/// Phase 47 Plan 47-04 — Web-target side of the ModuleLoader stripped-module
/// gate landed by Plan 47-03 Task 3. On Web, importing <c>@sfz</c> or
/// <c>@osc</c> MUST emit the charitable
/// <c>[target] module '...' unavailable on Web target</c> advisory on stderr
/// AND cause <see cref="FlowEngine.Execute"/> to return false (the
/// ModuleLoadResult.Error path).
///
/// On Desktop the same imports succeed (verified by
/// <c>WebTargetGuardTests.UseSfzImport_NoTargetAdvisory_OnDesktop</c>).
///
/// The third Fact is a NEGATIVE assertion verifying the strip list is
/// correctly SCOPED — <c>@notation-io</c> STAYS available on Web because
/// XmlWriter has no native deps. Catches drift if <c>IsStrippedOnWeb</c>
/// ever false-positive-flags <c>@notation-io</c>.
///
/// Facts marked <c>[FlowTargetFact("Web")]</c> — skipped on Desktop runs.
/// </summary>
public class WebTargetModuleLoaderTests
{
    [FlowTargetFact("Web")]
    public void UseSfzImport_EmitsCharitableAdvisory_OnWeb()
    {
        var origErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var src = "use \"@sfz\";";
            var engine = new FlowEngine();
            var ok = engine.Execute(src, "<test>");
            // Web build must return false (advisory + ModuleLoadResult.Error).
            Assert.False(ok, "Web build must report @sfz import as a failure.");
        }
        finally
        {
            Console.SetError(origErr);
        }
        var stderr = sw.ToString();
        Assert.Contains("[target] module '@sfz' unavailable on Web target", stderr);
    }

    [FlowTargetFact("Web")]
    public void UseOscImport_EmitsCharitableAdvisory_OnWeb()
    {
        var origErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var src = "use \"@osc\";";
            var engine = new FlowEngine();
            var ok = engine.Execute(src, "<test>");
            Assert.False(ok, "Web build must report @osc import as a failure.");
        }
        finally
        {
            Console.SetError(origErr);
        }
        var stderr = sw.ToString();
        Assert.Contains("[target] module '@osc' unavailable on Web target", stderr);
    }

    [FlowTargetFact("Web")]
    public void UseNotationIoImport_Succeeds_OnWeb()
    {
        // Phase 47 NEGATIVE assertion: @notation-io is NOT stripped on Web
        // (XmlWriter is BCL-only — works in browser). The IsStrippedOnWeb
        // helper at ModuleLoader returns false for @notation-io, so import
        // succeeds and no [target] advisory surfaces.
        var origErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var src = "use \"@notation-io\";";
            var engine = new FlowEngine();
            engine.Execute(src, "<test>");
        }
        finally
        {
            Console.SetError(origErr);
        }
        var stderr = sw.ToString();
        Assert.DoesNotContain("[target] module '@notation-io' unavailable", stderr);
    }
}
