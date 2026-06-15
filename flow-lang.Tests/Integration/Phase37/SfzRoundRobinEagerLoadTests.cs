using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 SAMP-01 EAGER-LOAD side (sweep-0614 regression). The render path
/// (<see cref="SfzRoundRobinDeterminismTests"/>) picks round-robin alternates by
/// seq_position, and those alternates normally point at DISTINCT sample files.
/// But the eager-load previously collected the load set from
/// <c>patch.Grid[midi, vel]</c> alone — the LAST-declared region per D-02 — so
/// only the grid-winner alternate's WAV was loaded. Every trigger landing on a
/// non-winner alternate then rendered silence at trigger time.
///
/// <para>This test builds a 2-alternate patch with DISTINCT sample files
/// (C4_sine.wav for seq_position=1, G5_sine.wav for seq_position=2) and asserts
/// that <c>EagerLoad</c> loads BOTH samples (RawSampleCount == 2 and both
/// GetSample lookups non-null). The existing round-robin fixtures deliberately
/// share one WAV, so this case needs a custom patch.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzRoundRobinEagerLoadTests : IDisposable
{
    public SfzRoundRobinEagerLoadTests()
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
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Parse an inline 2-alternate round-robin patch whose alternates point at
    /// DISTINCT sample files. The patch's filePath is set inside the committed
    /// sfz-smoke fixture dir so the sample paths (C4_sine.wav / G5_sine.wav)
    /// resolve to real on-disk WAVs at EagerLoad time.
    /// </summary>
    private static SfzData BuildDistinctRoundRobinPatch()
    {
        string smokeDir = Path.Combine(FindRepoRoot(), "flow-lang.Tests",
            "fixtures", "sfz-smoke");
        string fakeSfzPath = Path.Combine(smokeDir, "rr_distinct.sfz");

        string sfz = @"<region>
sample=C4_sine.wav
pitch_keycenter=60
lokey=60 hikey=60 lovel=1 hivel=127
seq_position=1 seq_length=2

<region>
sample=G5_sine.wav
pitch_keycenter=60
lokey=60 hikey=60 lovel=1 hivel=127
seq_position=2 seq_length=2
";
        return SfzParser.Parse(sfz, fakeSfzPath, "rr_distinct");
    }

    private static SongData OneNoteC4Song()
    {
        var ts = new TimeSignatureData(4, 4);
        var bar = new BarData(
            new List<MusicalNoteData>
            {
                new('C', 4, 0, durationValue: 4, isRest: false, velocity: 0.8),
            },
            ts);
        var seq = new SequenceData();
        seq.AddBar(bar);
        var section = new SectionData(
            "tmp",
            new Dictionary<string, SequenceData> { ["s"] = seq },
            context: null);
        var registry = new Dictionary<string, SectionData> { ["tmp"] = section };
        return new SongData(new List<SongSectionRef> { new("tmp", 1) }, registry);
    }

    [Fact]
    public void EagerLoad_RoundRobinDistinctSamples_LoadsAllAlternates()
    {
        var patch = BuildDistinctRoundRobinPatch();
        Assert.Equal(2, patch.Regions.Count);

        var cache = new SfzSampleCache();
        cache.EagerLoad(OneNoteC4Song(), patch);

        // Both distinct alternate WAVs must be loaded — not just the grid winner.
        Assert.Equal(2, cache.RawSampleCount);
        Assert.NotNull(cache.GetSample(patch, "C4_sine.wav")); // seq_position=1
        Assert.NotNull(cache.GetSample(patch, "G5_sine.wav")); // seq_position=2
    }
}
