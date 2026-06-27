using System;
using System.IO;
using System.Linq;
using FlowLang.Tests.Fixtures;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Xunit;

namespace FlowLang.Tests.Integration.Phase28;

/// <summary>
/// Phase 28 (SPEC-6) Plan 04 acceptance facts pinning multi-track MIDI export
/// via <c>writeMidi</c>:
///
///   • Chunk count = 1 conductor + N uniqueSequenceName tracks
///   • Each non-conductor track has its own ProgramChange matching the
///     prefix-based GM mapping
///   • Drum sequences route to MIDI channel 9 (GM percussion) — every
///     NoteOn/NoteOff in the drum track lands on channel 9
///   • Cross-section same-name sequences concatenate onto the SAME track
///     in chronological tick order
///   • Track content isolation — each track only carries its own sequence's
///     notes (no cross-track leakage)
///
/// All Facts use <see cref="FlowEngineRunner"/> to compile + render Flow
/// source, write a .mid file under tests/output/, then read it back with
/// DryWetMidi to walk events.
/// </summary>
[Collection("FlowScripts")]
public class MultiTrackMidiTests
{
    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    /// <summary>
    /// Mirrors Phase 22 PortamentoMidiFacts.RunAndWriteMidi: source contains
    /// `{{OUTPATH}}` placeholder substituted before the engine runs. Output goes
    /// to system temp to keep tests/ clean and avoid colliding with byte-pin
    /// tests that read from tests/output/.
    /// </summary>
    private static string RunAndWriteMidi(string flowSource, string outName)
    {
        string outDir = Path.Combine(Path.GetTempPath(), "flow_phase28_multitrack");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, $"{outName}_{Guid.NewGuid():N}.mid");
        if (File.Exists(outPath)) File.Delete(outPath);

        string source = flowSource.Replace("{{OUTPATH}}", outPath.Replace("\\", "/"));
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nstdout:\n{stdout}\nsource:\n{source}");
        Assert.True(File.Exists(outPath), $"writeMidi did not produce {outPath}; stderr={stderr}");
        return outPath;
    }

    /// <summary>
    /// Source with three sequences (piano, brass, drums) in one section. Tests 1-3 use this.
    /// writeMidi sits INSIDE the tempo/timesig blocks so `piece` is in scope
    /// (musical context blocks are lexical scopes — variables don't escape upward).
    /// {{OUTPATH}} is substituted by RunAndWriteMidi.
    /// </summary>
    private const string ThreeSequenceSource = Prelude + @"
