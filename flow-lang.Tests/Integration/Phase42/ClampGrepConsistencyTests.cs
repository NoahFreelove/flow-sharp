using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace FlowLang.Tests.Integration.Phase42;

/// <summary>
/// Phase 42 Plan 02 — inventory consistency pin for the
/// <c>scripts/audit/clamp-grep.sh</c> + <c>scripts/audit/flow-callers.sh</c>
/// extractors. Shells out to both scripts (bash, Linux/macOS only), then
/// asserts the produced inventory files have sane shape — line counts within
/// ±generous tolerance of the RESEARCH.md baselines (~72 Math.Clamp, ~117
/// WarnOnce) and the well-known sentinel advisory sites
/// (<c>tuning</c> / <c>granular</c> / <c>stretch</c> per Phase 32/37) are
/// surfaced.
///
/// Pins the inventory files regression-style so Plan 03's AUDIT.md §6
/// (load-bearing for Phase 44 per ROADMAP line 380) can rely on stable
/// extractor output.
///
/// Mirrors Phase 36 PrngRegistryNewRandomGateTests' FindRepoRoot walker and
/// Phase 29 LicenseAuditTests' Integration/PhaseNN namespace convention.
/// Phase 42 invariant: ZERO production code touched — this fixture only
/// reads scripts/audit/ output.
/// </summary>
public class ClampGrepConsistencyTests
{
    // RESEARCH.md baselines: ~72 Math.Clamp + ~117 WarnOnce. Tolerance windows
    // are generous to allow forward drift across Plans 03+ / Phases 43+ without
    // requiring a fixture update for every minor stdlib addition.
    private const int AllClampsLowerBound = 50;
    private const int AllClampsUpperBound = 200;
    private const int AdvisoryLowerBound = 80;
    private const int AdvisoryUpperBound = 300;

    [Fact]
    public void ClampGrep_ProducesAllInventoryFiles()
    {
        if (!IsBashAvailable()) return;

        string repoRoot = FindRepoRoot();
        RunBashScript(repoRoot, Path.Combine("scripts", "audit", "clamp-grep.sh"));

        string outDir = Path.Combine(
            repoRoot, ".planning", "phases", "42-type-system-stdlib-audit", "42-AUDIT-data");

        string[] expected =
        {
            "input-clamps.txt",
            "all-clamps.txt",
            "advisory-sites.txt",
            "charitable-sites.txt",
            "summary.txt",
        };

        foreach (var name in expected)
        {
            string path = Path.Combine(outDir, name);
            Assert.True(File.Exists(path), $"expected inventory file missing: {path}");
        }

        // input-clamps.txt and charitable-sites.txt MAY legitimately be empty
        // in some forward-drift scenarios; only the load-bearing ones must be
        // non-empty here.
        Assert.True(
            new FileInfo(Path.Combine(outDir, "all-clamps.txt")).Length > 0,
            "all-clamps.txt must be non-empty — RESEARCH baseline ~72 sites");
        Assert.True(
            new FileInfo(Path.Combine(outDir, "advisory-sites.txt")).Length > 0,
            "advisory-sites.txt must be non-empty — RESEARCH baseline ~117 sites");
    }

    [Fact]
    public void AllClamps_CountWithinTolerance()
    {
        if (!IsBashAvailable()) return;

        string repoRoot = FindRepoRoot();
        RunBashScript(repoRoot, Path.Combine("scripts", "audit", "clamp-grep.sh"));

        string path = Path.Combine(
            repoRoot, ".planning", "phases", "42-type-system-stdlib-audit",
            "42-AUDIT-data", "all-clamps.txt");

        int lineCount = File.ReadAllLines(path).Length;

        Assert.True(
            lineCount >= AllClampsLowerBound && lineCount <= AllClampsUpperBound,
            $"all-clamps.txt line count {lineCount} outside tolerance " +
            $"[{AllClampsLowerBound}, {AllClampsUpperBound}] (RESEARCH baseline ~72). " +
            "A large drift suggests either (a) Plan 03+ added many clamps " +
            "(adjust upper bound) or (b) the extractor regressed.");
    }

    [Fact]
    public void AdvisorySites_CountWithinTolerance()
    {
        if (!IsBashAvailable()) return;

        string repoRoot = FindRepoRoot();
        RunBashScript(repoRoot, Path.Combine("scripts", "audit", "clamp-grep.sh"));

        string path = Path.Combine(
            repoRoot, ".planning", "phases", "42-type-system-stdlib-audit",
            "42-AUDIT-data", "advisory-sites.txt");

        int lineCount = File.ReadAllLines(path).Length;

        Assert.True(
            lineCount >= AdvisoryLowerBound && lineCount <= AdvisoryUpperBound,
            $"advisory-sites.txt line count {lineCount} outside tolerance " +
            $"[{AdvisoryLowerBound}, {AdvisoryUpperBound}] (RESEARCH baseline ~117). " +
            "Large drift suggests Phase 44 strict-mode advisories added or " +
            "RenderingDiagnostics.WarnOnce was renamed.");
    }

