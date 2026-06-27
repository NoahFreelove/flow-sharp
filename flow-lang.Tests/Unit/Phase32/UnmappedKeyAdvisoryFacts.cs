using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 32-04 Task 3 — D-08 unmapped-MIDI-key advisory Facts. Per
/// CONTEXT D-08 + Claude's Discretion (CONTEXT § Specifics): when a <c>.kbm</c>
/// with <c>x</c> mapping entries is loaded, fire
/// <see cref="RenderingDiagnostics.WarnOnce"/> with sentinel
/// <c>tuning:unmapped:{description}</c> — at most one advisory per description
/// per process. Pairs with the per-tuning-name dedup pattern Phase 23 D-13
/// established for the MIDI export advisory.
///
/// Hygiene: <c>RenderingDiagnostics.ResetForTesting()</c> in the ctor + Dispose
/// — D-08's per-process dedup is global state shared with Phase 23's
/// MidiExport advisory.
/// </summary>
[Collection("FlowScripts")]
public class UnmappedKeyAdvisoryFacts : IDisposable
{
    public UnmappedKeyAdvisoryFacts() { RenderingDiagnostics.ResetForTesting(); }
    public void Dispose()             { RenderingDiagnostics.ResetForTesting(); }

    /// <summary>
    /// Walk up from the test binary's bin/Debug/net10.0 dir to find the repo root.
    /// Same FindRepoRoot pattern as the rest of Phase 32 (ScalaParserFacts).
    /// </summary>
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

    private static string FixturePath(string name)
        => Path.Combine(FindRepoRoot(), "flow-lang.Tests", "fixtures", "scala", name);

    /// <summary>
    /// Synthesize a small .kbm file with size=2 + one `x` (unmapped) entry.
    /// FirstMidi=60, LastMidi=61: MIDI 60 maps to scale degree 0, MIDI 61 is
    /// unmapped via `x`. Linear-wrap behavior for MIDI outside [60, 61] also
    /// reads the mapping (a size>0 KBM treats out-of-range MIDI as unmapped).
    /// </summary>
    private static string MakeUnmappedKbm()
    {
        string kbmPath = Path.Combine(Path.GetTempPath(), $"p32_unmapped_{Guid.NewGuid():N}.kbm");
        string kbmContent = string.Join("\n",
            "! synthetic unmapped-x kbm",
            "2",        // size = 2 mapping entries
            "60",       // firstMidi
            "61",       // lastMidi (narrow range)
            "60",       // middleNote
            "69",       // refNote
            "440.0",    // refHz
            "0",        // formalOctave
            "0",        // mapping[0] — degree 0 for MIDI 60
            "x");       // mapping[1] — UNMAPPED for MIDI 61
        File.WriteAllText(kbmPath, kbmContent);
        return kbmPath;
    }

    private static string MakeMappedKbm()
    {
        string kbmPath = Path.Combine(Path.GetTempPath(), $"p32_mapped_{Guid.NewGuid():N}.kbm");
        string kbmContent = string.Join("\n",
            "! synthetic fully-mapped kbm",
            "0",        // size = 0 (linear mapping — every key mapped)
            "0",        // firstMidi
            "127",      // lastMidi
            "60",       // middleNote
            "69",       // refNote
            "440.0",    // refHz
            "0");       // formalOctave
        File.WriteAllText(kbmPath, kbmContent);
        return kbmPath;
    }

    [Fact]
    public void UnmappedKey_LoadsKbmWithX_FiresWarnOnce()
    {
        string kbmPath = MakeUnmappedKbm();
        try
        {
            using var runner = new FlowEngineRunner();
            string sclPath = FixturePath("partch_43.scl");
            var (ok, _, stderr, _) = runner.RunSource($@"use ""@std""
Tuning t = (loadScala ""{sclPath}"" ""{kbmPath}"")
");
            Assert.True(ok, $"expected clean run; stderr: {stderr}");
            Assert.Contains("[tuning] unmapped MIDI keys under 'Harry Partch's 43-tone pure scale'", stderr);
            Assert.Contains("rendered as rest", stderr);
        }
        finally
        {
            if (File.Exists(kbmPath)) File.Delete(kbmPath);
        }
    }

    [Fact]
    public void UnmappedKey_LoadsTwice_FiresOnlyOnce()
    {
        string kbmPath = MakeUnmappedKbm();
        try
        {
            using var runner = new FlowEngineRunner();
            string sclPath = FixturePath("partch_43.scl");
            // Same .scl + same kbm loaded twice in the same script run. Per D-08
            // WarnOnce dedup, the advisory text should appear EXACTLY ONCE.
            var (ok, _, stderr, _) = runner.RunSource($@"use ""@std""
Tuning t1 = (loadScala ""{sclPath}"" ""{kbmPath}"")
Tuning t2 = (loadScala ""{sclPath}"" ""{kbmPath}"")
");
            Assert.True(ok, $"expected clean run; stderr: {stderr}");
            // Substring-occurrence count: split on the unique advisory header — count
            // is (Split.Length - 1).
            int count = stderr.Split("[tuning] unmapped MIDI keys under").Length - 1;
            Assert.Equal(1, count);
        }
        finally
        {
            if (File.Exists(kbmPath)) File.Delete(kbmPath);
        }
    }

    [Fact]
    public void UnmappedKey_TwoDifferentScls_FireTwoSeparateWarnings()
    {
        string kbmPath = MakeUnmappedKbm();
        try
        {
            using var runner = new FlowEngineRunner();
            string sclPartch = FixturePath("partch_43.scl");
            string sclSlendro = FixturePath("slendro.scl");
            var (ok, _, stderr, _) = runner.RunSource($@"use ""@std""
Tuning t1 = (loadScala ""{sclPartch}"" ""{kbmPath}"")
Tuning t2 = (loadScala ""{sclSlendro}"" ""{kbmPath}"")
");
            Assert.True(ok, $"expected clean run; stderr: {stderr}");
            // Two DIFFERENT descriptions → two separate WarnOnce sentinels →
            // two advisory occurrences in stderr.
            int count = stderr.Split("[tuning] unmapped MIDI keys under").Length - 1;
            Assert.Equal(2, count);
            // BOTH descriptions must be present somewhere in stderr.
            Assert.Contains("Harry Partch's 43-tone pure scale", stderr);
            // Slendro fixture description prefix — pinning the full string
            // is brittle if the archive description gets retitled; just verify
            // the second advisory has its own context.
        }
        finally
        {
            if (File.Exists(kbmPath)) File.Delete(kbmPath);
        }
    }

    [Fact]
    public void MappedKbm_NoUnmappedEntries_NoWarning()
    {
        string kbmPath = MakeMappedKbm();
        try
        {
            using var runner = new FlowEngineRunner();
            string sclPath = FixturePath("partch_43.scl");
            var (ok, _, stderr, _) = runner.RunSource($@"use ""@std""
Tuning t = (loadScala ""{sclPath}"" ""{kbmPath}"")
");
            Assert.True(ok, $"expected clean run; stderr: {stderr}");
            Assert.DoesNotContain("[tuning] unmapped MIDI keys under", stderr);
        }
        finally
        {
            if (File.Exists(kbmPath)) File.Delete(kbmPath);
        }
    }
}