tempo 120 {
    timesig 4/4 {
        Sequence pianoSeq = | C4q D4q E4q F4q |
        Sequence brassSeq = | E4q F4q G4q A4q |
        Sequence drumsSeq = | C2q D2q C2q D2q |

        section main {
            Sequence piano = pianoSeq
            Sequence brass = brassSeq
            Sequence drums = drumsSeq
        }

        Song piece = [main]
        (writeMidi ""{{OUTPATH}}"" piece)
    }
}
";

    [Fact]
    public void MultiTrackMidi_ChunkCount()
    {
        string path = RunAndWriteMidi(ThreeSequenceSource, nameof(MultiTrackMidi_ChunkCount));
        var midi = MidiFile.Read(path);
        // 1 conductor + 3 sequence tracks (piano, brass, drums)
        Assert.Equal(4, midi.Chunks.Count);
    }

    [Fact]
    public void MultiTrackMidi_ProgramChange()
    {
        string path = RunAndWriteMidi(ThreeSequenceSource, nameof(MultiTrackMidi_ProgramChange));
        var midi = MidiFile.Read(path);
        var trackChunks = midi.Chunks.OfType<TrackChunk>().Skip(1).ToArray(); // skip conductor
        Assert.Equal(3, trackChunks.Length);

        // Tracks land in insertion order: piano (first encountered) → brass → drums
        // Each non-conductor track must contain a ProgramChange event matching
        // the prefix-based GM mapping.
        var expected = new (int Program, int Channel)[]
        {
            (0, 0),   // piano   → GM 0,  channel 0
            (56, 0),  // brass   → GM 56, channel 0
            (0, 9),   // drums   → GM 0,  channel 9 (GM percussion)
        };

        for (int i = 0; i < trackChunks.Length; i++)
        {
            var pc = trackChunks[i].Events.OfType<ProgramChangeEvent>().FirstOrDefault();
            Assert.NotNull(pc);
            Assert.Equal(expected[i].Program, (int)(byte)pc!.ProgramNumber);
            Assert.Equal(expected[i].Channel, (int)(byte)pc.Channel);
        }
    }

    [Fact]
    public void MultiTrackMidi_DrumChannel9()
    {
        string path = RunAndWriteMidi(ThreeSequenceSource, nameof(MultiTrackMidi_DrumChannel9));
        var midi = MidiFile.Read(path);
        var trackChunks = midi.Chunks.OfType<TrackChunk>().Skip(1).ToArray();
        Assert.Equal(3, trackChunks.Length);

        // The drums track is the third (insertion order). Walk every NoteOn/NoteOff
        // and assert all land on channel 9.
        var drumTrack = trackChunks[2];
        var drumNotes = drumTrack.Events
            .Where(e => e is NoteOnEvent || e is NoteOffEvent)
            .Cast<ChannelEvent>()
            .ToArray();
        Assert.NotEmpty(drumNotes);
        Assert.All(drumNotes, e => Assert.Equal(9, (int)(byte)e.Channel));
    }

    [Fact]
    public void MultiTrackMidi_CrossSection()
    {
        // Two sections, each containing a sequence named "melody". The export
        // must produce ONE "melody" track with notes from both sections in
        // chronological tick order.
        const string source = Prelude + @"
tempo 120 {
    timesig 4/4 {
        Sequence partA = | C4q D4q E4q F4q |
        Sequence partB = | G4q A4q B4q C5q |

        section first  { Sequence melody = partA }
        section second { Sequence melody = partB }

        Song piece = [first second]
        (writeMidi ""{{OUTPATH}}"" piece)
    }
}
";
        string path = RunAndWriteMidi(source, nameof(MultiTrackMidi_CrossSection));
        var midi = MidiFile.Read(path);
        Assert.Equal(2, midi.Chunks.Count); // 1 conductor + 1 "melody" track

        var trackChunks = midi.Chunks.OfType<TrackChunk>().Skip(1).ToArray();
        Assert.Single(trackChunks);
        var melodyTrack = trackChunks[0];

        // NoteOn events with absolute ticks, sorted by occurrence
        var noteOns = melodyTrack.GetTimedEvents()
            .Where(te => te.Event is NoteOnEvent)
            .OrderBy(te => te.Time)
            .Select(te => (Tick: te.Time, NoteNumber: (int)(byte)((NoteOnEvent)te.Event).NoteNumber))
            .ToArray();
        Assert.Equal(8, noteOns.Length); // 4 from first + 4 from second

        // First section's notes (C4=60, D4=62, E4=64, F4=65) come BEFORE
        // second section's notes (G4=67, A4=69, B4=71, C5=72) in tick order.
        var firstSectionNotes = noteOns.Take(4).Select(n => n.NoteNumber).ToArray();
        var secondSectionNotes = noteOns.Skip(4).Take(4).Select(n => n.NoteNumber).ToArray();
        Assert.Equal(new[] { 60, 62, 64, 65 }, firstSectionNotes);
        Assert.Equal(new[] { 67, 69, 71, 72 }, secondSectionNotes);

        // The first second-section note's tick must be GREATER than the last
        // first-section note's tick (chronological accumulation).
        Assert.True(noteOns[4].Tick > noteOns[3].Tick,
            $"second section's first note (tick {noteOns[4].Tick}) must come after first section's last (tick {noteOns[3].Tick})");
    }

    [Fact]
    public void MultiTrackMidi_OnlyOneSequencePerTrack()
    {
        // Content isolation: each non-conductor track only carries its own
        // sequence's MIDI notes. Sanity check via NoteNumber sets.
        string path = RunAndWriteMidi(ThreeSequenceSource, nameof(MultiTrackMidi_OnlyOneSequencePerTrack));
        var midi = MidiFile.Read(path);
        var trackChunks = midi.Chunks.OfType<TrackChunk>().Skip(1).ToArray();
        Assert.Equal(3, trackChunks.Length);

        // Piano: C4=60, D4=62, E4=64, F4=65
        var pianoNotes = trackChunks[0].Events.OfType<NoteOnEvent>()
            .Select(n => (int)(byte)n.NoteNumber).Distinct().OrderBy(n => n).ToArray();
        Assert.Equal(new[] { 60, 62, 64, 65 }, pianoNotes);

        // Brass: E4=64, F4=65, G4=67, A4=69
        var brassNotes = trackChunks[1].Events.OfType<NoteOnEvent>()
            .Select(n => (int)(byte)n.NoteNumber).Distinct().OrderBy(n => n).ToArray();
        Assert.Equal(new[] { 64, 65, 67, 69 }, brassNotes);

        // Drums: C2=36, D2=38
        var drumNotes = trackChunks[2].Events.OfType<NoteOnEvent>()
            .Select(n => (int)(byte)n.NoteNumber).Distinct().OrderBy(n => n).ToArray();
        Assert.Equal(new[] { 36, 38 }, drumNotes);
    }
}
