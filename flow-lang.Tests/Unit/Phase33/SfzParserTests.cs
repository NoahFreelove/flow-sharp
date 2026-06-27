using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio.Sfz;
using Xunit;

namespace FlowLang.Tests.Unit.Phase33;

/// <summary>
/// Phase 33 Plan 33-04 Task 2 — fact suite proving the hand-rolled SFZ parser
/// shipped in Task 1. Mirrors Phase 32 <c>ScalaParserFacts</c>:
///
///   - <c>[Collection("FlowScripts")]</c> serializes Console.Error access so
///     the <see cref="RenderingDiagnostics"/> WarnOnce sentinel set is the only
///     shared state across facts; the ctor + Dispose <c>ResetForTesting</c>
///     calls keep the dedup contract clean per-fact (Pitfall 2).
///   - FindRepoRoot/LoadFixture helpers lifted from
///     <c>ScalaParserFacts.cs:27-43</c> verbatim.
///   - Synthetic SFZ content is built inline via raw strings — no fixture file
///     creation needed except <c>smoke.sfz</c> which is fed by the Plan 33-01
///     fixture under <c>flow-lang.Tests/fixtures/sfz-smoke/</c>.
///
/// Covers SPEC-3 (opcode whitelist + advisory dedup + strict numeric), SPEC-4
/// (grid lookup), SPEC-5 (loop_mode mapping), Pitfall 7 (pan ÷100), Pitfall 8
/// (volume dB→linear), Pitfall 11 (adjacent opcodes), T-33-PARSE-01 (MaxRegionCount
/// cap), T-33-NUM-01 (strict numeric), T-33-OPCODE-01 (StringComparer.Ordinal),
/// plus the VSCO-CONTROL-DECISION FOUND mandate (default_path cascade +
/// backslash → OS-separator normalisation).
/// </summary>
[Collection("FlowScripts")]
public class SfzParserTests : IDisposable
{
    public SfzParserTests() => RenderingDiagnostics.ResetForTesting();
    public void Dispose() => RenderingDiagnostics.ResetForTesting();

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

    private static string LoadSmokeFixture()
    {
        var path = Path.Combine(
            FindRepoRoot(), "flow-lang.Tests", "fixtures", "sfz-smoke", "smoke.sfz");
        return File.ReadAllText(path);
    }

    private static string SmokeFixturePath()
        => Path.Combine(
            FindRepoRoot(), "flow-lang.Tests", "fixtures", "sfz-smoke", "smoke.sfz");

    // ---- Sentinel for the WarnOnce dedup tests ----
    //
    // We can't reach inside RenderingDiagnostics' private HashSet. Instead we
    // capture Console.Error to a StringWriter for a fact-local read of how many
    // distinct lines fired during a given parse — sufficient to assert the
    // "no second advisory for the same key" contract.

    private sealed class CapturedStderr : IDisposable
    {
        private readonly TextWriter _previous;
        public StringWriter Buffer { get; } = new();
        public CapturedStderr()
        {
            _previous = Console.Error;
            Console.SetError(Buffer);
        }
        public void Dispose() => Console.SetError(_previous);
    }

    // ----------------------------------------------------------------
    // SmokeFixture_ParsesCleanly
    // ----------------------------------------------------------------

    [Fact]
    public void SmokeFixture_ParsesCleanly()
    {
        var content = LoadSmokeFixture();
        var sfz = SfzParser.Parse(content, SmokeFixturePath(), "smoke");

        Assert.Equal(2, sfz.Regions.Count);
        Assert.NotNull(sfz.Description);
        Assert.False(string.IsNullOrWhiteSpace(sfz.BasePath));

        // Region 1 covers (48..71, 1..127) at PitchKeycenter 60. Grid[60, 64]
        // must point at the C4_sine.wav region; grid[79, 64] must point at the
        // G5_sine.wav region (region 2 covers 72..127).
        var r60 = sfz.Grid[60, 64];
        Assert.NotNull(r60);
        Assert.EndsWith("C4_sine.wav", r60!.SamplePath);

        var r79 = sfz.Grid[79, 64];
        Assert.NotNull(r79);
        Assert.EndsWith("G5_sine.wav", r79!.SamplePath);

        // SortedByPitch covers 48..127 inclusive (ascending unique).
        Assert.Equal(48, sfz.SortedByPitch[0]);
        Assert.Equal(127, sfz.SortedByPitch[^1]);
    }

