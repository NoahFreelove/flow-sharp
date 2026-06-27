using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase33;

/// <summary>
/// Phase 33 Plan 33-08 — SPEC-7 acceptance gate. End-to-end smoke test that
/// renders the Plan 33-01 synthetic fixture (<c>flow-lang.Tests/fixtures/sfz-smoke/smoke.sfz</c>)
/// through the full <c>use "@sfz" → loadSfz(String) → Sfz binding →
/// renderSong "sampler:NAME"</c> pipeline and verifies the three SPEC-7
/// acceptance bullets:
///
/// <list type="number">
///   <item><description><c>SmokeFixture_ExitCode_Zero</c> — the FlowEngineRunner
///   reports Success == true with zero ErrorReporter entries (no parse,
///   resolution, or render errors).</description></item>
///
///   <item><description><c>SmokeFixture_Renders_NonEmpty_Above40dBFS</c> — the
///   rendered WAV is non-empty AND its RMS exceeds the SPEC-7 locked
///   threshold of -40 dBFS (linear ~0.01). Confirms the SFZ render produced
///   audible content — not silence — through the new <c>sampler:NAME</c>
///   dispatch.</description></item>
///
///   <item><description><c>SmokeFixture_Renders_DiscontinuityCheck</c> — on the
///   sustained body of a held <c>C4w</c> (whole note), no consecutive-sample
///   amplitude jump exceeds the SPEC-5 / SPEC-7 locked 0.05 ceiling. This is
///   the worst-case loop-boundary failure mode the Phase 33 RESEARCH §"Round
///   4" interview pinned as the gate that would invalidate sustained
///   orchestral renders if it failed.</description></item>
/// </list>
///
/// <para>Class name is <c>Phase33SfzSmokeTests</c> per Plan 33-08's
/// <c>must_haves.artifacts.contains_pattern</c> so
/// <c>dotnet test --filter "FullyQualifiedName~Phase33SfzSmoke"</c> matches
/// (33-VALIDATION.md test-map row).</para>
///
/// <para>Drives the loader through the absolute-path <c>loadSfz(String)</c>
/// overload so CI does not depend on the composer-side VSCO-CE install or
/// the <c>sfz_root</c> config. The Symbol-overload + config surface is
/// already covered by SfzSymbolLookupTests + SfzConfigTests at Plan 33-05.</para>
///
/// <para>[Collection("FlowScripts")] serializes alongside the rest of the
/// Plan 33-04..07 suite so the shared <see cref="RenderingDiagnostics"/>
/// sentinel set + <see cref="FlowConfig.Active"/> singleton don't leak
/// across parallel test workers.</para>
/// </summary>
[Collection("FlowScripts")]
public class Phase33SfzSmokeTests : IDisposable
{
    private readonly string _smokeSfzPath;
    private readonly string _flowEscapedPath;

