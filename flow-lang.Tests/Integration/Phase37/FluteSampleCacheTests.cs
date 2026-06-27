using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 FLUTE-01 (Plan 37-05 / RESEARCH §Pattern 10 / A6) — Flute
/// <see cref="SampleCache"/> has ≥3 sample points after Plan 37-05: G4 (MIDI 67),
/// A4 (MIDI 69 — NEW), G5 (MIDI 79). A4 chosen over D5 because G4→A4 = 2-semitone
/// varispeed stretch (vs G4→D5 = 7) gives better coverage of the flute's
/// expressive low register where most melodies live (Plan 37-05 A6 lock).
///
/// Phase 37 Plan 37-01 ships the Wave 0 scaffold; this plan (37-05) fills it.
/// </summary>
[Collection("FlowScripts")]
public class FluteSampleCacheTests : IDisposable
{
    public FluteSampleCacheTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// Minimal SongData for eager-load probing. Content irrelevant — eager-load
    /// walks the manifest, not the song (cache uses SongData's hash only as
    /// the idempotency key).
    /// </summary>
    private static SongData BuildEmptyFluteSong()
    {
        return new SongData(
            new List<SongSectionRef>(),
            new Dictionary<string, SectionData>());
    }

    /// <summary>
    /// Absolute path to <c>flow-lang/Samples</c>. xUnit runs from
    /// <c>bin/Debug/net10.0/</c>, so the default relative root won't resolve.
    /// Mirrors <see cref="PianoSampleCacheLayersTest"/>'s helper.
    /// </summary>
    private static string SamplesRoot()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        return Path.Combine(repoRoot, "flow-lang", "Samples");
    }

    [Fact]
    public void FluteSampleCache_HasAtLeast3SamplePoints()
    {
        var cache = new SampleCache(SamplesRoot());
        var song = BuildEmptyFluteSong();
        cache.EagerLoad(song, "flute");

        // FLUTE-01 lock: 3 disk-resident pitch points × single "mf" velocity layer
        // after Plan 37-05 (G4 + A4 + G5). The A4 sample (composer-dropped in
        // commit 681908c — Flute.vib.ff.A4 per LICENSE.md prose) closes the D5
        // timbre crossover gap.
        int[] expectedPitches = { 67, 69, 79 };

        foreach (int pitch in expectedPitches)
        {
            Assert.True(
                cache.HasLayer("flute", pitch, "mf"),
                $"FLUTE-01: cache missing layer (flute, {pitch}, mf) after eager-load — composer drop or manifest update incomplete");
        }

        // 3 raw entries minimum: 3 pitches × 1 velocity = 3 layers in memory.
        // Strict ≥3 — Plan 37-05 explicitly forbids regressing below the
        // Phase 29 baseline of 2.
        Assert.True(cache.RawSampleCount >= 3,
            $"FLUTE-01: expected ≥3 raw flute layers in cache, got {cache.RawSampleCount}");
    }
}
