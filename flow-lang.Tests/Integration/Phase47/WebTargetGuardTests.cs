using System.IO;
using FlowLang.Core;
using Xunit;

namespace FlowLang.Tests.Integration.Phase47;

/// <summary>
/// Phase 47 Plan 47-03 — Pin Desktop-side behavior of the FlowEngine static
/// flags + Parser live-block gate + ModuleLoader stripped-module gate.
/// On Desktop:
///  - <see cref="FlowEngine.IsWebTarget"/> must be false
///  - <see cref="FlowEngine.SupportsLiveBlocks"/> must be true
///  - `live 1bar { ... }` parses successfully
///  - `use "@sfz"` import does NOT emit the charitable target advisory
///
/// Web-side assertions (Web build's IsWebTarget=true, live block throws
/// ParseException, @sfz import emits advisory) require Plan 47-04's
/// FlowTargetFact attribute to discriminate; they land in
/// AssemblyReferenceScanTests / Plan 47-04 closing tests.
/// </summary>
public class WebTargetGuardTests
{
    [Fact]
    public void IsWebTarget_IsFalse_OnDesktopBuild()
    {
        // Phase 47 D-47-10: this test assembly compiles WITHOUT FLOW_WEB,
        // so the constant initializer in FlowEngine.cs yields false here.
        Assert.False(FlowEngine.IsWebTarget,
            "Desktop test assembly must read FlowEngine.IsWebTarget as false.");
    }

    [Fact]
    public void SupportsLiveBlocks_IsTrue_OnDesktopBuild()
    {
        Assert.True(FlowEngine.SupportsLiveBlocks,
            "Desktop build must have SupportsLiveBlocks=true so `live { ... }` parses.");
    }

    [Fact]
    public void LiveBlock_Parses_OnDesktop()
    {
        // Smoke test — a minimal live block parses to completion on Desktop.
        // Web target throws ParseException at this point (verified separately
        // via FlowTargetFact("Web") in Plan 47-04).
        var src = "live 1bar { Int x = 1; (print x); }";
        var engine = new FlowEngine();
        var ok = engine.Execute(src, "<test>");
        Assert.True(ok,
            "Desktop execution of a minimal live block must succeed (no parse error).");
    }

    [Fact]
    public void UseSfzImport_NoTargetAdvisory_OnDesktop()
    {
        // Smoke test — `use "@sfz"` on Desktop loads sfz.flow stdlib without
        // emitting the Plan 47-03 ModuleLoader charitable advisory. Verified
        // by checking stderr does NOT contain the "[target]" prefix.
        var origErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var src = "use \"@sfz\";";
            var engine = new FlowEngine();
            engine.Execute(src, "<test>");
        }
        finally
        {
            Console.SetError(origErr);
        }
        // Desktop must NOT print the stripped-module advisory.
        Assert.DoesNotContain("[target] module '@sfz' unavailable on Web target", sw.ToString());
    }
}
