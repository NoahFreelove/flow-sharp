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
/// is the deterministic section of the showcase (explicit generative seeds and
/// source-location-derived granular randomness); the
/// file's <c>live</c> block + real-time <c>midiOut</c> demo lives in a commented
/// section that never executes during this headless render (Pitfall 5,
/// D-v1.5-07).</para>
///
/// <para>The granular texture derives its seed from source location. Use a fixed
/// logical file name so checkout paths cannot change the expected audio.
/// Baseline updates require FLOW_UPDATE_AUDIO_BASELINES=1; missing files fail.</para>
/// </summary>
[Trait("Category", "Phase41")]
[Collection("FlowScripts")]
[Trait("Category", "LongRunning")]
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
        string renderedMidi = Path.ChangeExtension(renderedWav, ".mid");
        string source = File.ReadAllText(showcasePath)
            .Replace("\"/tmp/pulse.wav\"", "\"" + renderedWav.Replace("\\", "/") + "\"")
            .Replace("\"/tmp/pulse.mid\"", "\"" + renderedMidi.Replace("\\", "/") + "\"");

        // Run the showcase from repo root so its `use "@..."` stdlib imports
        // resolve identically to a CLI `flow run`.
        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, errorCount) = runner.RunSource(source, "examples/edm/pulse.flow");
            Assert.True(ok && errorCount == 0,
                $"Showcase render failed (errorCount={errorCount}):\n{stderr}");
            Assert.True(File.Exists(renderedWav),
                $"Showcase writeWav did not produce {renderedWav}");

            var rendered = WavReader.ReadWav(renderedWav);
            Assert.True(rendered.Frames > 0, "SHOWCASE-01 render produced zero frames");
            Assert.Equal(2, rendered.Channels);

            if (Environment.GetEnvironmentVariable("FLOW_UPDATE_AUDIO_BASELINES") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
                File.Copy(renderedWav, baselinePath, overwrite: true);
            }
            Assert.True(File.Exists(baselinePath),
                "Missing showcase baseline. Explicitly regenerate with FLOW_UPDATE_AUDIO_BASELINES=1.");

            // SPEC-8 locked ±0.5 dB / 100 ms window. Both the rendered WAV and
            // the baseline are already-dithered files on disk → single-read
            // compare (no double-dither), so use the file-path overload.
            RmsRegressionTests.AssertWavMatchesBaseline(renderedWav, baselinePath);

            var firstBytes = File.ReadAllBytes(renderedWav);
            using var secondRunner = new FlowEngineRunner();
            var second = secondRunner.RunSource(source, "examples/edm/pulse.flow");
            Assert.True(second.Success, second.Stderr);
            Assert.Equal(firstBytes, File.ReadAllBytes(renderedWav));
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
            if (File.Exists(renderedWav)) File.Delete(renderedWav);
            if (File.Exists(renderedMidi)) File.Delete(renderedMidi);
        }
    }
}
