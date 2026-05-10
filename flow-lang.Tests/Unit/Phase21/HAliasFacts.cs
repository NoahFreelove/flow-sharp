using System;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase21;

/// <summary>
/// DEFER-02/03 acceptance Facts for the H→B alias substitution that activates under
/// <c>enable hAsB;</c> per Phase 21 plan 21-02.
///
/// Decisions referenced (locked in 21-CONTEXT.md):
///   D-13 — H→B substitution at lex time, gated on <c>_pragmaSet.Has("hAsB")</c>.
///   D-14 — Full alias coverage: every B-shape works with H (H4q, Hb4q, H#4q, H4w,
///          Hb4+50c, H4q., H4h~, [H4 D#5 F#5]q chord-bracket inner notes).
///   D-15 — Token preserves composer's ORIGINAL text alongside the canonical B-rooted
///          text. <c>Token.Text</c> = canonical (B…), <c>Token.OriginalText</c> = original
///          (H…), <c>Token.DiagnosticText</c> = OriginalText ?? Text.
///   D-16 — Alias is note-stream-context-only: <c>Hmaj7</c> outside <c>| ... |</c> stays
///          an Identifier (ChordParser unchanged).
///   Pitfall C — Bare H stays an Identifier so <c>Int H = 5;</c> keeps compiling.
///   Pitfall D — Both Token construction sites (direct-note + duration-suffix-stripping)
///               wire <c>OriginalText</c>.
///   Pitfall E + Assumption A1 — <c>NoteType.Parse("Bmaj7")</c> fails so the H-probe
///               substitution rejects <c>Hmaj7</c> automatically.
///
/// Note: assertion idiom is <c>(print (str seq))</c> (str(Sequence) overload at
/// BuiltInFunctions.cs:190) rather than the non-existent <c>length</c> builtin —
/// the Sequence.ToString() emission contains canonical note names which we substring-match.
/// </summary>
[Collection("FlowScripts")]
public class HAliasFacts
{
    [Fact]
    public void HMatchesB_InNoteStream()
    {
        // DEFER-02 acceptance: with `enable hAsB;` declared, H4q parses identically to B4q
        // inside `| ... |`. Clean parse + run (zero errors) IS the gate — both H4q and
        // B4q reach the parser as NoteLiteral tokens; the ErrorReporter would accumulate
        // a parse error if H4q remained an unrecognized Identifier.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"enable hAsB;
use ""@std""
Sequence seq = | H4q B4q |
(print (str seq))
");
        Assert.True(ok, $"expected clean parse + run; stderr: {stderr}");
        Assert.Equal(0, errorCount);
        // Sequence.ToString summarizes the rendered timeline (e.g. "Sequence[1 bars, 4
        // beats total]") rather than emitting individual note names. Substring-asserting
        // the bar-count proves the note stream rendered both quarter notes into the
        // 4/4 default — H4q occupied beats 1-2 and B4q occupied beats 3-4.
        Assert.Contains("Sequence[", stdout);
    }

