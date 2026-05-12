using System;
using System.Linq;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase28;

/// <summary>
/// Phase 28 (SPEC-1, SPEC-3) Plan 01 acceptance facts:
///
///   1. <see cref="Parser_AcceptsLegToken"/> — `leg` after a note produces Articulation.Legato
///      while neighbours stay Articulation.Normal.
///   2. <see cref="VoiceBlockParser_AcceptsBasicSyntax"/> — `| {voice C4w} {voice C5q D5q E5q F5q} |`
///      parses without errors.
///   3. <see cref="VoiceBlockCompiler_EmitsParallelBars"/> — the same source compiles to a
///      BarData whose ParallelVoices contains two child bars (1 whole-note + 4 quarters).
/// </summary>
public class VoiceBlockParserTests
{
    private const string Prelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";

    [Fact]
    public void Parser_AcceptsLegToken()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(Prelude + @"
Sequence s = | C4q leg D4q E4q |
");
        Assert.Equal(0, errorCount);

        var seq = runner.GetVariable("s").As<SequenceData>();
        Assert.NotEmpty(seq.Bars);
        var notes = seq.Bars[0].MusicalNotes;
        Assert.Equal(3, notes.Count);
        // First note keeps Legato; "leg" attaches to the preceding note (TryParseArticulation
        // is consumed at the end of NoteElement parsing inside the bar loop).
        Assert.Equal(Articulation.Legato, notes[0].Articulation);
        Assert.Equal(Articulation.Normal, notes[2].Articulation);
    }

    [Fact]
    public void VoiceBlockParser_AcceptsBasicSyntax()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(Prelude + @"
Sequence s = | {voice C4w} {voice C5q D5q E5q F5q} |
");
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public void VoiceBlockCompiler_EmitsParallelBars()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, _, errorCount) = runner.RunSource(Prelude + @"
Sequence s = | {voice C4w} {voice C5q D5q E5q F5q} |
");
        Assert.Equal(0, errorCount);

        var seq = runner.GetVariable("s").As<SequenceData>();
        Assert.Single(seq.Bars);
        var bar = seq.Bars[0];

        Assert.NotNull(bar.ParallelVoices);
        Assert.Equal(2, bar.ParallelVoices!.Count);

        // First voice: C4 whole note (single non-rest)
        var v1 = bar.ParallelVoices[0].MusicalNotes.Where(n => !n.IsRest).ToArray();
        Assert.Single(v1);
        Assert.Equal('C', v1[0].NoteName);
        Assert.Equal(4, v1[0].Octave);
        Assert.Equal((int)NoteValueType.Value.WHOLE, v1[0].DurationValue);

        // Second voice: C5 D5 E5 F5 quarters
        var v2 = bar.ParallelVoices[1].MusicalNotes.Where(n => !n.IsRest).ToArray();
        Assert.Equal(4, v2.Length);
        Assert.Equal(new[] { 'C', 'D', 'E', 'F' }, v2.Select(n => n.NoteName).ToArray());
        Assert.All(v2, n => Assert.Equal(5, n.Octave));
        Assert.All(v2, n => Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue));
    }
}