    [Fact]
    public void AdvisorySites_ContainsKnownSentinels()
    {
        if (!IsBashAvailable()) return;

        string repoRoot = FindRepoRoot();
        RunBashScript(repoRoot, Path.Combine("scripts", "audit", "clamp-grep.sh"));

        string path = Path.Combine(
            repoRoot, ".planning", "phases", "42-type-system-stdlib-audit",
            "42-AUDIT-data", "advisory-sites.txt");

        string[] lines = File.ReadAllLines(path);

        // Sentinel substrings from CLAUDE.md / RESEARCH.md known-advisory list:
        //   - "tuning"  : Phase 32 D-08 unmapped MIDI key warning
        //   - "granular": Phase 37 DSP-01 unknown-windowing fallback
        //   - "stretch" : Phase 37 D-37-06 #auto mode HPS advisory
        // Case-insensitive match so file-path casing variants are tolerated.
        string[] sentinels = { "tuning", "granular", "stretch" };

        foreach (var sentinel in sentinels)
        {
            bool found = lines.Any(l =>
                l.Contains(sentinel, StringComparison.OrdinalIgnoreCase));
            Assert.True(found,
                $"expected sentinel advisory mention '{sentinel}' not found in " +
                $"any of {lines.Length} advisory-sites.txt rows — either Phase " +
                "32/37 advisory was removed or the extractor regressed.");
        }
    }

    [Fact]
    public void FlowCallers_DeclaresKnownStdlibProcs()
    {
        if (!IsBashAvailable()) return;

        string repoRoot = FindRepoRoot();
        RunBashScript(repoRoot, Path.Combine("scripts", "audit", "flow-callers.sh"));

        string path = Path.Combine(
            repoRoot, ".planning", "phases", "42-type-system-stdlib-audit",
            "42-AUDIT-data", "flow-proc-decls.txt");

        Assert.True(File.Exists(path),
            $"flow-proc-decls.txt missing at {path} — flow-callers.sh did not run");

        string[] procs = File.ReadAllLines(path);

        // Anchor the .flow cross-reference half of the registration graph
        // with well-known stdlib proc names. RESEARCH §Pattern 4: a C# builtin
        // with zero hits in this file AND zero hits in the call-sites table
        // is a true dead-end candidate.
        //
        // Note: the plan also suggested 'bar' as a sentinel, but the
        // (Bar) type-constructor / 'bar' is not declared as a .flow proc
        // (it's surfaced at the parser level for `| ... |` note streams).
        // Using only the verifiably-declared procs avoids a false-positive
        // fixture failure.
        string[] sentinels =
        {
            "barLength",  // flow-lang/bars.flow
            "mix",        // flow-lang/audio.flow
            "play",       // flow-lang/audio.flow
        };

        foreach (var name in sentinels)
        {
            bool found = procs.Any(p => p == name);
            Assert.True(found,
                $"expected proc declaration '{name}' not found among " +
                $"{procs.Length} entries — either the .flow stdlib lost the " +
                "proc or the grep pattern regressed.");
        }
    }

    [Fact]
    public void InventoryFiles_LandInPhase42DataDir()
    {
        if (!IsBashAvailable()) return;

        string repoRoot = FindRepoRoot();
        string expectedDir = Path.Combine(
            repoRoot, ".planning", "phases", "42-type-system-stdlib-audit",
            "42-AUDIT-data");

        // Capture a sentinel timestamp BEFORE running the scripts so we can
        // assert that the extractors only wrote inside the expected dir.
        string sentinelDir = Path.Combine(
            repoRoot, ".planning", "phases", "42-type-system-stdlib-audit");

        var beforeFiles = Directory
            .EnumerateFiles(sentinelDir, "*", SearchOption.TopDirectoryOnly)
            .Select(f => (f, ts: File.GetLastWriteTimeUtc(f)))
            .ToList();

        RunBashScript(repoRoot, Path.Combine("scripts", "audit", "clamp-grep.sh"));
        RunBashScript(repoRoot, Path.Combine("scripts", "audit", "flow-callers.sh"));

        // Expected directory exists.
        Assert.True(Directory.Exists(expectedDir),
            $"expected output dir missing: {expectedDir}");

        // None of the top-level phase-dir files (the *.md plans/research/etc.)
        // should have been touched by the scripts.
        foreach (var (f, ts) in beforeFiles)
        {
            var newTs = File.GetLastWriteTimeUtc(f);
            Assert.Equal(ts, newTs);
        }

        // Inventory files all land under expectedDir; we don't enumerate a
        // closed set (forward drift may add new files), but we DO assert that
        // the core six exist there.
        string[] core =
        {
            "input-clamps.txt",
            "all-clamps.txt",
            "advisory-sites.txt",
            "charitable-sites.txt",
            "summary.txt",
            "flow-proc-decls.txt",
        };
        foreach (var name in core)
        {
            string path = Path.Combine(expectedDir, name);
            Assert.True(File.Exists(path),
                $"core inventory file '{name}' missing under {expectedDir}");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mirrors Phase 36 PrngRegistryNewRandomGateTests:84-92. Walks up from
    /// the test assembly location looking for flow-sharp.sln.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException(
                "Could not locate flow-sharp.sln walking up from " +
                AppContext.BaseDirectory);
        return dir.FullName;
    }

    /// <summary>
    /// Bash is required for the audit shell scripts. On a hypothetical Windows
    /// CI run we skip the test bodies (early return from the caller).
    /// </summary>
    private static bool IsBashAvailable()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    /// <summary>
    /// Shells out to <paramref name="scriptRelPath"/> (relative to repo root)
    /// via /usr/bin/env bash. Asserts ExitCode == 0. Returns stdout for
    /// optional diagnostic use.
    /// </summary>
    private static string RunBashScript(string repoRoot, string scriptRelPath)
    {
        string scriptAbsPath = Path.Combine(repoRoot, scriptRelPath);
        Assert.True(File.Exists(scriptAbsPath),
            $"script not found: {scriptAbsPath}");

        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = scriptAbsPath,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        Assert.NotNull(proc);
        string stdout = proc!.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        Assert.True(proc.ExitCode == 0,
            $"{scriptRelPath} exited {proc.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        return stdout;
    }
}