    public Phase33SfzSmokeTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        _smokeSfzPath = LocateSmokeSfz();
        // Escape backslashes for Windows paths in Flow string literals.
        // No-op on Linux; preserved for cross-platform robustness.
        _flowEscapedPath = _smokeSfzPath.Replace("\\", "\\\\");
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
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
                $"Phase 33 smoke fixture missing: {path}. " +
                "Regenerate via Phase33FixtureGenerator_Smoke_GeneratesFixtures.");
        return path;
    }

    private static double Rms(AudioBuffer buf)
    {
        if (buf is null || buf.Data is null || buf.Data.Length == 0) return 0.0;
        double sumSq = 0.0;
        for (int i = 0; i < buf.Data.Length; i++)
            sumSq += (double)buf.Data[i] * buf.Data[i];
        return Math.Sqrt(sumSq / buf.Data.Length);
    }

    /// <summary>
    /// SPEC-7 acceptance #1 — the FlowEngineRunner exits cleanly. Success
    /// must be true AND the ErrorReporter must report zero errors. Catches
    /// silent regressions where the script parses but the renderer throws
    /// an InvalidOperationException that gets swallowed into the
    /// ErrorReporter without flipping Success.
    /// </summary>
    [Fact]
    public void SmokeFixture_ExitCode_Zero()
    {
        using var runner = new FlowEngineRunner();
        string script = $@"use ""@audio""
use ""@sfz""
Sfz smoke = (loadSfz ""{_flowEscapedPath}"")
section demo {{
    Sequence main = | C4q D4q E4q F4q |
}}
Song s = [demo]
Buffer mix = (renderSong s ""sampler:smoke"")
";
        var (ok, _, stderr, errorCount) = runner.RunSource(script, "<phase33-smoke-exitcode>");
        Assert.True(ok,
            $"expected sampler:smoke render to exit cleanly; stderr: {stderr}");
        Assert.Equal(0, errorCount);
    }

    /// <summary>
    /// SPEC-7 acceptance #2 — the rendered WAV is non-empty AND the RMS
    /// exceeds -40 dBFS (linear ~0.01). The smoke fixture's first region
    /// (C4_sine.wav, MIDI 48..71) covers C4..D4..E4..F4 directly so a
    /// 4-quarter-note melody hits that region without varispeed fallback.
    /// The C4_sine.wav body is amplitude 0.5 (per Phase33FixtureGenerator),
    /// so the rendered RMS lands well above -40 dBFS even after the Phase
    /// 28 articulation envelope shaping.
    /// </summary>
    [Fact]
    public void SmokeFixture_Renders_NonEmpty_Above40dBFS()
    {
        using var runner = new FlowEngineRunner();
        string script = $@"use ""@audio""
use ""@sfz""
Sfz smoke = (loadSfz ""{_flowEscapedPath}"")
section demo {{
    Sequence main = | C4q D4q E4q F4q |
}}
Song s = [demo]
Buffer mix = (renderSong s ""sampler:smoke"")
";
        var (ok, _, stderr, _) = runner.RunSource(script, "<phase33-smoke-rms>");
        Assert.True(ok, $"expected clean render; stderr: {stderr}");

        var buf = runner.GetVariable("mix").As<AudioBuffer>();
        Assert.NotNull(buf);
        Assert.True(buf.Frames > 0,
            "sampler:smoke render produced zero-frame buffer for C4q D4q E4q F4q");

        double rms = Rms(buf);
        // -40 dBFS = 10^(-40/20) = 0.01 linear. SPEC-7 locked threshold.
        const double minRmsLinear = 0.01;
        Assert.True(rms > minRmsLinear,
            $"sampler:smoke render RMS={rms:E4} below -40 dBFS threshold " +
            $"({minRmsLinear:E4}); SFZ region match likely failed or sample " +
            "loaded at wrong amplitude");
    }

    /// <summary>
    /// SPEC-5 + SPEC-7 acceptance — render a sustained note (<c>C4w</c>,
    /// 4 seconds at the test's tempo of 60 BPM with 4/4 time = 4 beats =
    /// 4 seconds of audio) and verify the loop-boundary equal-power
    /// crossfade produces no audible click. The acceptance threshold from
    /// SPEC-5 is "max consecutive-sample amplitude jump ≤ 0.05" measured
    /// across the sustained body (NOT the attack transient).
    ///
    /// <para>The smoke fixture's region 1 declares <c>loop_mode=loop_continuous</c>
    /// with <c>loop_start=2205</c> and <c>loop_end=4410</c> on a 4410-frame
    /// (100 ms) source body — so a 4-second sustained note loops the back
    /// half of the C4_sine.wav body roughly 38 times, exercising the
    /// 441-frame equal-power sin/cos crossfade at every loop seam.</para>
    ///
    /// <para>The check skips the first half-second of the rendered buffer
    /// to bypass the attack transient (Phase 28 envelope ramp), then
    /// iterates consecutive samples looking for the worst-case jump.
    /// At 60 BPM with 4/4 timesig, a whole-note rendered at 44.1 kHz / 2
    /// channels lands at 4 × 44100 = 176400 frames; we examine frames
    /// [22050 .. end] for the discontinuity ceiling.</para>
    /// </summary>
    [Fact]
    public void SmokeFixture_Renders_DiscontinuityCheck()
    {
        using var runner = new FlowEngineRunner();
        // tempo 60 / timesig 4/4: 1 beat = 1 second; whole note = 4 beats = 4 seconds.
        string script = $@"use ""@audio""
use ""@sfz""
Sfz smoke = (loadSfz ""{_flowEscapedPath}"")
tempo 60 {{
    timesig 4/4 {{
        section sustained {{
            Sequence main = | C4w |
        }}
    }}
}}
Song s = [sustained]
Buffer mix = (renderSong s ""sampler:smoke"")
";
        var (ok, _, stderr, _) = runner.RunSource(script, "<phase33-smoke-disc>");
        Assert.True(ok, $"expected clean sustained render; stderr: {stderr}");

        var buf = runner.GetVariable("mix").As<AudioBuffer>();
        Assert.NotNull(buf);
        // ~4 seconds at 44.1 kHz; allow some headroom for sample-boundary rounding.
        Assert.True(buf.Frames >= 44100 * 3,
            $"sustained C4w buffer too short ({buf.Frames} frames) — " +
            "expected ~4 seconds (~176400 frames at 60 BPM, 4/4)");

        // Discontinuity check on consecutive samples. Skip the first 0.5s to
        // step past the attack transient. Operate per-channel for stereo
        // (interleaved LRLRLR layout) so we don't confuse cross-channel
        // amplitude differences with intra-channel discontinuities.
        const float discontinuityCeiling = 0.05f;
        int sampleRate = buf.SampleRate;
        int channels = buf.Channels;
        int startFrame = sampleRate / 2;
        // Examine the first 2 seconds beyond the attack — covers ~85 loop
        // seams at 100ms loop body per iteration; well past Phase 28 attack.
        int endFrame = Math.Min(buf.Frames - 1, sampleRate / 2 + sampleRate * 2);

        float worstJump = 0f;
        int worstFrame = -1;
        int worstChannel = -1;
        for (int ch = 0; ch < channels; ch++)
        {
            for (int f = startFrame; f < endFrame; f++)
            {
                int i = f * channels + ch;
                int j = (f + 1) * channels + ch;
                float jump = MathF.Abs(buf.Data[j] - buf.Data[i]);
                if (jump > worstJump)
                {
                    worstJump = jump;
                    worstFrame = f;
                    worstChannel = ch;
                }
            }
        }

        Assert.True(worstJump <= discontinuityCeiling,
            $"loop-boundary discontinuity ceiling exceeded: " +
            $"max jump {worstJump:F4} > {discontinuityCeiling:F4} " +
            $"at frame {worstFrame}, channel {worstChannel}. " +
            "Equal-power crossfade may be regressing — check SfzRenderer.AssembleBody.");
    }
}
