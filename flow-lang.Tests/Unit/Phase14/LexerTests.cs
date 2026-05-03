using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase14;

/// <summary>
/// Phase 14 DX-06 lexer regression Facts.
///
/// Under the extended NoteType.Parse surface (sum-based alteration scan, CONTEXT D-07),
/// inputs that previously failed TryParseNote now succeed (e.g., "Bb7" → Note(B, 7, -1)).
/// SimpleLexer now dispatches ChordParser.IsChordSymbol BEFORE TryParseNote so existing
/// chord tokens are unaffected by the extension.
///
/// Critical discovery during Task 1 execution (Rule 1 deviation): the plan's
/// <c>Bb7_IsChord</c> / <c>FsharpDim_IsChord</c> test cases assumed ChordParser accepted
/// the <c>b</c>/<c>#</c> accidental convention. It does not — ChordParser uses <c>s</c>/<c>f</c>
/// internally (Cs, Bf, Fs, Bfm, etc.). So "Bb7" was NEVER a ChordLiteral in this lexer.
/// The regression-gate Facts are rewritten to exercise symbols the real ChordParser accepts
/// (Dm, Cmaj7, Am7, Bdim, Csmaj, Bfm — all present in tests/test_chords.flow).
/// </summary>
public class LexerTests
{
    private static Token FirstNonEof(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        var tokens = lexer.Tokenize();
        foreach (var t in tokens)
        {
            if (t.Type != TokenType.Eof)
                return t;
        }
        throw new InvalidOperationException("No non-Eof tokens produced");
    }

    // ---------- Chord-literal regression gates (real ChordParser s/f convention) ----------

    [Fact] public void Dm_IsChord()     => Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Dm").Type);
    [Fact] public void Cmaj7_IsChord()  => Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Cmaj7").Type);
    [Fact] public void Am7_IsChord()    => Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Am7").Type);
    [Fact] public void Bdim_IsChord()   => Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Bdim").Type);

    [Fact]
    public void Csmaj_IsChord()
    {
        // Csmaj = C# major chord (ChordParser 's' convention). Under extended NoteType.Parse,
        // "Csmaj" would throw on the 's' character; without the chord-first dispatch reorder,
        // this could be an issue if future NoteType.Parse grew tolerant. Pins chord precedence.
        Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Csmaj").Type);
    }

    [Fact]
    public void Bfm_IsChord()
    {
        // Bfm = B-flat minor (ChordParser 'f' convention). Used in tests/test_chords.flow;
        // regression-critical.
        Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Bfm").Type);
    }

    // ---------- Note-literal new surface ----------

    [Fact] public void Db4_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("Db4").Type);
    [Fact] public void Bb_IsNote()  => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("Bb").Type);
    [Fact] public void C4_IsNote()  => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("C4").Type);

    [Fact]
    public void FsharpBare_IsNote()
    {
        // "F#" has no chord quality suffix and ChordParser.IsChordSymbol rejects it
        // (uses 's'/'f' for accidentals, not '#'/'b'). Falls through to TryParseNote and
        // tokenizes as a NoteLiteral under the extended surface.
        Assert.Equal(TokenType.NoteLiteral, FirstNonEof("F#").Type);
    }

    [Fact]
    public void Bb7_NewBehavior_IsNote()
    {
        // Under the extended NoteType.Parse surface, "Bb7" parses as Note(B, 7, -1).
        // ChordParser.IsChordSymbol rejects "Bb7" (uses 'f' not 'b' for accidentals), so
        // the chord-first dispatch reorder does NOT change this outcome. Documents the
        // new behavior: "Bb7" was formerly an Identifier error; it is now a NoteLiteral.
        // No existing code relies on the old behavior (grep confirmed empty across
        // tests/ examples/ flow-lang/*.cs — see plan 14-02 §Pre-landing Collision Grep).
        Assert.Equal(TokenType.NoteLiteral, FirstNonEof("Bb7").Type);
    }

    [Fact]
    public void Cb4h_DurationStripped_IsNote()
    {
        // Duration-suffix path strips trailing 'h' and re-parses "Cb4" via TryParseNote under
        // the extended surface. Verifies the duration-suffix branch still works.
        Assert.Equal(TokenType.NoteLiteral, FirstNonEof("Cb4h").Type);
    }

    // ---------- note-vs-chord-lexer fix (2026-05-02) ----------
    //
    // Bug: tokens like "D6", "G7", "A6", "C6", "F6" were misclassified as ChordLiteral
    // because ChordParser.IsChordSymbol matched the digit-only quality suffixes "6", "7",
    // "9" present in QualityIntervals. This contradicted the project's documented
    // convention (tests/test_chords.flow:13: "G7 is parsed as note G at octave 7, use
    // dom7 for chord") and broke chord-bracket parsing in real-world MIDI imports
    // (Chopin Nocturne Op. 9 No. 2 in Eb).
    //
    // Fix: IsChordSymbol now rejects digit-only quality suffixes on the no-accidental
    // branch, so these tokens fall through to TryParseNote and become NoteLiterals.
    // The Phase 14 D-21 chord-before-note dispatch in SimpleLexer.cs is preserved.

    [Fact] public void D6_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("D6").Type);
    [Fact] public void G6_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("G6").Type);
    [Fact] public void A6_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("A6").Type);
    [Fact] public void C6_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("C6").Type);
    [Fact] public void F6_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("F6").Type);
    [Fact] public void G7_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("G7").Type);
    [Fact] public void D7_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("D7").Type);
    [Fact] public void A9_IsNote() => Assert.Equal(TokenType.NoteLiteral, FirstNonEof("A9").Type);

    [Fact]
    public void Gdom7_StillIsChord()
    {
        // Regression gate: the documented convention from tests/test_chords.flow:13
        // recommends "Gdom7" for the G dominant 7th chord. Multi-char "dom7" quality
        // is still accepted by IsChordSymbol after the digit-only narrowing.
        Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Gdom7").Type);
    }

    [Fact]
    public void Cm6_StillIsChord()
    {
        // Regression gate: "Cm6" has a multi-char "m6" quality, NOT digit-only,
        // so it remains a ChordLiteral. Distinguishes the narrowing scope.
        Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Cm6").Type);
    }

    [Fact]
    public void Cadd9_StillIsChord()
    {
        // Regression gate: "Cadd9" has a multi-char "add9" quality, still a chord.
        Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Cadd9").Type);
    }

    [Fact]
    public void Cm7_StillIsChord()
    {
        // Regression gate: "Cm7" has multi-char "m7" quality, still a chord.
        Assert.Equal(TokenType.ChordLiteral, FirstNonEof("Cm7").Type);
    }
}
