using Xunit;
using FlowLang.Core;
using FlowMidi.Conversion;
using FlowMidi.Midi;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using DwmMidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace FlowLang.Tests.Integration.Phase30;

public class Midi2FlowRoundTripTests
{
    static readonly string FixtureDir =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "midi");

    // SPEC-6 acceptance: per-fixture note-count + pitch + duration parity; ±1 tick tolerance.
    [Theory]
    [InlineData("ragtime_q_ee.mid")]
    [InlineData("two_voice_counterpoint.mid")]
    [InlineData("drum_loop.mid")]
    public void Round_Trip_Preserves_Note_Count_Pitch_Duration(string fixtureName)
    {
        var sourceMidPath = Path.Combine(FixtureDir, fixtureName);
        Assert.True(File.Exists(sourceMidPath), $"Fixture missing: {sourceMidPath}");

        // Step 1: midi2flow — read source.mid, generate roundTrip .flow source
        var bytes = File.ReadAllBytes(sourceMidPath);
        var fmMidi = MidiParser.Parse(bytes);
        var qr = Quantizer.Quantize(fmMidi);
        var flowSource = FlowGenerator.Generate(fmMidi, qr, fixtureName, roundTrip: true);

        // Step 2: write the .flow source to a temp file, then exec via FlowEngine
        var tmpFlow = Path.GetTempFileName() + ".flow";
        var tmpMid = Path.GetTempFileName() + ".mid";
        try
        {
            // Plan 30-08 contract: FlowGenerator emits `Song s = [roundtrip]` inside the
            // key/timesig/tempo blocks. Per Interpreter.ExecuteMusicalContext, `Song s`
            // is scoped to the key-frame and is unreachable after the closing braces.
            // Splice the writeMidi call on the line IMMEDIATELY AFTER the marker so it
            // lives in the same scope where `s` is bound.
            var lines = flowSource.Split('\n').ToList();
            var songLineIdx = lines.FindIndex(l => l.Contains("Song s = [roundtrip]"));
            if (songLineIdx < 0)
                throw new InvalidOperationException(
                    "Generated source missing 'Song s = [roundtrip]' marker — " +
                    "Plan 30-08 contract violated. FlowGenerator must emit this exact " +
                    "marker line in roundTrip mode for the round-trip test to splice " +
                    "the writeMidi call in the same scope.");
            var markerIndent = new string(lines[songLineIdx].TakeWhile(char.IsWhiteSpace).ToArray());
            var tmpMidEscaped = tmpMid.Replace("\\", "/");
            lines.Insert(songLineIdx + 1, $"{markerIndent}(writeMidi \"{tmpMidEscaped}\" s)");
            var wrapped = string.Join("\n", lines);
            File.WriteAllText(tmpFlow, wrapped);

            var engine = new FlowEngine(verbose: false);
            var ok = engine.Execute(wrapped, tmpFlow);
            Assert.True(ok, $"FlowEngine.Execute failed for {fixtureName}");

            Assert.True(File.Exists(tmpMid), $"writeMidi did not produce {tmpMid}");

            // Step 3: parse both source.mid and roundtrip.mid via DryWetMidi
            var srcMidi = DwmMidiFile.Read(sourceMidPath);
            var rtMidi = DwmMidiFile.Read(tmpMid);

            var srcNotes = srcMidi.GetNotes()
                .OrderBy(n => n.Time)
                .ThenBy(n => n.NoteNumber)
                .ToList();
            var rtNotes = rtMidi.GetNotes()
                .OrderBy(n => n.Time)
                .ThenBy(n => n.NoteNumber)
                .ToList();

            // Note count parity
            Assert.True(
                srcNotes.Count == rtNotes.Count,
                $"{fixtureName}: note count mismatch — src={srcNotes.Count}, rt={rtNotes.Count}");

            // Per-note pitch + duration ±1 tick parity (SPEC-6)
            for (int i = 0; i < srcNotes.Count; i++)
            {
                var s = srcNotes[i];
                var r = rtNotes[i];
                Assert.Equal((int)s.NoteNumber, (int)r.NoteNumber); // pitch exact
                var srcDur = (long)s.Length;
                var rtDur = (long)r.Length;
                Assert.True(
                    Math.Abs(srcDur - rtDur) <= 1,
                    $"{fixtureName} note {i} (pitch {s.NoteNumber}): " +
                    $"duration mismatch — src={srcDur}, rt={rtDur}");
            }
        }
        finally
        {
            try { File.Delete(tmpFlow); } catch { }
            try { File.Delete(tmpMid); } catch { }
        }
    }
}
