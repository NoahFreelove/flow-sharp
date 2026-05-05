using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Lexing;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.StandardLibrary.Harmony;
using FlowLsp.NoteStream;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 24 Plan 24-03 (LINT-01/02/03 + D-01..D-23): scale-lint analyzer.
/// Pure read-only AST + token traversal; never throws, never publishes
/// (returns IReadOnlyList&lt;Diagnostic&gt;). Plan 24-04 wires this into
/// the DocumentManager onParse callback via CombinedDiagnosticsPublisher.
///
/// D-19 activation gate: short-circuits to Array.Empty when
/// `Ast.Pragmas.Has("scaleLint")` is false (LINT-02 opt-in invariant).
/// D-21 innermost-key resolution: reuses NoteStreamContext.FindEnclosingKey
/// VERBATIM — no parallel resolver lives here.
/// D-22 silent fail-open: TryParseKeyWithMode false OR DiatonicSpellings null
/// → analyzer emits zero diagnostics for that block.
/// D-18 source string: every emitted Diagnostic carries Source="flow.scaleLint"
/// (NOT just "flow") so editors can filter scale-lint independently of parse errors.
/// </summary>
public static class ScaleLintAnalyzer
{
    /// <summary>
    /// Walk the AST + tokens and return Information-severity Diagnostic instances
    /// for non-diatonic notes inside `key { ... }` blocks. Returns an empty list
    /// when `enable scaleLint;` is not declared (D-19 / LINT-02 short-circuit).
    /// </summary>
    public static IReadOnlyList<Diagnostic> Analyze(
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source)
    {
        // D-19 short-circuit — opt-in only (LINT-02 invariant).
        if (!ast.Pragmas.Has("scaleLint"))
            return Array.Empty<Diagnostic>();

        var diagnostics = new List<Diagnostic>();
        WalkStatements(ast.Statements, ast, tokens, source, diagnostics);
        return diagnostics;
    }

    private static void WalkStatements(
        IReadOnlyList<Statement> stmts,
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        List<Diagnostic> diagnostics)
    {
        foreach (var stmt in stmts)
        {
            switch (stmt)
            {
                case MusicalContextStatement m:
                    WalkStatements(m.Body, ast, tokens, source, diagnostics);
                    break;
                case SectionDeclaration sd:
                    WalkStatements(sd.Body, ast, tokens, source, diagnostics);
                    break;
                case ProcDeclaration pd:
                    WalkStatements(pd.Body, ast, tokens, source, diagnostics);
                    break;
                case ExpressionStatement es when es.Expression is NoteStreamExpression ns:
                    WalkNoteStream(ns, ast, tokens, source, diagnostics);
                    break;
                case VariableDeclaration vd when vd.Value is NoteStreamExpression nsv:
                    WalkNoteStream(nsv, ast, tokens, source, diagnostics);
                    break;
            }
        }
    }

    private static void WalkNoteStream(
        NoteStreamExpression ns,
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        List<Diagnostic> diagnostics)
    {
        foreach (var bar in ns.Bars)
            foreach (var elem in bar.Elements)
                CheckElement(elem, ast, tokens, source, diagnostics);
    }

    private static void CheckElement(
        NoteStreamElement elem,
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        List<Diagnostic> diagnostics)
    {
        switch (elem)
        {
            // D-06: NoteElement always checked
            case NoteElement n:
                CheckNote(n.NoteName, n.Location, ast, tokens, source, diagnostics);
                break;
            // GhostNoteElement and GraceNoteElement carry note names too — same treatment.
            case GhostNoteElement g:
                CheckNote(g.NoteName, g.Location, ast, tokens, source, diagnostics);
                break;
            case GraceNoteElement gr:
                CheckNote(gr.NoteName, gr.Location, ast, tokens, source, diagnostics);
                break;
            // D-07: ChordElement recursed
            case ChordElement c:
                foreach (var note in c.Notes)
                    CheckNote(note, c.Location, ast, tokens, source, diagnostics);
                break;
            // D-09: RandomChoiceElement recursed
            case RandomChoiceElement r:
                foreach (var (note, _) in r.Choices)
                    CheckNote(note, r.Location, ast, tokens, source, diagnostics);
                break;
            // D-10: TupletElement recursed (incl. nested via the recursive call)
            case TupletElement t:
                foreach (var child in t.Children)
                    CheckElement(child, ast, tokens, source, diagnostics);
                break;
            // D-11/D-12/D-13/D-14 SKIP — no case branch:
            //   RomanNumeralElement, NamedChordElement, VariableReferenceElement, RestElement
        }
    }

