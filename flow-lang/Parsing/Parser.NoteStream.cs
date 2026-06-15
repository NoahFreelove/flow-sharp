namespace FlowLang.Parsing;

using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.TypeSystem.SpecialTypes;
using System.Collections.Generic;

public partial class Parser
{
    /// <summary>
    /// TUP-02 music21 shorthand convention: {N ...}q resolves to {N:M ...}q
    /// where M is looked up from this table. SPEC TUP-02 LOCKS entries 3, 5, 6, 7, 9.
    /// Counts 2, 4, 8, 10, 11 are music21-aligned per RESEARCH §"Code Examples" §1.
    /// Counts ≥ 12 raise a parse error citing the lookup-table bounds.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> MusicTwentyOneShorthand =
        new Dictionary<int, int>
        {
            { 2, 3 },   // duplet
            { 3, 2 },   // triplet (LOCKED by SPEC TUP-02)
            { 4, 6 },   // quadruplet
            { 5, 4 },   // quintuplet (LOCKED)
            { 6, 4 },   // sextuplet (LOCKED)
            { 7, 4 },   // septuplet (LOCKED)
            { 8, 6 },
            { 9, 8 },   // nonuplet (LOCKED)
            { 10, 8 },
            { 11, 8 },
        };

    /// <summary>
    /// Parses a note stream: | element element ... | element element ... |
    /// The opening | has already been consumed.
    /// </summary>
    private Expression ParseNoteStream(bool isPickup = false)
    {
        var location = PreviousToken.Location;
        var bars = new List<NoteStreamBar>();
        var currentBarElements = new List<NoteStreamElement>();
        double? stickyVelocity = null;
        bool nextBarIsPickup = isPickup;

        while (!IsAtEnd())
        {
            // End of bar / end of stream
            if (Match(TokenType.Pipe))
            {
                // Multi-line bar lists are written as
                //     | ... |
                //     | ... |
                // which produces token sequence: PIPE [bar1] PIPE PIPE [bar2] PIPE.
                // The two adjacent PIPEs between bars are the closing | of bar1 AND the
                // opening | of bar2 — they MUST collapse into a single bar boundary.
                // Pre-fix, every adjacent-PIPE pair silently inserted a whole-bar rest
                // between the content bars (the second PIPE saw currentBarElements empty
                // and pushed an empty-content NoteStreamBar). For the Phase 28 polyphony
                // fixture (4 source bars over 4 lines), this doubled-the-bar-count: 4 →
                // 7, surfacing as the UAT BLOCKER "grace note pre-attack" report — the
                // C2w bass voice attack at each rendered staccato-bar onset (offset by an
                // extra silent rest bar) became the first audible sound after a 2-second
                // silence, perceptually grafting a grace-note-like thump in front of each
                // staccato. Charitable-interpretation memory applies: a composer writing
                // multi-line bars NEVER means "insert silent rest bars between" — they
                // would write "| _ |" explicitly for that.
                if (currentBarElements.Count == 0 && bars.Count > 0)
                {
                    // Adjacent PIPE after a saved content bar — treat as opening | of the
                    // next bar, no save. nextBarIsPickup already reset to false.
                    continue;
                }

                // Save current bar
                bars.Add(new NoteStreamBar(location, currentBarElements, nextBarIsPickup));
                currentBarElements = new List<NoteStreamElement>();
                stickyVelocity = null;
                nextBarIsPickup = false; // Only the first bar is pickup

                // Check if this was the final closing pipe
                // A closing pipe is followed by a non-note-stream token
                if (IsAtEnd() || IsEndOfNoteStream())
                    break;

                continue;
            }

            // Rest element: _
            if (Match(TokenType.Underscore))
            {
                var elemLoc = PreviousToken.Location;
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                currentBarElements.Add(new RestElement(elemLoc, durSuffix, isDotted));
                continue;
            }

            // Parenthesized elements: (ghost C4), (grace B3), (? C4 E4 G4), (?? C4 E4 G4)
            if (Check(TokenType.LParen) && !IsAtEnd())
            {
                int savedPos = _current;
                Advance(); // consume (
                if (Check(TokenType.Identifier) && CurrentToken.Text == "ghost")
                {
                    var elemLoc = _tokens[savedPos].Location;
                    Advance(); // consume "ghost"
                    var noteToken = Expect(TokenType.NoteLiteral, "Expected note literal after 'ghost'");
                    string? durSuffix = TryParseDurationSuffix();
                    bool isDotted = durSuffix != null && Match(TokenType.Dot);
                    Expect(TokenType.RParen, "Expected ')' after ghost note");
                    currentBarElements.Add(new GhostNoteElement(elemLoc, noteToken.Text, durSuffix, isDotted));
                    continue;
                }

                if (Check(TokenType.Identifier) && CurrentToken.Text == "grace")
                {
                    var elemLoc = _tokens[savedPos].Location;
                    Advance(); // consume "grace"
                    var noteToken = Expect(TokenType.NoteLiteral, "Expected note literal after 'grace'");
                    Expect(TokenType.RParen, "Expected ')' after grace note");
                    currentBarElements.Add(new GraceNoteElement(elemLoc, noteToken.Text));
                    continue;
                }

                if (Check(TokenType.Identifier) && (CurrentToken.Text == "?" || CurrentToken.Text == "??"))
                {
                    var elemLoc = _tokens[savedPos].Location;
                    bool isSeeded = CurrentToken.Text == "??";
                    Advance(); // consume ? or ??
                    var choices = new List<(string Note, int? Weight)>();
                    while (!Check(TokenType.RParen) && !IsAtEnd())
                    {
                        if (Check(TokenType.NoteLiteral))
                        {
                            var noteToken = Advance();
                            int? weight = null;
                            if (Match(TokenType.Colon))
                            {
                                var wt = Expect(TokenType.IntLiteral, "Expected weight after ':'");
                                weight = (int)wt.Value!;
                            }
                            choices.Add((noteToken.Text, weight));
                        }
                        else if (Match(TokenType.Underscore))
                        {
                            // Allow rest _ as a choice
                            int? weight = null;
                            if (Match(TokenType.Colon))
                            {
                                var wt = Expect(TokenType.IntLiteral, "Expected weight after ':'");
                                weight = (int)wt.Value!;
                            }
                            choices.Add(("_", weight));
                        }
                        else
                        {
                            _errorReporter.ReportError($"Expected note or '_' in random choice, got '{CurrentToken.Text}'", CurrentToken.Location);
                            Advance();
                        }
                    }
                    Expect(TokenType.RParen, "Expected ')' after random choice");
                    if (choices.Count == 0) _errorReporter.ReportError("Random choice requires at least one option", elemLoc);
                    string? durSuffix = TryParseDurationSuffix();
                    bool isDotted = durSuffix != null && Match(TokenType.Dot);
                    currentBarElements.Add(new RandomChoiceElement(elemLoc, choices, isSeeded, durSuffix, isDotted));
                    continue;
                }
                else
                {
                    // Not a random choice — rewind
                    _current = savedPos;
                }
            }

            // Tuplet bracket: {N:M ...}q  or  {N ...}q  (shorthand) — TUP-01 / TUP-02 / TUP-03
            // Phase 28 (SPEC-1): also handles `{voice ...}` voice-block dispatch.
            if (Check(TokenType.LBrace))
            {
                var elemLoc = CurrentToken.Location;
                int savedPos = _current;
                Advance(); // consume {

                // Phase 28 voice-block branch: `{voice ...}` — parallel mini-bar.
                // Voice-block-inside-tuplet is rejected (Phase 28 scope) by ParseTupletChildren;
                // voice-block-inside-voice-block is rejected by ParseVoiceBlockChildren below.
                if (Check(TokenType.Identifier) && CurrentToken.Text == "voice")
                {
                    Advance(); // consume "voice"
                    var voiceChildren = ParseVoiceBlockChildren();
                    Expect(TokenType.RBrace, "Expected '}' to close voice block");
                    currentBarElements.Add(new VoiceBlockElement(elemLoc, voiceChildren));
                    continue;
                }

                // Not a voice block — fall through to tuplet path; restore position so the
                // existing tuplet code re-consumes the `{`.
                _current = savedPos;
                Advance(); // consume { again for the tuplet path

                var nToken = Expect(TokenType.IntLiteral, "Expected integer N in tuplet bracket");
                int n = (int)nToken.Value!;
                int denominator;

                if (Match(TokenType.Colon))
                {
                    var mToken = Expect(TokenType.IntLiteral, "Expected integer M after ':' in tuplet ratio");
                    denominator = (int)mToken.Value!;
                }
                else
                {
                    // TUP-02 music21 shorthand
                    if (!MusicTwentyOneShorthand.TryGetValue(n, out var lookup))
                    {
                        _errorReporter.ReportError(
                            $"Tuplet shorthand {{N}} only supports counts 2-11 (got {n}); use explicit {{N:M}} form",
                            elemLoc);
                        denominator = n; // best-effort recovery
                    }
                    else
                    {
                        denominator = lookup;
                    }
                }

                // Recursively parse children (note-stream elements until RBrace)
                var children = ParseTupletChildren();
                Expect(TokenType.RBrace, "Expected '}' to close tuplet bracket");

                // CONTEXT D-04 / SPEC D-USER-04: tuplet bracket REQUIRES explicit duration suffix
                string? durSuffix = TryParseDurationSuffix();
                if (durSuffix == null)
                {
                    _errorReporter.ReportError(
                        "Tuplet bracket requires explicit duration suffix",
                        elemLoc);
                    durSuffix = "q"; // best-effort recovery for downstream compile path
                }
                bool isDottedTuplet = Match(TokenType.Dot);

                currentBarElements.Add(new TupletElement(elemLoc, n, denominator, children, durSuffix, isDottedTuplet));
                continue;
            }

            // Chord bracket: [C4 E4 G4]
            if (Match(TokenType.LBracket))
            {
                var elemLoc = PreviousToken.Location;
                var notes = new List<string>();
                while (!Check(TokenType.RBracket) && !IsAtEnd())
                {
                    var noteToken = Expect(TokenType.NoteLiteral, "Expected note literal in chord bracket");
                    notes.Add(noteToken.Text);
                }
                Expect(TokenType.RBracket, "Expected ']' after chord bracket");
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                bool isTied = Match(TokenType.Tilde);
                currentBarElements.Add(new ChordElement(elemLoc, notes, durSuffix, isDotted, isTied));
                continue;
            }

            // Named chord element in note stream: Cmaj7, Dm, Cmaj7q (chord-duration-
            // fusion 0615 #5), Bb7w~ (tied), etc.
            if (Check(TokenType.ChordLiteral))
            {
                var chordToken = Advance();
                var elemLoc = chordToken.Location;
                string chordSymbol = chordToken.Text;
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                bool isTied = Match(TokenType.Tilde);
                currentBarElements.Add(new NamedChordElement(elemLoc, chordSymbol, durSuffix, isDotted, isTied));
                continue;
            }

            // Crescendo/decrescendo span markers (consumed as visual indicators;
            // actual interpolation is handled by NoteStreamCompiler post-processing)
            if (Check(TokenType.Identifier))
            {
                var text = CurrentToken.Text;
                if (text == "cresc" || text == "decresc")
                {
                    Advance();
                    continue;
                }
            }

            // Dynamic marking: pp, p, mp, mf, f, ff, fff, ppp, sfz, fp
            if (Check(TokenType.Identifier))
            {
                var dynVelocity = TryParseDynamicMarking(CurrentToken.Text);
                if (dynVelocity.HasValue)
                {
                    Advance();
                    stickyVelocity = dynVelocity.Value;
                    continue;
                }
            }

            // Note element: C4, C4q, C4q., C4h~, C4/12 (TUP-04), C4/3:2q (TUP-08)
            if (Check(TokenType.NoteLiteral))
            {
                var noteToken = Advance();
                var elemLoc = noteToken.Location;
                string noteName = noteToken.Text;

                // TUP-04 / TUP-08: per-note fractional-duration suffix /N or /X:Y[suffix]
                (int Num, int Denom)? tupletRatio = null;
                string? overrideDurSuffix = null;

                if (Match(TokenType.Slash))
                {
                    var nToken = Expect(TokenType.IntLiteral, "Expected integer after '/' in note duration");
                    int n = (int)nToken.Value!;

                    if (Match(TokenType.Colon))
                    {
                        // TUP-08: C4/X:Y[suffix]
                        var yToken = Expect(TokenType.IntLiteral, "Expected integer Y after ':' in per-note tuplet ratio");
                        int y = (int)yToken.Value!;
                        if (n < 1)
                        {
                            _errorReporter.ReportError(
                                $"Tuplet ratio numerator X must be ≥ 1; got {n}",
                                nToken.Location);
                            n = 1; // best-effort recovery
                        }
                        if (y < 1)
                        {
                            _errorReporter.ReportError(
                                $"Tuplet ratio denominator Y must be ≥ 1; got {y}",
                                yToken.Location);
                            y = 1;
                        }
                        tupletRatio = (n, y);
                        // Optional level suffix (w/h/q/e/s/t). Default null → compiler treats as quarter.
                        overrideDurSuffix = TryParseDurationSuffix();
                    }
                    else
                    {
                        // TUP-04: C4/N — encode as TupletRatio=(N, 1) sentinel for compiler branch
                        if (n < 1)
                        {
                            _errorReporter.ReportError(
                                $"Duration denominator must be ≥ 1; got {n}",
                                nToken.Location);
                            n = 1;
                        }
                        tupletRatio = (n, 1);
                        // No optional suffix for /N form — /N already specifies whole-note fraction.
                    }
                }

                string? durSuffix = overrideDurSuffix ?? TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                bool isTied = Match(TokenType.Tilde);
                double? centOffset = null;
                if (Check(TokenType.CentLiteral))
                {
                    centOffset = (double)Advance().Value!;
                }
                Articulation? articMark = TryParseArticulation();
                currentBarElements.Add(new NoteElement(elemLoc, noteName, durSuffix, isDotted, isTied,
                    centOffset, stickyVelocity, articMark, tupletRatio));
                continue;
            }

            // Identifier in note stream: roman numerals or variable references
            if (Check(TokenType.Identifier))
            {
                var identText = CurrentToken.Text;
                if (ScaleDatabase.IsRomanNumeral(identText))
                {
                    var rnToken = Advance();
                    var elemLoc = rnToken.Location;
                    string? durSuffix = TryParseDurationSuffix();
                    bool isDotted = durSuffix != null && Match(TokenType.Dot);
                    currentBarElements.Add(new RomanNumeralElement(elemLoc, identText, durSuffix, isDotted));
                    continue;
                }
                else if (identText is not ("w" or "h" or "q" or "e" or "s" or "t"))
                {
                    // Lowercase-initial identifiers are variable references
                    if (identText.Length > 0 && char.IsLower(identText[0]))
                    {
                        var varToken = Advance();
                        var elemLoc = varToken.Location;
                        string? durSuffix = TryParseDurationSuffix();
                        bool isDotted = durSuffix != null && Match(TokenType.Dot);
                        bool isTied = Match(TokenType.Tilde);
                        double? centOffset = null;
                        if (Check(TokenType.CentLiteral))
                            centOffset = (double)Advance().Value!;
                        currentBarElements.Add(new VariableReferenceElement(
                            elemLoc, identText, durSuffix, isDotted, isTied, centOffset));
                        continue;
                    }

                    // sweep-0614: an UPPERCASE non-roman-numeral identifier here is the
                    // shape of a mistyped note name (e.g. `Z9`) — the lexer only
                    // emits NoteLiteral when the first char is A-G. Recover charitably
                    // (CLAUDE.md charitable-interpretation), MIRRORING the lowercase
                    // variable-reference → rest path: emit a located one-shot advisory,
                    // render the typo as a rest (honoring its duration suffix), and keep
                    // parsing so the SURROUNDING notes are NOT silently dropped. Without
                    // this, the loop `break`s, abandons the rest of the stream, and the
                    // diagnostic points at the closing pipe instead of the offending token.
                    //
                    // sweep-0614 follow-up (regression-notestream-hasb): scope this
                    // NARROWLY to genuinely note-SHAPED typos. The unconditional
                    // `char.IsUpper` form was too aggressive on two fronts:
                    //   (a) It consumed the closing of a multi-line stream's NEXT
                    //       declaration — `| ... |` followed by `Sequence b = | ... |`
                    //       saw `Sequence` (an uppercase type name) and ate it as a rest,
                    //       so the stream never terminated and `=` was an "unexpected
                    //       token". A type name / statement keyword must instead `break`
                    //       (IsEndOfNoteStream already classifies it as end-of-stream).
                    //   (b) It bypassed the hAsB pragma: `H4q` WITHOUT `enable hAsB;`
                    //       reaches this branch (the lexer only canonicalizes H→B when the
                    //       pragma is set), and the charitable rest silently ACCEPTED it.
                    //       An H note without the pragma must STILL be rejected, so we
                    //       exclude H-prefixed tokens here and let the loop `break` →
                    //       parse error (PRAG-02 / DEFER-02 contract).
                    // A genuine note-name typo is "note-like-shaped": a single letter, OR
                    // it contains a digit (octave), OR its second char is an accidental
                    // (`b`/`#`). Pure-alpha multi-char words (type names, keywords) fail
                    // this test and fall through to the break.
                    if (IsNoteLikeTypoShape(identText))
                    {
                        var badToken = Advance();
                        var elemLoc = badToken.Location;
                        RenderingDiagnostics.WarnOnce(
                            $"note-stream-bad-note:{identText}:{elemLoc.Line}:{elemLoc.Column}",
                            $"{elemLoc.FileName}:{elemLoc.Line}:{elemLoc.Column}: [note-stream] unrecognized note name '{identText}' — rendered as rest");
                        string? durSuffix = TryParseDurationSuffix();
                        bool isDotted = durSuffix != null && Match(TokenType.Dot);
                        // Consume a trailing tie/cent so the typo's adornments don't
                        // re-enter the loop as stray tokens.
                        Match(TokenType.Tilde);
                        if (Check(TokenType.CentLiteral)) Advance();
                        currentBarElements.Add(new RestElement(elemLoc, durSuffix, isDotted));
                        continue;
                    }
                }
            }

            // If we encounter something unexpected, break out
            break;
        }

        // If we broke out without a closing pipe, the last bar is incomplete but still valid
        if (currentBarElements.Count > 0)
        {
            bars.Add(new NoteStreamBar(location, currentBarElements, nextBarIsPickup));
        }

        if (bars.Count == 0)
        {
            _errorReporter.ReportError("Empty note stream", location);
        }

        return new NoteStreamExpression(location, bars, Span: new Span(location, PreviousToken.Location));
    }

