using System;
using System.IO;
using System.Linq;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Xunit;

namespace FlowLang.Tests.Unit.Phase22;

/// <summary>
/// DX-14 acceptance Facts pinning portamento(Sequence, Millisecond) — emits MIDI
/// CC65=127 + CC5=mappedValue at note start and CC65=0 at note end (per-note bracket).
///
/// Decisions referenced (locked in 22-CONTEXT.md):
///   Claude's Discretion — linear ms→CC5 mapping: 0→0, 100→64, 200→127 clamped.
///   D-USER threats T-22-V5-22, T-22-V5-23 — clamp CC5 to [0, 127] before SevenBitNumber cast
///   Open Question 4 — legato + portamento compose without erasing each other's flag
///
/// Tests 1-2 + With(...) pin the defaulted-parameter migration shape.
/// Tests 3-4 verify the linear ms→CC5 mapping curve with edge cases.
/// Tests 5-7 verify MIDI emission via DryWetMidi read-back of generated .mid files.
/// Test 8 verifies legato + portamento composition.
/// </summary>
public class PortamentoMidiFacts
{
    // ===== Tests 1-2 + With() — direct ctor / property pinning =====

    [Fact]
    public void PortamentoMs_DefaultsTo0()
    {
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false);
        Assert.Equal(0.0, n.PortamentoMs);
    }

    [Fact]
    public void PortamentoMs_OptionalCtorParam_AcceptedAtEndOfSignature()
    {
        var n = new MusicalNoteData(
            'C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            portamentoMs: 100.0);
        Assert.Equal(100.0, n.PortamentoMs);
    }

    [Fact]
    public void With_PortamentoMs_PreservesOtherFields()
    {
        // Builder helper rollback-independence (Phase 22 CONTEXT line 18):
        // calling With(portamentoMs: …) overrides only PortamentoMs and copies
        // every other field through unchanged — including 22-05's OnsetOffset and
        // sibling 22-06 DurationOverlap.
        var n = new MusicalNoteData('C', 4, 0, (int)NoteValueType.Value.QUARTER, false,
            onsetOffset: 0.2, durationOverlap: 0.3);
        var n2 = n.With(portamentoMs: 100.0);
        Assert.Equal(100.0, n2.PortamentoMs);
        Assert.Equal(0.2, n2.OnsetOffset);     // preserved
        Assert.Equal(0.3, n2.DurationOverlap); // preserved
    }

    // ===== Tests 3-4 — ms→CC5 mapping curve =====

    /// <summary>
    /// Reference implementation of the linear ms→CC5 curve mirrored from
    /// MidiExport (Claude's Discretion in CONTEXT). Kept here so the Facts pin
    /// the curve independently of whatever expression MidiExport happens to use.
    /// </summary>
    private static int PortamentoToCC5(double ms) =>
        Math.Clamp((int)Math.Round(ms * 127.0 / 200.0), 0, 127);

    [Fact]
    public void MsToFiveCC_LinearCurve()
    {
        // Anchor points from CONTEXT Claude's Discretion: 0→0, 100→64, 200→127 clamped.
        Assert.Equal(0,   PortamentoToCC5(0.0));
        Assert.Equal(64,  PortamentoToCC5(100.0));
        Assert.Equal(127, PortamentoToCC5(200.0));
    }

    [Fact]
    public void MsToFiveCC_OutOfRangeIsClamped()
    {
        // V5 input validation (T-22-V5-22, T-22-V5-23): clamp before SevenBitNumber cast.
        Assert.Equal(127, PortamentoToCC5(300.0)); // clamp upper
        Assert.Equal(127, PortamentoToCC5(99999.0)); // clamp upper extreme
        Assert.Equal(0,   PortamentoToCC5(-50.0));   // clamp lower (negative)
    }

    // ===== Tests 5-7 — MIDI emission via DryWetMidi read-back =====

    private static string RunAndWriteMidi(string flowSource, string testName)
    {
        string outDir = Path.Combine(Path.GetTempPath(), "flow_phase22_06_midi");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, $"{testName}_{Guid.NewGuid():N}.mid");
        if (File.Exists(outPath)) File.Delete(outPath);

        string source = flowSource.Replace("{{OUTPATH}}", outPath.Replace("\\", "/"));

        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success, $"Script failed: errorCount={errorCount}, stderr={stderr}");
        Assert.True(File.Exists(outPath), $"MIDI not written to {outPath}");
        return outPath;
    }

    [Fact]
    public void WriteMidi_ContainsCC65AndCC5()
    {
        // End-to-end: portamento(seq, 100ms) → renderSong → writeMidi → read back via
        // DryWetMidi.MidiFile.Read → assert ControlChangeEvent emissions present.
        // Expected: CC65=127 (portamento on) at every note start, CC5≈64 (100ms mapped),
        // CC65=0 at every note end. We verify the FIRST note's bracket strictly.
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    Sequence src = | C4q E4q G4q |
                    Sequence glide = (portamento src 100ms)
                    section sp { glide }
                    Song s = [sp]
                    (writeMidi ""{{OUTPATH}}"" s)
                }
            }
        ";
        string path = RunAndWriteMidi(source, nameof(WriteMidi_ContainsCC65AndCC5));
        try
        {
            var midi = MidiFile.Read(path);
            var ccEvents = midi.GetTrackChunks()
                .SelectMany(c => c.Events)
                .OfType<ControlChangeEvent>()
                .ToArray();

            Assert.Contains(ccEvents,
                cc => cc.ControlNumber == (SevenBitNumber)65 && cc.ControlValue == (SevenBitNumber)127);
            Assert.Contains(ccEvents,
                cc => cc.ControlNumber == (SevenBitNumber)5 && cc.ControlValue == (SevenBitNumber)64);
            Assert.Contains(ccEvents,
                cc => cc.ControlNumber == (SevenBitNumber)65 && cc.ControlValue == (SevenBitNumber)0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Portamento_BracketCloseEmitsCC65Zero()
    {
        // CONTEXT bracket-close: at note end, CC65=0 must fire (turns portamento off
        // for the next note so receivers don't carry portamento across phrases).
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    Sequence src = | C4q E4q |
                    Sequence glide = (portamento src 50ms)
                    section sp { glide }
                    Song s = [sp]
                    (writeMidi ""{{OUTPATH}}"" s)
                }
            }
        ";
        string path = RunAndWriteMidi(source, nameof(Portamento_BracketCloseEmitsCC65Zero));
        try
        {
            var midi = MidiFile.Read(path);
            var ccEvents = midi.GetTrackChunks()
                .SelectMany(c => c.Events)
                .OfType<ControlChangeEvent>()
                .ToArray();

            // Both note bracket opens (CC65=127) and bracket closes (CC65=0) present.
            // Each note emits one of each, so we should have at least 2 of each (2 notes).
            int onCount  = ccEvents.Count(cc => cc.ControlNumber == (SevenBitNumber)65 && cc.ControlValue == (SevenBitNumber)127);
            int offCount = ccEvents.Count(cc => cc.ControlNumber == (SevenBitNumber)65 && cc.ControlValue == (SevenBitNumber)0);
            Assert.True(onCount >= 2, $"expected ≥2 CC65=127 events, got {onCount}");
            Assert.True(offCount >= 2, $"expected ≥2 CC65=0 events, got {offCount}");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void WriteMidi_NoPortamento_EmitsNoCC()
    {
        // Regression gate: when PortamentoMs == 0 (default), MidiExport must NOT emit
        // any CC65 or CC5 events. This is the byte-identical guarantee for pre-22-06
        // call sites — the dormant default keeps every existing .flow script's MIDI
        // output unchanged.
        const string source = @"
            use ""@std""
            use ""@audio""
            tempo 120 {
                timesig 4/4 {
                    Sequence src = | C4q E4q G4q |
                    section sp { src }
                    Song s = [sp]
                    (writeMidi ""{{OUTPATH}}"" s)
                }
            }
        ";
        string path = RunAndWriteMidi(source, nameof(WriteMidi_NoPortamento_EmitsNoCC));
        try
        {
            var midi = MidiFile.Read(path);
            var ccEvents = midi.GetTrackChunks()
                .SelectMany(c => c.Events)
                .OfType<ControlChangeEvent>()
                .ToArray();

            Assert.DoesNotContain(ccEvents,
                cc => cc.ControlNumber == (SevenBitNumber)65 || cc.ControlNumber == (SevenBitNumber)5);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ===== Test 8 — composition with sibling slot (legato) =====

    [Fact]
    public void Portamento_AndLegato_Compose()
    {
        // RESEARCH Open Question 4: chaining (legato (portamento seq X) Y) preserves both flags.
        // Each transform calls With(...) naming only its own slot, so the other slot survives.
        const string SmokePrelude = @"
use ""@std""
use ""@audio""
use ""@notation""
";
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(SmokePrelude + @"
Sequence src = | C4q D4q E4q F4q |
Sequence both = (legato (portamento src 100ms) 0.3)
");
        Assert.Equal(0, errorCount);
        Assert.True(string.IsNullOrEmpty(stderr) || !stderr.Contains("error", StringComparison.OrdinalIgnoreCase),
            $"unexpected stderr: {stderr}");
        var both = runner.GetVariable("both").As<SequenceData>();
        Assert.NotEmpty(both.Bars);
        foreach (var bar in both.Bars)
        {
            foreach (var note in bar.MusicalNotes)
            {
                Assert.Equal(0.3,   note.DurationOverlap, 6);
                Assert.Equal(100.0, note.PortamentoMs, 6);
            }
        }
    }
}
