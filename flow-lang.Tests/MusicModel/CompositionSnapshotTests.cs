using Flow.Music.Model;
using FlowLang.Core;
using FlowLang.Music;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.Syntax;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using Mono.Cecil;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class CompositionSnapshotTests
{
    private static SongData Song(BarData bar, MusicalContext? context = null, int repeats = 1)
    {
        var sequence = new SequenceData();
        sequence.AddBar(bar);
        return new(new() { new("verse", repeats) }, new()
        {
            ["verse"] = new("verse", new() { ["melody"] = sequence }, context),
        });
    }

    [Fact]
    public void ModelArtifactReferencesOnlyBclAndContainsNoRuntimeTypes()
    {
        using var module = ModuleDefinition.ReadModule(typeof(CompositionSnapshot).Assembly.Location);
        Assert.Equal("Flow.Music.Model", module.Assembly.Name.Name);
        Assert.All(module.AssemblyReferences, reference =>
            Assert.True(reference.Name == "netstandard" || reference.Name.StartsWith("System", StringComparison.Ordinal), reference.Name));
        Assert.DoesNotContain(module.GetTypeReferences(), t => t.Namespace.StartsWith("FlowLang", StringComparison.Ordinal));
    }

    [Fact]
    public void CompilerDetachesCollectionsContextAndResolvedTuning()
    {
        var note = new MusicalNoteData('E', 4, 0, (int)NoteValueType.Value.QUARTER, false, sourceLocation: new(3, 7, "song.flow"), sourceLength: 3);
        var bar = new BarData(new[] { note }, new(4, 4));
        var context = new MusicalContext { Tempo = 90, Pan = -0.2, Key = "Cmajor" };
        context.TuningStack.Push(new(TuningSystem.JustIntonation, Mode.Major, 'C', 0));
        var song = Song(bar, context, 2);
        var snapshot = CompositionCompiler.Compile(song, "song.flow");
        var same = CompositionCompiler.Compile(song, "song.flow");
        var section = Assert.Single(snapshot.Placements).Section;
        var frozen = Assert.Single(Assert.Single(section.Sequences).Notes);
        Assert.Equal(PitchConversion.NoteToFrequency(note, context.ActiveTuning), frozen.Pitch!.FrequencyHz);
        Assert.Equal(new SourceOrigin("song.flow", 3, 7, 3), frozen.Origin);
        Assert.Equal(frozen.Id, same.Placements[0].Section.Sequences[0].Notes[0].Id);
        context.Tempo = 300;
        context.TuningStack.Clear();
        bar.MusicalNotes.Clear();
        song.SectionRegistry.Clear();
        song.Sections.Clear();
        Assert.Equal(90, section.Settings.Bpm);
        Assert.Single(section.Sequences[0].Notes);
        Assert.Equal(2, snapshot.Timeline().Count());
        Assert.Equal(16.0 / 3, snapshot.DurationSeconds, 10);
    }

    [Fact]
    public void TupletsOverlapsArticulationAndParallelVoicesSurviveCompilation()
    {
        var note = new MusicalNoteData('C', 4, 0, null, false, isTied: true,
            articulation: Articulation.Legato, durationFraction: new Fraction(2, 3),
            onsetOffset: -0.1, durationOverlap: 0.5, portamentoMs: 80);
        var voice = new BarData(new[] { note }, new(6, 8));
        var bar = new BarData(Array.Empty<MusicalNoteData>(), new(6, 8))
            { ParallelVoices = new() { voice, new(new[] { note }, new(6, 8)) } };
        var snapshot = CompositionCompiler.Compile(Song(bar), "poly");
        var sequence = snapshot.Placements[0].Section.Sequences[0];
        Assert.Equal(3, sequence.DurationQuarters);
        Assert.Equal(2, sequence.Notes.Count);
        Assert.NotEqual(sequence.Notes[0].VoiceId, sequence.Notes[1].VoiceId);
        Assert.All(sequence.Notes, n =>
        {
            Assert.Equal(new RationalDuration(2, 3), n.ExactDuration);
            Assert.Equal(2.0 / 3, n.DurationQuarters);
            Assert.Equal(-0.1, n.OffsetQuarters);
            Assert.Equal(NoteArticulation.Legato, n.Articulation);
            Assert.Equal(0.5, n.DurationOverlap);
            Assert.Equal(80, n.PortamentoMs);
            Assert.True(n.IsTied);
        });
    }

    [Fact]
    public void NativeAndFlowAuthoredScoresHaveTheSameEvaluatedNotes()
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate("use \"@std\"; section verse { Sequence melody = | C4q D4q | } Song song = [verse]; song", "score.flow");
        Assert.True(result.Succeeded, engine.ErrorReporter.FormatErrors());
        var fromFlow = CompositionCompiler.Compile(result.LastValue!.As<SongData>(), "score");
        var native = Song(new(new[] { new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false), new MusicalNoteData('D', 4, 0, (int)NoteValueType.Value.QUARTER, false) }, new(4, 4)));
        var fromNative = CompositionCompiler.Compile(native, "score");
        var flowNotes = fromFlow.Placements[0].Section.Sequences[0].Notes;
        var nativeNotes = fromNative.Placements[0].Section.Sequences[0].Notes;
        Assert.Equal(nativeNotes.Select(n => (n.OffsetQuarters, n.DurationQuarters, n.Pitch)),
            flowNotes.Select(n => (n.OffsetQuarters, n.DurationQuarters, n.Pitch)));
    }

    [Fact]
    public void NativeModelOwnsInputListsAndPreservesTempoChangesAcrossRepeats()
    {
        var notes = new List<NoteEvent> { new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440)) };
        var sequence = new SequenceSnapshot(Guid.NewGuid(), "melody", 4, notes);
        notes.Clear();
        var first = new SectionSnapshot(Guid.NewGuid(), "first", new(Bpm: 120), new[] { sequence });
        var second = new SectionSnapshot(Guid.NewGuid(), "second", new(Bpm: 60), new[] { sequence });
        var model = new CompositionSnapshot(Guid.NewGuid(), new[]
        {
            new SectionPlacement(Guid.NewGuid(), first, 2), new SectionPlacement(Guid.NewGuid(), second),
        });
        Assert.Single(sequence.Notes);
        Assert.Equal(new[] { 0.0, 2, 4 }, model.Timeline().Select(s => s.OffsetSeconds));
        Assert.Equal(8, model.DurationSeconds);
        Assert.Equal(1.5, Timing.QuartersToSeconds(3, 120));
        Assert.Equal(3, Timing.SecondsToQuarters(1.5, 120));
        Assert.Throws<ArgumentOutOfRangeException>(() => Timing.QuartersToSeconds(1, double.NaN));
    }

    [Fact]
    public void CompilationHonorsCancellation()
    {
        var song = Song(new(Array.Empty<MusicalNoteData>(), new(4, 4)));
        Assert.Throws<OperationCanceledException>(() => CompositionCompiler.Compile(song, "cancel",
            new CancellationToken(canceled: true)));
    }
}
