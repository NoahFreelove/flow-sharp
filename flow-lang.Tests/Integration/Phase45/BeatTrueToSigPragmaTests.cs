using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.StandardLibrary;
using Xunit;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Integration.Phase45;

/// <summary>
/// Phase 45 Plan 45-03 — Facts pinning the file-scope <c>beat-true-to-sig</c>
/// pragma plumbing per D-03 / D-04:
///
/// <list type="number">
///   <item>The pragma is a recognized <see cref="PragmaRegistry"/> entry with
///         the verbatim D-03 description.</item>
///   <item><see cref="ExecutionContext.BeatTrueToSig"/> defaults false, is
///         settable, and reflects the DECLARING file's pragma bit at the
///         Execute boundary (FlowEngine.ApplyBeatTrueToSigPragma).</item>
///   <item>Typo path routes through the existing Levenshtein suggester.</item>
///   <item>Cross-file save-set-restore is finally-protected — a pragma-on file
///         <c>use</c>-ing a pragma-off file (or vice versa, or a throwing
///         import) restores the importer's bit (ModuleLoader Anti-Pattern 1).</item>
/// </list>
///
/// <para>
/// Single-field design per D-04 / Pitfall 3 — there is deliberately NO
/// companion <c>CallerBeatTrueToSig</c> field (the multiplier reads the
/// executing file's pragma bit, so no leaf-clamp-site asymmetry exists).
/// </para>
///
/// <para>
/// Mirrors Phase 44's <c>ExecutionContextStrictModeTests</c> +
/// <c>ModuleLoaderStrictPropagationTests</c> + <c>PragmaRegistryStrictTests</c>
/// shapes (NewContext helper + tempdir cross-file authoring + verbatim
/// description constant).
/// </para>
/// </summary>
[Trait("Category", Phase45TestCategory.Phase45)]
[Collection("FlowScripts")]
public class BeatTrueToSigPragmaTests : IDisposable
{
    /// <summary>
    /// D-03 verbatim description string. Single source of truth for the
    /// description Fact + the dict entry in <c>flow-lang/Lexing/PragmaRegistry.cs</c>.
    /// If either site drifts, the description Fact surfaces it.
    /// </summary>
    private const string D03Description =
        "Opt-in: Nb literals and (beat N) constructor calls multiply by 4/denominator at eval time, reading active timesig. So in 'timesig 6/8 { }' with pragma on, 1b = 1 eighth. File-scoped, no propagation via use imports.";

    private readonly string _tempDir;

    public BeatTrueToSigPragmaTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "flow-45-03-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static ExecutionContext NewContext()
    {
        var reporter = new ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new ExecutionContext(reporter, registry);
    }

    // ===== Task 1 — pragma registration + context bit + FlowEngine helper =====

    [Fact]
    public void PragmaRegistryEntry()
    {
        Assert.True(PragmaRegistry.KnownPragmas.ContainsKey("beat-true-to-sig"),
            "PragmaRegistry.KnownPragmas must contain 'beat-true-to-sig' per Phase 45 D-03.");
        Assert.True(PragmaRegistry.IsKnown("beat-true-to-sig"),
            "PragmaRegistry.IsKnown(\"beat-true-to-sig\") must return true per Phase 45 D-03.");
        Assert.Equal(D03Description, PragmaRegistry.KnownPragmas["beat-true-to-sig"]);
    }

    [Fact]
    public void PragmaSetsContextBit()
    {
        using var engine = new FlowEngine();
        var ok = engine.Execute("enable beat-true-to-sig;\n(print \"ok\")", "<test>");
        Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");
        Assert.True(engine.Context.BeatTrueToSig,
            "ApplyBeatTrueToSigPragma must flip BeatTrueToSig=true after `enable beat-true-to-sig;`.");
    }

    [Fact]
    public void AbsenceLeavesBitFalse()
    {
        using var engine = new FlowEngine();
        var ok = engine.Execute("(print \"ok\")", "<test>");
        Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");
        Assert.False(engine.Context.BeatTrueToSig,
            "Without the pragma, BeatTrueToSig must remain false (D-04 default).");
    }

