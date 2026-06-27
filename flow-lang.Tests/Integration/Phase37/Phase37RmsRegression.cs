using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 GLOBAL — SPEC-8 RMS regression baselines (±0.5 dB / 100 ms)
/// committed for behavior-changing renders across the phase. Wave 0 scaffold
/// shipped by Plan 37-01; Plan 37-04 PIANO-01 close-out fills the first
/// concrete baseline (bundled-piano warmth smoke render).
///
/// Baseline routing decision (Plan 37-04 / D-37-12 / 37-HUMAN-UAT.md):
/// Plan 37-04 pins a SMALL bundled-piano fixture
/// (`fixtures/Phase37/piano_warmth_smoke.flow`) that exercises:
///   - The 4-way velocity crossfade (pp/mp/mf/ff) via varied velocities in a
///     short 4-bar phrase
///   - The `release=2.0s` named arg (renderSong third positional)
///   - SAMP-03 multiplier overlay via mixed articulations (stacc, marc, leg)
///
/// The baseline `baselines/Phase37/piano_warmth_smoke.wav` is the deterministic
/// 423 KB stereo WAV produced by this fixture at Plan 37-04 commit time.
/// Future regression that changes the bundled-piano render path more than
/// ±0.5 dB / 100 ms anywhere in the buffer is caught here.
/// </summary>
[Collection("FlowScripts")]
public class Phase37RmsRegression : IDisposable
{
    public Phase37RmsRegression()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    [Fact]
    public void PIANO01_BundledPianoWarmth_RmsMatchesBaseline()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string fixturePath = Path.Combine(repoRoot, "flow-lang.Tests", "fixtures", "Phase37", "piano_warmth_smoke.flow");
        string baselinePath = Path.Combine(repoRoot, "flow-lang.Tests", "baselines", "Phase37", "piano_warmth_smoke.wav");
        Assert.True(File.Exists(fixturePath), $"Fixture missing: {fixturePath}");
        Assert.True(File.Exists(baselinePath), $"Baseline missing: {baselinePath}");

        // The fixture writes to /tmp/piano_warmth_smoke.wav; render then compare.
        string renderedPath = "/tmp/piano_warmth_smoke_regression.wav";
        if (File.Exists(renderedPath)) File.Delete(renderedPath);

        // Patch the fixture's writeWav path so we don't collide with parallel
        // test runs. Reading source + rewriting the path is the simplest way
        // (the fixture is small; the line we patch is fixed).
        string source = File.ReadAllText(fixturePath);
        source = source.Replace("/tmp/piano_warmth_smoke.wav", renderedPath);

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;
            using var runner = new FlowEngineRunner();
            var result = runner.RunSource(source, fixturePath);
            Assert.True(result.Success, $"PIANO01 fixture render failed: {result.Stderr}");
            Assert.True(File.Exists(renderedPath),
                $"PIANO01 fixture did not write output WAV at {renderedPath}; stderr: {result.Stderr}");

            // SPEC-8 default tolerance (±0.5 dB / 100 ms windows).
            RmsRegressionTests.AssertWavMatchesBaseline(renderedPath, baselinePath);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
            if (File.Exists(renderedPath)) File.Delete(renderedPath);
        }
    }
}
