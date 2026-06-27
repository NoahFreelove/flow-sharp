using System;
using System.IO;
using System.Linq;
using FlowLang.Tests.Fixtures;
using Melanchall.DryWetMidi.Core;
using Xunit;

namespace FlowLang.Tests.Unit.Sweep0614;

/// <summary>
/// sweep-0614: writeMidi used to take a SINGLE global tempo from the FIRST section
/// only and write one SetTempoEvent at tick 0 for the whole file, while the audio
/// renderer (SongRenderer) + midiOut both honor per-section tempo. So a multi-tempo
/// Song's exported .mid played every section after the first at the WRONG speed,
/// breaking the documented "sounds identical to the exported .mid" (D-40-02) parity.
///
/// <para>MidiExport now emits a SetTempoEvent at each section boundary whenever the
/// section tempo differs from the previously-emitted one (consecutive equal tempos
/// are de-duped so single-tempo output stays byte-identical / two-run cmp-clean).</para>
/// </summary>
[Collection("FlowScripts")]
public class MultiTempoMidiExportTests
{
    private static MidiFile RenderMidi(string source, string testName)
    {
        string outDir = Path.Combine(Path.GetTempPath(), "flow_sweep0614_multitempo");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, $"{testName}_{Guid.NewGuid():N}.mid");
        if (File.Exists(outPath)) File.Delete(outPath);

        string src = source.Replace("{{OUTPATH}}", outPath.Replace("\\", "/"));
        using (var runner = new FlowEngineRunner())
        {
            var (ok, _, stderr, errors) = runner.RunSource(src, "<sweep0614-multitempo>");
            Assert.True(ok && errors == 0, $"render failed: {stderr}");
        }
        Assert.True(File.Exists(outPath), $"MIDI not written to {outPath}");
        var midi = MidiFile.Read(outPath);
        File.Delete(outPath);
        return midi;
    }

    private static (long tick, int microsPerBeat)[] TempoEvents(MidiFile midi)
    {
        // The conductor track is the first chunk; SetTempoEvents are absolute-time
        // sorted within it. Re-derive absolute ticks from the delta-time stream.
        var chunk = midi.GetTrackChunks().First();
        long abs = 0;
        var result = new System.Collections.Generic.List<(long, int)>();
        foreach (var ev in chunk.Events)
        {
            abs += ev.DeltaTime;
            if (ev is SetTempoEvent st)
                result.Add((abs, (int)st.MicrosecondsPerQuarterNote));
        }
        return result.ToArray();
    }

    [Fact]
    public void TwoSectionDifferentTempos_EmitsTwoTempoEvents()
    {
        // Section A at 60 BPM (1,000,000 µs/beat), section B at 120 BPM (500,000).
        const string source = @"
            use ""@audio""
            tempo 60 { timesig 4/4 { section slow { Sequence p = | C4q D4q E4q F4q | } } }
            tempo 120 { timesig 4/4 { section fast { Sequence p = | C4q D4q E4q F4q | } } }
            Song s = [slow fast]
            (writeMidi ""{{OUTPATH}}"" s)
        ";
        var midi = RenderMidi(source, "two_tempo");
        var tempos = TempoEvents(midi);

        // Before the fix there was exactly ONE tempo event (60 BPM at tick 0) and the
        // fast section played at 60. Now there must be TWO: 60 at tick 0, 120 at the
        // section-B boundary (tick > 0).
        Assert.Equal(2, tempos.Length);
        Assert.Equal(0, tempos[0].tick);
        Assert.Equal(1_000_000, tempos[0].microsPerBeat);   // 60 BPM
        Assert.True(tempos[1].tick > 0, "second tempo event must land at the section-B start tick");
        Assert.Equal(500_000, tempos[1].microsPerBeat);     // 120 BPM
    }

    [Fact]
    public void SingleTempoSong_StillHasExactlyOneTempoEvent()
    {
        // A single-tempo song must NOT gain a redundant section-boundary tempo event
        // (dedup of consecutive equal tempos → byte-identical with the pre-fix output).
        const string source = @"
            use ""@audio""
            tempo 100 { timesig 4/4 {
                section a { Sequence p = | C4q D4q E4q F4q | }
                section b { Sequence p = | G4q A4q B4q C5q | }
                Song s = [a b]
                (writeMidi ""{{OUTPATH}}"" s)
            } }
        ";
        var midi = RenderMidi(source, "single_tempo");
        var tempos = TempoEvents(midi);
        Assert.Single(tempos);
        Assert.Equal(0, tempos[0].tick);
        Assert.Equal(600_000, tempos[0].microsPerBeat);     // 100 BPM
    }

    [Fact]
    public void MultiTempoExport_IsDeterministic_TwoRunIdentical()
    {
        const string source = @"
            use ""@audio""
            tempo 60 { timesig 4/4 { section slow { Sequence p = | C4q D4q E4q F4q | } } }
            tempo 120 { timesig 4/4 { section fast { Sequence p = | C4q D4q E4q F4q | } } }
            Song s = [slow fast]
            (writeMidi ""{{OUTPATH}}"" s)
        ";

        string outDir = Path.Combine(Path.GetTempPath(), "flow_sweep0614_multitempo");
        Directory.CreateDirectory(outDir);

        byte[] Run()
        {
            string outPath = Path.Combine(outDir, $"det_{Guid.NewGuid():N}.mid");
            string src = source.Replace("{{OUTPATH}}", outPath.Replace("\\", "/"));
            using (var runner = new FlowEngineRunner())
            {
                var (ok, _, stderr, errors) = runner.RunSource(src, "<sweep0614-det>");
                Assert.True(ok && errors == 0, $"render failed: {stderr}");
            }
            var bytes = File.ReadAllBytes(outPath);
            File.Delete(outPath);
            return bytes;
        }

        var r1 = Run();
        var r2 = Run();
        Assert.True(r1.AsSpan().SequenceEqual(r2),
            "two writeMidi runs of the same multi-tempo source must be byte-identical (two-run cmp-clean)");
    }
}
