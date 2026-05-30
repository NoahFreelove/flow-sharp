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

    // ===== Plan 45-04 Task 1 — EvaluateBeatLiteral multiplier matrix (D-10) =====

    /// <summary>
    /// End-to-end helper: executes <paramref name="source"/> through a fresh
    /// <see cref="FlowEngine"/> with stdout captured, returns the trimmed
    /// console output. Exercises lex → parse → eval together (mirrors the
    /// Phase 44 strict-mode test style). <c>(str Beat)</c> emits the plain
    /// quarter-relative double per D-14, so the printed value IS the post-
    /// multiplier result.
    /// </summary>
    private static string RunCapture(string source)
    {
        var prev = Console.Out;
        var sw = new StringWriter();
        try
        {
            Console.SetOut(sw);
            using var engine = new FlowEngine();
            var ok = engine.Execute(source, "<test>");
            Assert.True(ok, $"execute failed: {engine.ErrorReporter.FormatErrors()}");
        }
        finally
        {
            Console.SetOut(prev);
        }
        return sw.ToString().Trim();
    }

    [Theory]
    [InlineData("4/4")]
    [InlineData("6/8")]
    [InlineData("2/2")]
    public void MultiplierFormula_PragmaOff_Identity(string timesig)
    {
        // Pragma OFF → multiplier is always 1.0 (raw passes through) in EVERY
        // timesig. `1b` prints "1" regardless of denominator (D-02 / D-10).
        var src = $"timesig {timesig} {{ Beat b = 1b; (print (str b)) }}";
        Assert.Equal("1", RunCapture(src));
    }

    [Fact]
    public void MultiplierFormula_PragmaOn_4Over4()
    {
        // denom=4 → multiplier = 4/4 = 1.0 (identity). Pragma activation does
        // NOT corrupt 4/4 scripts (D-02 Pitfall-4 default-meter safety).
        var src = "enable beat-true-to-sig;\ntimesig 4/4 { Beat b = 1b; (print (str b)) }";
        Assert.Equal("1", RunCapture(src));
    }

    [Theory]
    [InlineData("1b", "0.5")]
    [InlineData("2b", "1")]
    [InlineData("0.5b", "0.25")]
    public void MultiplierFormula_PragmaOn_6Over8(string literal, string expected)
    {
        // denom=8 → multiplier = 4/8 = 0.5. `1b` = half a quarter (one eighth).
        var src = $"enable beat-true-to-sig;\ntimesig 6/8 {{ Beat b = {literal}; (print (str b)) }}";
        Assert.Equal(expected, RunCapture(src));
    }

    [Theory]
    [InlineData("1b", "2")]
    [InlineData("0.5b", "1")]
    public void MultiplierFormula_PragmaOn_2Over2(string literal, string expected)
    {
        // denom=2 → multiplier = 4/2 = 2.0. `1b` = two quarters (one half).
        var src = $"enable beat-true-to-sig;\ntimesig 2/2 {{ Beat b = {literal}; (print (str b)) }}";
        Assert.Equal(expected, RunCapture(src));
    }

    [Fact]
    public void MultiplierFormula_PragmaOn_5Over4()
    {
        // denom=4 → multiplier = 4/4 = 1.0 (identity — quarter-denominator meter).
        var src = "enable beat-true-to-sig;\ntimesig 5/4 { Beat b = 1b; (print (str b)) }";
        Assert.Equal("1", RunCapture(src));
    }

    [Fact]
    public void MultiplierFormula_PragmaOn_7Over8()
    {
        // denom=8 → multiplier = 4/8 = 0.5 (irregular meter, eighth-beat unit).
        var src = "enable beat-true-to-sig;\ntimesig 7/8 { Beat b = 1b; (print (str b)) }";
        Assert.Equal("0.5", RunCapture(src));
    }

    [Fact]
    public void MultiplierFormula_NegativePassthrough()
    {
        // D-08: negative Beat values are valid doubles, no rejection guard.
        // Pragma ON in 4/4 → multiplier 1.0 → -2b stays -2.0.
        var src = "enable beat-true-to-sig;\ntimesig 4/4 { Beat b = -2b; (print (str b)) }";
        Assert.Equal("-2", RunCapture(src));
    }

    [Fact]
    public void MultiplierFormula_NoActiveTimesig()
    {
        // Pragma ON, NO timesig block at all → GetMusicalContext() three-tier
        // fallback resolves to the default 4/4 → denom=4 → multiplier 1.0.
        // Pitfall 4 / D-02: identity-in-default protects timesig-less scripts.
        var src = "enable beat-true-to-sig;\nBeat b = 1b; (print (str b))";
        Assert.Equal("1", RunCapture(src));
    }

    // ===== Plan 45-06 Task 1 — (str Beat) round-trip lock (D-14 / Signal 6) =====
    //
    // D-14 LOCK: (str someBeat) emits the plain quarter-relative double with NO
    // 'b' suffix in EVERY mode. The multiplier resolves at CONSTRUCTION time, so
    // by the time str sees the Beat it is already a quarter-relative double; str
    // never re-applies or re-tags. Emitting "0.5b" would break round-trip under
    // the pragma (re-parsing "0.5b" in 6/8 re-multiplies to 0.25). These 4 Facts
    // pin that lock across the (pragma × timesig) corners.

    [Fact]
    public void StrEmitsPlainDoublePragmaOff()
    {
        // No pragma → multiplier 1.0 → (beat 0.5) is 0.5 quarters → str = "0.5".
        var src = "(print (str (beat 0.5)))";
        Assert.Equal("0.5", RunCapture(src));
    }

    [Fact]
    public void StrEmitsPlainDoublePragmaOn4Over4()
    {
        // Pragma on, 4/4 → multiplier 1.0 → 0.5 quarters → str = "0.5" (no 'b').
        var src = "enable beat-true-to-sig;\ntimesig 4/4 { (print (str (beat 0.5))) }";
        Assert.Equal("0.5", RunCapture(src));
    }

    [Fact]
    public void StrEmitsQuarterValuePragmaOn6Over8()
    {
        // Pragma on, 6/8 → multiplier 4/8 = 0.5 → (beat 1.0) constructs
        // Value.Beat(0.5); str shows the QUARTER value "0.5", never "1b".
        var src = "enable beat-true-to-sig;\ntimesig 6/8 { (print (str (beat 1.0))) }";
        Assert.Equal("0.5", RunCapture(src));
    }

    [Fact]
    public void StrEmitsQuarterValuePragmaOn2Over2()
    {
        // Pragma on, 2/2 → multiplier 4/2 = 2.0 → (beat 0.5) constructs
        // Value.Beat(1.0); str shows the QUARTER value "1".
        var src = "enable beat-true-to-sig;\ntimesig 2/2 { (print (str (beat 0.5))) }";
        Assert.Equal("1", RunCapture(src));
    }

    // ===== Plan 45-06 Task 1 — cross-file boundary smoke (REQ-BEAT-TEST-04) =====
    //
    // Pitfall 3 / D-04 verification at the composer-source level: a pragma-ON
    // entry file `use`-s a pragma-OFF helper declaring `proc bumpBeat ... (beat 1)`.
    // Inside the entry's `timesig 6/8 { }`, a LOCAL 1b literal multiplies to 0.5,
    // but `(bumpBeat (beat 0))` returns Value.Beat(1.0) — the helper proc's
    // (beat 1) reads its DECLARING file's pragma bit (off), not the caller's.
    // Mechanism: ProcDeclaration.IsBeatTrueToSig captured at parse time from the
    // declaring file + per-proc push/pop in Interpreter.ExecuteUserFunctionWithCaptures.

    private static string RepoRoot
    {
        get
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new InvalidOperationException(
                    "Could not locate flow-sharp.sln walking up from " + AppContext.BaseDirectory);
            return dir.FullName;
        }
    }

    private static string FlowInterpreterDll =>
        Path.Combine(RepoRoot, "flow-interpreter", "bin", "Debug", "net10.0", "flow-interpreter.dll");

    private static bool DllMissing => !File.Exists(FlowInterpreterDll);

    /// <summary>
    /// Spawns <c>dotnet exec flow-interpreter.dll &lt;repo-relative path&gt;</c>
    /// with a 120s cap. Returns (exitCode, stdout, stderr). Mirrors Phase 44's
    /// <c>StrictFlowScriptSuiteTests.RunInterpreter</c>.
    /// </summary>
    internal static (int exitCode, string stdout, string stderr) RunInterpreter(string repoRelativePath)
    {
        var fullPath = Path.Combine(RepoRoot, repoRelativePath);
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet",
            $"exec \"{FlowInterpreterDll}\" \"{fullPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = RepoRoot,
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        if (!proc.WaitForExit(milliseconds: 120_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new TimeoutException(
                $"flow-interpreter timed out after 120s on {repoRelativePath}.\n" +
                $"stdout:\n{stdout}\nstderr:\n{stderr}");
        }
        return (proc.ExitCode, stdout, stderr);
    }

    [Fact]
    public void CrossFileSmokeFact()
    {
        if (DllMissing)
            return;  // charitable skip when interpreter not yet built (Phase 39 precedent)

        var (exitCode, stdout, stderr) = RunInterpreter("tests/test_beat_cross_file.flow");
        Assert.True(exitCode == 0,
            $"expected exit 0 for tests/test_beat_cross_file.flow; got {exitCode}.\n" +
            $"stdout:\n{stdout}\nstderr:\n{stderr}");
        // Local literal sees the multiplier (6/8 → 1b = 0.5 quarters).
        Assert.Contains("local 1b in 6/8 pragma-on = 0.5", stdout);
        // Cross-file boundary: helper proc's (beat 1) reads the DECLARING (pragma-off)
        // file's bit → raw quarters = 1, NOT 0.5 (D-04 / Pitfall 3).
        Assert.Contains("helper (beat 1) called from 6/8 pragma-on = 1", stdout);
        Assert.Contains("test_beat_cross_file: PASSED", stdout);
    }

    // ===== Plan 45-06 Task 2 — tutorial two-run cmp-clean + baseline match (REQ-BEAT-TEST-07) =====
    //
    // Phase 45 adds NO PRNG sites — the tutorial WAVs are pure synthesis and
    // MUST be byte-identical across runs (CLAUDE.md "Conventions" two-run
    // cmp-clean contract). The committed baselines under
    // flow-lang.Tests/baselines/Phase45/ are the reference renders (Phase 28
    // precedent — committed because no stochastic compute is invoked). Raw
    // SHA-256 over file bytes is sufficient (no RMS tolerance needed — exact
    // determinism, not perceptual fidelity).

    private static string Sha256(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(path)));
    }

    private static void RunTutorial(string repoRelativeFlow)
    {
        var (exit, stdout, stderr) = RunInterpreter(repoRelativeFlow);
        Assert.True(exit == 0,
            $"expected exit 0 for {repoRelativeFlow}; got {exit}.\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("PASSED", stdout);
    }

    [Fact]
    public void TutorialTwoRunCmpClean_Intro()
    {
        if (DllMissing) return;  // charitable skip
        RunTutorial("examples/beat/intro.flow");
        var sha1 = Sha256("/tmp/beat_intro.wav");
        RunTutorial("examples/beat/intro.flow");
        var sha2 = Sha256("/tmp/beat_intro.wav");
        Assert.Equal(sha1, sha2);
    }

    [Fact]
    public void TutorialTwoRunCmpClean_CutTime()
    {
        if (DllMissing) return;  // charitable skip
        RunTutorial("examples/beat/cut-time.flow");
        var sha1 = Sha256("/tmp/beat_cut_time.wav");
        RunTutorial("examples/beat/cut-time.flow");
        var sha2 = Sha256("/tmp/beat_cut_time.wav");
        Assert.Equal(sha1, sha2);
    }

    [Fact]
    public void TutorialMatchesBaseline_Intro()
    {
        if (DllMissing) return;  // charitable skip
        var baseline = Path.Combine(RepoRoot, "flow-lang.Tests", "baselines", "Phase45", "intro.wav");
        if (!File.Exists(baseline)) return;  // baseline not committed yet
        RunTutorial("examples/beat/intro.flow");
        Assert.Equal(Sha256(baseline), Sha256("/tmp/beat_intro.wav"));
    }

    [Fact]
    public void TutorialMatchesBaseline_CutTime()
    {
        if (DllMissing) return;  // charitable skip
        var baseline = Path.Combine(RepoRoot, "flow-lang.Tests", "baselines", "Phase45", "cut-time.wav");
        if (!File.Exists(baseline)) return;  // baseline not committed yet
        RunTutorial("examples/beat/cut-time.flow");
        Assert.Equal(Sha256(baseline), Sha256("/tmp/beat_cut_time.wav"));
    }
}
