using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-01 Task 2 — Facts pinning the per-DECLARING-file strict-bit
/// propagation contract per D-02 / D-03:
///
/// <list type="number">
///   <item>Top-level <c>enable strict;</c> sets <c>engine.Context.StrictMode</c>
///         after Execute returns (FlowEngine.ApplyStrictPragma).</item>
///   <item>Absent <c>enable strict;</c> keeps StrictMode false.</item>
///   <item>Strict file <c>use</c>s a non-strict inner — the inner's Execute
///         observes <c>StrictMode == false</c> at runtime; the outer's bit is
///         RESTORED to true after the import returns
///         (ModuleLoader save-set-restore).</item>
///   <item>Symmetric case: non-strict file <c>use</c>s a strict inner —
///         inner observes true; outer stays false.</item>
///   <item>Strict file imports a MISSING module — outer's strict bit MUST
///         still be restored on the error path (try/finally semantics).</item>
/// </list>
///
/// <para>
/// Observation strategy: a test-only builtin <c>_test_strict_observe</c> is
/// registered into the engine's <see cref="InternalFunctionRegistry"/> via
/// <c>engine.Context.InternalRegistry.Register</c> BEFORE Execute runs. The
/// builtin captures <c>engine.Context.StrictMode</c> into a closed-over
/// <c>bool?</c> the moment it's invoked from inner.flow's body. After
/// Execute, the test reads both the in-import capture and the post-import
/// <c>engine.Context.StrictMode</c> to assert the save-restore contract.
/// </para>
///
/// <para>
/// Inner .flow files are authored into a per-Fact tempdir and cleaned up on
/// Dispose to keep the test hermetic. Mirrors the Phase 36 StyleRegistry +
/// Phase 42 audit-harness tempdir pattern.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class ModuleLoaderStrictPropagationTests : IDisposable
{
    private readonly string _tempDir;

    public ModuleLoaderStrictPropagationTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "flow-44-01-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    /// <summary>
    /// Builds a sandbox engine and registers a test-only <c>_test_strict_observe</c>
    /// builtin that captures <c>engine.Context.StrictMode</c> at invocation time
    /// into <paramref name="captured"/>. Returns the engine.
    ///
    /// The builtin takes no args and returns Void. It's safe to invoke from any
    /// Flow source via <c>(_test_strict_observe)</c>.
    /// </summary>
    private static FlowEngine NewEngineWithObserver(out Action<bool> setCapture, out Func<bool?> getCapture)
    {
        bool? captured = null;
        setCapture = v => captured = v;
        getCapture = () => captured;
        var engine = new FlowEngine();
        var sig = new FunctionSignature(
            Name: "_test_strict_observe",
            InputTypes: Array.Empty<FlowType>(),
            IsVarArgs: false);
        engine.Context.InternalRegistry.Register(
            "_test_strict_observe",
            sig,
            _ =>
            {
                captured = engine.Context.StrictMode;
                return Value.Void();
            });
        // Keep the local 'captured' alive across the closures by hooking getCapture
        // to a fresh delegate after registration so callers can read it post-Execute.
        getCapture = () => captured;
        return engine;
    }

    [Fact]
    public void Fact_TopLevelEnableStrict_SetsContextStrictMode()
    {
        using var engine = new FlowEngine();
        var ok = engine.Execute("enable strict;\nInt x = 5;\n", "<top>");
        Assert.True(ok, $"top-level execute failed: {engine.ErrorReporter.FormatErrors()}");
        Assert.True(engine.Context.StrictMode,
            "ApplyStrictPragma must flip StrictMode=true after a top-level `enable strict;`.");
    }

    [Fact]
    public void Fact_NoEnableStrict_StrictModeStaysFalse()
    {
        using var engine = new FlowEngine();
        var ok = engine.Execute("Int x = 5;\n", "<top>");
        Assert.True(ok, $"top-level execute failed: {engine.ErrorReporter.FormatErrors()}");
        Assert.False(engine.Context.StrictMode,
            "Without `enable strict;`, StrictMode must remain false (D-02 default).");
    }

    [Fact]
    public void Fact_StrictFileImportsNonStrictModule_ImportedExecuteSeesFalse()
    {
        // inner.flow: no `enable strict;` — must execute with StrictMode=false
        // even when imported from a strict outer. The observer proc declaration
        // is placed in the outer BEFORE the `use` so the proc is in scope when
        // inner runs (imports execute in caller's context; no new frame).
        var innerPath = Path.Combine(_tempDir, "inner_nonstrict.flow");
        File.WriteAllText(innerPath, "(_test_strict_observe)\nInt innerX = 10;\n");

        var engine = NewEngineWithObserver(out _, out var getCapture);
        try
        {
            var innerPosix = innerPath.Replace('\\', '/');
            var outerSrc =
                "enable strict;\n" +
                "internal proc _test_strict_observe ()\n" +
                "use \"" + innerPosix + "\"\n" +
                "Int y = 5;\n";
            var ok = engine.Execute(outerSrc, "<top>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");

            // D-03 contract: inner.flow's body saw StrictMode == false because
            // inner did NOT declare `enable strict;`. ModuleLoader saved the
            // outer's true, set false for inner, then restored true after import.
            Assert.True(getCapture().HasValue,
                "_test_strict_observe must have been invoked during inner Execute");
            Assert.False(getCapture()!.Value,
                "non-strict inner module must observe StrictMode=false at runtime (D-03).");

            // Restore contract: outer's strict bit is back to true after the import.
            Assert.True(engine.Context.StrictMode,
                "outer file's StrictMode=true must be RESTORED after the import returns.");
        }
        finally { engine.Dispose(); }
    }

    [Fact]
    public void Fact_NonStrictFileImportsStrictModule_OuterBitStaysFalse()
    {
        // Symmetric case: inner declares strict, outer does not. Observer proc
        // declared in outer before the use, as in the prior Fact.
        var innerPath = Path.Combine(_tempDir, "inner_strict.flow");
        File.WriteAllText(innerPath,
            "enable strict;\n" +
            "(_test_strict_observe)\n" +
            "Int innerX = 10;\n");

        var engine = NewEngineWithObserver(out _, out var getCapture);
        try
        {
            var innerPosix = innerPath.Replace('\\', '/');
            var outerSrc =
                "internal proc _test_strict_observe ()\n" +
                "use \"" + innerPosix + "\"\n" +
                "Int y = 5;\n";
            var ok = engine.Execute(outerSrc, "<top>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");

            Assert.True(getCapture().HasValue,
                "_test_strict_observe must have been invoked during inner Execute");
            Assert.True(getCapture()!.Value,
                "strict inner module must observe StrictMode=true at runtime (D-03).");

            // Outer's bit stays false (it never declared the pragma).
            Assert.False(engine.Context.StrictMode,
                "non-strict outer's StrictMode=false must remain false after import.");
        }
        finally { engine.Dispose(); }
    }

    [Fact]
    public void Fact_StrictFileImportFailure_OuterBitStillRestored()
    {
        // Outer is strict + imports a missing path. The import errors, but the
        // outer's StrictMode bit MUST still be restored to true via the
        // try/finally save/restore (Anti-Pattern 1: NEVER mutate StrictMode
        // without paired restore).
        using var engine = new FlowEngine();
        var missingPath = Path.Combine(_tempDir, "definitely_missing.flow").Replace('\\', '/');
        var outerSrc =
            "enable strict;\n" +
            "use \"" + missingPath + "\"\n";
        // Execute returns false because the import errors, but we care about the
        // POST-execute state of StrictMode — the try/finally in ModuleLoader must
        // have run its restore even on the missing-file error path.
        engine.Execute(outerSrc, "<top>");

        Assert.True(engine.Context.StrictMode,
            "outer file's StrictMode must be RESTORED after import-failure unwind " +
            "(try/finally save/restore — Anti-Pattern 1).");
    }
}