    // ----------------------------------------------------------------
    // AllKnownOpcodes_Parse
    // ----------------------------------------------------------------

    [Fact]
    public void AllKnownOpcodes_Parse()
    {
        // A single region declaring all 13 base opcodes (the 14th, default_path,
        // belongs to <control> and is exercised separately).
        var content = string.Join('\n', new[]
        {
            "<region>",
            "sample=foo.wav",
            "pitch_keycenter=64",
            "lokey=60",
            "hikey=72",
            "lovel=20",
            "hivel=120",
            "loop_mode=loop_continuous",
            "loop_start=128",
            "loop_end=4096",
            "ampeg_attack=0.05",
            "ampeg_release=0.20",
            "volume=-6",
            "pan=50",
        });
        var sfz = SfzParser.Parse(content, "/tmp/test.sfz", "test");

        Assert.Single(sfz.Regions);
        var r = sfz.Regions[0];
        Assert.EndsWith("foo.wav", r.SamplePath);
        Assert.Equal(64, r.PitchKeycenter);
        Assert.Equal(60, r.LoKey);
        Assert.Equal(72, r.HiKey);
        Assert.Equal(20, r.LoVel);
        Assert.Equal(120, r.HiVel);
        Assert.Equal(SfzLoopMode.LoopContinuous, r.LoopMode);
        Assert.Equal(128, r.LoopStart);
        Assert.Equal(4096, r.LoopEnd);
        Assert.Equal(0.05, r.AmpegAttack, precision: 6);
        Assert.Equal(0.20, r.AmpegRelease, precision: 6);
        // volume=-6 dB → 10^(-6/20) ≈ 0.5012
        Assert.Equal(0.5011872336, r.Volume, precision: 3);
        // pan=50 → 0.5
        Assert.Equal(0.5, r.Pan, precision: 6);
    }

    // ----------------------------------------------------------------
    // UnknownOpcode_AdvisoryOnce
    // ----------------------------------------------------------------

    [Fact]
    public void UnknownOpcode_AdvisoryOnce()
    {
        var content = string.Join('\n', new[]
        {
            "<region>",
            "sample=foo.wav",
            "fil_type=lpf_2p",
            "fil_cutoff=2000",
            "fil_resonance=3",
            "amp_velcurve=1",
            "bend_up=200",
            "lokey=60",
            "hikey=72",
        });

        using (var capture = new CapturedStderr())
        {
            var first = SfzParser.Parse(content, "/tmp/u.sfz", "patchU");
            Assert.Single(first.Regions);
            Assert.Equal(60, first.Regions[0].LoKey);
            Assert.Equal(72, first.Regions[0].HiKey);
            var afterFirst = capture.Buffer.ToString();

            // Five unknown opcodes → five advisory lines on first run.
            int firstAdvisoryCount = CountLines(afterFirst);
            Assert.Equal(5, firstAdvisoryCount);

            // Re-parse same content + same patchDescription. None of the five
            // unknown-opcode sentinels should fire again per the dedup contract.
            SfzParser.Parse(content, "/tmp/u.sfz", "patchU");
            var afterSecond = capture.Buffer.ToString();
            Assert.Equal(firstAdvisoryCount, CountLines(afterSecond));
        }
    }

    private static int CountLines(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        // Count newlines; trailing newline → don't double-count blank.
        int n = 0;
        foreach (var ch in s) if (ch == '\n') n++;
        return n;
    }

    // ----------------------------------------------------------------
    // CharitableIgnoreOpcodes_NoAdvisory — sweep-0614 gap-routing-tuning-format
    // ----------------------------------------------------------------

