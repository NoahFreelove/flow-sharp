using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Sfz;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DRUM-01 — <c>(loadSfz #drums)</c> resolves to <c>GM-StylePerc.sfz</c>
/// via the sfz.flow GM dict and parses without error. W7 LOCK ACCEPTANCE:
/// <c>SfzData.IsPercussion</c> is set to <c>true</c> at SfzBuiltins LOAD TIME
/// when the dict-symbol is <c>#drums</c>; <c>false</c> for all other 19 GM
/// entries and for the <c>loadSfz(String)</c> bypass path.
///
/// <para>Setup: a per-test temp directory is seeded with a renamed-copy of
/// the Phase 33 smoke fixture <c>smoke.sfz</c> under names
/// <c>GM-StylePerc.sfz</c> + <c>UprightPiano.sfz</c>. The .sfz body is
/// well-formed for <see cref="SfzParser.Parse"/>; only the filename
/// differs. This sidesteps the need for a real VSCO-CE install in CI while
/// still exercising the dict lookup + parse + Value.Sfz wrapping codepath.
/// The W7 acceptance fact <see cref="LoadSfzDrums_SetsIsPercussionTrue"/>
/// directly inspects <see cref="SfzData.IsPercussion"/> on the unwrapped
/// SfzData, which is the source of truth — the actual .sfz file contents
/// are irrelevant for percussion classification per the W7 LOCK (dict-symbol
/// drives the flag, NOT filename or file contents).</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzDrumsLoadTest : IDisposable
{
    private readonly string _tmpSfzRoot;

    public SfzDrumsLoadTest()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        _tmpSfzRoot = Path.Combine(Path.GetTempPath(),
            $"p37_06_sfzroot_{Guid.NewGuid():N}");
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
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static void SeedFakeVscoBundle(string root)
    {
        string fixtureDir = Path.Combine(FindRepoRoot(), "flow-lang.Tests",
            "fixtures", "sfz-smoke");
        File.Copy(Path.Combine(fixtureDir, "smoke.sfz"),
            Path.Combine(root, "GM-StylePerc.sfz"));
        File.Copy(Path.Combine(fixtureDir, "smoke.sfz"),
            Path.Combine(root, "UprightPiano.sfz"));
        File.Copy(Path.Combine(fixtureDir, "C4_sine.wav"),
            Path.Combine(root, "C4_sine.wav"));
        File.Copy(Path.Combine(fixtureDir, "G5_sine.wav"),
            Path.Combine(root, "G5_sine.wav"));
    }

    /// <summary>
    /// Fact 1 — <c>(loadSfz #drums)</c> resolves the dict entry to
    /// <c>GM-StylePerc.sfz</c>, joins with <c>sfz_root</c>, parses, and the
    /// returned <c>Sfz</c> value has at least one region.
    /// </summary>
    [Fact]
    public void LoadSfzDrums_ResolvesFromGmDict()
    {
        FlowConfig.Active = new FlowConfigPoco { SfzRoot = _tmpSfzRoot };
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@sfz""
Sfz drums = (loadSfz #drums)
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        // Probe the actual SfzData via GetVariable + As<SfzData>().
        var v = runner.GetVariable("drums");
        var data = v.As<SfzData>();
        Assert.NotNull(data);
        Assert.True(data.Regions.Count >= 1,
            "expected smoke fixture to parse at least one region");
    }

    /// <summary>
    /// Fact 2 — W7 LOCK ACCEPTANCE: <c>SfzData.IsPercussion</c> is set to
    /// <c>true</c> when produced by <c>loadSfz(#drums)</c> and <c>false</c>
    /// for <c>loadSfz(#piano)</c>. The flag is driven by the dict-symbol at
    /// LOAD TIME, NOT by the filename or any file-content inspection.
    /// </summary>
    [Fact]
    public void LoadSfzDrums_SetsIsPercussionTrue()
    {
        FlowConfig.Active = new FlowConfigPoco { SfzRoot = _tmpSfzRoot };
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@sfz""
Sfz drums = (loadSfz #drums)
Sfz piano = (loadSfz #piano)
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var drumsData = runner.GetVariable("drums").As<SfzData>();
        var pianoData = runner.GetVariable("piano").As<SfzData>();

        Assert.True(drumsData.IsPercussion,
            "W7 LOCK: #drums must set SfzData.IsPercussion = true");
        Assert.False(pianoData.IsPercussion,
            "W7 LOCK: #piano must leave SfzData.IsPercussion = false");
    }

    /// <summary>
    /// Fact 3 — W7 LOCK ACCEPTANCE: <c>loadSfz(String)</c> bypass-the-dict
    /// path always yields <c>IsPercussion = false</c>. The composer who
    /// chose the string path opts out of percussion routing — even if the
    /// path happens to be GM-StylePerc.sfz. Per the W7 spec line: "the
    /// composer using the string path opts out of percussion routing." A
    /// future v1.6 builtin can let composers manually mark a string-loaded
    /// patch as percussion.
    /// </summary>
    [Fact]
    public void LoadSfzString_BypassPath_LeavesIsPercussionFalse()
    {
        string absPath = Path.Combine(_tmpSfzRoot, "GM-StylePerc.sfz");
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource($@"use ""@sfz""
Sfz drums = (loadSfz ""{absPath}"")
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");

        var data = runner.GetVariable("drums").As<SfzData>();
        Assert.False(data.IsPercussion,
            "W7 LOCK: loadSfz(String) bypass path leaves IsPercussion=false " +
            "even when the filename matches GM-StylePerc.sfz — composer opts " +
            "into percussion routing via the #drums dict-symbol path only.");
    }
}
