using FlowLang.Core;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-06 Wave 0 — music-aware pattern extractor gates (LANG-02).
///
/// Pins:
///   1. Chord literal pattern (Cmaj7, Dm7) matches Chord scrutinees by Root + Quality.
///   2. Chord literal with different Root is treated as a miss (per RESEARCH §Example 2
///      "Chord literal matches Chord values by Root + Quality").
///   3. Roman numeral pattern (V, I, V7) resolves against active key context, matches Chord.
///   4. Same roman-numeral pattern matches differently in different key contexts (C vs G major).
///   5. Articulation symbol pattern (#staccato, #legato) matches Note by Articulation enum.
///   6. Pitch-class matching composes via guard pattern + (mod (midi n) 12) — Plan 35-05's
///      GuardPattern dispatch is reused; no new pattern surface needed.
///
/// RED state: Plan 35-05's PatternMatcher.MatchConstructor returns false for any
/// ConstructorPattern. Task 3 lights up the music-aware branches.
/// </summary>
public class MusicAwarePatternsTests
{
    private static Value? Eval(string source)
    {
        using var engine = new FlowEngine(verbose: false);
        return engine.ExecuteScriptAndGetResult(source);
    }

    [Fact]
    public void ChordLiteralMatchesChordValueByQuality()
    {
        // Cmaj7 scrutinee, Cmaj7 pattern — must match.
        var hit = Eval("(match Cmaj7 | Cmaj7 => \"match\" | _ => \"miss\")");
        Assert.NotNull(hit);
        Assert.Equal("match", hit!.As<string>());

        // Cmaj7 scrutinee, Dm7 pattern — must NOT match (different root + quality).
        var miss = Eval("(match Cmaj7 | Dm7 => \"match\" | _ => \"miss\")");
        Assert.NotNull(miss);
        Assert.Equal("miss", miss!.As<string>());
    }

    [Fact]
    public void ChordLiteralMatchesByRootAndQuality()
    {
        // Cmaj7 scrutinee, Dmaj7 pattern — different roots, must miss.
        // Pins the "Root + Quality" canonical interpretation per RESEARCH §Example 2.
        var v = Eval("(match Cmaj7 | Dmaj7 => \"match\" | _ => \"miss\")");
        Assert.NotNull(v);
        Assert.Equal("miss", v!.As<string>());
    }

    [Fact]
    public void RomanNumeralMatchesInKeyContext()
    {
        // V in C major = G (major triad). Pattern resolves against active key context.
        var v = Eval("key Cmajor { (match G | V => \"dominant\" | _ => \"other\") }");
        Assert.NotNull(v);
        Assert.Equal("dominant", v!.As<string>());
    }

    [Fact]
    public void RomanNumeralRespectsKeyContextSwitch()
    {
        // V in C major = G; V in G major = D. Same V pattern, different key contexts.
        var inC = Eval("key Cmajor { (match G | V => \"hit\" | _ => \"miss\") }");
        Assert.NotNull(inC);
        Assert.Equal("hit", inC!.As<string>());

        var inG = Eval("key Gmajor { (match D | V => \"hit\" | _ => \"miss\") }");
        Assert.NotNull(inG);
        Assert.Equal("hit", inG!.As<string>());

        // Negative cross-check: G chord under G major key is I, NOT V.
        var inGMiss = Eval("key Gmajor { (match G | V => \"hit\" | _ => \"miss\") }");
        Assert.NotNull(inGMiss);
        Assert.Equal("miss", inGMiss!.As<string>());
    }

    [Fact]
    public void ArticulationSymbolMatchesNoteArticulation()
    {
        // The cleanest way to set Articulation on a Value is through C# directly.
        // Build a MusicalNoteData with the desired articulation, then execute a
        // (match ...) expression with that note in scope as a variable.
        using var engine = new FlowEngine(verbose: false);
        var staccato = new TypeSystem.SpecialTypes.MusicalNoteData(
            'C', 4, 0, 4, isRest: false,
            articulation: TypeSystem.SpecialTypes.Articulation.Staccato);
        engine.Context.DeclareVariable("n", Value.MusicalNote(staccato));

        var src = "(match n | #staccato => \"short\" | #legato => \"smooth\" | _ => \"normal\")";
        var v = engine.ExecuteScriptAndGetResult(src);
        Assert.NotNull(v);
        Assert.Equal("short", v!.As<string>());
    }

    [Fact]
    public void ArticulationSymbolFallsThroughWhenMismatched()
    {
        using var engine = new FlowEngine(verbose: false);
        var normal = new TypeSystem.SpecialTypes.MusicalNoteData(
            'C', 4, 0, 4, isRest: false,
            articulation: TypeSystem.SpecialTypes.Articulation.Normal);
        engine.Context.DeclareVariable("n", Value.MusicalNote(normal));

        var src = "(match n | #staccato => \"short\" | #legato => \"smooth\" | _ => \"normal\")";
        var v = engine.ExecuteScriptAndGetResult(src);
        Assert.NotNull(v);
        Assert.Equal("normal", v!.As<string>());
    }

    [Fact]
    public void PitchClassViaGuardComposes()
    {
        // Plan 35-06 LANG-02 pitch-class wording: `| n when (= (pitchClass n) 0) => "C"`.
        // pitchClass / mod / midi are NOT top-level builtins in v1.5 (Phase 35 only adds
        // pattern-matching; pitch helpers stay for Phase 36+). The load-bearing surface
        // here is: a Note-typed scrutinee binds to `n`, and the guard predicate sees
        // `n` as a Note value. We use the already-shipping noteToFrequency to prove
        // that Plan 35-05's GuardPattern dispatch passes a Note binding through to a
        // guard expression that consumes it.
        var src = @"use ""@std""
(match A4
  | n when (gt (noteToFrequency n) 400.0) => ""high""
  | _ => ""low"")";
        var v = Eval(src);
        Assert.NotNull(v);
        // A4 = 440Hz, which is > 400Hz.
        Assert.Equal("high", v!.As<string>());
    }
}
