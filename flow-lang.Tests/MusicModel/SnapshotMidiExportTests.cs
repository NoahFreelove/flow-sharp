using Flow.Music.IO;
using Flow.Music.Model;
using FlowLang.Core;
using FlowLang.Music;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.SpecialTypes;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Mono.Cecil;
using Xunit;
using NoteEvent = Flow.Music.Model.NoteEvent;

namespace FlowLang.Tests.MusicModel;

[Collection("FlowScripts")]
public class SnapshotMidiExportTests
{
    private static NoteEvent Note(double offset, double duration, int midi, string voice = "v",
        double overlap = 0, double portamento = 0, RationalDuration? exact = null) =>
        new(Guid.NewGuid(), voice, offset, duration, new('C', 4, 0, null, midi, 440),
            DurationOverlap: overlap, PortamentoMs: portamento, ExactDuration: exact);

    private static SequenceSnapshot Sequence(string name, double duration, params NoteEvent[] notes) =>
        new(Guid.NewGuid(), name, duration, notes, [new(Guid.NewGuid(), 0, duration, 4, 4, false)]);

    private static CompositionSnapshot Score(params (SectionSnapshot section, int repeats)[] placements) =>
        new(Guid.NewGuid(), placements.Select(p => new SectionPlacement(Guid.NewGuid(), p.section, p.repeats)));

    private static SectionSnapshot Section(SectionSettings settings, params SequenceSnapshot[] sequences) =>
        new(Guid.NewGuid(), "section", settings, sequences);

