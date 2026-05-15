using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Parsing;
using FlowLang.StandardLibrary.Audio.Tuning;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 02 — .kbm parser + Default factory + exception-format Facts.
///
/// Facts in this file:
/// - Task 1 (exception-format): 2 Facts pinning ScalaParseException +
///   ScalaKbmParseException message format ({file}:{line}:{col} — expected X, got 'Y'
///   with em-dash U+2014).
/// - Task 3 (parser + factory): ≥ 6 Facts pinning ScalaKbmParser.Parse + Default.
///
/// Tied to SPEC-4 (.kbm format support) and SPEC-7 (diagnostic clarity).
/// </summary>
public class ScalaKbmParserFacts
{
    // ─── Task 1: exception-format Facts ──────────────────────────────────────

    [Fact]
    public void ScalaParseException_MessageFormat_MatchesFlowDiagnosticStyle()
    {
        // Plan 32-02 Task 1 behavior: em-dash U+2014 in the separator, quoted 'found'.
        var ex = new ScalaParseException(
            filePath: "foo.scl",
            line: 4,
            column: 1,
            expected: "step count (positive integer)",
            found: "-5");
        Assert.Equal("foo.scl:4:1 — expected step count (positive integer), got '-5'", ex.Message);
        Assert.Equal("foo.scl", ex.FilePath);
        Assert.Equal(4, ex.Line);
        Assert.Equal(1, ex.Column);
        Assert.Equal("step count (positive integer)", ex.Expected);
        Assert.Equal("-5", ex.Found);
        // Inheritance contract: extends Flow's existing ParseException so callers can
        // catch the shared base type (mirror of TypeParser.cs:335).
        Assert.IsAssignableFrom<ParseException>(ex);
    }

    [Fact]
    public void ScalaKbmParseException_MessageFormat_MatchesFlowDiagnosticStyle()
    {
        var ex = new ScalaKbmParseException(
            filePath: "bad.kbm",
            line: 7,
            column: 1,
            expected: "reference frequency (positive Hz)",
            found: "-50");
        Assert.Equal("bad.kbm:7:1 — expected reference frequency (positive Hz), got '-50'", ex.Message);
        Assert.Equal("bad.kbm", ex.FilePath);
        Assert.Equal(7, ex.Line);
        Assert.Equal(1, ex.Column);
        Assert.Equal("reference frequency (positive Hz)", ex.Expected);
        Assert.Equal("-50", ex.Found);
        Assert.IsAssignableFrom<ParseException>(ex);
    }

