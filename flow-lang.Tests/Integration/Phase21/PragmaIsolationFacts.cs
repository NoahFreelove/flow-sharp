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
/// Plan 21-01 shipped one Fact (Importer_LoadsModule_WithoutInheritingItsPragmas)
/// asserting both files parsed cleanly. Plan 21-02 TIGHTENS the surface:
///   * The .flow fixtures now exercise the module's H4q under its own pragma
///     (proving the pragma is alive inside the module).
///   * A second Fact (Importer_WithoutHAsB_RejectsHNote_EvenWhenModuleEnablesIt)
///     uses an inline RunSource that places H4q in the IMPORTER body and asserts
///     the parse error — this is the load-bearing PRAG-02 acceptance.
/// </summary>
[Collection("FlowScripts")]
public class PragmaIsolationFacts
{
    [Fact]
    public void Importer_LoadsModuleCleanly_WithoutInheritingItsPragmas()
    {
        // tests/test_pragma_isolation.flow + module fixture: module declares
        // `enable hAsB;` and exercises `| H4q B4q |` under that pragma. The
        // importer does NOT declare the pragma but only `use`s the module — so
        // both files parse cleanly. This Fact proves the structural integration:
        // module pragma scan ran, module rendered its own H-notes, importer's
        // separate scan produced its own (empty) PragmaSet without inheriting.
        var testsRoot = FlowScriptData.FindTestsRoot();
        var importerPath = Path.Combine(testsRoot, "test_pragma_isolation.flow");

        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunFile(importerPath);
        Assert.True(ok, $"importer + module should both parse cleanly. stderr: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("test_pragma_isolation_module: PASSED", stdout);
        Assert.Contains("test_pragma_isolation: PASSED", stdout);
    }

    [Fact]
    public void Importer_WithoutHAsB_RejectsHNote_EvenWhenModuleEnablesIt()
    {
        // PRAG-02 acceptance: an importer that uses H4q (no pragma declared) MUST
        // fail, regardless of whether an imported module declared `enable hAsB;`
        // locally. The H token never resolves to a NoteLiteral in the importer
        // because PragmaScanner produced an empty PragmaSet for THIS file —
        // pragma scope is strictly per-file (D-06).
        //
        // Inline RunSource keeps the failing assertion isolated to the Fact
        // (NOT a tracked tests/test_*.flow script that the integration loop
        // would pick up as FAIL). The module path resolves relative to
        // ModuleLoader's caller-directory convention; using the absolute
        // tests/ path guarantees discovery regardless of xUnit cwd.
        var testsRoot = FlowScriptData.FindTestsRoot();
        var modulePath = Path.Combine(testsRoot, "test_pragma_isolation_module.flow")
            .Replace('\\', '/');

        using var runner = new FlowEngineRunner();
        var (ok, _, _, errorCount) = runner.RunSource(@"use ""@std""
use """ + modulePath + @"""
(print (str | H4q |))
");
        Assert.False(ok, "expected parse failure: importer used H4q without declaring enable hAsB;");
        Assert.True(errorCount > 0);
    }
}