    /// <summary>
    /// Tries to parse a duration suffix (w, h, q, e, s, t, x, y) from the current token.
    /// Returns null if no valid duration suffix is found.
    /// </summary>
    private string? TryParseDurationSuffix()
    {
        if (Check(TokenType.Identifier))
        {
            var text = CurrentToken.Text;
            if (text is "w" or "h" or "q" or "e" or "s" or "t" or "x" or "y")
            {
                Advance();
                return text;
            }
        }
        return null;
    }

    /// <summary>
    /// Tries to parse an articulation mark after a note element.
    /// Recognizes: > (accent), stacc (staccato), ten (tenuto), marc (marcato), leg (legato).
    /// Returns null if no articulation is found.
    ///
    /// Phase 28 (SPEC-3): `leg` produces Articulation.Legato — distinct from the Phase 22
    /// legato() transform which adjusts DurationOverlap. This is the per-note articulation
    /// envelope; renderers extend duration ~110% with a soft crossfade.
    /// </summary>
    private Articulation? TryParseArticulation()
    {
        if (Check(TokenType.GreaterThan))
        {
            Advance();
            return Articulation.Accent;
        }
        if (Check(TokenType.Identifier))
        {
            var text = CurrentToken.Text;
            switch (text)
            {
                case "stacc":
                    Advance();
                    return Articulation.Staccato;
                case "ten":
                    Advance();
                    return Articulation.Tenuto;
                case "marc":
                    Advance();
                    return Articulation.Marcato;
                case "leg":
                    Advance();
                    return Articulation.Legato;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if the current position looks like the end of a note stream.
    /// Returns true if the next token is not a note-stream element.
    /// </summary>
    /// <summary>
    /// regression-notestream-hasb: true when <paramref name="text"/> is an uppercase
    /// identifier shaped like a mistyped NOTE NAME — the only shape the charitable
    /// "unrecognized note → rest" recovery may consume. Distinguishes a genuine typo
    /// (`Z9`, `Q5`, `Xb3`) from a type name / statement keyword (`Sequence`, `Song`,
    /// `Int`) that must terminate the stream, and from an H-prefixed token that must be
    /// rejected by the hAsB pragma gate (H reaches this branch only when the pragma is
    /// OFF; charitably swallowing it would silently accept an H note without
    /// `enable hAsB;`).
    /// </summary>
    private static bool IsNoteLikeTypoShape(string text)
    {
        if (text.Length == 0 || !char.IsUpper(text[0]))
            return false;
        // H-prefixed tokens are governed by the hAsB pragma, NOT charitable typo
        // recovery. Without `enable hAsB;`, an `H<digit>` must still be rejected
        // (break → parse error), preserving PRAG-02 / DEFER-02. (With the pragma set,
        // the lexer already canonicalized H→B, so it never arrives here as an Identifier.)
        if (text[0] == 'H')
            return false;
        // Note-like shapes: a single letter, OR contains a digit (octave),
        // OR second char is an accidental (b/#). Mirrors TryParseNote's accepted
        // shapes (SimpleLexer) and the WR-01 looksNoteLike pickup gate.
        return text.Length == 1
            || text.Any(char.IsDigit)
            || text[1] == 'b'
            || text[1] == '#';
    }

    private bool IsEndOfNoteStream()
    {
        var type = CurrentToken.Type;
        // Note stream elements are: notes, rests, chord brackets, named chords, pipes,
        // tuplet brackets `{N ...}` (TUP-01), and Phase 28 voice blocks `{voice ...}`.
        // Identifiers can be roman numerals inside note streams.
        if (type is TokenType.NoteLiteral or TokenType.Underscore
            or TokenType.LBracket or TokenType.Pipe or TokenType.ChordLiteral
            or TokenType.LParen or TokenType.GreaterThan
            or TokenType.LBrace)
            return false;
        // Check if identifier is a roman numeral, dynamic marking, articulation mark, or cresc/decresc
        if (type == TokenType.Identifier && (ScaleDatabase.IsRomanNumeral(CurrentToken.Text) || TryParseDynamicMarking(CurrentToken.Text).HasValue || CurrentToken.Text is "stacc" or "ten" or "marc" or "leg" or "cresc" or "decresc"))
            return false;
        // Lowercase identifiers are variable references — continue the stream
        if (type == TokenType.Identifier)
        {
            var text = CurrentToken.Text;
            if (text.Length > 0 && char.IsLower(text[0])
                && text is not ("w" or "h" or "q" or "e" or "s" or "t"))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if a token text is a dynamic marking and returns its velocity (0.0-1.0).
    /// Returns null if not a dynamic marking.
    /// </summary>
    private static double? TryParseDynamicMarking(string text)
    {
        return text switch
        {
            "ppp" => 0.125,
            "pp"  => 0.25,
            "p"   => 0.375,
            "mp"  => 0.5,
            "mf"  => 0.625,
            "f"   => 0.75,
            "ff"  => 0.875,
            "fff" => 1.0,
            "sfz" => 0.95,
            "fp"  => 0.75,
            _     => null
        };
    }

    /// <summary>
    /// Recursively parses note-stream elements until RBrace is reached.
    /// Mirrors the dispatch shape of ParseNoteStream's main loop but terminates
    /// on '}' instead of pipe-or-EOF. Supports nested tuplets (TUP-03) by
    /// recursing into the LBrace arm via this same loop.
    /// </summary>
    private List<NoteStreamElement> ParseTupletChildren()
    {
        var children = new List<NoteStreamElement>();

        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            // Nested tuplet: {N:M ...}q — recursively dispatch through the same shape
            if (Check(TokenType.LBrace))
            {
                var elemLoc = CurrentToken.Location;
                Advance(); // consume {
                var nToken = Expect(TokenType.IntLiteral, "Expected integer N in nested tuplet");
                int n = (int)nToken.Value!;
                int denominator;
                if (Match(TokenType.Colon))
                {
                    var mToken = Expect(TokenType.IntLiteral, "Expected integer M after ':' in nested tuplet ratio");
                    denominator = (int)mToken.Value!;
                }
                else if (MusicTwentyOneShorthand.TryGetValue(n, out var lookup))
                {
                    denominator = lookup;
                }
                else
                {
                    _errorReporter.ReportError(
                        $"Tuplet shorthand {{N}} only supports counts 2-11 (got {n}); use explicit {{N:M}} form",
                        elemLoc);
                    denominator = n;
                }

                var nestedChildren = ParseTupletChildren();
                Expect(TokenType.RBrace, "Expected '}' to close nested tuplet bracket");

                string? nestedSuffix = TryParseDurationSuffix();
                if (nestedSuffix == null)
                {
                    _errorReporter.ReportError(
                        "Tuplet bracket requires explicit duration suffix",
                        elemLoc);
                    nestedSuffix = "q";
                }
                bool nestedDotted = Match(TokenType.Dot);

                children.Add(new TupletElement(elemLoc, n, denominator, nestedChildren, nestedSuffix, nestedDotted));
                continue;
            }

            // NoteLiteral: C4, D#5, C4/12, C4/3:2q — supports per-note fractional duration inside tuplet brackets too
            if (Check(TokenType.NoteLiteral))
            {
                var noteToken = Advance();
                var noteLoc = noteToken.Location;
                string noteName = noteToken.Text;

                (int Num, int Denom)? tupletRatio = null;
                string? overrideDurSuffix = null;

                if (Match(TokenType.Slash))
                {
                    var nToken = Expect(TokenType.IntLiteral, "Expected integer after '/' in note duration");
                    int n = (int)nToken.Value!;

                    if (Match(TokenType.Colon))
                    {
                        var yToken = Expect(TokenType.IntLiteral, "Expected integer Y after ':' in per-note tuplet ratio");
                        int y = (int)yToken.Value!;
                        if (n < 1)
                        {
                            _errorReporter.ReportError(
                                $"Tuplet ratio numerator X must be ≥ 1; got {n}",
                                nToken.Location);
                            n = 1;
                        }
                        if (y < 1)
                        {
                            _errorReporter.ReportError(
                                $"Tuplet ratio denominator Y must be ≥ 1; got {y}",
                                yToken.Location);
                            y = 1;
                        }
                        tupletRatio = (n, y);
                        overrideDurSuffix = TryParseDurationSuffix();
                    }
                    else
                    {
                        if (n < 1)
                        {
                            _errorReporter.ReportError(
                                $"Duration denominator must be ≥ 1; got {n}",
                                nToken.Location);
                            n = 1;
                        }
                        tupletRatio = (n, 1);
                    }
                }

                string? noteSuffix = overrideDurSuffix ?? TryParseDurationSuffix();
                bool noteDotted = noteSuffix != null && Match(TokenType.Dot);
                bool noteTied = Match(TokenType.Tilde);
                double? noteCent = null;
                if (Check(TokenType.CentLiteral))
                {
                    noteCent = (double)Advance().Value!;
                }
                Articulation? articMark = TryParseArticulation();
                children.Add(new NoteElement(noteLoc, noteName, noteSuffix, noteDotted, noteTied,
                    noteCent, null, articMark, tupletRatio));
                continue;
            }

            // Rest: _
            if (Match(TokenType.Underscore))
            {
                var restLoc = PreviousToken.Location;
                string? restSuffix = TryParseDurationSuffix();
                bool restDotted = restSuffix != null && Match(TokenType.Dot);
                children.Add(new RestElement(restLoc, restSuffix, restDotted));
                continue;
            }

            // ChordLiteral: Cmaj7, Dm, Cmaj7q (chord-duration-fusion 0615 #5)
            if (Check(TokenType.ChordLiteral))
            {
                var chordToken = Advance();
                string? chordSuffix = TryParseDurationSuffix();
                bool chordDotted = chordSuffix != null && Match(TokenType.Dot);
                bool chordTied = Match(TokenType.Tilde);
                children.Add(new NamedChordElement(chordToken.Location, chordToken.Text, chordSuffix, chordDotted, chordTied));
                continue;
            }

            // Unknown token inside tuplet — report and recover
            _errorReporter.ReportError(
                $"Unexpected token '{CurrentToken.Text}' inside tuplet bracket",
                CurrentToken.Location);
            Advance();
        }

        return children;
    }

    /// <summary>
    /// Phase 28 (SPEC-1): parses note-stream elements inside a `{voice ...}` block.
    /// Accepts NoteElement, RestElement, ChordElement, NamedChordElement, RandomChoiceElement,
    /// and TupletElement. Nested voice blocks are rejected with a clear error
    /// (Phase 28 scope: voice blocks may not contain other voice blocks).
    /// Terminates on RBrace; the caller consumes the `}` after this returns.
    /// </summary>
    private List<NoteStreamElement> ParseVoiceBlockChildren()
    {
        var children = new List<NoteStreamElement>();

        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            // Brace inside voice block: nested tuplet OK, nested voice block REJECTED.
            if (Check(TokenType.LBrace))
            {
                var innerLoc = CurrentToken.Location;
                int savedPos = _current;
                Advance(); // consume {

                if (Check(TokenType.Identifier) && CurrentToken.Text == "voice")
                {
                    _errorReporter.ReportError(
                        "Nested voice blocks are not supported (Phase 28 scope)",
                        innerLoc);
                    // Skip past the nested voice block contents to a matching `}`
                    Advance(); // consume "voice"
                    int depth = 1;
                    while (!IsAtEnd() && depth > 0)
                    {
                        if (Check(TokenType.LBrace)) depth++;
                        else if (Check(TokenType.RBrace)) depth--;
                        if (depth == 0) break;
                        Advance();
                    }
                    if (Check(TokenType.RBrace)) Advance();
                    continue;
                }

                // Nested tuplet — rewind and let ParseTupletChildren-style logic handle it
                _current = savedPos;
                Advance(); // re-consume {
                var nToken = Expect(TokenType.IntLiteral, "Expected integer N in nested tuplet");
                int n = (int)nToken.Value!;
                int denominator;
                if (Match(TokenType.Colon))
                {
                    var mToken = Expect(TokenType.IntLiteral, "Expected integer M after ':' in nested tuplet ratio");
                    denominator = (int)mToken.Value!;
                }
                else if (MusicTwentyOneShorthand.TryGetValue(n, out var lookup))
                {
                    denominator = lookup;
                }
                else
                {
                    _errorReporter.ReportError(
                        $"Tuplet shorthand {{N}} only supports counts 2-11 (got {n}); use explicit {{N:M}} form",
                        innerLoc);
                    denominator = n;
                }

                var nestedChildren = ParseTupletChildren();
                Expect(TokenType.RBrace, "Expected '}' to close nested tuplet bracket");

                string? nestedSuffix = TryParseDurationSuffix();
                if (nestedSuffix == null)
                {
                    _errorReporter.ReportError(
                        "Tuplet bracket requires explicit duration suffix",
                        innerLoc);
                    nestedSuffix = "q";
                }
                bool nestedDotted = Match(TokenType.Dot);

                children.Add(new TupletElement(innerLoc, n, denominator, nestedChildren, nestedSuffix, nestedDotted));
                continue;
            }

            // NoteLiteral inside voice block: full NoteElement parsing (with articulation).
            if (Check(TokenType.NoteLiteral))
            {
                var noteToken = Advance();
                var noteLoc = noteToken.Location;
                string noteName = noteToken.Text;
                string? noteSuffix = TryParseDurationSuffix();
                bool noteDotted = noteSuffix != null && Match(TokenType.Dot);
                bool noteTied = Match(TokenType.Tilde);
                double? noteCent = null;
                if (Check(TokenType.CentLiteral))
                {
                    noteCent = (double)Advance().Value!;
                }
                Articulation? articMark = TryParseArticulation();
                children.Add(new NoteElement(noteLoc, noteName, noteSuffix, noteDotted, noteTied,
                    noteCent, null, articMark));
                continue;
            }

            // Rest: _
            if (Match(TokenType.Underscore))
            {
                var restLoc = PreviousToken.Location;
                string? restSuffix = TryParseDurationSuffix();
                bool restDotted = restSuffix != null && Match(TokenType.Dot);
                children.Add(new RestElement(restLoc, restSuffix, restDotted));
                continue;
            }

            // Chord bracket [C4 E4 G4]q
            if (Match(TokenType.LBracket))
            {
                var elemLoc = PreviousToken.Location;
                var notes = new List<string>();
                while (!Check(TokenType.RBracket) && !IsAtEnd())
                {
                    var nToken = Expect(TokenType.NoteLiteral, "Expected note literal in chord bracket");
                    notes.Add(nToken.Text);
                }
                Expect(TokenType.RBracket, "Expected ']' after chord bracket");
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                bool isTied = Match(TokenType.Tilde);
                children.Add(new ChordElement(elemLoc, notes, durSuffix, isDotted, isTied));
                continue;
            }

            // Named chord: Cmaj7, Dm, Cmaj7q (chord-duration-fusion 0615 #5)
            if (Check(TokenType.ChordLiteral))
            {
                var chordToken = Advance();
                string? chordSuffix = TryParseDurationSuffix();
                bool chordDotted = chordSuffix != null && Match(TokenType.Dot);
                bool chordTied = Match(TokenType.Tilde);
                children.Add(new NamedChordElement(chordToken.Location, chordToken.Text, chordSuffix, chordDotted, chordTied));
                continue;
            }

            // Unknown token inside voice block — report and recover
            _errorReporter.ReportError(
                $"Unexpected token '{CurrentToken.Text}' inside voice block",
                CurrentToken.Location);
            Advance();
        }

        return children;
    }

}
