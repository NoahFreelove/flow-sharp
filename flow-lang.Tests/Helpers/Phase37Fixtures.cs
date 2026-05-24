using System;
using System.IO;
using FlowLang.StandardLibrary.Audio;

namespace FlowLang.Tests.Helpers;

/// <summary>
/// Phase 37 Plan 37-02 Task 1 — synthetic WAV fixture generator for
/// <c>flow-lang.Tests/fixtures/Phase37/{sine_440,kick_hit,mixed}.wav</c>.
/// Called from each Phase 37 test class ctor (after
/// <c>RenderingDiagnostics.ResetForTesting()</c>) to materialize the three
/// fixtures idempotently — present-and-non-empty WAVs are left alone.
///
/// <para>
/// Fixture summary (matches <c>flow-lang.Tests/fixtures/Phase37/README.md</c>):
/// <list type="bullet">
///   <item><description><c>sine_440.wav</c> — 5 s sustained 440 Hz sine,
///   44.1 kHz / 16-bit mono. Vocoder smoke (DSP-02 #vocoder).</description></item>
///   <item><description><c>kick_hit.wav</c> — 200 ms kick-drum transient
///   (60 Hz exp-decay tone + click), 44.1 kHz / 16-bit mono. PSOLA smoke
///   (DSP-02 #psola).</description></item>
///   <item><description><c>mixed.wav</c> — 5 s sine + repeated kick hits at
///   t = 0, 1, 2, 3, 4 s. HPS smoke (DSP-02 #auto).</description></item>
/// </list>
/// </para>
///
/// <para>
/// Generation uses <see cref="FileIO.LoadWav"/>-compatible WAV writes via
/// <c>FileIO.ExportWav</c>. Two-run cmp-clean determinism is preserved because
/// the contents are pure mathematical functions of the buffer size — no PRNG
/// is consulted for fixture generation.
/// </para>
/// </summary>
public static class Phase37Fixtures
{
    private const int SampleRate = 44100;

