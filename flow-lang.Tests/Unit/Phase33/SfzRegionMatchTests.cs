using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase33;

/// <summary>
/// Phase 33 Plan 33-06 — facts pinning SfzRenderer's SPEC-4 region-match +
/// nearest-pitch fallback behavior. Direct method invocations against
/// <see cref="SfzRenderer.Render"/> — no FlowEngine, no song construction.
/// The renderer takes <c>(MusicalNoteData, sampleRate, durationBeats, bpm, SfzData)</c>
/// and returns an <see cref="AudioBuffer"/>; tests exercise that signature.
///
/// Serialized via <c>[Collection("FlowScripts")]</c> so the
/// <see cref="RenderingDiagnostics"/> singleton dedup state is isolated
/// between facts (each fact resets it in the ctor + Dispose).
/// </summary>
[Collection("FlowScripts")]
public class SfzRegionMatchTests : IDisposable
{
    // 100 ms of mono sine at 44.1 kHz = 4410 frames — matches the smoke fixture's
    // C4_sine.wav / G5_sine.wav shape so render-time RMS / discontinuity math is
    // well-defined.
    private const int SampleRate = 44100;
    private const int SampleFrames = 4410;

    // Each fact builds its own temp .wav path so the file system never carries
    // residual state across facts (also makes tests safe in parallel CI).
    private readonly string _tmpRoot;

    public SfzRegionMatchTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tmpRoot = Path.Combine(Path.GetTempPath(), $"sfz-region-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpRoot);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        try { Directory.Delete(_tmpRoot, recursive: true); } catch { /* swallow — best-effort cleanup */ }
    }

    [Fact]
    public void TwoRegionOverlap_RoutesByPitchRange()
    {
        // Two regions whose key ranges tile MIDI 48..127. The renderer's lookup
        // is Grid[midi, vel] — a constant-time array dereference — so each note
        // must hit the correct region without any scan.
        WriteSineWav(Path.Combine(_tmpRoot, "low.wav"), frequencyHz: 261.63, frames: SampleFrames);
        WriteSineWav(Path.Combine(_tmpRoot, "high.wav"), frequencyHz: 783.99, frames: SampleFrames);

        var rLow  = MakeRegion("low.wav",  pitchKeycenter: 60, loKey: 48, hiKey: 71, loVel: 1, hiVel: 127);
        var rHigh = MakeRegion("high.wav", pitchKeycenter: 79, loKey: 72, hiKey: 127, loVel: 1, hiVel: 127);
        var patch = BuildPatch(_tmpRoot, "two-region", rLow, rHigh);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        // A4 (MIDI 69) is inside rLow.lokey..hikey → must route to rLow (low.wav).
        var a4 = MakeNote('A', octave: 4, velocity: 0.5);
        var bufA4 = renderer.Render(a4, SampleRate, durationBeats: 1.0, bpm: 60, patch);
        Assert.True(Rms(bufA4) > 0, "A4 should render non-silent under rLow coverage.");

        // D4 (MIDI 62) also inside rLow.
        var d4 = MakeNote('D', octave: 4, velocity: 0.5);
        var bufD4 = renderer.Render(d4, SampleRate, durationBeats: 1.0, bpm: 60, patch);
        Assert.True(Rms(bufD4) > 0, "D4 should render non-silent under rLow coverage.");

        // A5 (MIDI 81) is inside rHigh.lokey..hikey → must route to rHigh (high.wav).
        var a5 = MakeNote('A', octave: 5, velocity: 0.5);
        var bufA5 = renderer.Render(a5, SampleRate, durationBeats: 1.0, bpm: 60, patch);
        Assert.True(Rms(bufA5) > 0, "A5 should render non-silent under rHigh coverage.");

        // Structural anchor: A4 (varispeed-shifted low.wav) should differ in RMS
        // from A5 (varispeed-shifted high.wav) because the source pitches differ.
        // We don't pin the exact ratio (depends on linear-interp resample math),
        // but the rendered buffers must NOT be byte-identical — that would
        // indicate both notes routed to the same region.
        Assert.False(BuffersByteIdentical(bufA4, bufA5),
            "A4 and A5 must route to different regions — non-identical buffers expected.");
    }