    // ─── Task 3: ScalaKbmParser Facts ────────────────────────────────────────

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo root");
    }

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", name);
        return File.ReadAllText(path);
    }

    private static ParsedScala ParsePartch43()
    {
        var content = LoadFixture("partch_43.scl");
        return ScalaParser.Parse(content, "partch_43.scl");
    }

    [Fact]
    public void Default_ForOctaveTuning_LinearMapping_Adopts1200CentPeriod()
    {
        // D-05/D-07: synthetic linear-mapping factory. Period auto-adopts the
        // tuning's PeriodCents — for partch_43 that's 1200.0 (2/1 final step).
        var scl = ParsePartch43();
        var kbm = ScalaKbmParser.Default(scl);

        Assert.Equal(0, kbm.Size);          // linear mapping
        Assert.Equal(60, kbm.MiddleNote);   // C4
        Assert.Equal(69, kbm.ReferenceNote); // A4
        Assert.Equal(440.0, kbm.ReferenceHz);
        Assert.Equal(0, kbm.FormalOctave);  // 0 = use scl's period as wrap
        Assert.Empty(kbm.Mapping);          // size=0 → no mapping entries
        Assert.Equal(scl.PeriodCents, kbm.Period);
        Assert.Equal(1200.0, kbm.Period, precision: 9);
        Assert.Equal(0, kbm.FirstMidi);
        Assert.Equal(127, kbm.LastMidi);
    }

    [Fact]
    public void Default_ForNonOctaveTuning_Adopts1404CentPeriod_D07()
    {
        // D-07 is the key design insight: the default KBM auto-adopts the
        // tuning's period. For Carlos Alpha (period 1404¢), this dissolves the
        // period-mismatch edge case structurally.
        var content = LoadFixture("carlos_alpha.scl");
        var scl = ScalaParser.Parse(content, "carlos_alpha.scl");
        var kbm = ScalaKbmParser.Default(scl);

        Assert.Equal(1404.0, kbm.Period, precision: 9);
        Assert.Equal(scl.PeriodCents, kbm.Period);
    }

    [Fact]
    public void Parse_MinimalValidKbm_LinearMapping_Size0()
    {
        // Handcrafted minimal valid .kbm: size=0 + 6 header fields + formalOctave=0.
        // Linear mapping, no mapping entries.
        var content =
            "! minimal valid kbm\n" +
            "0\n" +    // size
            "0\n" +    // firstMidi
            "127\n" +  // lastMidi
            "60\n" +   // middleNote
            "69\n" +   // referenceNote
            "440.0\n" + // referenceHz
            "0\n";     // formalOctave
        var kbm = ScalaKbmParser.Parse(content, "test.kbm");

        Assert.Equal(0, kbm.Size);
        Assert.Equal(0, kbm.FirstMidi);
        Assert.Equal(127, kbm.LastMidi);
        Assert.Equal(60, kbm.MiddleNote);
        Assert.Equal(69, kbm.ReferenceNote);
        Assert.Equal(440.0, kbm.ReferenceHz);
        Assert.Equal(0, kbm.FormalOctave);
        Assert.Empty(kbm.Mapping);
    }

    [Fact]
    public void Parse_Size2Mapping_WithUnmappedX_PreservesNullEntry()
    {
        // Handcrafted size-2 mapping: degree 0 mapped at index 0, unmapped at 1.
        // RESEARCH §unmapped encoding: literal lowercase `x` → null in Mapping[].
        var content =
            "! test kbm\n" +
            "2\n" +
            "60\n" +
            "61\n" +
            "60\n" +
            "69\n" +
            "440.0\n" +
            "0\n" +
            "0\n" +    // mapping entry 0: scale degree 0
            "x\n";     // mapping entry 1: unmapped per D-08
        var kbm = ScalaKbmParser.Parse(content, "test.kbm");

        Assert.Equal(2, kbm.Size);
        Assert.Equal(2, kbm.Mapping.Count);
        Assert.Equal(0, kbm.Mapping[0]);
        Assert.Null(kbm.Mapping[1]);
    }

    [Fact]
    public void Parse_MalformedKbm_NegativeReferenceFrequency_LineColExact()
    {
        var content = LoadFixture("malformed_kbm.kbm");
        var ex = Assert.Throws<ScalaKbmParseException>(
            () => ScalaKbmParser.Parse(content, "malformed_kbm.kbm"));

        // Plan 32-01's malformed_kbm.kbm puts -50.0 at line 7 col 1.
        Assert.Equal(7, ex.Line);
        Assert.Equal(1, ex.Column);
        Assert.Contains("reference frequency", ex.Expected);
        Assert.Contains("positive Hz", ex.Expected);
        Assert.Equal("-50.0", ex.Found);
        Assert.Matches(
            new System.Text.RegularExpressions.Regex(
                @"malformed_kbm\.kbm:\d+:1 — expected reference frequency \(positive Hz\), got '-50\.0'"),
            ex.Message);
    }

    [Fact]
    public void Parse_NonZeroFormalOctave_Rejected_RESEARCH_A10()
    {
        // Plan defers non-zero formal octaves to v1.5; Phase 32 strict-rejects
        // with a clear error per RESEARCH A10.
        var content =
            "0\n" +    // size
            "0\n" +    // firstMidi
            "127\n" +  // lastMidi
            "60\n" +   // middleNote
            "69\n" +   // referenceNote
            "440.0\n" + // referenceHz
            "2\n";     // formal octave NON-ZERO — should reject
        var ex = Assert.Throws<ScalaKbmParseException>(
            () => ScalaKbmParser.Parse(content, "nonzero_octave.kbm"));
        Assert.Contains("formal octave 0", ex.Expected);
    }
}