    [Fact]
    public void LevenshteinSuggestion()
    {
        // `enable bea-true-to-sig;` (typo: missing 't') routes through the
        // existing PragmaRegistry.SuggestNearest closed-set candidate list,
        // which now includes 'beat-true-to-sig'. No PragmaScanner code change.
        var reporter = new ErrorReporter();
        var (_, _) = PragmaScanner.Scan("enable bea-true-to-sig;\n", "<test>", reporter);

        Assert.True(reporter.HasErrors,
            "expected unknown-pragma error for 'enable bea-true-to-sig;'");
        var msg = reporter.Errors[0].Message;
        Assert.Contains("did you mean", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("beat-true-to-sig", msg);
    }

    [Fact]
    public void BeatTrueToSig_DefaultsFalse()
    {
        var ctx = NewContext();
        Assert.False(ctx.BeatTrueToSig,
            "ExecutionContext.BeatTrueToSig must default to false per Phase 45 D-04.");
    }

    [Fact]
    public void BeatTrueToSig_Settable()
    {
        var ctx = NewContext();
        ctx.BeatTrueToSig = true;
        Assert.True(ctx.BeatTrueToSig);
        ctx.BeatTrueToSig = false;
        Assert.False(ctx.BeatTrueToSig);
    }

    // ===== Task 2 — ModuleLoader cross-file save-set-restore =====

    [Fact]
    public void CrossFileRestoreToFalse()
    {
        // Pragma-OFF outer file `use`-s a pragma-ON helper. After the import
        // returns, the importer's bit must be restored to its pre-import value
        // (false). The helper declares the pragma; the importer does not.
        var innerPath = Path.Combine(_tempDir, "inner_pragma_on.flow");
        File.WriteAllText(innerPath,
            "enable beat-true-to-sig;\n" +
            "Int innerX = 10;\n");

        using var engine = new FlowEngine();
        var innerPosix = innerPath.Replace('\\', '/');
        var outerSrc =
            "use \"" + innerPosix + "\"\n" +
            "Int y = 5;\n";
        var ok = engine.Execute(outerSrc, "<top>");
        Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");

        Assert.False(engine.Context.BeatTrueToSig,
            "pragma-off outer's BeatTrueToSig must be RESTORED to false after the " +
            "pragma-on import returns (ModuleLoader save-set-restore).");
    }

    [Fact]
    public void CrossFileRestoreToTrue()
    {
        // Pragma-ON outer file `use`-s a pragma-OFF helper. After the import
        // returns, the importer's bit must be restored to true.
        var innerPath = Path.Combine(_tempDir, "inner_pragma_off.flow");
        File.WriteAllText(innerPath, "Int innerX = 10;\n");

        using var engine = new FlowEngine();
        var innerPosix = innerPath.Replace('\\', '/');
        var outerSrc =
            "enable beat-true-to-sig;\n" +
            "use \"" + innerPosix + "\"\n" +
            "Int y = 5;\n";
        var ok = engine.Execute(outerSrc, "<top>");
        Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");

        Assert.True(engine.Context.BeatTrueToSig,
            "pragma-on outer's BeatTrueToSig must be RESTORED to true after the " +
            "pragma-off import returns (the import's set-to-false is overridden by " +
            "the finally restore).");
    }

    [Fact]
    public void CrossFileRestoreAfterThrow()
    {
        // Pragma-ON outer file `use`-s a MISSING module. The import errors, but
        // the outer's bit MUST still be restored to true via the try/finally
        // save-restore (Anti-Pattern 1: never mutate without a paired restore).
        using var engine = new FlowEngine();
        var missingPath = Path.Combine(_tempDir, "definitely_missing.flow").Replace('\\', '/');
        var outerSrc =
            "enable beat-true-to-sig;\n" +
            "use \"" + missingPath + "\"\n";
        // Execute returns false because the import errors; we care about the
        // POST-execute state — the finally-restore must have run on the error path.
        engine.Execute(outerSrc, "<top>");

        Assert.True(engine.Context.BeatTrueToSig,
            "pragma-on outer's BeatTrueToSig must be RESTORED after import-failure " +
            "unwind (try/finally save/restore — Anti-Pattern 1).");
    }

    [Fact]
    public void StdlibImportLeavesBitUnchanged()
    {
        // Pragma-ON entry file `use`-s a stdlib module (@audio) which declares
        // no pragma. After the import returns, the importer's bit must still be
        // true — the import's set-to-false is overridden by the finally restore.
        using var engine = new FlowEngine();
        var outerSrc =
            "enable beat-true-to-sig;\n" +
            "use \"@audio\"\n" +
            "Int y = 5;\n";
        var ok = engine.Execute(outerSrc, "<top>");
        Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");

        Assert.True(engine.Context.BeatTrueToSig,
            "pragma-on entry file's BeatTrueToSig must remain true after a " +
            "pragma-less stdlib `use \"@audio\"` (finally restore-to-true).");
    }
}