    [Fact]
    public void VelocityOverlap_RoutesByVelocityBand()
    {
        // Two regions at C4 split velocity 1..63 vs 64..127. Both reference the
        // SAME WAV but at different Volume so the rendered buffers' RMS differs
        // enough to distinguish the routing in-test.
        WriteSineWav(Path.Combine(_tmpRoot, "c4.wav"), frequencyHz: 261.63, frames: SampleFrames);

        var rSoft = MakeRegion("c4.wav", pitchKeycenter: 60, loKey: 60, hiKey: 60, loVel: 1, hiVel: 63, volume: 0.25);
        var rLoud = MakeRegion("c4.wav", pitchKeycenter: 60, loKey: 60, hiKey: 60, loVel: 64, hiVel: 127, volume: 1.0);
        var patch = BuildPatch(_tmpRoot, "vel-overlap", rSoft, rLoud);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        // Velocity 0.3 → MIDI vel 38 → inside rSoft (1..63).
        var soft = MakeNote('C', octave: 4, velocity: 0.3);
        var bufSoft = renderer.Render(soft, SampleRate, 1.0, 60, patch);
        // Velocity 0.9 → MIDI vel 114 → inside rLoud (64..127).
        var loud = MakeNote('C', octave: 4, velocity: 0.9);
        var bufLoud = renderer.Render(loud, SampleRate, 1.0, 60, patch);

        double rmsSoft = Rms(bufSoft);
        double rmsLoud = Rms(bufLoud);
        Assert.True(rmsSoft > 0 && rmsLoud > 0, "Both renders must produce non-silent buffers.");
        // rLoud's volume is 4× rSoft's — RMS ratio should reflect that the
        // velocity band routed to the correct region (loud > soft).
        Assert.True(rmsLoud > rmsSoft,
            $"rLoud (volume 1.0) RMS ({rmsLoud}) must exceed rSoft (volume 0.25) RMS ({rmsSoft}) — proves velocity routing.");
    }

    [Fact]
    public void NearestPitchFallback_VarispeedShiftsClosestRegion()
    {
        // Region covers exactly C4 (MIDI 60..60). Render B5 (MIDI 83) — outside
        // ALL coverage. Renderer must walk SortedByPitch[] for the nearest pitch
        // (60), match Grid[60, vel], then varispeed-shift by 83 - 60 = +23
        // semitones. Output frequency should approximate sampleHz × 2^(23/12).
        WriteSineWav(Path.Combine(_tmpRoot, "c4.wav"), frequencyHz: 261.63, frames: SampleFrames * 8);
        var rC4 = MakeRegion("c4.wav", pitchKeycenter: 60, loKey: 60, hiKey: 60, loVel: 1, hiVel: 127);
        var patch = BuildPatch(_tmpRoot, "nearest", rC4);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        var b5 = MakeNote('B', octave: 5, velocity: 0.5);
        var buf = renderer.Render(b5, SampleRate, durationBeats: 1.0, bpm: 60, patch);

        Assert.True(Rms(buf) > 0.0, "Nearest-pitch fallback must render audible content (non-zero RMS).");
        // The +23-semitone shift means the source sine is played back faster,
        // raising its frequency. We don't pin the exact spectral peak (linear-
        // interp varispeed introduces some aliasing), but the buffer must
        // contain energy AND it must NOT be byte-identical to the unshifted C4
        // render — which would indicate the shift wasn't applied.
        var c4 = MakeNote('C', octave: 4, velocity: 0.5);
        var bufC4 = renderer.Render(c4, SampleRate, 1.0, 60, patch);
        Assert.False(BuffersByteIdentical(buf, bufC4),
            "B5 (fallback + 23-semitone shift) must differ from C4 (unshifted) — proves varispeed applied.");
    }

