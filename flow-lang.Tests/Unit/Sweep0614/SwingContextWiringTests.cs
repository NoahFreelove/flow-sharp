using System.Linq;
using FlowLang.Ast.Expressions;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Sweep0614;

/// <summary>
/// sweep-0614: the <c>swing N { }</c> musical-context block used to be a render
/// no-op — <see cref="MusicalContext.Swing"/> was written, cloned, and stringified
/// but never READ by any render path, so a composer who wrote <c>swing 0.62 { ... }</c>
/// got straight eighths. <see cref="NoteStreamCompiler"/> now reads the active swing
/// and delays every offbeat eighth-note onset via <see cref="MusicalNoteData.OnsetOffset"/>
/// (the same eighth-note swing grid the <c>quantize</c> builtin uses), which reaches
/// BOTH the audio and MIDI paths through <c>BarType.ToTimeline</c>.
///
/// Context swing is [0.0, 1.0] (0.5 = straight); the onset-shift math is [-1.0, 1.0]
/// (0 = straight). The compiler bridges the convention and short-circuits at exactly
/// straight so non-swing renders stay byte-identical / two-run cmp-clean.
/// </summary>
// Serialized: the end-to-end facts drive a FlowEngineRunner that redirects the
// process-wide Console.Out/Error, so they must not run concurrently with other
// Console-redirecting test classes.
[Collection("FlowScripts")]
public class SwingContextWiringTests
{
    // Eight straight eighths in 4/4 → slots 0..7, offbeats at odd slots 1,3,5,7.
    private const string EightEighths = "| C4e D4e E4e F4e G4e A4e B4e C5e |";

    private static SequenceData Compile(double? swing)
    {
        var reporter = new ErrorReporter();
        var tokens = new SimpleLexer(EightEighths, reporter).Tokenize();
        var program = new Parser(tokens, reporter).Parse();
        Assert.False(reporter.HasErrors, $"Parse errors: {reporter.FormatErrors()}");

        var stmt = program.Statements.OfType<FlowLang.Ast.Statements.ExpressionStatement>().Single();
        var noteStream = (NoteStreamExpression)stmt.Expression;

        var ctx = new MusicalContext { TimeSignature = new TimeSignatureData(4, 4), Swing = swing };
        return new NoteStreamCompiler().Compile(noteStream, ctx);
    }

    [Fact]
    public void SwingContext_DelaysOffbeatEighths()
    {
        var notes = Compile(swing: 0.62).Bars[0].MusicalNotes;
        Assert.Equal(8, notes.Count);
        for (int i = 0; i < notes.Count; i++)
        {
            if (i % 2 == 0)
                Assert.Equal(0.0, notes[i].OnsetOffset, 9); // onbeats unshifted
            else
                Assert.True(notes[i].OnsetOffset > 0.0,      // offbeats delayed
                    $"offbeat eighth #{i} should be swing-delayed, got {notes[i].OnsetOffset}");
        }
    }

    [Fact]
    public void HeavierSwing_DelaysMore()
    {
        // A deeper swing value pushes the offbeat further off the grid.
        double light = Compile(swing: 0.60).Bars[0].MusicalNotes[1].OnsetOffset;
        double heavy = Compile(swing: 0.75).Bars[0].MusicalNotes[1].OnsetOffset;
        Assert.True(heavy > light, $"swing 0.75 ({heavy}) should delay more than 0.60 ({light})");
    }

    [Fact]
    public void NoSwing_AllOnsetsZero_DeterminismPreserved()
    {
        // The identity guard: with no swing override every OnsetOffset stays 0, so a
        // non-swing render is byte-identical to pre-fix output (two-run cmp-clean).
        foreach (var n in Compile(swing: null).Bars[0].MusicalNotes)
            Assert.Equal(0.0, n.OnsetOffset, 9);
    }