    private static SongData Evaluate(string source)
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate("use \"@std\"\n" + source + "\nsong", "midi.flow");
        Assert.True(result.Succeeded, engine.ErrorReporter.FormatErrors());
        return result.LastValue!.As<SongData>();
    }

    private static byte[] Legacy(SongData song)
    {
        var path = Path.Combine(Path.GetTempPath(), $"flow-snapshot-midi-{Guid.NewGuid():N}.mid");
        try
        {
            MidiExport.WriteMidi([Value.String(path), MusicValue.Song(song)]);
            return File.ReadAllBytes(path);
        }
        finally { File.Delete(path); }
    }

    private static MidiFile Read(byte[] bytes) => MidiFile.Read(new MemoryStream(bytes));

    private static List<(long on, long off, int key, int velocity, int channel)> Notes(TrackChunk track) =>
        track.GetNotes().Select(n => (n.Time, n.EndTime, (int)n.NoteNumber, (int)n.Velocity, (int)n.Channel)).ToList();

    [Fact]
    public void IoArtifactDependsOnlyOnModelMidiLibraryAndBcl()
    {
        using var module = ModuleDefinition.ReadModule(typeof(MidiCompositionExporter).Assembly.Location);
        Assert.Equal("Flow.Music.IO", module.Assembly.Name.Name);
        Assert.All(module.AssemblyReferences, r => Assert.True(r.Name is "Flow.Music.Model" or "Melanchall.DryWetMidi"
            or "netstandard" || r.Name.StartsWith("System", StringComparison.Ordinal), r.Name));
        Assert.DoesNotContain(module.GetTypeReferences(), t => t.Namespace.StartsWith("FlowLang", StringComparison.Ordinal));
    }

    public static TheoryData<string> LegacyCorpus => new()
    {
        // Chords, rests, triplets, drum routing, key and per-section tempo, repeats, shared tracks.
        """
        key Dmajor {
          tempo 96 { section intro {
            Sequence piano = | [D4 F#4 A4]q _q {3:2 E4e F#4e G4e}q A4q |
            Sequence drums = | C2q C2q _q C2q |
          } }
          tempo 140 { section verse { Sequence piano = | D5h. E5q | } }
        }
        Song song = [intro verse*2 intro]
        """,
        // Serial legato overlap and portamento controllers, routed programs.
        """
        tempo 132 { section verse {
          Sequence strings = (legato | D4q E4q F#4h | 0.5)
          Sequence violin = (portamento | A4h B4h | 80ms)
          Sequence brass = | G3w |
        } }
        Song song = [verse*3]
        """,
        // Parallel voice blocks without overlap/portamento, 3/4 meter, quintuplets, septuplets (TPQN 3360).
        """
        timesig 3/4 { key Gminor { section waltz {
          Sequence bass = | {voice G2h. } {voice Bb3q D4q G4q} |
          Sequence flute = | {5:4 G5s A5s Bb5s C6s D6s}q G5h |
          Sequence harp = | {7:4 G4s A4s Bb4s C5s D5s Eb5s F5s}q _h |
        } } }
        Song song = [waltz*2]
        """,
    };

    [Theory]
    [MemberData(nameof(LegacyCorpus))]
    public void FlowScoresExportBytesIdenticalToLegacyWriteMidi(string source)
    {
        var song = Evaluate(source);
        var expected = Legacy(song);
        var actual = MidiCompositionExporter.ToBytes(CompositionCompiler.Compile(song, "midi.flow"));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NativeScoreExportsReadableTracksTempoMapAndRouting()
    {
        var verse = Section(new(Bpm: 120, Key: "Ebmajor"),
            Sequence("sampler:violin", 4, Note(0, 1, 67), Note(1, 1, 69, overlap: 0.5)),
            Sequence("drums", 4, Note(0, 0.5, 36), Note(2, 0.5, 38)));
        var outro = Section(new(Bpm: 90, Key: "Ebmajor"), Sequence("Sampler:Violin", 2, Note(0, 2, 70, portamento: 100)));
        var file = Read(MidiCompositionExporter.ToBytes(Score((verse, 2), (outro, 1))));

        Assert.Equal(480, ((TicksPerQuarterNoteTimeDivision)file.TimeDivision).TicksPerQuarterNote);
        var tracks = file.GetTrackChunks().ToArray();
        Assert.Equal(3, tracks.Length); // conductor + case-insensitively shared violin + drums
        var tempo = file.GetTempoMap();
        Assert.Equal(500_000, tempo.GetTempoAtTime(new MidiTimeSpan(0)).MicrosecondsPerQuarterNote);
        Assert.Equal(666_666, tempo.GetTempoAtTime(new MidiTimeSpan(3840)).MicrosecondsPerQuarterNote);
        var key = Assert.Single(tracks[0].Events.OfType<KeySignatureEvent>());
        Assert.Equal((-3, 0), ((int)key.Key, (int)key.Scale));

        var names = tracks.Skip(1).Select(t => t.Events.OfType<SequenceTrackNameEvent>().Single().Text).ToArray();
        Assert.Equal(["violin", "drums"], names);
        Assert.Equal(40, (int)tracks[1].Events.OfType<ProgramChangeEvent>().Single().ProgramNumber);
        Assert.Equal(9, (int)tracks[2].Events.OfType<ProgramChangeEvent>().Single().Channel);
        Assert.Equal(
            [(0L, 480L, 67, 80, 0), (480, 1200, 69, 80, 0), (1920, 2400, 67, 80, 0), (2400, 3120, 69, 80, 0), (3840, 4800, 70, 80, 0)],
            Notes(tracks[1]));
        Assert.Equal([(0L, 240L, 36, 80, 9), (960, 1200, 38, 80, 9), (1920, 2160, 36, 80, 9), (2880, 3120, 38, 80, 9)],
            Notes(tracks[2]));
        var controls = tracks[1].GetTimedEvents().Where(e => e.Event is ControlChangeEvent)
            .Select(e => (e.Time, (int)((ControlChangeEvent)e.Event).ControlNumber, (int)((ControlChangeEvent)e.Event).ControlValue));
        Assert.Equal([(3840L, 65, 127), (3840, 5, 64), (4800, 65, 0)], controls);
    }

    [Fact]
    public void ScoreTimingDivergesFromLegacyWhereLegacyDisagreesWithAudio()
    {
        // An overfull monophonic bar is nine quarters long in the audio timeline; legacy MIDI
        // advances the next section by the 4/4 capacity, overlapping the repeat.
        var song = Evaluate("section run { Sequence piano = | C4q D4q E4q F4q G4q A4q B4q C5h | }\nSong song = [run*2]");
        static long[] Onsets(byte[] midi, int key) =>
            Notes(Read(midi).GetTrackChunks().ElementAt(1)).Where(n => n.key == key).Select(n => n.on).ToArray();
        Assert.Equal([0L, 1920], Onsets(Legacy(song), 60));
        Assert.Equal([0L, 9 * 480], Onsets(MidiCompositionExporter.ToBytes(CompositionCompiler.Compile(song, "run")), 60));

        // Voice-block notes receive the same overlap extension as serial notes.
        var voices = Evaluate("section v { Sequence piano = (legato | {voice C4h C4h} {voice E4w} | 0.5) }\nSong song = [v]");
        static long FirstOff(byte[] midi) => Notes(Read(midi).GetTrackChunks().ElementAt(1)).First(n => n.key == 60).off;
        Assert.Equal(960, FirstOff(Legacy(voices)));
        Assert.Equal(1440, FirstOff(MidiCompositionExporter.ToBytes(CompositionCompiler.Compile(voices, "v"))));
    }

    [Fact]
    public void TicksRoundFromAbsoluteScorePositionsAndClampBeforeTheSongStart()
    {
        // Note-offs round from absolute ends too, so a repeated key never ends its successor early.
        var sequence = Sequence("piano", 4, Note(-0.25, 1, 60), Note(0.75, 1, 60), Note(0.7, 0.3, 62),
            Note(2.1, 0.7, 64), Note(3 + 10.5 / 480, 10.5 / 480, 65), Note(3 + 21.0 / 480, 0.5, 65));
        var file = Read(MidiCompositionExporter.ToBytes(Score((Section(new(), sequence), 1))));
        Assert.Equal([(0L, 360L, 60, 80, 0), (336, 480, 62, 80, 0), (360, 840, 60, 80, 0), (1008, 1344, 64, 80, 0),
            (1451, 1461, 65, 80, 0), (1461, 1701, 65, 80, 0)], Notes(file.GetTrackChunks().ElementAt(1)));
    }

    [Fact]
    public void MeterAndKeyChangesAreEmittedAtTheirScorePositions()
    {
        var waltz = new SequenceSnapshot(Guid.NewGuid(), "piano", 6, [Note(0, 3, 60), Note(3, 3, 62)],
            [new(Guid.NewGuid(), 0, 3, 3, 4, false), new(Guid.NewGuid(), 3, 3, 3, 4, false)]);
        var march = new SequenceSnapshot(Guid.NewGuid(), "piano", 4, [Note(0, 4, 64)],
            [new(Guid.NewGuid(), 0, 2, 2, 4, false), new(Guid.NewGuid(), 2, 2, 4, 8, false)]);
        var file = Read(MidiCompositionExporter.ToBytes(Score(
            (Section(new(Key: "Cmajor"), waltz), 2), (Section(new(Key: "Aminor"), march), 1))));
        var conductor = file.GetTrackChunks().First().GetTimedEvents().ToArray();
        Assert.Equal([(0L, 3, 4), (5760, 2, 4), (6720, 4, 8)], conductor.Where(e => e.Event is TimeSignatureEvent)
            .Select(e => (e.Time, (int)((TimeSignatureEvent)e.Event).Numerator, (int)((TimeSignatureEvent)e.Event).Denominator)));
        Assert.Equal([(0L, 0, 0), (5760, 0, 1)], conductor.Where(e => e.Event is KeySignatureEvent)
            .Select(e => (e.Time, (int)((KeySignatureEvent)e.Event).Key, (int)((KeySignatureEvent)e.Event).Scale)));
    }

    [Fact]
    public void TupletResolutionElevatesTicksAndRejectsUnsupportedScoresBeforeWriting()
    {
        CompositionSnapshot Tuplet(int denominator) => Score((Section(new(),
            Sequence("piano", 4, Note(0, 2.0 / denominator, 60, exact: new(2, denominator)))), 1));
        Assert.Equal(3360, ((TicksPerQuarterNoteTimeDivision)Read(MidiCompositionExporter.ToBytes(Tuplet(7))).TimeDivision).TicksPerQuarterNote);

        using var stream = new MemoryStream();
        var error = Assert.Throws<InvalidOperationException>(() => MidiCompositionExporter.Write(Score((Section(new(),
            Sequence("piano", 4, Note(0, 1, 60, exact: new(1, 11)), Note(1, 1, 62, exact: new(1, 13)))), 1)), stream));
        Assert.Contains("exceeds cap 9600", error.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => MidiCompositionExporter.Write(
            Score((Section(new(), Sequence("piano", 4, Note(0, 1, 128))), 1)), stream));
        var wideBar = new SequenceSnapshot(Guid.NewGuid(), "piano", 4, [Note(0, 1, 60)], [new(Guid.NewGuid(), 0, 4, 256, 4, false)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => MidiCompositionExporter.Write(Score((Section(new(), wideBar), 1)), stream));
        // SMF delta times are 28-bit; refuse positions no reader can represent.
        Assert.Throws<InvalidOperationException>(() => MidiCompositionExporter.Write(
            Score((Section(new(), Sequence("piano", 600_000, Note(599_999, 1, 60))), 1)), stream));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => MidiCompositionExporter.Write(
            Score((Section(new(), Sequence("piano", 4, Note(0, 1, 60))), 1)), stream, cancelled.Token));
        Assert.Equal(0, stream.Length);
    }
}