    [Fact]
    public void MissingRegion_RendersSilence_AndAdvisoryDedupes()
    {
        // Patch with a region covering ONLY C4 (60..60). Render F#3 (MIDI 54)
        // — the nearest-pitch fallback STILL routes to C4 (the only pitch in
        // SortedByPitch). To trigger the "no region anywhere" path, build a
        // patch with an empty regions list / empty SortedByPitch. Renderer must:
        //   (1) emit a WarnOnce advisory once per (patch, midi, vel) sentinel
        //   (2) return a silence buffer of the authored duration
        //   (3) NOT throw
        var empty = BuildEmptyPatch(_tmpRoot, "empty-patch");
        var cache = new SfzSampleCache();
        var renderer = new SfzRenderer(cache);

        // Capture stderr to verify the advisory fires exactly once.
        var originalErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var note = MakeNote('C', octave: 4, velocity: 0.5);
            var buf1 = renderer.Render(note, SampleRate, 1.0, 60, empty);
            var buf2 = renderer.Render(note, SampleRate, 1.0, 60, empty);

            // Both renders return silence buffers (all zeros).
            Assert.True(Rms(buf1) == 0.0, "Missing-region render must be silence.");
            Assert.True(Rms(buf2) == 0.0, "Second missing-region render must also be silence.");
        }
        finally
        {
            Console.SetError(originalErr);
        }
        string stderr = sw.ToString();
        Assert.Contains("[sfz] no region for", stderr);
        // WarnOnce dedupes per sentinel — the second call with the same
        // (patch, midi, vel) must NOT emit a second advisory line.
        int adviseCount = stderr.Split('\n').Count(line => line.Contains("[sfz] no region for"));
        Assert.Equal(1, adviseCount);
    }

    [Fact]
    public void VelocityZero_ClampsToOne_AndMatchesLovel1Region()
    {
        // Pitfall 9 clamp: note.Velocity == 0.0 → MIDI velocity raw is 0; renderer
        // must clamp to 1 so the lovel=1 default region still matches. A region
        // with loVel=1 hivel=127 must therefore render audibly for vel=0.0.
        WriteSineWav(Path.Combine(_tmpRoot, "c4.wav"), frequencyHz: 261.63, frames: SampleFrames);
        var region = MakeRegion("c4.wav", pitchKeycenter: 60, loKey: 60, hiKey: 60, loVel: 1, hiVel: 127);
        var patch = BuildPatch(_tmpRoot, "vel-zero", region);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patch);
        var renderer = new SfzRenderer(cache);

        var note = MakeNote('C', octave: 4, velocity: 0.0);  // unclamped → MIDI 0 → should clamp to 1
        var buf = renderer.Render(note, SampleRate, 1.0, 60, patch);
        Assert.True(Rms(buf) > 0.0, "velocity 0.0 must clamp to MIDI 1 and hit the lovel=1 region.");
    }

    [Fact]
    public void VolumeOpcode_HalvesAmplitude_VsUnityGain()
    {
        // region.Volume is LINEAR (parser already converted from dB per
        // Pitfall 8). volume=0.5 should halve the rendered amplitude vs
        // volume=1.0 on the same source.
        WriteSineWav(Path.Combine(_tmpRoot, "c4.wav"), frequencyHz: 261.63, frames: SampleFrames);

        var rUnity = MakeRegion("c4.wav", pitchKeycenter: 60, loKey: 60, hiKey: 60, loVel: 1, hiVel: 127, volume: 1.0);
        var patchUnity = BuildPatch(_tmpRoot, "vol-unity", rUnity);

        var rHalf = MakeRegion("c4.wav", pitchKeycenter: 60, loKey: 60, hiKey: 60, loVel: 1, hiVel: 127, volume: 0.5);
        var patchHalf = BuildPatch(_tmpRoot, "vol-half", rHalf);

        var cache = new SfzSampleCache();
        EagerLoadDirect(cache, patchUnity);
        EagerLoadDirect(cache, patchHalf);
        var renderer = new SfzRenderer(cache);

        var note = MakeNote('C', octave: 4, velocity: 0.5);
        var bufUnity = renderer.Render(note, SampleRate, 1.0, 60, patchUnity);
        var bufHalf  = renderer.Render(note, SampleRate, 1.0, 60, patchHalf);

        double rmsU = Rms(bufUnity);
        double rmsH = Rms(bufHalf);
        Assert.True(rmsU > 0 && rmsH > 0);
        // Ratio should be ~0.5 — allow a generous ±15% window because Phase 28
        // envelope's attack ramp + release tail aren't perfectly linear.
        double ratio = rmsH / rmsU;
        Assert.InRange(ratio, 0.35, 0.65);
    }

    // ===== Helpers =====

    private static SfzRegion MakeRegion(
        string samplePath,
        int pitchKeycenter,
        int loKey, int hiKey,
        int loVel, int hiVel,
        SfzLoopMode loopMode = SfzLoopMode.NoLoop,
        int loopStart = 0,
        int loopEnd = 0,
        double ampegAttack = 0.0,
        double ampegRelease = 0.0,
        double volume = 1.0,
        double pan = 0.0)
    {
        // SfzRegion is a positional record — positional args by index, not name.
        return new SfzRegion(
            samplePath, pitchKeycenter,
            loKey, hiKey, loVel, hiVel,
            loopMode, loopStart, loopEnd,
            ampegAttack, ampegRelease,
            volume, pan);
    }

    /// <summary>
    /// Builds a SfzData with the given regions, populating Grid + SortedByPitch
    /// per CONTEXT D-01 / D-02 (last-declared-wins). Mirrors the SfzParser's
    /// behavior so tests can construct synthetic patches without parsing .sfz
    /// text.
    /// </summary>
    private static SfzData BuildPatch(string basePath, string description, params SfzRegion[] regions)
    {
        var grid = new SfzRegion?[128, 128];
        foreach (var r in regions)
        {
            for (int p = Math.Max(0, r.LoKey); p <= Math.Min(127, r.HiKey); p++)
                for (int v = Math.Max(0, r.LoVel); v <= Math.Min(127, r.HiVel); v++)
                    grid[p, v] = r;  // last-declared-wins
        }
        var sortedPitches = regions
            .SelectMany(r => Enumerable.Range(Math.Max(0, r.LoKey), Math.Min(127, r.HiKey) - Math.Max(0, r.LoKey) + 1))
            .Distinct()
            .OrderBy(p => p)
            .ToArray();
        return new SfzData(description, basePath, regions.ToList(), grid, sortedPitches);
    }

    /// <summary>
    /// Builds an empty SfzData (zero regions, all-null Grid, empty SortedByPitch)
    /// for the "no region anywhere" advisory test.
    /// </summary>
    private static SfzData BuildEmptyPatch(string basePath, string description)
    {
        return new SfzData(description, basePath, new List<SfzRegion>(), new SfzRegion?[128, 128], Array.Empty<int>());
    }

    /// <summary>
    /// Force the cache to load every region's WAV under <paramref name="patch"/>
    /// — bypassing the SongData walk, which we don't construct in unit tests.
    /// Mirrors the post-EagerLoad cache state SongRenderer (Plan 33-07) will
    /// produce in production.
    /// </summary>
    private static void EagerLoadDirect(SfzSampleCache cache, SfzData patch)
    {
        // SfzSampleCache exposes EagerLoad(SongData, SfzData), but in isolation
        // tests we use a synthetic minimal SongData with one section + one
        // sequence + one bar holding every region's keycenter pitch — so the
        // walk dereferences every region exactly once.
        var section = new SectionData(
            "tmp",
            new Dictionary<string, SequenceData> { ["s"] = BuildSequence(patch) },
            context: null);
        var registry = new Dictionary<string, SectionData> { ["tmp"] = section };
        var song = new SongData(new List<SongSectionRef> { new("tmp", 1) }, registry);
        cache.EagerLoad(song, patch);
    }

    private static SequenceData BuildSequence(SfzData patch)
    {
        var seq = new SequenceData();
        var ts = new TimeSignatureData(4, 4);
        var notes = new List<MusicalNoteData>();
        foreach (var r in patch.Regions)
        {
            // Synthesize one note at each region's keycenter pitch + middle vel
            // so the EagerLoad walk dereferences Grid[keycenter, 64] → the
            // region we just built.
            int midi = Math.Clamp(r.PitchKeycenter, 0, 127);
            char nn; int oct; int alt;
            MidiToPitch(midi, out nn, out oct, out alt);
            notes.Add(new MusicalNoteData(nn, oct, alt, durationValue: 4, isRest: false, velocity: 0.5));
        }
        if (notes.Count == 0)
        {
            // Empty patch — add a single rest so the bar isn't degenerate
            notes.Add(new MusicalNoteData('C', 4, 0, durationValue: 4, isRest: true));
        }
        var bar = new BarData(notes, ts);
        seq.AddBar(bar);
        return seq;
    }

    private static void MidiToPitch(int midi, out char noteName, out int octave, out int alteration)
    {
        // Inverse of PitchConversion.GetMidiNote for natural notes; for
        // alterations we always emit Cnatural as a fallback that matches the
        // letter the test author chose. The renderer's Render method uses
        // GetMidiNote(note.NoteName, note.Octave, note.Alteration) which is
        // bijective for natural notes — picking the natural mapping covers
        // every test fixture in this suite (all keycenters are at natural pitches).
        int oct = (midi / 12) - 1;
        int semi = midi - (oct + 1) * 12;
        // Natural-only inverse map.
        switch (semi)
        {
            case 0:  noteName = 'C'; octave = oct; alteration = 0; return;
            case 2:  noteName = 'D'; octave = oct; alteration = 0; return;
            case 4:  noteName = 'E'; octave = oct; alteration = 0; return;
            case 5:  noteName = 'F'; octave = oct; alteration = 0; return;
            case 7:  noteName = 'G'; octave = oct; alteration = 0; return;
            case 9:  noteName = 'A'; octave = oct; alteration = 0; return;
            case 11: noteName = 'B'; octave = oct; alteration = 0; return;
            // Non-natural: round down to the natural and add +1 alt so the
            // composed midi still matches.
            case 1:  noteName = 'C'; octave = oct; alteration = 1; return;
            case 3:  noteName = 'D'; octave = oct; alteration = 1; return;
            case 6:  noteName = 'F'; octave = oct; alteration = 1; return;
            case 8:  noteName = 'G'; octave = oct; alteration = 1; return;
            case 10: noteName = 'A'; octave = oct; alteration = 1; return;
            default:
                noteName = 'C'; octave = oct; alteration = 0; return;
        }
    }

    private static MusicalNoteData MakeNote(char noteName, int octave, double velocity, int alteration = 0)
    {
        return new MusicalNoteData(noteName, octave, alteration, durationValue: 4, isRest: false, velocity: velocity);
    }

    /// <summary>
    /// Writes a 16-bit PCM mono sine WAV to <paramref name="path"/> at the
    /// canonical 44100 Hz. Used to manufacture region samples without
    /// shipping additional test fixtures.
    /// </summary>
    internal static void WriteSineWav(string path, double frequencyHz, int frames)
    {
        var buf = new AudioBuffer(frames, 1, SampleRate);
        for (int i = 0; i < frames; i++)
            buf.Data[i] = (float)(0.5 * Math.Sin(2.0 * Math.PI * frequencyHz * i / SampleRate));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileIO.WriteWav(new List<FlowLang.Runtime.Value>
        {
            FlowLang.Runtime.Value.String(path),
            FlowLang.Runtime.Value.Buffer(buf)
        });
    }

    internal static double Rms(AudioBuffer buf)
    {
        if (buf.Data.Length == 0) return 0.0;
        double sum = 0;
        for (int i = 0; i < buf.Data.Length; i++) sum += buf.Data[i] * buf.Data[i];
        return Math.Sqrt(sum / buf.Data.Length);
    }

    private static bool BuffersByteIdentical(AudioBuffer a, AudioBuffer b)
    {
        if (a.Data.Length != b.Data.Length) return false;
        for (int i = 0; i < a.Data.Length; i++)
            if (a.Data[i] != b.Data[i]) return false;
        return true;
    }
}
