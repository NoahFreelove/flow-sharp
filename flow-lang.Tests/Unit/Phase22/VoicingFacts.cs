using FlowLang.StandardLibrary.Harmony;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase22;

/// <summary>
/// DX-11 acceptance Facts pinning the inversion(Chord, Int) and voicing(Chord, String)
/// chord-shape transforms. Decisions referenced (locked in 22-CONTEXT.md):
///   D-07 — charitable interpretation: incomplete chords return input unchanged.
///   D-08 — voicing functions document the charitable behavior in their doc comment.
///
/// The Voicings.* helpers are pure static methods over ChordData (no engine state),
/// so most facts call them directly — only Inversion_RegisteredViaEngine and
/// Voicing_RegisteredViaEngine route through FlowEngineRunner to confirm the
/// registry hookup. Direct C# calls give us byte-precise NoteNames assertions
/// without S-expression overhead.
///
/// Phase 22 plan 22-03 — RED state: the file references the not-yet-existing
/// Voicings static class. Until Task 2 lands Voicings.cs the project will not
/// compile and every Fact is implicitly RED.
/// </summary>
[Collection("FlowScripts")]
public class VoicingFacts
{
    /// <summary>
    /// Helper: builds a ChordData from explicit note-name list. Mirrors the format
    /// produced by ChordParser (per-note octaves, "+" accidentals).
    /// </summary>
    private static ChordData Make(string root, string quality, int octave, params string[] notes) =>
        new(root, quality, octave, notes);

    private static ChordData Cmaj_Triad() => Make("C", "maj", 4, "C4", "E4", "G4");
    private static ChordData Cmaj7_FourNote() => Make("C", "maj7", 4, "C4", "E4", "G4", "B4");
    private static ChordData FsDim_Triad() => Make("Fs", "dim", 4, "F4+", "A4", "C5");
    private static ChordData Dyad() => Make("C", "maj", 4, "C4", "E4");

    [Fact]
    public void FirstInversion_RaisesLowestNoteOctave()
    {
        // (inversion Cmaj 1) → C goes up an octave: ["E4", "G4", "C5"]
        var result = Voicings.Inversion(Cmaj_Triad(), 1);
        Assert.Equal(new[] { "E4", "G4", "C5" }, result.NoteNames);
    }

    [Fact]
    public void SecondInversion_RaisesTwoLowestNotes()
    {
        // (inversion Cmaj 2) → ["G4", "C5", "E5"]
        var result = Voicings.Inversion(Cmaj_Triad(), 2);
        Assert.Equal(new[] { "G4", "C5", "E5" }, result.NoteNames);
    }

    [Fact]
    public void Inversion_NEqualsZero_ReturnsUnchanged()
    {
        // n=0 → identity
        var input = Cmaj_Triad();
        var result = Voicings.Inversion(input, 0);
        Assert.Equal(input.NoteNames, result.NoteNames);
    }

    [Fact]
    public void Inversion_NGreaterEqualNoteCount_ReturnsUnchanged()
    {
        // RESEARCH Open Question 3 charitable resolution: n >= NoteNames.Length → unchanged.
        var input = Cmaj_Triad();
        var result = Voicings.Inversion(input, 5);
        Assert.Equal(input.NoteNames, result.NoteNames);
    }

    [Fact]
    public void Inversion_NegativeN_ReturnsUnchanged()
    {
        // D-07 charitable interpretation: negative n → unchanged.
        var input = Cmaj_Triad();
        var result = Voicings.Inversion(input, -1);
        Assert.Equal(input.NoteNames, result.NoteNames);
    }

    [Fact]
    public void Drop2_LowersSecondFromTop()
    {
        // (voicing Cmaj7 "drop2") on ["C4","E4","G4","B4"] → G drops octave to G3,
        // re-sorted by pitch: ["G3","C4","E4","B4"].
        var result = Voicings.Voicing(Cmaj7_FourNote(), "drop2");
        Assert.Equal(new[] { "G3", "C4", "E4", "B4" }, result.NoteNames);
    }

    [Fact]
    public void Drop2_OnTriad_ReturnsUnchanged()
    {
        // D-07: drop2 on 3-note triad → input unchanged (no error).
        var input = Cmaj_Triad();
        var result = Voicings.Voicing(input, "drop2");
        Assert.Equal(input.NoteNames, result.NoteNames);
    }

