using System.IO;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase21;

/// <summary>
/// PRAG-02 isolation: pragmas declared inside a module loaded via `use` MUST NOT
/// propagate to the importer. Closed structurally by per-imported-file PragmaSet
/// in <c>ModuleLoader.LoadModule</c> (D-06).
///
/// Plan 21-01 baseline: importer + module both parse cleanly; module declares
/// <c>enable hAsB;</c> internally. Plan 21-02 will tighten this Fact (or add a
/// sibling) to verify that an H4q in the importer raises a parse error.
/// </summary>
[Collection("FlowScripts")]
public class PragmaIsolationFacts
{
    [Fact]
    public void Importer_LoadsModule_WithoutInheritingItsPragmas()
    {
        // Resolve absolute path via FlowScriptData.FindTestsRoot() because xUnit
        // runs with cwd = bin/Debug/net10.0/, not the repo root.
        var testsRoot = FlowScriptData.FindTestsRoot();
        var importerPath = Path.Combine(testsRoot, "test_pragma_isolation.flow");

        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunFile(importerPath);
        Assert.True(ok, $"importer + module should both parse cleanly under Plan 21-01 baseline. stderr: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("test_pragma_isolation_module: PASSED", stdout);
        Assert.Contains("test_pragma_isolation: PASSED", stdout);
        // Plan 21-02 will tighten this Fact (or add a sibling) to verify that an
        // H4q in the importer raises a parse error after H-substitution lands.
    }
}
