using Flow.Music.Model;
using Flow.Music.Model.Editing;
using FlowLang.Core;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.MusicModel;

[Collection("FlowScripts")]
public class NoteEditingTests
{
    private static NoteEvent Note(double offset, double duration = 0.5, int midi = 60) =>
        new(Guid.NewGuid(), "v", offset, duration, new('C', 4, 0, null, midi, 261.6), Velocity: 0.7,
            Articulation: NoteArticulation.Tenuto, Origin: new("score", 3, 4));

    [Theory]
    [InlineData(0.3, 1.0, 0.0, 0.5)]
    [InlineData(0.2, 1.0, 0.0, 0.0)]
    [InlineData(0.25, 1.0, 0.0, 0.5)]   // halfway snaps later
    [InlineData(0.3, 0.5, 0.0, 0.4)]    // strength interpolates from the real onset
    [InlineData(0.5, 1.0, 0.5, 0.625)]  // odd grid positions swing by swing × grid/2
    [InlineData(1.0, 1.0, 0.5, 1.0)]    // even grid positions stay put
    [InlineData(-0.2, 1.0, 0.5, 0.0)]
    [InlineData(-0.3, 1.0, 0.5, -0.375)]   // swing always shifts later
    public void OnsetSnapsRealPositionsToTheGrid(double onset, double strength, double swing, double expected)
    {
        Assert.Equal(expected, Quantization.Onset(onset, new(0.5, strength, swing)), 12);
    }

    [Fact]
    public void ApplyAnchorsTheGridToEachBarAndMovesChordsTogether()
    {
        // A half-quarter pickup, then a 3/4 bar starting at 0.5.
        var bars = new BarSpan[] { new(Guid.NewGuid(), 0, 0.5, 1, 8, true), new(Guid.NewGuid(), 0.5, 3, 3, 4, false) };
        var chord = new[] { Note(1.2), Note(1.2, midi: 64), Note(1.2, midi: 67) };
        var source = new SequenceSnapshot(Guid.NewGuid(), "piano", 3.5, [Note(0.1), .. chord, Note(2.05)], bars);

        var result = Quantization.Apply(source, new(GridQuarters: 0.5, Swing: 0.5));

        Assert.Equal([0.0, 1.125, 1.125, 1.125, 2.125], result.Notes.Select(n => n.OffsetQuarters));
        Assert.Equal(source.Notes.Select(n => n with { OffsetQuarters = 0 }),
            result.Notes.Select(n => n with { OffsetQuarters = 0 }));
        Assert.Equal(source.Bars, result.Bars);
        Assert.Equal((source.Id, source.Name, source.DurationQuarters), (result.Id, result.Name, result.DurationQuarters));
        Assert.Equal(1.2, source.Notes[1].OffsetQuarters); // input untouched
    }