    [Fact]
    public void CharitableIgnoreOpcodes_NoAdvisory()
    {
        // ampeg_dynamic + tune are whitelisted as charitable-ignore (e.g. the
        // VSCO-CE SViolinVib patch declares both). They must parse WITHOUT
        // firing the unrecognized-opcode WarnOnce advisory, while every other
        // recognized opcode still applies.
        var content = string.Join('\n', new[]
        {
            "<region>",
            "sample=foo.wav",
            "ampeg_dynamic=1",
            "tune=-7",
            "lokey=60",
            "hikey=72",
        });

        using var capture = new CapturedStderr();
        var sfz = SfzParser.Parse(content, "/tmp/charitable.sfz", "charitablePatch");

        Assert.Single(sfz.Regions);
        Assert.Equal(60, sfz.Regions[0].LoKey);
        Assert.Equal(72, sfz.Regions[0].HiKey);

        // No "unrecognized opcode" advisory for ampeg_dynamic / tune.
        var stderr = capture.Buffer.ToString();
        Assert.DoesNotContain("unrecognized opcode", stderr);
        Assert.DoesNotContain("ampeg_dynamic", stderr);
        Assert.DoesNotContain("tune", stderr);
    }

    // ----------------------------------------------------------------
    // HeaderInheritance
    // ----------------------------------------------------------------

    [Fact]
    public void HeaderInheritance()
    {
        var content = string.Join('\n', new[]
        {
            "<global>",
            "ampeg_attack=0.01",
            "ampeg_release=0.5",
            "<group>",
            "volume=-6",  // group-level
            "<region>",
            "sample=a.wav",
            "lokey=60",
            "hikey=71",
            "<region>",
            "sample=b.wav",
            "lokey=72",
            "hikey=84",
            "volume=0",   // region overrides group's -6 dB
        });
        var sfz = SfzParser.Parse(content, "/tmp/h.sfz", "h");

        Assert.Equal(2, sfz.Regions.Count);

        // Both regions inherit ampeg_attack/release from <global>.
        Assert.Equal(0.01, sfz.Regions[0].AmpegAttack, precision: 6);
        Assert.Equal(0.5, sfz.Regions[0].AmpegRelease, precision: 6);
        Assert.Equal(0.01, sfz.Regions[1].AmpegAttack, precision: 6);
        Assert.Equal(0.5, sfz.Regions[1].AmpegRelease, precision: 6);

        // Region 0 inherits group's volume=-6 → linear ≈ 0.5012.
        Assert.Equal(0.5011872336, sfz.Regions[0].Volume, precision: 3);
        // Region 1 overrides with volume=0 → linear 1.0.
        Assert.Equal(1.0, sfz.Regions[1].Volume, precision: 6);
    }

    // ----------------------------------------------------------------
    // StrictNumeric_RejectsExponent
    // ----------------------------------------------------------------

    [Fact]
    public void StrictNumeric_RejectsExponent()
    {
        var content = string.Join('\n', new[]
        {
            "<region>",
            "sample=foo.wav",
            "volume=1.5e2",  // exponent — rejected
        });
        var sfz = SfzParser.Parse(content, "/tmp/n.sfz", "n");
        Assert.Single(sfz.Regions);
        // volume default = 0 dB → linear 1.0 (NOT 150).
        Assert.Equal(1.0, sfz.Regions[0].Volume, precision: 6);
    }

    // ----------------------------------------------------------------
    // StrictNumeric_RejectsThousands
    // ----------------------------------------------------------------

    [Fact]
    public void StrictNumeric_RejectsThousands()
    {
        var content = string.Join('\n', new[]
        {
            "<region>",
            "sample=foo.wav",
            "lokey=1,500",  // thousands separator — rejected
        });
        var sfz = SfzParser.Parse(content, "/tmp/n.sfz", "n2");
        Assert.Single(sfz.Regions);
        // lokey default = 0.
        Assert.Equal(0, sfz.Regions[0].LoKey);
    }

    // ----------------------------------------------------------------
    // MaxRegionCount_Caps
    // ----------------------------------------------------------------

    [Fact]
    public void MaxRegionCount_Caps()
    {
        // 10001 <region> headers — must throw before completing.
        var sb = new StringBuilder();
        for (int i = 0; i <= SfzParser.MaxRegionCount; i++)
        {
            sb.Append("<region>\nsample=x.wav\n");
        }
        var ex = Assert.Throws<SfzParseException>(
            () => SfzParser.Parse(sb.ToString(), "/tmp/big.sfz", "big"));
        Assert.Contains("region count", ex.Message);
    }

    // ----------------------------------------------------------------
    // GridBuild_LastDeclaredWins
    // ----------------------------------------------------------------

