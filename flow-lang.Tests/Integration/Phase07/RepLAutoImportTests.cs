using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase07;

/// <summary>
/// DX-04 regression test: the REPL (flow-interpreter/Repl.cs
/// AutoImportStandardModules) hardcodes the same three <c>use</c> statements
/// below, so interactive users can call <c>print</c> / <c>list</c> /
/// <c>createSineTone</c> without typing any <c>use</c> themselves.
///
/// The v1.1 audit (see v1.1-MILESTONE-AUDIT.md) noted DX-04 "is not
/// e2e-testable via piped stdin -- verified by code inspection only" because
/// piped stdin routes to RunFromStdin (script mode), not the REPL. 07-02
/// SUMMARY line 104 confirms this: <i>"REPL auto-import cannot be tested via
/// echo | dotnet run because piped stdin routes to RunFromStdin"</i>.
///
/// This Fact executes the SAME three imports the REPL hardcodes
/// (<c>@std</c>, <c>@audio</c>, <c>@collections</c>) via FlowEngine and
/// verifies the symbols they export (<c>print</c>, <c>list</c>,
/// <c>createSineTone</c>) resolve without "function not found" errors. It is
/// the closest automatable proxy for the REPL's auto-import contract.
/// </summary>
[Collection("FlowScripts")]
public class RepLAutoImportTests
{
    [Fact]
    public void AutoImportedModulesResolve_StdAudioCollections()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
use ""@collections""
Int[] xs = (list 1 2 3)
Buffer b = (createSineTone 0.1 440.0 0.3)
(print ""ok"")
");
        Assert.True(ok, $"script errored: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("ok", stdout);
    }
}
