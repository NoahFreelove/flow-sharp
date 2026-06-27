using System;
using System.IO;
using FlowLang.Core;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// Phase 29 REQ-1 — smoke tests verifying SampledInstrumentRenderer renders each
/// tonal instrument without exception (using bundled samples). Does NOT verify
/// audible quality — Plan 03's VelocityLayerTests + ArticulationOnSampleTests
/// cover quality assertions. This is the "doesn't throw" floor.
///
/// Each test does two checks:
///   1. Direct API: construct SampleCache + SampledInstrumentRenderer, manually
///      eager-load the instrument's manifest, and Render a single C-octave note.
///      Catches integration bugs (e.g. file path mismatch, varispeed math NaN).
///   2. End-to-end via FlowEngine: a tiny renderSong script through the
///      interpreter, asserting Success and a non-empty Buffer. Catches wiring
///      bugs (FlowEngine→CurrentSampleCache→SongRenderer→EagerLoad path).
///
/// Serialized via <c>[Collection("FlowScripts")]</c> for the same reason as
/// SampleCacheTests — each test mutates <see cref="Environment.CurrentDirectory"/>
/// and parallel cwd-mutating suites corrupt path resolution.
/// </summary>
[Collection("FlowScripts")]
public class SampledInstrumentSmokeTests
{
    [Theory]
    [InlineData("piano", true)]
    [InlineData("brass", false)]
    [InlineData("sax", false)]
    [InlineData("strings", false)]
    [InlineData("flute", false)]
    [InlineData("bell", false)]
    public void RenderingTonalInstrument_DoesNotThrow(string instrument, bool hasVelocityLayers)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string sampleDir = Path.Combine(repoRoot, "flow-lang", "Samples", instrument);
        if (!Directory.Exists(sampleDir) || Directory.GetFiles(sampleDir, "*.wav").Length == 0)
            return;  // Skip if Plan 01 samples not yet committed

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            // SampleCache resolves filenames relative to cwd by default
            // (samplesRoot = "flow-lang/Samples"). Set cwd before instantiation.
            Environment.CurrentDirectory = repoRoot;

            // --- Direct-API check ------------------------------------------------
            // Build a minimal SongData via the interpreter so EagerLoad has
            // something to walk, then render one note via the renderer directly.
            using (var runner = new FlowEngineRunner())
            {
                string setupScript = $@"
                    use ""@audio""
                    tempo 120 {{
                        section demo_{instrument} {{
                            Sequence main = | C4q |
                        }}
                    }}
                    Song s = [demo_{instrument}]
                    Buffer rendered = (renderSong s ""{instrument}"")
                ";
                var setup = runner.RunSource(setupScript, $"<setup-{instrument}>");
                Assert.True(setup.Success,
                    $"Setup render for {instrument} failed before direct-API smoke probe: {setup.Stderr}");

                var cache = FlowEngine.CurrentSampleCache;
                Assert.NotNull(cache);
                Assert.True(cache!.HasInstrument(instrument),
                    $"SampleCache.HasInstrument(\"{instrument}\") must be true after eager-load.");

                var renderer = new SampledInstrumentRenderer(cache, instrument, hasVelocityLayers);

                // Render a middle-C quarter note at moderate velocity.
                var note = new MusicalNoteData(
                    noteName: 'C', octave: 4, alteration: 0,
                    durationValue: 4, isRest: false, velocity: 0.7);
                var buffer = renderer.Render(note,
                    sampleRate: 44100, durationBeats: 1.0, bpm: 120.0,
                    tuning: FlowLang.StandardLibrary.Audio.Tuning.RenderTuning.Default);
                Assert.NotNull(buffer);
                Assert.True(buffer.Frames > 0,
                    $"{instrument} Render produced zero-frame buffer for C4 quarter note.");
            }

            // --- End-to-end check (fresh FlowEngine) -----------------------------
            using (var runner2 = new FlowEngineRunner())
            {
                string e2eScript = $@"
                    use ""@audio""
                    tempo 120 {{
                        section demo_e2e_{instrument} {{
                            Sequence main = | C4q D4q |
                        }}
                    }}
                    Song s = [demo_e2e_{instrument}]
                    Buffer rendered = (renderSong s ""{instrument}"")
                ";
                var e2e = runner2.RunSource(e2eScript, $"<smoke-{instrument}>");
                Assert.True(e2e.Success,
                    $"End-to-end renderSong for {instrument} failed: {e2e.Stderr}");
            }
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
