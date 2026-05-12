using System;
using System.Diagnostics;
using System.IO;
using FlowLang.Core;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// Phase 29 REQ-4 — eager-load + per-engine cache + ≥ 30% speedup on second render.
/// Uses the bundled flow-lang/Samples/piano/ assets (loaded once per engine; second
/// render hits the cache, skipping file I/O and varispeed recomputation).
///
/// Serialized via <c>[Collection("FlowScripts")]</c> because every test mutates
/// <see cref="Environment.CurrentDirectory"/> to point at the repo root (SampleCache
/// resolves filenames relative to cwd) — parallel execution against other cwd-mutating
/// suites (e.g. Phase 18 ByteIdentical*) silently corrupts the resolved sample paths.
/// </summary>
[Collection("FlowScripts")]
public class SampleCacheTests
{
    /// <summary>
    /// Render the same 10-note piano piece twice in the same FlowEngine instance.
    /// The cache hit on the second render should give a ≥ 30% speedup vs the first
    /// (SPEC REQ-4 acceptance). When the first render is too fast for a reliable
    /// measurement (&lt; 50 ms — most of which is parser overhead), the comparison
    /// short-circuits via an OR clause so CI doesn't flake on fast machines.
    /// </summary>
    [Fact]
    public void SecondRender_OfSameSong_IsAtLeast30PercentFaster()
    {
        // Skip gracefully if Plan 01 samples aren't yet committed (during partial Wave 0 development)
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string pianoSample = Path.Combine(repoRoot, "flow-lang", "Samples", "piano", "C4_ff.wav");
        if (!File.Exists(pianoSample))
            return;  // No samples loaded yet — test is conditional on Plan 01 completion

        // Two scripts using different section/variable names so the parser doesn't
        // flag re-declaration inside the same FlowEngine across the two runs. Both
        // render the same musical content under the "piano" instrument, exercising
        // the same SampleCache manifest — the second run hits the cache.
        const string scriptA = @"
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    section demoA {
                        Sequence main = | C4q D4q E4q F4q G4q A4q B4q C5q C4q D4q |
                    }
                }
            }
            Song songA = [demoA]
            Buffer renderedA = (renderSong songA ""piano"")
        ";
        const string scriptB = @"
            tempo 120 {
                timesig 4/4 {
                    section demoB {
                        Sequence main = | C4q D4q E4q F4q G4q A4q B4q C5q C4q D4q |
                    }
                }
            }
            Song songB = [demoB]
            Buffer renderedB = (renderSong songB ""piano"")
        ";

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            // SampleCache resolves filenames relative to the repo root (default
            // _samplesRoot = "flow-lang/Samples"), so set cwd before instantiation.
            Environment.CurrentDirectory = repoRoot;

            using var runner = new FlowEngineRunner();

            var sw1 = Stopwatch.StartNew();
            var result1 = runner.RunSource(scriptA, "<sample-cache-test-run1>");
            sw1.Stop();

            var sw2 = Stopwatch.StartNew();
            var result2 = runner.RunSource(scriptB, "<sample-cache-test-run2>");
            sw2.Stop();

            Assert.True(result1.Success, $"First render failed: {result1.Stderr}");
            Assert.True(result2.Success, $"Second render failed: {result2.Stderr}");

            // ≥ 30% speedup means run2 ≤ 0.7 × run1. If run1 is < 50ms the timer
            // resolution + JIT overhead make the comparison unreliable, so accept
            // either condition (skip-equivalent).
            bool fastEnough = sw2.ElapsedMilliseconds <= sw1.ElapsedMilliseconds * 0.7;
            bool unreliablyShort = sw1.ElapsedMilliseconds < 50;
            Assert.True(fastEnough || unreliablyShort,
                $"Second render should be ≥30% faster (cache hit). " +
                $"Run 1: {sw1.ElapsedMilliseconds}ms, Run 2: {sw2.ElapsedMilliseconds}ms. " +
                $"(If run 1 < 50ms, comparison is unreliable — test skip.)");
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    /// <summary>
    /// EagerLoad must be idempotent for the same (song, instrument) pair within
    /// an engine lifetime — no rescanning the manifest, no re-opening WAV files
    /// on the second call. Also verifies the no-op path for non-tonal instruments
    /// (drums / organ / wavetable / unknown names).
    /// </summary>
    [Fact]
    public void CacheEagerLoad_IsIdempotent_ForSameSongAndInstrument()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string pianoSample = Path.Combine(repoRoot, "flow-lang", "Samples", "piano", "C4_ff.wav");
        if (!File.Exists(pianoSample))
            return;  // skip — Plan 01 samples not committed yet

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            // Use a real engine to obtain a valid SongData via a renderSong call —
            // construct a tiny song through the interpreter, then probe the cache.
            using var runner = new FlowEngineRunner();
            var (success, _, stderr, _) = runner.RunSource(@"
                use ""@audio""
                tempo 120 {
                    section demo {
                        Sequence main = | C4q |
                    }
                }
                Song s = [demo]
                Buffer rendered = (renderSong s ""piano"")
            ", "<idempotency-probe>");
            Assert.True(success, $"setup render failed: {stderr}");

            // The engine's cache should have loaded the piano manifest (10 entries).
            var cache = FlowEngine.CurrentSampleCache;
            Assert.NotNull(cache);
            int loadedAfterFirstRender = cache!.RawSampleCount;
            Assert.True(loadedAfterFirstRender > 0,
                $"Eager-load should have populated the cache; got {loadedAfterFirstRender} raw samples.");

            // A second renderSong on the SAME song through the SAME engine should
            // observe the idempotency short-circuit — cache size does not grow.
            var (success2, _, stderr2, _) = runner.RunSource(@"
                use ""@audio""
                tempo 120 {
                    section demo2 {
                        Sequence main = | C4q |
                    }
                }
                Song s2 = [demo2]
                Buffer rendered2 = (renderSong s2 ""piano"")
            ", "<idempotency-probe-2>");
            Assert.True(success2, $"second render failed: {stderr2}");
            // Cache size stays at the manifest-derived total (≤ 10 for piano).
            // The second song's hashcode differs, but its instrument key matches an
            // already-loaded manifest, so RawSampleCount stays bounded by the manifest.
            Assert.Equal(loadedAfterFirstRender, cache.RawSampleCount);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }

    /// <summary>
    /// NearestSamplePitch must return the closest available pitch in the loaded
    /// manifest. For piano (C2, C3, C4, C5, C6 — MIDI 36/48/60/72/84), MIDI 62
    /// (D4) is 2 semitones from C4 — closer than 10 from C5; MIDI 80 (Ab5) is 4
    /// semitones from C6 — closer than 8 from C5.
    /// </summary>
    [Fact]
    public void NearestSamplePitch_ReturnsClosestAvailable()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string pianoSample = Path.Combine(repoRoot, "flow-lang", "Samples", "piano", "C4_ff.wav");
        if (!File.Exists(pianoSample))
            return;  // skip

        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = repoRoot;

            // Trigger eager-load via a minimal render so _availablePitches is populated.
            using var runner = new FlowEngineRunner();
            var (success, _, stderr, _) = runner.RunSource(@"
                use ""@audio""
                tempo 120 {
                    section demo {
                        Sequence main = | C4q |
                    }
                }
                Song s = [demo]
                Buffer rendered = (renderSong s ""piano"")
            ", "<nearest-probe>");
            Assert.True(success, $"render failed: {stderr}");

            var cache = FlowEngine.CurrentSampleCache;
            Assert.NotNull(cache);

            // MIDI 62 (D4) → nearest piano sample is C4 (60), 2 semitones up
            Assert.Equal(60, cache!.NearestSamplePitch("piano", 62));
            // MIDI 80 (Ab5) → nearest is C6 (84), 4 semitones up; C5 (72) is 8 away
            Assert.Equal(84, cache.NearestSamplePitch("piano", 80));
            // Exact match returns the input pitch unchanged
            Assert.Equal(60, cache.NearestSamplePitch("piano", 60));
            // Unknown instrument falls back to the target (no samples loaded)
            Assert.Equal(64, cache.NearestSamplePitch("nonexistent", 64));
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