    private static void CheckNote(
        string noteName,
        FlowLang.Core.SourceLocation loc,
        FlowProgram ast,
        IReadOnlyList<Token> tokens,
        string source,
        List<Diagnostic> diagnostics)
    {
        // D-21: innermost-key resolution via NoteStreamContext (verbatim reuse).
        // Pitfall 5: convert SourceLocation (1-based) → Position (0-based).
        var pos = new Position(Math.Max(0, loc.Line - 1), Math.Max(0, loc.Column - 1));
        var keyName = NoteStreamContext.FindEnclosingKey(ast, tokens, source, pos);
        if (keyName is null) return;  // D-15: no enclosing key → silent

        // D-02: parse key+mode (canonical root form).
        if (!ScaleDatabase.TryParseKeyWithMode(keyName, out var root, out var mode))
            return;  // D-22: silent fail-open on unparseable key

        // D-04 / D-05: 7-string diatonic spelling set.
        var spellings = DiatonicSpellings.GetDiatonicSpellings(root!, mode);
        if (spellings is null) return;  // D-22 silent (defensive)

        // D-08 / Pitfall 4: NoteName is already cent-stripped — strip the octave.
        var (spelling, octave) = ExtractSpellingAndOctave(noteName);

        // D-01: spelling-aware membership check.
        if (spellings.Contains(spelling)) return;  // diatonic — no diagnostic

        // Build Diagnostic per D-16/D-17/D-18.
        diagnostics.Add(BuildDiagnostic(noteName, spelling, octave, loc, keyName, spellings, tokens));
    }

    /// <summary>
    /// Strip the trailing octave digit(s) and any trailing cent-offset sign from a
    /// NoteName like "F#4" / "Ebb3" / "C10" / "E4+" (cent-offset prefix kept by lexer
    /// per SimpleLexer note-with-trailing-+/- branch — the cent magnitude/unit is
    /// peeled off into a separate CentLiteral token so we only need to drop the sign
    /// here). Returns (spelling, octave). Octave defaults to 4 when absent.
    /// Per D-08, cent offsets are irrelevant to diatonicity — base spelling decides.
    /// </summary>
    private static (string Spelling, int Octave) ExtractSpellingAndOctave(string noteName)
    {
        if (string.IsNullOrEmpty(noteName)) return ("", 4);
        // Drop a trailing cent-offset sign ('+' or '-') that the lexer glues onto
        // notes (see SimpleLexer.NextToken note-suffix branch). The cent magnitude
        // and unit ('50c') are a separate CentLiteral token, so only the sign reaches us.
        int end = noteName.Length;
        while (end > 0 && (noteName[end - 1] == '+' || noteName[end - 1] == '-')) end--;
        var trimmed = noteName[..end];
        // Strip trailing octave digits.
        int i = trimmed.Length;
        while (i > 0 && char.IsDigit(trimmed[i - 1])) i--;
        var spelling = trimmed[..i];
        var octStr = trimmed[i..];
        int octave = 4;
        if (octStr.Length > 0 && int.TryParse(octStr, out var o)) octave = o;
        return (spelling, octave);
    }

    /// <summary>Map a letter+accidental spelling to its 0..11 pitch class. Returns null on malformed input.</summary>
    private static int? SpellingToPitchClass(string spelling)
    {
        if (string.IsNullOrEmpty(spelling)) return null;
        int pc;
        switch (char.ToUpper(spelling[0]))
        {
            case 'C': pc = 0; break;
            case 'D': pc = 2; break;
            case 'E': pc = 4; break;
            case 'F': pc = 5; break;
            case 'G': pc = 7; break;
            case 'A': pc = 9; break;
            case 'B': pc = 11; break;
            default: return null;
        }
        for (int k = 1; k < spelling.Length; k++)
        {
            if (spelling[k] == '#') pc++;
            else if (spelling[k] == 'b') pc--;
            else return null;
        }
        return ((pc % 12) + 12) % 12;
    }

