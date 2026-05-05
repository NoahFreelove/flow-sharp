using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase24;

/// <summary>
/// Phase 24 Plan 24-03 (LINT-01/02/03 acceptance + D-01 through D-23 unit pins).
/// The analyzer is invoked unconditionally on every parse but short-circuits when
/// `enable scaleLint;` is absent (D-19 / LINT-02). When active, it walks the AST
/// looking for note-elements inside `key { ... }` blocks and flags non-diatonic
/// notes with Information-severity LSP Diagnostic instances tagged
/// Source="flow.scaleLint" (D-18).
///
/// Decisions referenced (24-CONTEXT.md): D-01 (spelling-aware), D-02 (7 modes),
/// D-04..D-10 (element traversal), D-11..D-14 (SKIPs), D-15 (no key → silent),
/// D-16 (message format), D-17 (token-wide range), D-18 (source string),
/// D-19 (pragma gate), D-21 (FindEnclosingKey reuse), D-22 (silent fail-open),
/// D-23 (no meta-diagnostic when no key block).
/// </summary>
public class ScaleLintAnalyzerFacts
{
    // ── LINT-01 Acceptance ──

    [Fact]
    public void NonDiatonic_FsharpInCmajor_FlagsOneDiagnostic()
    {
        var src = "enable scaleLint;\nkey Cmajor { | C4 D4 E4 F#4 G4 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Information, diags[0].Severity);
        Assert.Equal("flow.scaleLint", diags[0].Source);
        Assert.Contains("F#4", diags[0].Message);
        Assert.Contains("not diatonic in Cmajor", diags[0].Message);
    }

    // ── LINT-02 Acceptance (D-19 short-circuit) ──

    [Fact]
    public void PragmaAbsent_NeverFlags_LINT02()
    {
        var src = "key Cmajor { | C4 D4 E4 F#4 G4 | }";  // NO enable scaleLint;
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    // ── LINT-03 Acceptance (innermost-key-wins) ──

    [Fact]
    public void NestedKeys_InnermostWins_NoFlag()
    {
        // F#4 IS diatonic in Gmajor (the inner key) — D-21 says inner key wins.
        var src = "enable scaleLint;\nkey Cmajor { key Gmajor { | F#4 | } }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    // ── D-01 Spelling-aware ──

    [Fact]
    public void SpellingAware_EsharpInCmajor_Flags_PitchClassMatchHint()
    {
        var src = "enable scaleLint;\nkey Cmajor { | E#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Contains("pitch-class matches F", diags[0].Message);
    }

    // ── D-08 Cent offsets ──

    [Fact]
    public void CentOffset_E4plus50c_InCmajor_Silent()
    {
        // Base note E IS diatonic in Cmajor. Cents are intentional fine-tuning, never trigger lint.
        var src = "enable scaleLint;\nkey Cmajor { | E4+50c | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void CentOffset_Ebplus50c_InCmajor_FlagsBaseSpelling()
    {
        // Base spelling Eb is NOT diatonic in Cmajor. Cents irrelevant to the message.
        var src = "enable scaleLint;\nkey Cmajor { | Eb4+50c | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Contains("Eb4", diags[0].Message);
    }

    // ── D-11/D-12/D-14 SKIP rules ──

    [Fact]
    public void Skip_RomanNumerals()
    {
        // Roman numerals are diatonic-by-construction.
        var src = "enable scaleLint;\nkey Cmajor { | I IV V7 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void Skip_NamedChordLiterals()
    {
        // Named chord literals like F#m7 are intentional declarative notation.
        // Composers reaching for borrowed chords / modal mixture; do NOT flag.
        var src = "enable scaleLint;\nkey Cmajor { | F#m7 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void Skip_Rests()
    {
        var src = "enable scaleLint;\nkey Cmajor { | _q _h | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    // ── D-17 Token-wide range ──

    [Fact]
    public void Range_SpansFullTokenWidth()
    {
        var src = "enable scaleLint;\nkey Cmajor { | F#4q | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        var range = diags[0].Range;
        // F#4q is 4 characters wide; Range must span at least 4 (token-wide, not 1-char default).
        var width = range.End.Character - range.Start.Character;
        Assert.True(width >= 4,
            $"expected Range width >= 4 for token 'F#4q'; got {width}");
    }

    // ── D-18 Source string ──

    [Fact]
    public void Source_IsFlowScaleLint()
    {
        var src = "enable scaleLint;\nkey Cmajor { | F#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.NotEmpty(diags);
        Assert.All(diags, d => Assert.Equal("flow.scaleLint", d.Source));
    }

    // ── D-22 Silent fail-open ──

    [Fact]
    public void UnparseableKey_SilentFailOpen()
    {
        // `Eblues` is not in TryParseKeyWithMode's accept-set (no "blues" mode).
        // Per D-22 the analyzer emits zero scale-lint diagnostics for that block.
        // Note: a separate IsValidKey error may surface — that is NOT a scale-lint
        // diagnostic, so this Fact filters by Source.
        var src = "enable scaleLint;\nkey Eblues { | F#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    // ── D-15 No enclosing key ──

    [Fact]
    public void NoEnclosingKey_Silent()
    {
        // Note stream OUTSIDE any key block. FindEnclosingKey returns null → silent.
        var src = "enable scaleLint;\n| F#4 |";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    // ── D-23 No key block ──

    [Fact]
    public void PragmaOn_NoKeyBlocks_Silent()
    {
        // Pragma declared but no key block exists anywhere. No meta-diagnostic.
        var src = "enable scaleLint;\n| C4 D4 |";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    // ── D-02 7-mode coverage ──

    [Theory]
    [InlineData("Cmajor",      "F#4")]   // F# is non-diatonic in Cmajor
    [InlineData("Aminor",      "G#4")]   // G# is non-diatonic in Aminor (natural)
    [InlineData("Edorian",     "F4")]    // Edorian has F# diatonic; F natural flags
    [InlineData("Cphrygian",   "E4")]    // Cphrygian has Eb diatonic; E natural flags
    [InlineData("Glydian",     "C4")]    // Glydian has C# diatonic; C natural flags
    [InlineData("Bmixolydian", "A#4")]   // Bmixolydian has A diatonic; A# flags
    [InlineData("Dlocrian",    "F#4")]   // Dlocrian has F diatonic; F# flags
    public void EachMode_FlagsExpectedNonDiatonic(string keyName, string nonDiatonicNote)
    {
        var src = $"enable scaleLint;\nkey {keyName} {{ | {nonDiatonicNote} | }}";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Contains(nonDiatonicNote, diags[0].Message);
    }
}
