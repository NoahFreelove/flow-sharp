using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Melanchall.DryWetMidi.Core;
using Xunit;

namespace FlowLang.Tests.Integration.Phase33;

/// <summary>
/// Phase 33 Plan 33-07 Task 3 — D-15 / D-16 / D-17 acceptance facts for the
/// MIDI export side of the SFZ pipeline. The contract is that a song using
/// <c>sampler:NAME</c> instrument names exports correct GM program changes
/// AND correct prefix-stripped track-name meta-events, so the receiving DAW
/// gets a sensible General-MIDI-compatible file even without VSCO-CE
/// installed.
///
/// <para>Three concern-areas are covered:</para>
/// <list type="bullet">
///   <item><description><c>ResolveGmProgram</c> direct unit-style facts —
///   pin the prefix-strip ordering (Pitfall 6) and the 12 new GM-program
///   entries from D-16, plus the unchanged Phase 28 entries as
///   regression net.</description></item>
///   <item><description>End-to-end MIDI export of a sampler-instrument
///   song — verifies the .mid file's ProgramChange + SequenceTrackName
///   events carry the prefix-stripped name + correct GM program (D-17).</description></item>
///   <item><description>Phase 28 byte-identical contract preservation
///   probe — a song using only Phase 28 instrument names (no sampler:
///   prefix) still produces a valid multi-track .mid; the existing
///   Phase 28 MIDI test suite is run as part of the verify step.</description></item>
/// </list>
///
/// <para>[Collection("FlowScripts")] for the same diagnostics-state and
/// FlowConfig-singleton isolation rationale as Plan 33-04/05/06 tests.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzMidiExportTests : IDisposable
{
    private readonly string _tmpDir;

    public SfzMidiExportTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        _tmpDir = Path.Combine(Path.GetTempPath(),
            $"p33_07_midi_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        try { Directory.Delete(_tmpDir, recursive: true); } catch { /* best-effort */ }
    }

    // =====================================================================
    // ResolveGmProgram direct unit-style facts (Pitfall 6 + D-16 + D-15)
    // =====================================================================

    /// <summary>
    /// Pitfall 6: the <c>sampler:</c> prefix MUST be stripped at the TOP of
    /// <see cref="MidiExport.ResolveGmProgram"/>. Without the strip, a
    /// <c>"sampler:violin"</c> name would not match the <c>violin*</c>
    /// prefix check (because the literal string starts with <c>"sampler:"</c>)
    /// and would fall through to the <c>(0, 0)</c> default — a regression
    /// that would silently route every sampler instrument to GM-0 piano.
    /// </summary>
    [Fact]
    public void ResolveGmProgram_SamplerPrefix_Stripped()
    {
        Assert.Equal((40, 0), MidiExport.ResolveGmProgram("sampler:violin"));
    }

    /// <summary>
    /// Pitfall 6 corollary — the strip must happen BEFORE the existing
    /// Phase 28 entries too. Otherwise <c>sampler:flute</c> would route to
    /// (0, 0) instead of (73, 0).
    /// </summary>
    [Fact]
    public void ResolveGmProgram_SamplerPrefix_Flute_RoutesCorrectly()
    {
        Assert.Equal((73, 0), MidiExport.ResolveGmProgram("sampler:flute"));
    }

    /// <summary>
    /// D-16 — violin → 40 (GM Violin).
    /// </summary>
    [Fact]
    public void ResolveGmProgram_NewEntry_Violin()
    {
        Assert.Equal((40, 0), MidiExport.ResolveGmProgram("violin"));
    }

    /// <summary>
    /// D-16 — cello → 42 (GM Cello). Pins the alphabetic-ordering test net.
    /// </summary>
    [Fact]
    public void ResolveGmProgram_NewEntry_Cello()
    {
        Assert.Equal((42, 0), MidiExport.ResolveGmProgram("cello"));
    }

    /// <summary>
    /// D-16 — timpani → (47, 9). Channel 9 = GM percussion. Timpani is the
    /// only orchestral entry that uses the percussion channel; the other
    /// 14 orchestral entries default to channel 0.
    /// </summary>
    [Fact]
    public void ResolveGmProgram_NewEntry_Timpani_Channel9()
    {
        Assert.Equal((47, 9), MidiExport.ResolveGmProgram("timpani"));
    }

    /// <summary>
    /// D-16 ordering precedence — <c>horn</c> MUST resolve to (60, 0)
    /// (French horn) and NOT (56, 0) (the historical Phase 28 brass/horn
    /// entry). The new <c>horn</c> entry MUST be checked BEFORE the existing
    /// <c>brass</c> entry; if the order is wrong, <c>horn</c> falls through
    /// to the brass match and produces the wrong GM program.
    /// </summary>
    [Fact]
    public void ResolveGmProgram_NewEntry_Horn_BeatsBrass()
    {
        Assert.Equal((60, 0), MidiExport.ResolveGmProgram("horn"));
    }

    /// <summary>
    /// Phase 28 regression net — <c>brass</c> still resolves to (56, 0).
    /// Catches an accidental reorder that would route brass to (60, 0)
    /// (French horn) via the new horn entry.
    /// </summary>
    [Fact]
    public void ResolveGmProgram_ExistingEntry_Brass_Unchanged()
    {
        Assert.Equal((56, 0), MidiExport.ResolveGmProgram("brass"));
    }

    /// <summary>
    /// Phase 28 regression net — <c>piano</c> still resolves to (0, 0).
    /// </summary>
    [Fact]
    public void ResolveGmProgram_ExistingEntry_Piano_Unchanged()
    {
        Assert.Equal((0, 0), MidiExport.ResolveGmProgram("piano"));
    }

    // =====================================================================
    // End-to-end MIDI export — D-17 track-name meta-event
    // =====================================================================

    /// <summary>
    /// D-17: a written .mid file's SequenceTrackName meta-event for a
    /// <c>sampler:violin</c> sequence reads <c>"violin"</c> (NOT
    /// <c>"sampler:violin"</c>). Pins the contract that the two-stripped-name
    /// sites (the GM-lookup AND the track-name event) BOTH use the helper
    /// — if a future refactor moves one of them, the test catches it.
    ///
    /// <para>This fact also exercises the dispatch end-to-end via writeMidi:
    /// the song's sequence is named <c>sampler:violin</c>, the file is
    /// written, then read back to find the SequenceTrackNameEvent on the
    /// non-conductor track and assert its Text payload is the stripped name.
    /// The corresponding ProgramChange must be GM 40 (violin).</para>
    ///
    /// <para>Note: this fact does NOT need the SFZ runtime to be set up — it
    /// uses the writeMidi path with a sequence named <c>sampler:violin</c>
    /// directly, which exercises the export-side prefix-strip without
    /// requiring an actual SFZ patch render. The SongRenderer's sampler:
    /// dispatch is a separate code path covered by SfzBindingTests.</para>
    /// </summary>
    [Fact]
    public void TrackName_StripsSamplerPrefix()
    {
        string outPath = Path.Combine(_tmpDir, "track_name_strip.mid");

        // Why C# direct API and not Flow source: Flow identifiers don't
        // accept the `:` character, so a script can't author a sequence
        // literally named `sampler:violin`. The MIDI export path consumes
        // SongData / SectionData / Sequence by string name regardless of
        // how the song was assembled — calling MidiExport.WriteMidi
        // directly with a programmatically-built SongData exercises the
        // export-side prefix-strip without requiring an actual SFZ render.
        // The Flow-script-end-to-end sampler: dispatch is a separate code
        // path covered by SfzBindingTests.
        BuildAndWriteSamplerViolinSong(outPath);

        Assert.True(File.Exists(outPath), $"writeMidi did not produce {outPath}");

        var midi = MidiFile.Read(outPath);
        var trackChunks = midi.Chunks.OfType<TrackChunk>().Skip(1).ToArray();  // skip conductor
        Assert.Single(trackChunks);
        var track = trackChunks[0];

        // SequenceTrackNameEvent's Text must be the prefix-stripped name.
        var trackNameEvent = track.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
        Assert.NotNull(trackNameEvent);
        Assert.Equal("violin", trackNameEvent!.Text);

        // ProgramChange at the start must be GM 40 (violin). Confirms the
        // strip applied to the GM lookup too.
        var pc = track.Events.OfType<ProgramChangeEvent>().FirstOrDefault();
        Assert.NotNull(pc);
        Assert.Equal(40, (int)(byte)pc!.ProgramNumber);
    }

    /// <summary>
    /// Directly constructs a SongData with a single section containing a
    /// single sequence named <c>"sampler:violin"</c>, then calls
    /// <see cref="MidiExport.WriteMidi(System.Collections.Generic.IReadOnlyList{Value})"/>
    /// to write it to <paramref name="outPath"/>. Sidesteps Flow's
    /// identifier-syntax restrictions (colons aren't valid in identifiers,
    /// so we cannot author <c>Sequence sampler:violin = ...</c> at the
    /// composer surface — but the renderer / exporter accept any string as
    /// the sequence name).
    /// </summary>
    private static void BuildAndWriteSamplerViolinSong(string outPath)
    {
        // Build a tiny single-bar 4/4 sequence: C4q D4q E4q F4q.
        // MusicalNoteData ctor positional args:
        //   noteName, octave, alteration, durationValue (4 = quarter),
        //   isRest, centOffset, isTied, velocity, articulation, isDotted, ...
        var timesig = new TimeSignatureData(4, 4);
        var notes = new System.Collections.Generic.List<MusicalNoteData>
        {
            new MusicalNoteData('C', 4, 0, 4, false, null, false, 0.7),
            new MusicalNoteData('D', 4, 0, 4, false, null, false, 0.7),
            new MusicalNoteData('E', 4, 0, 4, false, null, false, 0.7),
            new MusicalNoteData('F', 4, 0, 4, false, null, false, 0.7),
        };
        var bar = new BarData(notes, timesig);
        var sequence = new SequenceData();
        sequence.AddBar(bar);

        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = timesig };
        var sequences = new System.Collections.Generic.Dictionary<string, SequenceData>
        {
            ["sampler:violin"] = sequence,
        };
        var section = new SectionData("main", sequences, ctx, null);
        var sectionRegistry = new System.Collections.Generic.Dictionary<string, SectionData>
        {
            ["main"] = section,
        };
        var song = new SongData(
            new System.Collections.Generic.List<SongSectionRef>
            {
                new SongSectionRef("main", 1),
            },
            sectionRegistry);

        MidiExport.WriteMidi(new System.Collections.Generic.List<Value>
        {
            Value.String(outPath),
            Value.Song(song),
        });
    }

    // =====================================================================
    // Phase 28 byte-identical regression probe
    // =====================================================================

    /// <summary>
    /// Regression net — the existing Phase 28 multi-track MIDI export still
    /// produces the expected chunk count + ProgramChange events. The full
    /// Phase 28 byte-identical contract is exercised by
    /// Phase28.MultiTrackMidiTests + Phase28.RagtimeFixtureTests
    /// (run as part of the plan's verify step); this fact spot-checks the
    /// most-load-bearing assertion (chunk count + per-track GM program)
    /// inside this suite so a regression here surfaces immediately.
    /// </summary>
    [Fact]
    public void Phase28_MidiExport_NonSamplerInstruments_StillWork()
    {
        string outPath = Path.Combine(_tmpDir, "phase28_regression.mid");
        BuildAndWritePhase28PianoSong(outPath);

        Assert.True(File.Exists(outPath), $"writeMidi did not produce {outPath}");
        var midi = MidiFile.Read(outPath);
        // 1 conductor + 1 piano sequence track
        Assert.Equal(2, midi.Chunks.Count);

        var track = midi.Chunks.OfType<TrackChunk>().Skip(1).First();
        var pc = track.Events.OfType<ProgramChangeEvent>().FirstOrDefault();
        Assert.NotNull(pc);
        Assert.Equal(0, (int)(byte)pc!.ProgramNumber);  // piano → GM 0

        // The track-name meta-event should now exist for the "piano" sequence
        // too (additive — one event per track). Assert that the name is
        // unchanged (no prefix to strip).
        var trackName = track.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
        Assert.NotNull(trackName);
        Assert.Equal("piano", trackName!.Text);
    }

    private static void BuildAndWritePhase28PianoSong(string outPath)
    {
        var timesig = new TimeSignatureData(4, 4);
        var notes = new System.Collections.Generic.List<MusicalNoteData>
        {
            new MusicalNoteData('C', 4, 0, 4, false, null, false, 0.7),
            new MusicalNoteData('D', 4, 0, 4, false, null, false, 0.7),
        };
        var bar = new BarData(notes, timesig);
        var sequence = new SequenceData();
        sequence.AddBar(bar);

        var ctx = new MusicalContext { Tempo = 120.0, TimeSignature = timesig };
        var sequences = new System.Collections.Generic.Dictionary<string, SequenceData>
        {
            ["piano"] = sequence,
        };
        var section = new SectionData("main", sequences, ctx, null);
        var sectionRegistry = new System.Collections.Generic.Dictionary<string, SectionData>
        {
            ["main"] = section,
        };
        var song = new SongData(
            new System.Collections.Generic.List<SongSectionRef>
            {
                new SongSectionRef("main", 1),
            },
            sectionRegistry);

        MidiExport.WriteMidi(new System.Collections.Generic.List<Value>
        {
            Value.String(outPath),
            Value.Song(song),
        });
    }
}
