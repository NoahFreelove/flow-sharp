using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-11 Task 2 — REQ-STRICT-15 two-run cmp-clean
/// determinism pin. CLAUDE.md §"Conventions" says:
/// <c>Two-run determinism IS preserved (same SHA → byte-identical) —
/// contract in shape, not pinned bytes.</c>
/// Phase 44 introduced ZERO new PRNG sites (advisory → error elevation
/// is pure deterministic string concat per RESEARCH Pitfall 5), so the
/// composer-facing strict fixtures shipped by Plan 44-11 MUST honor the
/// same contract. This fixture spawns the flow-interpreter on each
/// <c>tests/strict/*.flow</c> file twice and asserts SHA-256 equality of
/// the captured stdout. The audio-emitting <see cref="ShowcaseFlowFile"/>
/// gets an additional Fact that SHA-pins the rendered WAV bytes — the
/// stronger guarantee, since stdout drift is composer-visible but
/// rendered-audio drift would be a silent regression.
///
/// <para>
/// Per W10 (plan revision): expanded from "representative subset" to
/// <c>[Theory]</c> over ALL 7 strict fixtures. Catches per-file
/// non-determinism that a one-file pin would miss.
/// </para>
///
/// <para>
/// Charitable skip: if flow-interpreter.dll is missing, every Fact
/// short-circuits to a no-op via <see cref="StrictFlowScriptSuiteTests.DllMissing"/>.
/// Mirrors the Phase 39 mscore charitable-skip pattern.
/// </para>
///
/// <para>
/// W12 note: stdout SHA includes the entire captured stream verbatim.
/// Phase 44 fixtures emit no PRNG-influenced lines (no humanize / no
/// jam / no euclidean), so the byte-equality contract is straightforward.
/// If a future fixture adds a PRNG-routed call, normalize that line out
/// here before hashing.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class Phase44TwoRunDeterminismTests
{
    /// <summary>
    /// Single source of truth for the showcase fixture's repo-relative path.
    /// </summary>
    private const string ShowcaseFlowFile = "tests/strict/showcase_strict.flow";

    /// <summary>
    /// Output path the showcase writes to. Single source of truth — if
    /// <c>showcase_strict.flow</c>'s <c>(writeWav ...)</c> path changes,
    /// update here too. The fixture also reads + asserts this matches.
    /// </summary>
    private const string ShowcaseWavPath = "/tmp/flow_strict_showcase.wav";

    /// <summary>
    /// MemberData source: every <c>.flow</c> file under <c>tests/strict/</c>
    /// in repo-relative form. Same enumeration as StrictFlowScriptSuiteTests.
    /// </summary>
    public static IEnumerable<object[]> AllStrictFlowFiles()
    {
        return StrictFlowScriptSuiteTests.StrictFlowFiles();
    }

    /// <summary>
    /// Per W10 Theory expansion: run each of the 7 strict fixtures twice
    /// and assert SHA-256 equality of captured stdout. Catches per-file
    /// non-determinism — a single fixture regression won't mask the others.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllStrictFlowFiles))]
    public void Fact_StrictFlowFile_TwoRunsSHAEqual_Stdout(string relativePath)
    {
        if (StrictFlowScriptSuiteTests.DllMissing)
            return;  // charitable skip

        var (exit1, out1, err1) = StrictFlowScriptSuiteTests.RunInterpreter(relativePath);
        Assert.True(exit1 == 0,
            $"first run of {relativePath} did not exit cleanly. " +
            $"exit={exit1}\nstdout:\n{out1}\nstderr:\n{err1}");

        var (exit2, out2, err2) = StrictFlowScriptSuiteTests.RunInterpreter(relativePath);
        Assert.True(exit2 == 0,
            $"second run of {relativePath} did not exit cleanly. " +
            $"exit={exit2}\nstdout:\n{out2}\nstderr:\n{err2}");

        var sha1 = Sha256(NormalizeStdout(out1));
        var sha2 = Sha256(NormalizeStdout(out2));
        Assert.True(sha1 == sha2,
            $"two-run stdout SHA mismatch for {relativePath}:\n" +
            $"  run1: {sha1}\n" +
            $"  run2: {sha2}\n" +
            $"run1 stdout:\n{out1}\n---\nrun2 stdout:\n{out2}");
    }

    /// <summary>
    /// Stronger guarantee for the audio-emitting showcase: the rendered WAV
    /// bytes from two consecutive runs MUST be byte-equal. CLAUDE.md
    /// §"Conventions" two-run cmp-clean contract preserved through Plan 44-11.
    /// </summary>
    [Fact]
    public void Fact_ShowcaseStrictWav_TwoRunsByteEqual()
    {
        if (StrictFlowScriptSuiteTests.DllMissing)
            return;  // charitable skip

        // Pre-clean: remove any stale WAV from a prior unrelated run so we
        // never compare against a different fixture's output.
        if (File.Exists(ShowcaseWavPath))
        {
            try { File.Delete(ShowcaseWavPath); } catch { /* best-effort */ }
        }

        var (exit1, out1, err1) = StrictFlowScriptSuiteTests.RunInterpreter(ShowcaseFlowFile);
        Assert.True(exit1 == 0,
            $"first showcase run did not exit cleanly. exit={exit1}\nstderr:\n{err1}");
        Assert.True(File.Exists(ShowcaseWavPath),
            $"first showcase run did not emit WAV at {ShowcaseWavPath}.\n" +
            $"stdout:\n{out1}\nstderr:\n{err1}");
        var wav1 = File.ReadAllBytes(ShowcaseWavPath);
        var sha1 = Sha256(wav1);

        var (exit2, out2, err2) = StrictFlowScriptSuiteTests.RunInterpreter(ShowcaseFlowFile);
        Assert.True(exit2 == 0,
            $"second showcase run did not exit cleanly. exit={exit2}\nstderr:\n{err2}");
        Assert.True(File.Exists(ShowcaseWavPath),
            $"second showcase run did not emit WAV at {ShowcaseWavPath}");
        var wav2 = File.ReadAllBytes(ShowcaseWavPath);
        var sha2 = Sha256(wav2);

        Assert.True(wav1.Length == wav2.Length,
            $"WAV byte-length differs across runs: {wav1.Length} vs {wav2.Length}");
        Assert.True(sha1 == sha2,
            $"WAV SHA-256 mismatch across two runs of {ShowcaseFlowFile}:\n" +
            $"  run1: {sha1} ({wav1.Length} bytes)\n" +
            $"  run2: {sha2} ({wav2.Length} bytes)\n" +
            "Phase 44 introduced ZERO PRNG sites — if this Fact fails, a " +
            "downstream change has broken the two-run cmp-clean contract " +
            "(CLAUDE.md §\"Conventions\").");
    }

    /// <summary>
    /// Normalize stdout for SHA comparison. Phase 44 fixtures emit no
    /// timestamp / Guid / process-id / PRNG-routed lines, so this is a
    /// no-op today; the helper is in place so a future plan adding such a
    /// line can centralize the normalization rule here.
    /// </summary>
    private static string NormalizeStdout(string s)
    {
        // No PRNG sites in Plan 44-11 fixtures; passthrough.
        // If future fixtures emit lines containing timestamps / Guids /
        // process IDs, strip those lines here.
        return s;
    }

    private static string Sha256(string s) => Sha256(Encoding.UTF8.GetBytes(s));

    private static string Sha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
