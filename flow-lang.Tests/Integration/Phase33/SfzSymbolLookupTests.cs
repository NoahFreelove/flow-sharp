using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase33;

/// <summary>
/// Phase 33 Plan 33-05 Task 2 — SPEC-2 acceptance facts for the Symbol
/// + String <c>loadSfz</c> overloads. With <c>sfz_root</c> set via a
/// <see cref="FlowConfig.Active"/> override and a real .sfz file at the
/// resolved path:
///
/// <list type="bullet">
///   <item><description><c>(loadSfz #violin)</c> looks up <c>"SViolinVib.sfz"</c>
///   in the 19-entry GM dict, joins with the override <c>sfz_root</c>,
///   parses the resulting absolute path, and returns a non-null Sfz value.</description></item>
///
///   <item><description><c>(loadSfz #notarealinstrument)</c> errors with a message
///   listing all 19 supported symbols (the entire dict surfaces in the
///   error so a composer's typo always gets a helpful suggestion).</description></item>
///
///   <item><description><c>(loadSfz #choir)</c> (a TBD row from the Plan 33-01 audit)
///   errors with a "not bundled with VSCO Community Edition" message
///   pointing at the absolute-path overload — distinguishable from a
///   missing-file FileNotFoundException because we want the composer to
///   know it is NOT their install that is broken.</description></item>
///
///   <item><description><c>(loadSfz "/abs/path.sfz")</c> bypasses the dict
///   entirely and parses the literal path; missing file produces a
///   normal FileNotFoundException-equivalent.</description></item>
/// </list>
///
/// <para>Setup uses a per-test temp directory seeded with renamed-copies of
/// the Plan 33-01 smoke fixture's <c>smoke.sfz</c>. The .sfz body is
/// well-formed for SfzParser.Parse (Plan 33-04); only the filename
/// differs. This sidesteps the need for a real VSCO-CE install while
/// still exercising the join + parse + Value.Sfz wrapping codepath.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzSymbolLookupTests : IDisposable
{
    private readonly string _tmpSfzRoot;

    public SfzSymbolLookupTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        // Build a per-test temp directory and seed it with renamed copies of
        // the smoke fixture's .sfz body + sample WAVs. The path-join math
        // exercised here doesn't care about WAV contents — only that the
        // .sfz parses cleanly through SfzParser.Parse.
        _tmpSfzRoot = Path.Combine(Path.GetTempPath(),
            $"p33_05_sfzroot_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpSfzRoot);
        SeedFakeVscoBundle(_tmpSfzRoot);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        try { Directory.Delete(_tmpSfzRoot, recursive: true); } catch { /* best-effort */ }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Copies the Plan 33-01 smoke fixture's .sfz + .wav files into the temp
    /// dir, renaming the .sfz to "SViolinVib.sfz" so the GM dict lookup for
    /// <c>#violin</c> resolves to a real on-disk file. The .wav siblings keep
    /// their original names so SfzParser's <c>sample=</c> references inside
    /// the .sfz body still resolve once Plan 33-06's renderer loads them
    /// (not exercised by this plan, just for completeness so the parser
    /// produces well-formed regions).
    /// </summary>
    private static void SeedFakeVscoBundle(string root)
    {
        string fixtureDir = Path.Combine(FindRepoRoot(),
            "flow-lang.Tests", "fixtures", "sfz-smoke");
        // The .sfz body — copy under the violin name so #violin resolves.
        File.Copy(Path.Combine(fixtureDir, "smoke.sfz"),
            Path.Combine(root, "SViolinVib.sfz"));
        // Also copy under a few other GM-dict filenames so additional symbols
        // resolve without needing per-test setup. Mirrors the Plan 33-01
        // audit's 15 verified rows.
        foreach (string name in new[]
            {
                "ViolaEnsSusVib.sfz",
                "CelloEnsSusVib.sfz",
                "FluteSusVib.sfz",
            })
        {
            File.Copy(Path.Combine(fixtureDir, "smoke.sfz"),
                Path.Combine(root, name));
        }
        // WAV siblings (referenced by the .sfz body's `sample=` opcodes).
        File.Copy(Path.Combine(fixtureDir, "C4_sine.wav"),
            Path.Combine(root, "C4_sine.wav"));
        File.Copy(Path.Combine(fixtureDir, "G5_sine.wav"),
            Path.Combine(root, "G5_sine.wav"));
    }

    [Fact]
    public void LoadSfz_WithSymbol_AndConfig_ResolvesPath()
    {
        FlowConfig.Active = new FlowConfigPoco { SfzRoot = _tmpSfzRoot };
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@sfz""
Sfz v = (loadSfz #violin)
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
    }

    [Fact]
    public void LoadSfz_MultipleSymbols_AllResolve()
    {
        // Smoke-tests the dict-lookup path for several entries, not just #violin —
        // catches a regression where one entry works by accident (e.g. case-folded
        // symbol name).
        FlowConfig.Active = new FlowConfigPoco { SfzRoot = _tmpSfzRoot };
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@sfz""
Sfz v1 = (loadSfz #violin)
Sfz v2 = (loadSfz #viola)
Sfz v3 = (loadSfz #cello)
Sfz v4 = (loadSfz #flute)
");
        Assert.True(ok, $"expected clean run on 4 symbols; stderr: {stderr}");
    }

    [Fact]
    public void LoadSfz_WithUnknownSymbol_Errors()
    {
        FlowConfig.Active = new FlowConfigPoco { SfzRoot = _tmpSfzRoot };
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@sfz""
Sfz v = (loadSfz #notarealinstrument)
");
        Assert.False(ok, "expected non-zero exit on unknown symbol");
        // The error message must list the 19 supported symbols so a composer
        // can spot their typo. Spot-check several of them — the full list is
        // long enough that exact-match would be brittle, but the presence of
        // multiple known symbols pins the contract.
        Assert.Contains("notarealinstrument", stderr);
        Assert.Contains("#violin", stderr);
        Assert.Contains("#piano", stderr);
        Assert.Contains("#timpani", stderr);
    }

    [Fact]
    public void LoadSfz_WithTbdSymbol_ErrorsWithVscoCeNote()
    {
        FlowConfig.Active = new FlowConfigPoco { SfzRoot = _tmpSfzRoot };
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@sfz""
Sfz v = (loadSfz #choir)
");
        Assert.False(ok, "expected non-zero exit on TBD symbol (#choir not in VSCO-CE 1.1.0)");
        // Composer-facing message must reference VSCO Community Edition AND the
        // absolute-path overload so they know the workaround.
        Assert.Contains("VSCO", stderr);
        Assert.Contains("loadSfz \"", stderr);
    }

    [Fact]
    public void LoadSfz_WithString_BypassesDict()
    {
        // No sfz_root needed for the absolute-path overload — the join doesn't run.
        // FlowConfig.Reset is already in ctor, so SfzRoot is null here.
        string absPath = Path.Combine(_tmpSfzRoot, "SViolinVib.sfz");
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource($@"use ""@sfz""
Sfz v = (loadSfz ""{absPath}"")
");
        Assert.True(ok, $"expected clean run on absolute path; stderr: {stderr}");
    }

    [Fact]
    public void LoadSfz_WithString_MissingFile_Errors()
    {
        string bogus = Path.Combine(_tmpSfzRoot, "does_not_exist.sfz");
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource($@"use ""@sfz""
Sfz v = (loadSfz ""{bogus}"")
");
        Assert.False(ok, "expected non-zero exit on missing absolute-path file");
        // The path or "not found" or "does not exist" should surface somewhere in
        // stderr — don't pin the exact wording (matches LoadScala_NonexistentFile_RaisesError
        // shape from Phase 32 LoadScalaBuiltinFacts.cs).
        Assert.True(
            stderr.Contains("does_not_exist") || stderr.Contains("not found")
                || stderr.Contains("could not find") || stderr.Length > 0,
            $"expected error message; got: {stderr}");
    }
}