    /// <summary>
    /// Resolves the per-process fixture directory under the repo root. Uses
    /// the same walk-up-from-AppContext.BaseDirectory pattern as Phase 33's
    /// RepoSizeTests so the path resolves correctly from xUnit's test bin
    /// directory.
    /// </summary>
    public static string FixtureDir =>
        Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "Phase37");

    /// <summary>
    /// Path of a named fixture WAV. Used by Phase 37 tests via
    /// <see cref="FileIO.LoadWavInternal"/>.
    /// </summary>
    public static string FixturePath(string name) => Path.Combine(FixtureDir, name);

    /// <summary>
    /// Idempotently generate the three Phase 37 fixtures. Safe to call from
    /// every test ctor: a present-and-non-empty WAV is left alone.
    /// </summary>
    public static void EnsureFixturesExist()
    {
        Directory.CreateDirectory(FixtureDir);

        string sinePath = Path.Combine(FixtureDir, "sine_440.wav");
        string kickPath = Path.Combine(FixtureDir, "kick_hit.wav");
        string mixedPath = Path.Combine(FixtureDir, "mixed.wav");

        if (!IsPresentAndNonEmpty(sinePath))
            WriteWav16(BuildSine440(), sinePath);
        if (!IsPresentAndNonEmpty(kickPath))
            WriteWav16(BuildKick(), kickPath);
        if (!IsPresentAndNonEmpty(mixedPath))
            WriteWav16(BuildMixed(), mixedPath);
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
            "Phase37Fixtures: could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static bool IsPresentAndNonEmpty(string path)
        => File.Exists(path) && new FileInfo(path).Length > 0;

    /// <summary>
    /// 5-second 440 Hz sustained sine at amplitude 0.5 — pure stationary
    /// harmonic content for the vocoder smoke test.
    /// </summary>
    private static AudioBuffer BuildSine440()
    {
        const int durationSeconds = 5;
        int frames = SampleRate * durationSeconds;
        var buf = new AudioBuffer(frames, 1, SampleRate);
        for (int n = 0; n < frames; n++)
        {
            buf.Data[n] = 0.5f * (float)Math.Sin(2.0 * Math.PI * 440.0 * n / SampleRate);
        }
        return buf;
    }

    /// <summary>
    /// 200 ms synthetic kick — broadband transient click + 60 Hz exp-decay
    /// body. Onset is at frame 0; transient-onset assertion in
    /// <c>StretchPsolaTransientTests</c> uses the post-stretch first-
    /// significant-sample frame index.
    ///
    /// <para>
    /// Click weight is intentionally large (broadband noise burst with fast
    /// decay) so HPS's per-frame percussive-vs-harmonic ratio crosses the
    /// 0.3 threshold on the first analysis frame — the auto-mode advisory
    /// test on this fixture asserts at least one psola-classified frame.
    /// </para>
    /// </summary>
    private static AudioBuffer BuildKick()
    {
        int frames = (int)(0.200 * SampleRate); // 200 ms = 8820 frames
        var buf = new AudioBuffer(frames, 1, SampleRate);
        // Deterministic broadband noise burst for the click — fixed seed so
        // two runs produce byte-identical fixtures (two-run cmp-clean).
        var noise = new Random(20260522);
        for (int n = 0; n < frames; n++)
        {
            double t = n / (double)SampleRate;
            // Body: 60 Hz tone with exponential decay (25 = ~40 ms time constant).
            double body = 0.7 * Math.Exp(-t * 25.0) * Math.Sin(2.0 * Math.PI * 60.0 * t);
            // Click: white-noise burst with very fast exponential decay
            // (500 = ~2 ms time constant) — broadband energy ensures HPS
            // classifies the onset frame(s) as percussive.
            double clickEnv = Math.Exp(-t * 500.0);
            double click = 0.9 * clickEnv * (noise.NextDouble() * 2.0 - 1.0);
            double sample = body + click;
            if (sample > 1.0) sample = 1.0;
            if (sample < -1.0) sample = -1.0;
            buf.Data[n] = (float)sample;
        }
        return buf;
    }

    /// <summary>
    /// 5-second mixed content — the sine at 0.4× attenuation plus kick hits
    /// repeated every second at t = 0, 1, 2, 3, 4 s. Drives HPS's per-frame
    /// percussive-vs-harmonic decision — neither 0% nor 100% percussive.
    /// </summary>
    private static AudioBuffer BuildMixed()
    {
        const int durationSeconds = 5;
        int frames = SampleRate * durationSeconds;
        var buf = new AudioBuffer(frames, 1, SampleRate);

        // Sine bed at reduced amplitude (leaves headroom for kick stacking).
        for (int n = 0; n < frames; n++)
        {
            buf.Data[n] = 0.4f * (float)Math.Sin(2.0 * Math.PI * 440.0 * n / SampleRate);
        }

        // Layer 5 kick hits one second apart.
        var kick = BuildKick();
        for (int hit = 0; hit < 5; hit++)
        {
            int offsetFrame = hit * SampleRate;
            for (int k = 0; k < kick.Frames; k++)
            {
                int idx = offsetFrame + k;
                if (idx >= frames) break;
                double mixed = buf.Data[idx] + kick.Data[k];
                if (mixed > 1.0) mixed = 1.0;
                if (mixed < -1.0) mixed = -1.0;
                buf.Data[idx] = (float)mixed;
            }
        }
        return buf;
    }

    /// <summary>
    /// Writes a mono / 16-bit WAV. Uses the existing FileIO export pipeline
    /// (<see cref="FileIO.WriteWav"/>) so the resulting file is byte-identical
    /// to what FlowEngine would produce for the same buffer.
    /// </summary>
    private static void WriteWav16(AudioBuffer buf, string path)
    {
        var args = new[]
        {
            FlowLang.Runtime.Value.String(path),
            FlowLang.Runtime.Value.Buffer(buf),
        };
        FileIO.WriteWav(args);
    }
}
