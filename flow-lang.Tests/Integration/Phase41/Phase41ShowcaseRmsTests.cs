using System;
using System.IO;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase41;

/// <summary>
/// Phase 41 SHOWCASE-01 (D-13) — the third-genre EDM showcase piece's offline
/// WAV render must hold the SPEC-8 RMS-windowed regression (±0.5 dB / 100 ms)
/// against the committed baseline at
/// <c>flow-lang.Tests/baselines/Phase41/showcase.wav</c>.
///
/// <para>This test renders the ACTUAL committed showcase source
/// (<c>examples/edm/pulse.flow</c>) through a hermetic <see cref="FlowEngineRunner"/>
/// and reads back the WAV the script's own <c>writeWav</c> writes (the script's
/// render buffer is scoped inside its <c>tempo/timesig/key</c> context blocks, so
/// the written file — not a global binding — is the comparison surface; mirrors
/// the Phase 28 <c>HeldNoteRmsTests</c> read-back pattern). The pinned render path
/// is the SEEDED, deterministic section of the showcase (every stochastic call
/// carries an explicit seed → PrngRegistry reseeds at the writeWav boundary); the
/// file's <c>live</c> block + real-time <c>midiOut</c> demo lives in a commented
/// section that never executes during this headless render (Pitfall 5,
/// D-v1.5-07).</para>
///
/// <para><b>Baseline regeneration</b>: on first run with no baseline file, the
/// rendered WAV is written to the baseline path and the test passes (so the
/// committer sees a clean run); the committed baseline is asserted against on
/// every subsequent run. To regenerate after a deliberate change, delete the
/// .wav and re-run — the showcase render is two-run cmp-clean so the regenerated
/// baseline is byte-stable.</para>
/// </summary>
[Trait("Category", "Phase41")]
[Collection("FlowScripts")]
public class Phase41ShowcaseRmsTests : IDisposable
{
    public Phase41ShowcaseRmsTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
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
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "baselines")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Showcase_RmsWithinTolerance()
    {
        var repoRoot = FindRepoRoot();
        var showcasePath = Path.Combine(repoRoot, "examples", "edm", "pulse.flow");
        var baselinePath = Path.Combine(repoRoot, "flow-lang.Tests",
            "baselines", "Phase41", "showcase.wav");

        Assert.True(File.Exists(showcasePath),
            $"Showcase source missing: {showcasePath}");

        // Redirect the script's writeWav target to a unique temp file so the
        // test is hermetic + parallel-safe (the source ships writing to a fixed
        // /tmp/pulse.wav; we rewrite that single literal). The render buffer is
        // scoped inside the script's tempo/timesig/key blocks, so the written
        // WAV — not a global binding — is the comparison surface.
        string renderedWav = Path.Combine(Path.GetTempPath(),
            $"flow_phase41_showcase_{Guid.NewGuid():N}.wav");
        string source = File.ReadAllText(showcasePath)
            .Replace("\"/tmp/pulse.wav\"", "\"" + renderedWav.Replace("\\", "/") + "\"");

        // Run the showcase from repo root so its `use "@..."` stdlib imports
        // resolve identically to a CLI `flow run`.
        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, errorCount) = runner.RunSource(source, showcasePath);
            Assert.True(ok && errorCount == 0,
                $"Showcase render failed (errorCount={errorCount}):\n{stderr}");
            Assert.True(File.Exists(renderedWav),
                $"Showcase writeWav did not produce {renderedWav}");

            var rendered = WavReader.ReadWav(renderedWav);
            Assert.True(rendered.Frames > 0, "SHOWCASE-01 render produced zero frames");
            Assert.Equal(2, rendered.Channels);

            if (!File.Exists(baselinePath))
            {
                // First-run: seed the baseline from the rendered WAV and pass.
                // The committer then commits baselines/Phase41/showcase.wav;
                // subsequent runs pin to it. The showcase render is two-run
                // cmp-clean so the baseline is byte-stable.
                Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
                File.Copy(renderedWav, baselinePath, overwrite: true);
                Assert.True(File.Exists(baselinePath),
                    $"Baseline write failed at {baselinePath}");
                return;
            }

            // SPEC-8 locked ±0.5 dB / 100 ms window. Both the rendered WAV and
            // the baseline are already-dithered files on disk → single-read
            // compare (no double-dither), so use the file-path overload.
            RmsRegressionTests.AssertWavMatchesBaseline(renderedWav, baselinePath);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
            if (File.Exists(renderedWav)) File.Delete(renderedWav);
        }
    }
}