    [Fact]
    public void Drop3_LowersThirdFromTop()
    {
        // (voicing Cmaj7 "drop3") on ["C4","E4","G4","B4"] → E drops octave to E3,
        // re-sorted: ["E3","C4","G4","B4"].
        var result = Voicings.Voicing(Cmaj7_FourNote(), "drop3");
        Assert.Equal(new[] { "E3", "C4", "G4", "B4" }, result.NoteNames);
    }

    [Fact]
    public void Drop3_OnTriad_ReturnsUnchanged()
    {
        // D-07: drop3 on 3-note triad → input unchanged.
        var input = Cmaj_Triad();
        var result = Voicings.Voicing(input, "drop3");
        Assert.Equal(input.NoteNames, result.NoteNames);
    }

    [Fact]
    public void Open_OnTriad_DoublesRangeViaOctaveSpread()
    {
        // (voicing Cmaj "open") on ["C4","E4","G4"] → middle note (E4) up an octave,
        // re-sorted: ["C4","G4","E5"].
        var result = Voicings.Voicing(Cmaj_Triad(), "open");
        Assert.Equal(new[] { "C4", "G4", "E5" }, result.NoteNames);
    }

    [Fact]
    public void Close_ReturnsTightlyVoicedChord()
    {
        // close voicing collapses any notes >1 octave above root down. Build a wide chord:
        // C4, E4, G5 (G is in oct 5, more than an octave above C4) → close drops G to G4.
        // Expected after sort: ["C4","E4","G4"].
        var wide = Make("C", "maj", 4, "C4", "E4", "G5");
        var result = Voicings.Voicing(wide, "close");
        Assert.Equal(new[] { "C4", "E4", "G4" }, result.NoteNames);
    }

    [Fact]
    public void Spread_OnTriad_DoublesRangeBetweenLowestHighest()
    {
        // (voicing Cmaj "spread") on ["C4","E4","G4"] → top note (G4) raised an octave,
        // re-sorted: ["C4","E4","G5"].
        var result = Voicings.Voicing(Cmaj_Triad(), "spread");
        Assert.Equal(new[] { "C4", "E4", "G5" }, result.NoteNames);
    }

    [Fact]
    public void Spread_OnDyad_ReturnsUnchanged()
    {
        // D-07: spread requires ≥3 notes; 2-note dyad → input unchanged.
        var input = Dyad();
        var result = Voicings.Voicing(input, "spread");
        Assert.Equal(input.NoteNames, result.NoteNames);
    }

    [Fact]
    public void Voicing_UnknownName_ReturnsUnchanged()
    {
        // D-07: unknown voicing name (not in {drop2,drop3,open,close,spread}) → unchanged.
        var input = Cmaj_Triad();
        var result = Voicings.Voicing(input, "wibble");
        Assert.Equal(input.NoteNames, result.NoteNames);
    }

    [Fact]
    public void NoteNames_PreserveCanonicalAccidental()
    {
        // Pitfall 5: F#dim → ["F4+","A4","C5"]. Inversion 1 raises F4+ an octave:
        // ["A4","C5","F5+"]. The "+" accidental form must round-trip through
        // NoteType.Parse + NoteType.Format — never become "Fs" or "F#".
        var result = Voicings.Inversion(FsDim_Triad(), 1);
        Assert.Equal(new[] { "A4", "C5", "F5+" }, result.NoteNames);
        // Sanity: every emitted name must be Parse-able and round-trip identical.
        foreach (var n in result.NoteNames)
        {
            var (letter, oct, alt) = NoteType.Parse(n);
            Assert.Equal(n, NoteType.Format(letter, oct, alt));
        }
    }

    [Fact]
    public void Inversion_RegisteredViaEngine()
    {
        // Engine-eval gate: confirms Voicings.Register wired inversion into the
        // S-expression dispatch path used by .flow scripts.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
Chord ic = (inversion Cmaj 1)
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", System.StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");
        var chord = runner.GetVariable("ic").As<ChordData>();
        Assert.Equal(new[] { "E4", "G4", "C5" }, chord.NoteNames);
    }

    [Fact]
    public void Voicing_RegisteredViaEngine()
    {
        // Engine-eval gate: confirms Voicings.Register wired voicing into S-expression dispatch.
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
Chord d2 = (voicing Cmaj7 ""drop2"")
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", System.StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");
        var chord = runner.GetVariable("d2").As<ChordData>();
        Assert.Equal(new[] { "G3", "C4", "E4", "B4" }, chord.NoteNames);
    }
}