    [Fact]
    public void InvalidSettingsAreRejected()
    {
        var sequence = new SequenceSnapshot(Guid.NewGuid(), "piano", 1, [Note(0)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => Quantization.Apply(sequence, new(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Quantization.Apply(sequence, new(0.5, Strength: 1.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Quantization.Apply(sequence, new(0.5, Swing: -2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Quantization.Onset(double.NaN, new(0.5)));
    }

    private static SequenceData Evaluate(string source)
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate("use \"@std\"\nuse \"@notation\"\n" + source, "quantize.flow");
        Assert.True(result.Succeeded, engine.ErrorReporter.FormatErrors());
        return result.LastValue!.As<SequenceData>();
    }

    private static double[] Onsets(SequenceData sequence) =>
        sequence.ToTimeline().SelectMany(b => b.bar.ToTimeline().Select(n => Math.Round(b.offsetBeats + n.offsetBeats, 9))).ToArray();

    [Fact]
    public void FlowQuantizeSwingsOffbeatGridPositionsNotEveryOtherNote()
    {
        // Old behavior swung note #2 (D, on beat 2) because it was the second note.
        Assert.Equal([0.0, 1.0, 1.625, 2.0], Onsets(Evaluate("(quantize | C4q D4e E4e F4h | EIGHTH 1.0 0.5)")));
    }

    [Fact]
    public void FlowQuantizeKeepsChordTonesTogether()
    {
        // Old behavior advanced its grid cursor over chord tones, smearing the chord.
        Assert.Equal([0.0, 0.0, 0.0, 0.625, 1.0, 1.625, 2.0],
            Onsets(Evaluate("(quantize | [C4 E4 G4]e D4e E4e F4e _h | EIGHTH 1.0 0.5)")));
    }

    [Fact]
    public void FlowQuantizeStartsFromTheRealOnset()
    {
        // Partial strength pulls existing swing halfway back toward the straight grid.
        var swung = "(quantize | C4e D4e E4e F4e _h | EIGHTH 1.0 0.5)";
        Assert.Equal([0.0, 0.5625, 1.0, 1.5625, 2.0], Onsets(Evaluate($"(quantize {swung} EIGHTH 0.5 0.0)")));
    }

    [Fact]
    public void FlowQuantizeOfStraightEvenRhythmsIsUnchanged()
    {
        Assert.Equal([0.0, 0.575, 1.0, 1.575, 2.0, 2.575, 3.0, 3.575],
            Onsets(Evaluate("(quantize | C4e D4e E4e F4e G4e A4e B4e C5e | EIGHTH 1.0 0.3)")));
    }

    [Theory]
    [InlineData('B', 3, 0, 1, 'C', 4, 0, 60, false)]
    [InlineData('E', 4, -1, 2, 'F', 4, 0, 65, false)]
    [InlineData('A', 4, 0, -2, 'G', 4, 0, 67, false)]
    [InlineData('C', 4, 0, 1, 'C', 4, 1, 61, false)]    // sharps spelling
    [InlineData('C', 10, 0, 12, 'E', 10, 0, 136, true)] // clamped to E10
    [InlineData('F', 0, 0, -12, 'E', 0, 0, 16, true)]   // clamped to E0
    public void TransposeRespellsWithSharpsAndClampsToTheFlowRange(char letter, int octave, int alteration,
        int semitones, char expectedLetter, int expectedOctave, int expectedAlteration, int midi, bool clamped)
    {
        var result = Transposition.Transpose(letter, octave, alteration, semitones);
        Assert.Equal((expectedLetter, expectedOctave, expectedAlteration, midi, clamped),
            (result.Letter, result.Octave, result.Alteration, result.MidiKey, result.Clamped));
    }

    [Theory]
    [InlineData(150, 1, 50)]
    [InlineData(-150, -1, -50)]
    [InlineData(50, 0, 50)]
    [InlineData(200, 2, 0)]
    public void CentsSplitTowardZero(double cents, int semitones, double remainder)
    {
        Assert.Equal((semitones, remainder), Transposition.SplitCents(cents));
    }

    [Fact]
    public void ApplyTransposesSnapshotPitchesAndRescalesFrequency()
    {
        var plain = new NoteEvent(Guid.NewGuid(), "v", 0, 1, new('C', 4, 0, null, 60, 261.6255653005986), Velocity: 0.4);
        var bent = new NoteEvent(Guid.NewGuid(), "v", 1, 1, new('A', 4, 0, 10, 69, 440));
        var rest = new NoteEvent(Guid.NewGuid(), "v", 2, 1, null);
        var source = new SequenceSnapshot(Guid.NewGuid(), "lead", 3, [plain, bent, rest]);

        var result = Transposition.Apply(source, semitones: 3, cents: 50);

        Assert.Equal(new NotePitch('D', 4, 1, 50, 63, 261.6255653005986 * Math.Pow(2, 3.5 / 12)), result.Notes[0].Pitch);
        Assert.Equal(new NotePitch('C', 5, 0, 60, 72, 440 * Math.Pow(2, 3.5 / 12)), result.Notes[1].Pitch);
        Assert.Null(result.Notes[2].Pitch);
        Assert.Equal(plain with { Pitch = null }, result.Notes[0] with { Pitch = null });
        Assert.Equal(60, source.Notes[0].Pitch!.MidiKey);

        var custom = Transposition.Apply(source, 12, frequency: p => p.MidiKey);
        Assert.Equal([72.0, 81.0], custom.Notes.Take(2).Select(n => n.Pitch!.FrequencyHz));
    }

    [Fact]
    public void FlowTransposeMatchesTheSharedTransform()
    {
        var original = Evaluate("| C4q Eb4q [G4 B4]q _q |");
        var transposed = Evaluate("(transpose | C4q Eb4q [G4 B4]q _q | 5)");
        static IEnumerable<(char, int, int, int)> Pitches(SequenceData s) => s.Bars.SelectMany(b => b.MusicalNotes)
            .Where(n => !n.IsRest).Select(n => (n.NoteName, n.Octave, n.Alteration,
                FlowLang.StandardLibrary.Audio.PitchConversion.GetMidiNote(n.NoteName, n.Octave, n.Alteration)));
        var expected = Pitches(original).Select(p => Transposition.Transpose(p.Item1, p.Item2, p.Item3, 5))
            .Select(t => (t.Letter, t.Octave, t.Alteration, t.MidiKey));
        Assert.Equal(expected, Pitches(transposed));
        Assert.Equal([('F', 4, 0, 65), ('G', 4, 1, 68), ('C', 5, 0, 72), ('E', 5, 0, 76)], Pitches(transposed));
    }
}