    [Fact]
    public void StraightSwing_IsNoOp()
    {
        // swing 0.5 == straight → identical to no swing at all (short-circuit).
        foreach (var n in Compile(swing: 0.5).Bars[0].MusicalNotes)
            Assert.Equal(0.0, n.OnsetOffset, 9);
    }

    // ---- end-to-end render facts: swing must reach BOTH the WAV and MIDI paths ----

    private static byte[] RenderToFile(string body, string outPath)
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errors) = runner.RunSource("use \"@audio\"\n" + body, "<swing-e2e>");
        Assert.True(ok && errors == 0, $"render failed: {stderr}");
        Assert.True(System.IO.File.Exists(outPath), $"expected output at {outPath}");
        return System.IO.File.ReadAllBytes(outPath);
    }

    private static string Body(string outPath, string writeCall, bool swing)
    {
        // Distinct section name per variant so no cross-run section cache can
        // return a stale rendered buffer keyed by section name.
        string sec = swing ? "swung" : "straight";
        string inner =
            $"  section {sec} {{ Sequence p = | C4e D4e E4e F4e G4e A4e B4e C5e | }}\n" +
            $"  Song s = [{sec}]\n" +
            $"  {writeCall}\n";
        return swing
            ? "tempo 120 { timesig 4/4 { swing 0.62 {\n" + inner + "} } }"
            : "tempo 120 { timesig 4/4 {\n" + inner + "} }";
    }

    [Fact]
    public void SwingBlock_ChangesRenderedWav()
    {
        string on = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sweep_swing_on_{System.Guid.NewGuid():N}.wav");
        string off = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sweep_swing_off_{System.Guid.NewGuid():N}.wav");
        try
        {
            byte[] onBytes = RenderToFile(Body(on, $"(writeWav \"{on}\" (renderSong s \"sine\"))", swing: true), on);
            byte[] offBytes = RenderToFile(Body(off, $"(writeWav \"{off}\" (renderSong s \"sine\"))", swing: false), off);
            Assert.Equal(offBytes.Length, onBytes.Length);
            Assert.False(onBytes.AsSpan().SequenceEqual(offBytes),
                "swing 0.62 { } produced a byte-identical WAV to no swing — swing context is still a no-op");
        }
        finally { TryDelete(on); TryDelete(off); }
    }

    [Fact]
    public void SwingBlock_ChangesExportedMidi()
    {
        string on = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sweep_swing_on_{System.Guid.NewGuid():N}.mid");
        string off = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sweep_swing_off_{System.Guid.NewGuid():N}.mid");
        try
        {
            byte[] onBytes = RenderToFile(Body(on, $"(writeMidi \"{on}\" s)", swing: true), on);
            byte[] offBytes = RenderToFile(Body(off, $"(writeMidi \"{off}\" s)", swing: false), off);
            Assert.False(onBytes.AsSpan().SequenceEqual(offBytes),
                "swing 0.62 { } exported a byte-identical .mid to no swing — swing did not reach MIDI export");
        }
        finally { TryDelete(on); TryDelete(off); }
    }

    [Fact]
    public void SwingRender_IsDeterministic_TwoRunIdentical()
    {
        string p1 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sweep_swing_d1_{System.Guid.NewGuid():N}.wav");
        string p2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sweep_swing_d2_{System.Guid.NewGuid():N}.wav");
        try
        {
            byte[] r1 = RenderToFile(Body(p1, $"(writeWav \"{p1}\" (renderSong s \"sine\"))", swing: true), p1);
            byte[] r2 = RenderToFile(Body(p2, $"(writeWav \"{p2}\" (renderSong s \"sine\"))", swing: true), p2);
            Assert.True(r1.AsSpan().SequenceEqual(r2),
                "two renders of the same swing source must be byte-identical (two-run cmp-clean)");
        }
        finally { TryDelete(p1); TryDelete(p2); }
    }

    private static void TryDelete(string path)
    {
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { /* best-effort */ }
    }
}