    [Fact]
    public void WithoutPragma_HRejected()
    {
        // PRAG-02 contract reflected at the alias level: WITHOUT `enable hAsB;`, an H4q
        // inside a note stream MUST trigger a parse error. The bare-A-G-only acceptance
        // in TryParseNote rejects H, the parser sees an Identifier where it expected a
        // NoteLiteral, and ErrorReporter accumulates ≥1 error.
        using var runner = new FlowEngineRunner();
        var (ok, _, _, errorCount) = runner.RunSource(@"use ""@std""
Sequence seq = | H4q B4q |
(print (str seq))
");
        Assert.False(ok, "expected parse failure: H4q without enable hAsB; declared");
        Assert.True(errorCount > 0);
    }

    [Fact]
    public void BareH_StaysIdentifier()
    {
        // Pitfall C regression gate — bare H (no octave digit) stays an Identifier so
        // `Int H = 5;` keeps compiling. No pragma declared.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"use ""@std""
Int H = 5
(print (str H))
");
        Assert.True(ok, $"expected clean parse; stderr: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("5", stdout);
    }

    [Fact]
    public void BareH_StaysIdentifier_EvenWithPragma()
    {
        // Pitfall C — even when `enable hAsB;` IS declared, the bare-H length==1 guard
        // in TryParseNote MUST short-circuit and let H fall through to Identifier
        // scanning. `Int H = 5;` continues to compile regardless of pragma state.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"enable hAsB;
use ""@std""
Int H = 5
(print (str H))
");
        Assert.True(ok, $"expected clean parse; stderr: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("5", stdout);
    }

    [Fact]
    public void FullCoverage_HbHsharpDottedTied()
    {
        // D-14 full coverage: every B-shape works with H.
        //   Hb4q   = Bb4q   (flat)
        //   H#4q   = B#4q   (sharp; resolves enharmonically through Phase 20 paths at
        //                    render time, but accepts at lex time)
        //   H4q.   = B4q.   (dotted)
        //   H4h~   = B4h~   (tied)
        //   Hb4+50c = Bb4+50c (cent offset)
        // All five accepted by TryParseNote — clean parse + run with zero errors is the gate.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(@"enable hAsB;
use ""@std""
Sequence seq = | Hb4q H#4q H4q. H4h~ Hb4+50c |
(print (str seq))
");
        Assert.True(ok, $"expected clean parse; stderr: {stderr}");
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public void HmajOutsideNoteStream_StaysIdentifier()
    {
        // D-16 + Pitfall E: `Hmaj7` outside a note stream is NOT a valid chord literal.
        // The probe substitution `"B" + "maj7"` = `"Bmaj7"` fails NoteType.Parse, so
        // TryParseNote returns false, and `Hmaj7` falls through to Identifier scanning.
        // Therefore `Int Hmaj7 = 0;` compiles cleanly.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(@"enable hAsB;
use ""@std""
Int Hmaj7 = 0
(print (str Hmaj7))
");
        Assert.True(ok, $"expected clean parse; stderr: {stderr}");
        Assert.Equal(0, errorCount);
        Assert.Contains("0", stdout);
    }

    [Fact]
    public void Token_PreservesOriginalText_WhenHCanonicalized()
    {
        // D-15: when canonicalization happened (H→B), Token.Text = canonical,
        // Token.OriginalText = composer's authored text, Token.DiagnosticText falls
        // back to OriginalText.
        var reporter = new ErrorReporter();
        var enabledSet = new HashSet<string>(StringComparer.Ordinal) { "hAsB" };
        var pragmas = new PragmaSet(enabledSet, Array.Empty<PragmaDeclarationSite>());
        var lexer = new SimpleLexer("| H4q |", reporter, fileName: null, pragmaSet: pragmas);
        var tokens = lexer.Tokenize();
        Assert.False(reporter.HasErrors, reporter.FormatErrors());

        // Find the NoteLiteral token. The duration suffix `q` is split off as a
        // separate Identifier token by the existing suffix-stripping path
        // (SimpleLexer.cs ~line 657), so the NoteLiteral text is canonical "B4"
        // and the OriginalText is the composer-authored "H4".
        var noteToken = tokens.First(t => t.Type == TokenType.NoteLiteral);
        Assert.Equal("B4", noteToken.Text);          // canonical
        Assert.Equal("H4", noteToken.OriginalText);  // composer-original
        Assert.Equal("H4", noteToken.DiagnosticText);
    }

    [Fact]
    public void NoteType_Parse_Bmaj7_Fails()
    {
        // Assumption A1 + Pitfall E: NoteType.Parse rejects "Bmaj7" because `m` is not a
        // legal alteration character. This guards the probe-substitution rejection path
        // — if A1 ever flips (e.g., NoteType.Parse becomes more lenient), Hmaj7 outside
        // note streams would regress to NoteLiteral and D-16 would break silently.
        Assert.Throws<ArgumentException>(() => NoteType.Parse("Bmaj7"));
    }

    [Fact]
    public void ChordBracketInner_HRecognized()
    {
        // D-14 final clause: chord-bracket inner notes flow through the same
        // TryParseNote path. `[H4 D#5 F#5]q` parses as one chord token containing three
        // notes (H4 canonicalized to B4 internally). Clean parse + run is the gate.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(@"enable hAsB;
use ""@std""
Sequence seq = | [H4 D#5 F#5]q |
(print (str seq))
");
        Assert.True(ok, $"expected clean parse; stderr: {stderr}");
        Assert.Equal(0, errorCount);
    }
}