    private static Diagnostic BuildDiagnostic(
        string noteName,
        string spelling,
        int octave,
        FlowLang.Core.SourceLocation loc,
        string keyName,
        IReadOnlySet<string> spellings,
        IReadOnlyList<Token> tokens)
    {
        // Pitfall 3: token-to-element matching by full SourceLocation equality.
        int matchedIdx = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Location.Line == loc.Line && t.Location.Column == loc.Column)
            {
                matchedIdx = i;
                break;
            }
        }
        Token? matched = matchedIdx >= 0 ? tokens[matchedIdx] : null;

        // Display text uses Token.DiagnosticText (= OriginalText ?? Text) for D-15
        // spelling preservation through H→B canonicalization (Phase 21 D-15).
        string display = matched?.DiagnosticText ?? noteName;
        int line0 = Math.Max(0, loc.Line - 1);
        int col0 = Math.Max(0, loc.Column - 1);
        int width = matched?.Text.Length ?? 1;

        // D-17: token-wide squiggle covers the composer-typed extent of the note.
        // The lexer breaks `F#4q` into two tokens (NoteLiteral "F#4" + Identifier "q";
        // see SimpleLexer note+duration-suffix path that rewinds by 1). Extend the
        // range to absorb an immediately-adjacent single-character duration suffix
        // so the squiggle covers the full visible note as the composer typed it.
        if (matched is not null && matchedIdx + 1 < tokens.Count)
        {
            var next = tokens[matchedIdx + 1];
            int matchEnd = matched.Location.Column + matched.Text.Length;
            if (next.Type == TokenType.Identifier
                && next.Location.Line == matched.Location.Line
                && next.Location.Column == matchEnd
                && next.Text.Length == 1
                && (next.Text[0] is 'w' or 'h' or 'q' or 'e' or 's' or 't'))
            {
                width += next.Text.Length;
            }
        }

        var range = new Range(new Position(line0, col0), new Position(line0, col0 + width));

        // D-16: three-branch message.
        var typedPc = SpellingToPitchClass(spelling);
        string? enharmonicMatch = null;
        if (typedPc.HasValue)
        {
            foreach (var s in spellings)
            {
                var pc = SpellingToPitchClass(s);
                if (pc.HasValue && pc.Value == typedPc.Value)
                {
                    enharmonicMatch = s;
                    break;
                }
            }
        }

        string message;
        if (enharmonicMatch is not null)
        {
            // Spelling-aware case: typed pitch-class IS diatonic but spelling is not.
            message = $"{display} not diatonic in {keyName}; pitch-class matches {enharmonicMatch} (try {enharmonicMatch}{octave})";
        }
        else
        {
            // Standard case: find lower + upper diatonic neighbors by pitch-class distance.
            var (lower, upper) = FindNeighbors(spelling, spellings);
            if (lower is not null && upper is not null)
                message = $"{display} not diatonic in {keyName} (try {lower}{octave} or {upper}{octave})";
            else if (lower is not null)
                message = $"{display} not diatonic in {keyName} (try {lower}{octave})";
            else if (upper is not null)
                message = $"{display} not diatonic in {keyName} (try {upper}{octave})";
            else
                message = $"{display} not diatonic in {keyName}";
        }

        return new Diagnostic
        {
            Severity = DiagnosticSeverity.Information,
            Source = "flow.scaleLint",
            Message = message,
            Range = range
        };
    }

    /// <summary>
    /// Find the diatonic spelling immediately below and immediately above the typed
    /// spelling by chromatic distance. Lower-first ordering per Pattern 4 in RESEARCH.
    /// </summary>
    private static (string? Lower, string? Upper) FindNeighbors(string typed, IReadOnlySet<string> spellings)
    {
        var typedPc = SpellingToPitchClass(typed);
        if (!typedPc.HasValue) return (null, null);
        string? lower = null;
        string? upper = null;
        int lowerDist = int.MaxValue;
        int upperDist = int.MaxValue;
        foreach (var s in spellings)
        {
            var pc = SpellingToPitchClass(s);
            if (!pc.HasValue) continue;
            // Distance walking down from typed (lower neighbor).
            int dDown = ((typedPc.Value - pc.Value) + 12) % 12;
            // Distance walking up from typed (upper neighbor).
            int dUp = ((pc.Value - typedPc.Value) + 12) % 12;
            if (dDown > 0 && dDown < lowerDist) { lowerDist = dDown; lower = s; }
            if (dUp > 0 && dUp < upperDist) { upperDist = dUp; upper = s; }
        }
        return (lower, upper);
    }
}
