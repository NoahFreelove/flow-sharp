using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase06;

/// <summary>
/// QOL-01 regression test: the --verbose flag (FlowEngineRunner constructor
/// arg forwarded to FlowEngine) MUST cause the interpreter to emit a
/// "[verbose] Executing" diagnostic line to stderr at engine start.
/// Pre-QOL-01 no verbose output existed. This Fact pins the post-QOL-01
/// contract that the prefix survives future refactors of FlowEngine.cs.
/// Empirical anchor: flow-lang/Core/FlowEngine.cs:81.
/// </summary>
[Collection("FlowScripts")]
public class VerboseFlagTests
{
    [Fact]
    public void RunSource_WithVerbose_WritesVerbosePrefixToStderr()
    {
        using var runner = new FlowEngineRunner(verbose: true);
        var (_, _, stderr, _) = runner.RunSource(@"
use ""@std""
(print ""ok"")
");
        Assert.Contains("[verbose] Executing", stderr);
    }

    [Fact]
    public void RunSource_WithoutVerbose_DoesNotWriteVerbosePrefix()
    {
        using var runner = new FlowEngineRunner(verbose: false);
        var (_, _, stderr, _) = runner.RunSource(@"
use ""@std""
(print ""ok"")
");
        Assert.DoesNotContain("[verbose]", stderr);
    }
}