    [Fact]
    public void GridBuild_LastDeclaredWins()
    {
        var content = string.Join('\n', new[]
        {
            "<region>",
            "sample=first.wav",
            "lokey=60",
            "hikey=60",
            "lovel=64",
            "hivel=64",
            "<region>",
            "sample=second.wav",
            "lokey=60",
            "hikey=60",
            "lovel=64",
            "hivel=64",
        });
        var sfz = SfzParser.Parse(content, "/tmp/g.sfz", "g");
        Assert.Equal(2, sfz.Regions.Count);
        // D-02: last write wins on the grid cell.
        Assert.NotNull(sfz.Grid[60, 64]);
        Assert.EndsWith("second.wav", sfz.Grid[60, 64]!.SamplePath);
    }

    // ----------------------------------------------------------------
    // SortedByPitch_AscendingUnique
    // ----------------------------------------------------------------

    [Fact]
    public void SortedByPitch_AscendingUnique()
    {
        var content = LoadSmokeFixture();
        var sfz = SfzParser.Parse(content, SmokeFixturePath(), "smokeSP");

        // Ascending strict ordering, unique.
        for (int i = 1; i < sfz.SortedByPitch.Length; i++)
        {
            Assert.True(sfz.SortedByPitch[i] > sfz.SortedByPitch[i - 1],
                $"SortedByPitch[{i}] = {sfz.SortedByPitch[i]} not > [{i-1}] = {sfz.SortedByPitch[i-1]}");
        }
        Assert.Equal(48, sfz.SortedByPitch[0]);
        Assert.Equal(127, sfz.SortedByPitch[^1]);
        // 80 unique pitches (48..127 inclusive).
        Assert.Equal(80, sfz.SortedByPitch.Length);
    }

    // ----------------------------------------------------------------
    // VolumeOpcode_DbToLinear
    // ----------------------------------------------------------------

    [Fact]
    public void VolumeOpcode_DbToLinear()
    {
        // volume=0 → linear 1.0
        var r0 = ParseSingleRegion(new[] {
            "<region>", "sample=x.wav", "volume=0",
        });
        Assert.Equal(1.0, r0.Volume, precision: 6);

        // volume=-6 → 10^(-6/20) ≈ 0.5012
        var r6 = ParseSingleRegion(new[] {
            "<region>", "sample=x.wav", "volume=-6",
        });
        Assert.Equal(Math.Pow(10.0, -6.0 / 20.0), r6.Volume, precision: 6);
        Assert.InRange(r6.Volume, 0.500, 0.502);

        // volume=-12 → ≈ 0.2512
        var r12 = ParseSingleRegion(new[] {
            "<region>", "sample=x.wav", "volume=-12",
        });
        Assert.Equal(Math.Pow(10.0, -12.0 / 20.0), r12.Volume, precision: 6);
        Assert.InRange(r12.Volume, 0.250, 0.252);
    }

    // ----------------------------------------------------------------
    // PanOpcode_NormalizedToFlowRange
    // ----------------------------------------------------------------

    [Fact]
    public void PanOpcode_NormalizedToFlowRange()
    {
        var r100 = ParseSingleRegion(new[] {
            "<region>", "sample=x.wav", "pan=100",
        });
        Assert.Equal(1.0, r100.Pan, precision: 6);

        var rNeg100 = ParseSingleRegion(new[] {
            "<region>", "sample=x.wav", "pan=-100",
        });
        Assert.Equal(-1.0, rNeg100.Pan, precision: 6);

        var r50 = ParseSingleRegion(new[] {
            "<region>", "sample=x.wav", "pan=50",
        });
        Assert.Equal(0.5, r50.Pan, precision: 6);

        // Default (no pan) → 0.0
        var rDef = ParseSingleRegion(new[] {
            "<region>", "sample=x.wav",
        });
        Assert.Equal(0.0, rDef.Pan, precision: 6);
    }

    // ----------------------------------------------------------------
    // MultipleOpcodesOnHeaderLine — Pitfall 11
    // ----------------------------------------------------------------

    [Fact]
    public void MultipleOpcodesOnHeaderLine()
    {
        // All three opcodes share a line with the <region> header.
        var content = "<region> sample=foo.wav lokey=60 hikey=72";
        var sfz = SfzParser.Parse(content, "/tmp/p.sfz", "p");
        Assert.Single(sfz.Regions);
        Assert.EndsWith("foo.wav", sfz.Regions[0].SamplePath);
        Assert.Equal(60, sfz.Regions[0].LoKey);
        Assert.Equal(72, sfz.Regions[0].HiKey);
    }

