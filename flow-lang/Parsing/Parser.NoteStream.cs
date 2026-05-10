namespace FlowLang.Parsing;

using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Core;
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
            if (Check(TokenType.LBrace))
            {
                var elemLoc = CurrentToken.Location;
                Advance(); // consume {

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
                currentBarElements.Add(new ChordElement(elemLoc, notes, durSuffix, isDotted));
                continue;
            }

            // Named chord element in note stream: Cmaj7, Dm, etc.
            if (Check(TokenType.ChordLiteral))
            {
                var chordToken = Advance();
                var elemLoc = chordToken.Location;
                string chordSymbol = chordToken.Text;
                string? durSuffix = TryParseDurationSuffix();
                bool isDotted = durSuffix != null && Match(TokenType.Dot);
                currentBarElements.Add(new NamedChordElement(elemLoc, chordSymbol, durSuffix, isDotted));
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

        return new NoteStreamExpression(location, bars);
    }

    /// <summary>
    /// Tries to parse a duration suffix (w, h, q, e, s, t) from the current token.
    /// Returns null if no valid duration suffix is found.
    /// </summary>
    private string? TryParseDurationSuffix()
    {
        if (Check(TokenType.Identifier))
        {
            var text = CurrentToken.Text;
            if (text is "w" or "h" or "q" or "e" or "s" or "t")
            {
                Advance();
                return text;
            }
        }
        return null;
    }

    /// <summary>
    /// Tries to parse an articulation mark after a note element.
    /// Recognizes: > (accent), stacc (staccato), ten (tenuto), marc (marcato).
    /// Returns null if no articulation is found.
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
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if the current position looks like the end of a note stream.
    /// Returns true if the next token is not a note-stream element.
    /// </summary>
    private bool IsEndOfNoteStream()
    {
        var type = CurrentToken.Type;
        // Note stream elements are: notes, rests, chord brackets, named chords, pipes
        // Identifiers can be roman numerals inside note streams
        if (type is TokenType.NoteLiteral or TokenType.Underscore
            or TokenType.LBracket or TokenType.Pipe or TokenType.ChordLiteral
            or TokenType.LParen or TokenType.GreaterThan)
            return false;
        // Check if identifier is a roman numeral, dynamic marking, articulation mark, or cresc/decresc
        if (type == TokenType.Identifier && (ScaleDatabase.IsRomanNumeral(CurrentToken.Text) || TryParseDynamicMarking(CurrentToken.Text).HasValue || CurrentToken.Text is "stacc" or "ten" or "marc" or "cresc" or "decresc"))
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

            // ChordLiteral: Cmaj7, Dm
            if (Check(TokenType.ChordLiteral))
            {
                var chordToken = Advance();
                string? chordSuffix = TryParseDurationSuffix();
                bool chordDotted = chordSuffix != null && Match(TokenType.Dot);
                children.Add(new NamedChordElement(chordToken.Location, chordToken.Text, chordSuffix, chordDotted));
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

}
