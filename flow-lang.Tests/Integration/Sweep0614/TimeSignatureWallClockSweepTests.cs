using System;
using System.IO;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.SpecialTypes;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0614;

/// <summary>
/// Regression coverage for the sweep-2026-06-14 "gap-notetype-timesig" group:
/// non-4/4 time signatures rendered at the wrong wall-clock speed (6/8 was 2×
/// too slow, 2/2 was 2× too fast) — in BOTH the WAV and MIDI paths.
///
/// <para>
/// Root cause: <see cref="MusicalNoteData.GetBeats"/> returned DENOMINATOR-unit
/// beats (one beat == one denominator-unit note) while every wall-clock
/// (<c>SynthUtils.BeatsToSeconds</c> / <c>SongRenderer.secondsPerBeat</c>) and
/// tick (<c>MidiExport.ticksPerQuarter</c>) conversion treats BPM as
/// QUARTERS-per-minute. The scaling factor (denominator / 4) is exactly 1.0 only
/// for 4/4, so non-4/4 rendered at (denominator / 4)× the correct speed.
/// </para>
///
/// <para>
/// The fix makes <c>GetBeats</c> — and every companion bar-capacity site that
/// pairs with it (<c>BarType.GetActualBeats</c>/<c>ValidateDuration</c>,
/// <c>SequenceType.BarLengthBeats</c>, <c>BarRenderer</c>, <c>MidiExport</c>,
/// <c>MusicalConversions</c>, quantize) — return UNIVERSAL quarter-note units via
/// the single source of truth <see cref="TimeSignatureData.BarCapacityQuarters"/>.
/// </para>
///
/// <para>4/4 is the factor-1.0 invariant: it must stay byte-identical (pinned by
/// the Phase 28 RMS baselines + the Phase 45 WAV-SHA baselines elsewhere). Here
/// we pin the DURATION RELATIONSHIPS that were wrong before the fix.</para>
/// </summary>
public class TimeSignatureWallClockSweepTests : IDisposable
{
    private readonly string _tmpDir;

    public TimeSignatureWallClockSweepTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(),
            "flow-sweep-0614-timesig-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    /// <summary>Renders one full bar of the given note stream in the given meter at
    /// 120 BPM and returns the written WAV's wall-clock seconds.</summary>
    private double RenderBarSeconds(string timesig, string stream)
    {
        var wav = Path.Combine(_tmpDir, $"ts_{timesig.Replace('/', '_')}.wav");
        var src =
            "use \"@audio\"\n" +
            "tempo 120 {\n" +
            $"  timesig {timesig} {{\n" +
            $"    section s {{ Sequence x = {stream} }}\n" +
            "    Song g = [s]\n" +
            "    Buffer b = (renderSong g \"piano\")\n" +
            $"    (writeWav \"{wav.Replace("\\", "/")}\" b)\n" +
            "  }\n" +
            "}\n";

        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(src);
        Assert.True(ok && errorCount == 0, $"render failed (errors={errorCount}):\n{stderr}");
        Assert.True(File.Exists(wav), $"writeWav did not produce {wav}");

        var buffer = WavReader.ReadWav(wav);
        Assert.True(buffer.Frames > 0, "render produced zero frames");
        return (double)buffer.Frames / buffer.SampleRate;
    }

    [Fact]
    public void FourFour_OneBar_TwoSeconds()
    {
        // 4 quarters @120BPM = 4 × 0.5s = 2.0s. The factor-1.0 control case —
        // unchanged by the fix.
        double secs = RenderBarSeconds("4/4", "| C4q C4q C4q C4q |");
        Assert.Equal(2.0, secs, 1);
    }

    [Fact]
    public void SixEight_OneBar_OnePointFiveSeconds_Not3()
    {
        // 6 eighths = 3 quarters @120BPM = 3 × 0.5s = 1.5s. Pre-fix this rendered
        // 3.0s (GetBeats(8) returned 2.0 denominator-units per quarter → 2× slow).
        double secs = RenderBarSeconds("6/8", "| C4e C4e C4e C4e C4e C4e |");
        Assert.Equal(1.5, secs, 1);
    }

    [Fact]
    public void TwoTwo_OneBar_TwoSeconds_Not1()
    {
        // 2 halves = 4 quarters @120BPM = 4 × 0.5s = 2.0s. Pre-fix this rendered
        // 1.0s (GetBeats(2) returned 1.0 denominator-unit per half → 2× fast).
        double secs = RenderBarSeconds("2/2", "| C4h C4h |");
        Assert.Equal(2.0, secs, 1);
    }

    [Fact]
    public void DurationRelationships_AcrossMeters()
    {
        // The whole point: a 6/8 bar of 6 eighths is SHORTER than a 4/4 bar of 4
        // quarters (3 vs 4 quarters), and a 2/2 bar of 2 halves equals the 4/4 bar
        // (both 4 quarters). Pre-fix the relationships were inverted (6/8 longer,
        // 2/2 shorter).
        double s44 = RenderBarSeconds("4/4", "| C4q C4q C4q C4q |");
        double s68 = RenderBarSeconds("6/8", "| C4e C4e C4e C4e C4e C4e |");
        double s22 = RenderBarSeconds("2/2", "| C4h C4h |");

        // 6/8 bar (3 quarters) is 3/4 of the 4/4 bar (4 quarters).
        Assert.Equal(0.75, s68 / s44, 2);
        // 2/2 bar (4 quarters) equals the 4/4 bar (4 quarters).
        Assert.Equal(1.0, s22 / s44, 2);
    }

    // ===== Unit-level pin: GetBeats returns quarter-note units in every meter =====

    [Theory]
    [InlineData(NoteValueType.Value.QUARTER, 1.0)]
    [InlineData(NoteValueType.Value.EIGHTH, 0.5)]
    [InlineData(NoteValueType.Value.HALF, 2.0)]
    [InlineData(NoteValueType.Value.WHOLE, 4.0)]
    public void GetBeats_IsQuarterUnits_RegardlessOfDenominator(
        NoteValueType.Value noteValue, double expectedQuarters)
    {
        var note = new MusicalNoteData('C', 4, 0, (int)noteValue, false);
        // The timeSigDenominator argument no longer scales the result — a quarter
        // note is 1.0 quarters whether asked in 4/4 (denom=4), 6/8 (denom=8), or
        // 2/2 (denom=2).
        Assert.Equal(expectedQuarters, note.GetBeats(4), 10);
        Assert.Equal(expectedQuarters, note.GetBeats(8), 10);
        Assert.Equal(expectedQuarters, note.GetBeats(2), 10);
    }

    [Theory]
    [InlineData(4, 4, 4.0)]   // 4/4 → 4 quarters
    [InlineData(6, 8, 3.0)]   // 6/8 → 3 quarters
    [InlineData(2, 2, 4.0)]   // 2/2 → 4 quarters
    [InlineData(3, 4, 3.0)]   // 3/4 → 3 quarters
    [InlineData(7, 8, 3.5)]   // 7/8 → 3.5 quarters
    public void BarCapacityQuarters_IsNumeratorTimesFourOverDenominator(
        int num, int denom, double expected)
    {
        var ts = new TimeSignatureData(num, denom);
        Assert.Equal(expected, ts.BarCapacityQuarters, 10);
    }
}
