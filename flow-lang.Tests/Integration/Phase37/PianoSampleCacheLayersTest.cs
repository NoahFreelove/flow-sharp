using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 PIANO-01 (Plan 37-04 / D-37-09) — Piano <see cref="SampleCache"/>
/// has ≥4 velocity layers per pitch point (pp/mp/mf/ff) after eager-load.
/// Plan 37-01 ships the Wave 0 scaffold; Plan 37-04 fills it.
///
/// Two facts:
///   1. <see cref="PianoSampleCache_HasAtLeast4VelocityLayers"/> — each of the
///      5 piano pitch points carries pp/mp/mf/ff in the raw cache after
///      <see cref="SampleCache.EagerLoad"/> walks the manifest.
///   2. <see cref="PianoCache_MpLayer_IsSynthesizedNot_OnDisk"/> — no
///      <c>C4_mp.wav</c> exists on disk, but
///      <c>cache.HasLayer("piano", 60, "mp")</c> returns <c>true</c> (mp is
///      synthesized post-load via <c>RmsInterpolate(pp, mf, alpha=0.6)</c> per
///      RESEARCH §Pattern 9 Path 1).
/// </summary>
[Collection("FlowScripts")]
public class PianoSampleCacheLayersTest : IDisposable
{
    public PianoSampleCacheLayersTest()
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
    /// Builds a minimal SongData with one section + one sequence pinning at
    /// least one piano note. The actual content is irrelevant — eager-load
    /// walks the manifest, not the song. The cache uses the SongData's hash
    /// as part of its idempotency key, so a real (non-null) song instance
    /// is sufficient.
    /// </summary>
    private static SongData BuildEmptyPianoSong()
    {
        return new SongData(
            new System.Collections.Generic.List<SongSectionRef>(),
            new System.Collections.Generic.Dictionary<string, SectionData>());
    }

    /// <summary>
    /// Absolute path to the repo's flow-lang/Samples directory. Required
    /// because xUnit runs from <c>bin/Debug/net10.0/</c>, so the default
    /// relative <c>"flow-lang/Samples"</c> root won't resolve.
    /// </summary>
    private static string SamplesRoot()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        return Path.Combine(repoRoot, "flow-lang", "Samples");
    }

    [Fact]
    public void PianoSampleCache_HasAtLeast4VelocityLayers()
    {
        var cache = new SampleCache(SamplesRoot());
        var song = BuildEmptyPianoSong();
        cache.EagerLoad(song, "piano");

        // Five canonical pitches × four velocities = 20 layers in memory after
        // eager-load (15 disk-loaded + 5 synthesized mp). D-37-09 lock.
        int[] pitches = { 36, 48, 60, 72, 84 };
        string[] velocities = { "pp", "mp", "mf", "ff" };

        foreach (int pitch in pitches)
        {
            foreach (string vel in velocities)
            {
                Assert.True(
                    cache.HasLayer("piano", pitch, vel),
                    $"PIANO-01: cache missing layer (piano, {pitch}, {vel}) after eager-load");
            }
        }

        // 20 raw entries: 5 pitches × 4 velocity layers (D-37-09).
        Assert.True(cache.RawSampleCount >= 20,
            $"PIANO-01: expected ≥20 raw piano layers in cache, got {cache.RawSampleCount}");
    }

    [Fact]
    public void PianoCache_MpLayer_IsSynthesizedNot_OnDisk()
    {
        // RESEARCH §Pattern 9 Path 1 + Pitfall 9: U-Iowa MIS source ships only
        // pp/mf/ff — no mp files exist on disk. Plan 37-04 synthesizes mp at
        // eager-load via RmsInterpolate(pp, mf, alpha=0.6).
        string pianoDir = Path.Combine(SamplesRoot(), "piano");
        Assert.True(Directory.Exists(pianoDir),
            $"PIANO-01 setup: piano samples directory missing at {pianoDir}");

        // No _mp.wav files should exist on disk — mp is synthesized.
        foreach (var pitchName in new[] { "C2", "C3", "C4", "C5", "C6" })
        {
            string mpPath = Path.Combine(pianoDir, $"{pitchName}_mp.wav");
            Assert.False(File.Exists(mpPath),
                $"PIANO-01 invariant: {mpPath} must NOT exist (mp is synthesized, not on disk)");
        }

        // But after eager-load, cache.HasLayer("piano", N, "mp") MUST return true.
        var cache = new SampleCache(SamplesRoot());
        cache.EagerLoad(BuildEmptyPianoSong(), "piano");
        Assert.True(cache.HasLayer("piano", 60, "mp"),
            "PIANO-01: cache.HasLayer(piano, 60 (C4), mp) must be true after eager-load — RmsInterpolate synthesizes mp from pp+mf");
    }
}
