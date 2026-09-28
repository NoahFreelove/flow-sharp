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
}