    // ----------------------------------------------------------------
    // LoopMode_UnknownValue_FallsBackToNoLoop
    // ----------------------------------------------------------------

    [Fact]
    public void LoopMode_UnknownValue_FallsBackToNoLoop()
    {
        using var capture = new CapturedStderr();
        var content = string.Join('\n', new[]
        {
            "<region>",
            "sample=foo.wav",
            "loop_mode=bogus",
        });
        var sfz = SfzParser.Parse(content, "/tmp/lm.sfz", "lm");
        Assert.Single(sfz.Regions);
        Assert.Equal(SfzLoopMode.NoLoop, sfz.Regions[0].LoopMode);

        // Advisory must mention the offending value verbatim.
        var stderr = capture.Buffer.ToString();
        Assert.Contains("loop_mode", stderr);
        Assert.Contains("bogus", stderr);
    }

    // ----------------------------------------------------------------
    // CommentStripping_RemovesLineComments
    // ----------------------------------------------------------------

    [Fact]
    public void CommentStripping_RemovesLineComments()
    {
        var content = string.Join('\n', new[]
        {
            "// header comment",
            "<region>",
            "sample=foo.wav // trailing inline comment",
            "lokey=60",
            "// stray comment line",
            "hikey=72",
        });
        var sfz = SfzParser.Parse(content, "/tmp/c.sfz", "c");
        Assert.Single(sfz.Regions);
        // sample value must NOT include the comment-derived suffix.
        Assert.EndsWith("foo.wav", sfz.Regions[0].SamplePath);
        Assert.Equal(60, sfz.Regions[0].LoKey);
        Assert.Equal(72, sfz.Regions[0].HiKey);
    }

    // ----------------------------------------------------------------
    // ControlHeader_DefaultPathCascade — VSCO-CONTROL-DECISION FOUND
    // ----------------------------------------------------------------

    [Fact]
    public void ControlHeader_DefaultPathCascade_BackslashNormalised()
    {
        // VSCO-CE pattern: <control> default_path=Strings\Solo Violin\Arco Vib\
        // declares the per-region sample= filename and the parser pre-joins
        // default_path so that downstream code can use SamplePath verbatim.
        var content = string.Join('\n', new[]
        {
            "<control>",
            "default_path=Strings\\Solo Violin\\Arco Vib\\",
            "<region>",
            "sample=LLVln_ArcoVib_A3_p.wav",
            "lokey=60",
            "hikey=72",
        });
        var sfz = SfzParser.Parse(content, "/tmp/v.sfz", "vsco-violin");

        Assert.Single(sfz.Regions);
        var path = sfz.Regions[0].SamplePath;
        // Backslashes normalised to OS separator (or `/`).
        Assert.DoesNotContain('\\', path);
        // Path includes the cascaded folder + filename.
        Assert.Contains("Strings", path);
        Assert.Contains("Solo Violin", path);
        Assert.Contains("Arco Vib", path);
        Assert.EndsWith("LLVln_ArcoVib_A3_p.wav", path);
    }

    // ----------------------------------------------------------------
    // NoControlHeader_PreservesPlainRelativePath
    // ----------------------------------------------------------------

    [Fact]
    public void NoControlHeader_PreservesPlainRelativePath()
    {
        // No <control> → SamplePath is the bare relative value (smoke fixture
        // codepath).
        var content = string.Join('\n', new[]
        {
            "<region>",
            "sample=C4_sine.wav",
            "lokey=60",
            "hikey=72",
        });
        var sfz = SfzParser.Parse(content, "/tmp/n.sfz", "n3");
        Assert.Single(sfz.Regions);
        Assert.Equal("C4_sine.wav", sfz.Regions[0].SamplePath);
    }

    // ---- helper -----------------------------------------------------
    private static SfzRegion ParseSingleRegion(IEnumerable<string> lines)
    {
        var content = string.Join('\n', lines);
        var sfz = SfzParser.Parse(content, "/tmp/single.sfz", "single-" + Guid.NewGuid().ToString("N"));
        Assert.Single(sfz.Regions);
        return sfz.Regions[0];
    }
}
