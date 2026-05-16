using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase33;

/// <summary>
/// Phase 33 Plan 33-08 — two-run byte-identical determinism contract preserved
/// end-to-end through the SFZ surface. Mirrors the
/// <see cref="Phase29.Phase29ByteIdenticalTests"/> structure: render the smoke
/// fixture to a temp WAV file twice and assert <c>bytes1.SequenceEqual(bytes2)</c>.
/// This is the Phase 18/25/27 contract Phase 33 must inherit unchanged.
///
/// <list type="number">
///   <item><description><c>TwoRun_CmpClean_SmokeFixture</c> — fresh
///   <see cref="FlowEngineRunner"/> for each render (cache-cold). Proves the
///   sample-load order (the one place where iteration-order non-determinism
///   could leak in via HashSet enumeration) is sorted deterministically per
///   Plan 33-06's locked <c>OrderBy(SamplePath, Ordinal).ThenBy(PitchKeycenter)</c>
///   pattern.</description></item>
///
///   <item><description><c>TwoRun_SameEngine_CmpClean</c> — single
///   <see cref="FlowEngineRunner"/> across both renders (cache-warm). Proves
///   the per-engine SfzSampleCache idempotency check shipped in Plan 33-06
///   is in fact idempotent — re-rendering the same song under the same
///   patch must hit the cache hashtable AND produce identical bytes.</description></item>
/// </list>
///
/// <para>The dither RNG is reseeded at every <c>writeWav</c> call (Phase 15
/// Plan 05 — see <c>FileIO.cs:24-25</c> <c>DitherSeed = 0xD17E2</c>); SFZ
/// rendering inherits that property because the WAV-write path is identical
/// for sampler:NAME renders and Phase 29 bundled-sample renders. Anything
/// else that leaks per-run randomness into the audio (e.g. an unseeded
/// noise oscillator wired through the SFZ envelope) would cause this test
/// to fail.</para>
///
/// <para>[Collection("FlowScripts")] for the same singleton-isolation
/// reasons as the rest of the Plan 33-04..08 suite. Each fact uses a
/// per-test temp output directory so two facts running back-to-back don't
/// clobber each other's WAV files.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzDeterminismTests : IDisposable
{
    private readonly string _tmpOutDir;
    private readonly string _smokeSfzPath;
    private readonly string _flowEscapedPath;

    public SfzDeterminismTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        _tmpOutDir = Path.Combine(Path.GetTempPath(),
            $"sfz-det-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpOutDir);
        _smokeSfzPath = LocateSmokeSfz();
        _flowEscapedPath = _smokeSfzPath.Replace("\\", "\\\\");
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        try { Directory.Delete(_tmpOutDir, recursive: true); } catch { /* best-effort */ }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static string LocateSmokeSfz()
    {
        var path = Path.Combine(FindRepoRoot(),
            "flow-lang.Tests", "fixtures", "sfz-smoke", "smoke.sfz");
        if (!File.Exists(path))
            throw new InvalidOperationException(
                "Phase 33 smoke fixture missing: " + path);
        return path;
    }

    /// <summary>
    /// Builds the smoke-render Flow script with the WAV output redirected
    /// to <paramref name="outPath"/>. The script is structurally identical
    /// across the two runs — only the writeWav target differs.
    ///
    /// <para>Tempo + timesig + section structure mirror Plan 33-08 task 1
    /// (SfzSmokeTests). The 4-quarter-note melody hits the C4_sine.wav
    /// region (MIDI 48..71) directly with no varispeed fallback, so the
    /// determinism contract is exercised on the simplest possible code
    /// path through the SFZ render pipeline.</para>
    /// </summary>
    private string BuildSmokeRenderScript(string outPath)
    {
        // Forward slashes on Linux; backslashes-doubled on Windows.
        string flowOutPath = outPath.Replace("\\", "\\\\");
        return $@"use ""@audio""
use ""@sfz""
Sfz smoke = (loadSfz ""{_flowEscapedPath}"")
section demo {{
    Sequence main = | C4q D4q E4q F4q |
}}
Song s = [demo]
Buffer mix = (renderSong s ""sampler:smoke"")
(writeWav ""{flowOutPath}"" mix)
";
    }

    /// <summary>
    /// Two-run byte-identical contract — fresh FlowEngineRunner per run
    /// (cache-cold path). Each instantiation creates a new SfzSampleCache
    /// and walks the eager-load region collection from scratch. If the
    /// HashSet→OrderBy iteration order shipped in Plan 33-06 isn't truly
    /// stable across processes, this fact fires.
    /// </summary>
    [Fact]
    public void TwoRun_CmpClean_SmokeFixture()
    {
        string path1 = Path.Combine(_tmpOutDir, "sfz_run1.wav");
        string path2 = Path.Combine(_tmpOutDir, "sfz_run2.wav");

        // Run 1 — fresh engine.
        using (var runner1 = new FlowEngineRunner())
        {
            var (ok, _, stderr, _) = runner1.RunSource(
                BuildSmokeRenderScript(path1),
                "<sfz-determinism-run1>");
            Assert.True(ok, $"run 1 failed: {stderr}");
        }

        // Run 2 — fresh engine. The whole cache + interpreter state is reset.
        using (var runner2 = new FlowEngineRunner())
        {
            var (ok, _, stderr, _) = runner2.RunSource(
                BuildSmokeRenderScript(path2),
                "<sfz-determinism-run2>");
            Assert.True(ok, $"run 2 failed: {stderr}");
        }

        Assert.True(File.Exists(path1), $"run 1 output missing: {path1}");
        Assert.True(File.Exists(path2), $"run 2 output missing: {path2}");

        byte[] bytes1 = File.ReadAllBytes(path1);
        byte[] bytes2 = File.ReadAllBytes(path2);
        Assert.True(bytes1.Length > 0, $"empty WAV: {path1}");
        Assert.True(bytes1.SequenceEqual(bytes2),
            $"Phase 33 SFZ render is not deterministic — two runs produced " +
            $"different bytes (run1: {bytes1.Length} bytes, " +
            $"run2: {bytes2.Length} bytes). " +
            "Sample-load order or render path leaked per-run state. " +
            "Check SfzSampleCache.EagerLoad's OrderBy(SamplePath, Ordinal) " +
            "and any unseeded RNG in SfzRenderer.");
    }

    /// <summary>
    /// Cache-warm two-run contract — single FlowEngineRunner across both
    /// renders. The second renderSong call hits the warm SfzSampleCache via
    /// the idempotency check at <c>SfzSampleCache.EagerLoad</c>'s
    /// <c>_eagerLoadedKeys</c> guard. Both renders must produce identical
    /// WAV bytes — the cache returns the same buffer references, the
    /// renderer applies the same envelope shape, and the writeWav path
    /// reseeds its dither RNG to the same fixed seed.
    ///
    /// <para>Implementation note: the script renders TWO buffers
    /// (<c>mix1</c> and <c>mix2</c>) inside a single Flow program so the
    /// SfzPatchRegistry binding (D-12) is reused without re-declaring the
    /// <c>Sfz</c> variable. The two renderSong calls use the same Song +
    /// patch — the second call exercises the cache-warm path.</para>
    /// </summary>
    [Fact]
    public void TwoRun_SameEngine_CmpClean()
    {
        string path1 = Path.Combine(_tmpOutDir, "sfz_warm_run1.wav");
        string path2 = Path.Combine(_tmpOutDir, "sfz_warm_run2.wav");
        string flowOut1 = path1.Replace("\\", "\\\\");
        string flowOut2 = path2.Replace("\\", "\\\\");

        // Single Flow program — two renderSong calls in sequence on the
        // SAME bound Sfz patch. The second call hits the warm cache.
        string script = $@"use ""@audio""
use ""@sfz""
Sfz smoke = (loadSfz ""{_flowEscapedPath}"")
section demo {{
    Sequence main = | C4q D4q E4q F4q |
}}
Song s = [demo]
Buffer mix1 = (renderSong s ""sampler:smoke"")
(writeWav ""{flowOut1}"" mix1)
Buffer mix2 = (renderSong s ""sampler:smoke"")
(writeWav ""{flowOut2}"" mix2)
";

        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(script,
            "<sfz-determinism-warm-twin>");
        Assert.True(ok, $"warm twin-render failed: {stderr}");

        Assert.True(File.Exists(path1), $"warm run 1 output missing: {path1}");
        Assert.True(File.Exists(path2), $"warm run 2 output missing: {path2}");

        byte[] bytes1 = File.ReadAllBytes(path1);
        byte[] bytes2 = File.ReadAllBytes(path2);
        Assert.True(bytes1.Length > 0, $"empty WAV: {path1}");
        Assert.True(bytes1.SequenceEqual(bytes2),
            $"Phase 33 SFZ warm-cache render is not deterministic — two " +
            $"renderSong calls within the same engine produced different bytes " +
            $"(mix1: {bytes1.Length} bytes, mix2: {bytes2.Length} bytes). " +
            "SfzSampleCache idempotency check may be regressed, OR a per-render " +
            "unseeded RNG is leaking state into the second render.");
    }
}
