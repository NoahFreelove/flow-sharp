using System;
using System.Diagnostics;
using System.Linq;
using FlowLang.Audio;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Midi;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 MIDI-RT-02 — note/CC/program byte assertions via the in-process
/// <see cref="CaptureMidiBackend"/> seam (no real ALSA). Includes the explicit
/// drum→ch9 channel-mapping guard (Pitfall 3). The real-ALSA / virtual-MIDI
/// end-to-end portion charitable-skips when <c>librtmidi.so</c> / <c>snd-virmidi</c>
/// are absent (mirrors Phase 39 MusicXmlRoundTripTests.CharitableSkipWhenMscoreAbsent).
///
/// <para>Plan 40-01 Task 3 extends this file with midiOut GM-routing assertions
/// (drum* → ch9 program/channel via InstrumentRouting.ResolveGmProgram).</para>
///
/// <para><b>Serialized with the WASM console collection:</b> the Task-3 methods
/// drive a <see cref="FlowEngineRunner"/> which redirects the process-wide
/// <see cref="System.Console.Out"/>/<c>Error</c>. The Phase 48 WASM tests do the
/// same. Sharing <see cref="WasmEntryConsoleCollection"/> forces both to run
/// SERIALLY so neither captures the other's stdout (the cross-class
/// Console-redirection race documented on that collection).</para>
/// </summary>
[Collection(WasmEntryConsoleCollection.Name)]
public class VirtualMidiTests
{
    /// <summary>
    /// Probe for real virtual-MIDI capability (librtmidi.so present AND a virtual
    /// port enumerable). When false, the end-to-end real-port Facts charitable-skip.
    /// </summary>
    private static bool VirtualMidiAvailable()
    {
        try
        {
            using var mgr = new MidiPlaybackManager();
            return mgr.IsMidiAvailable() && mgr.GetBackend().ListPorts().Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// MIDI-RT-02: SendNoteOn / SendControlChange record the correct 3-byte
    /// status/data tuples through the capture seam, and the drum channel (9)
    /// maps correctly (Pitfall 3 — RtMidi is 1-based, Flow/GM is 0-based).
    /// </summary>
    [Fact]
    public void VirtualMidiNoteBytes()
    {
        var capture = new CaptureMidiBackend("Virtual Raw MIDI");
        var handle = capture.OpenOutput("Virtual Raw MIDI");
        Assert.NotNull(handle);

        // Note On: channel 0, middle C (60), velocity 100 → 0x90 0x3C 0x64
        handle!.SendNoteOn(0, 60, 100);
        // Control Change: channel 0, CC7 (volume) value 90 → 0xB0 0x07 0x5A
        handle.SendControlChange(0, 7, 90);
        // Drum note on the GM percussion channel 9 → status 0x99 (0x90 | 9)
        handle.SendNoteOn(9, 36, 110);   // kick drum (GM 36)

        var sent = capture.Sent;
        Assert.Equal(3, sent.Count);

        Assert.Equal(new byte[] { 0x90, 0x3C, 0x64 }, sent[0]);
        Assert.Equal(new byte[] { 0xB0, 0x07, 0x5A }, sent[1]);
        // The status byte's low nibble MUST be 9 — drum on the percussion channel.
        Assert.Equal(0x99, sent[2][0]);
        Assert.Equal(36, sent[2][1]);
        Assert.Equal(110, sent[2][2]);
    }

#if !FLOW_WEB
    /// <summary>
    /// Pitfall 3 guard: <c>RtMidiMidiBackend.ToRtChannel</c> maps Flow's 0-based
    /// channel to RtMidi.Core's 1-based <c>Channel</c> enum. The drum channel 0-based
    /// 9 must land on the enum member whose ordinal is 9 (Channel10 == GM percussion
    /// bus), NOT off-by-one. Asserted against the enum's int value to avoid binding
    /// the RtMidi.Core enum type at the test surface.
    /// </summary>
    [Fact]
    public void ToRtChannel_DrumChannelMapsCorrectly()
    {
        // 0-based 9 → enum ordinal 9 (RtMidi Channel10, the percussion bus).
        Assert.Equal(9, (int)RtMidiMidiBackend.ToRtChannel(9));
        // 0-based 0 → enum ordinal 0 (RtMidi Channel1).
        Assert.Equal(0, (int)RtMidiMidiBackend.ToRtChannel(0));
        // 0-based 15 → enum ordinal 15 (RtMidi Channel16).
        Assert.Equal(15, (int)RtMidiMidiBackend.ToRtChannel(15));
        // Defensive clamp: out-of-range collapses into 0..15.
        Assert.Equal(0, (int)RtMidiMidiBackend.ToRtChannel(-3));
        Assert.Equal(15, (int)RtMidiMidiBackend.ToRtChannel(99));
    }
#endif

    /// <summary>
    /// MIDI-RT-02 end-to-end: only runs when real virtual MIDI is available;
    /// otherwise emits the absent-advisory and PASSES (charitable-skip, D-40-07,
    /// mirrors Phase 39 mscore gate). On this dev box librtmidi.so is absent so
    /// this skips.
    /// </summary>
    [Fact]
    public void VirtualMidiEndToEnd_CharitableSkipWhenAbsent()
    {
        if (VirtualMidiAvailable())
        {
            // Real virtual port present — open + send a note end-to-end (must not throw).
            using var mgr = new MidiPlaybackManager();
            var backend = mgr.GetBackend();
            var port = backend.ListPorts().First();
            var handle = backend.OpenOutput(port);
            // handle may still be null if open failed charitably; that's acceptable.
            handle?.SendNoteOn(0, 60, 100);
            handle?.SendNoteOff(0, 60);
            handle?.Close();
            return;
        }

        // Absent path: advisory + PASS.
        var originalErr = Console.Error;
        var sw = new System.IO.StringWriter();
        Console.SetError(sw);
        try
        {
            RenderingDiagnostics.WarnOnce(
                "midi-virtual-absent",
                "[midi] virtual MIDI (librtmidi.so / snd-virmidi) absent — end-to-end MIDI test skipped charitably.");
        }
        finally
        {
            Console.SetError(originalErr);
        }
        Assert.Contains("[midi]", sw.ToString());
    }

    // ===== Task 3: @midi builtin surface end-to-end (via real engine + capture seam) =====

    /// <summary>
    /// Runs a `.flow` script with a CaptureMidiBackend injected so byte/routing
    /// assertions need no real ALSA. Restores BackendOverride in finally.
    /// </summary>
    private static CaptureMidiBackend RunWithCapture(string script, params string[] ports)
    {
        var capture = new CaptureMidiBackend(ports);
        RenderingDiagnostics.ResetForTesting();
        MidiFunctions.ResetForTesting();
        MidiFunctions.BackendOverride = capture;
        // CR-03: dispatch the high-level midiOut event timeline IMMEDIATELY (no
        // real-time sleeps) so byte/routing assertions stay fast — the dedicated
        // MidiOut_SchedulesNonZeroNoteSpacing Fact checks the timing offsets.
        MidiFunctions.ScheduleInspectOverride = _ => { };
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(script, "<phase40-midi>");
            Assert.True(ok, $"@midi script failed: {stderr}");
        }
        finally
        {
            MidiFunctions.ResetForTesting();
        }
        return capture;
    }

    /// <summary>
    /// MIDI-RT-02 / D-40-02: midiOut routes a drum* sequence to GM percussion
    /// channel 9 via InstrumentRouting.ResolveGmProgram — the program-change AND
    /// every note land on channel 9 (status low-nibble == 9), identical to writeMidi.
    /// </summary>
    [Fact]
    public void MidiOut_DrumSequence_RoutesToChannel9()
    {
        const string script = @"use ""@midi""
section main {
    Sequence drums = | C2q C2q |
}
Song s = [main]
(midiOut s ""Synth"")
";
        var capture = RunWithCapture(script, "Synth");
        Assert.NotEmpty(capture.Sent);

        // First byte sent for the drums sequence is a Program Change on ch9 (0xC9).
        var pc = capture.Sent.First(b => (b[0] & 0xF0) == 0xC0);
        Assert.Equal(0xC9, pc[0]);

        // Every note-on/off for this drum sequence is on channel 9.
        var notes = capture.Sent.Where(b => (b[0] & 0xF0) == 0x90 || (b[0] & 0xF0) == 0x80).ToList();
        Assert.NotEmpty(notes);
        Assert.All(notes, b => Assert.Equal(9, b[0] & 0x0F));
    }

    /// <summary>
    /// D-40-02: midiOut routes a piano* sequence to channel 0, program 0 (GM
    /// acoustic grand) — same as writeMidi.
    /// </summary>
    [Fact]
    public void MidiOut_PianoSequence_RoutesToChannel0Program0()
    {
        const string script = @"use ""@midi""
section main {
    Sequence piano = | C4q E4q |
}
Song s = [main]
(midiOut s ""Synth"")
";
        var capture = RunWithCapture(script, "Synth");
        var pc = capture.Sent.First(b => (b[0] & 0xF0) == 0xC0);
        Assert.Equal(0xC0, pc[0]);   // program change on ch0
        Assert.Equal(0, pc[1]);      // GM program 0
    }

    /// <summary>
    /// CR-02: the documented `overrides=` named-arg is CALLABLE and remaps the
    /// per-sequence channel. The original registration declared 3 ParameterNames
    /// against 2 InputTypes (an arity error the resolver rejected), so this call
    /// used to be a hard parse/resolve error. With the 3-InputTypes overload it
    /// resolves: the drum* sequence (default ch9) is remapped to ch5 by
    /// overrides=(dict "drums" 5). The GM program still derives from the name.
    /// </summary>
    [Fact]
    public void MidiOut_OverridesNamedArg_RemapsChannel()
    {
        const string script = @"use ""@midi""
section main {
    Sequence drums = | C2q C2q |
}
Song s = [main]
(midiOut s ""Synth"" overrides=(dict ""drums"" 5))
";
        var capture = RunWithCapture(script, "Synth");
        Assert.NotEmpty(capture.Sent);

        // Every note-on/off lands on the OVERRIDDEN channel 5 (not the GM ch9).
        var notes = capture.Sent.Where(b => (b[0] & 0xF0) == 0x90 || (b[0] & 0xF0) == 0x80).ToList();
        Assert.NotEmpty(notes);
        Assert.All(notes, b => Assert.Equal(5, b[0] & 0x0F));
    }

    /// <summary>
    /// CR-02 charitable: the 2-arg `(midiOut song "port")` form still resolves and
    /// routes via pure GM (no override) — the override is genuinely optional, not
    /// a required slot. A drum* sequence stays on GM ch9.
    /// </summary>
    [Fact]
    public void MidiOut_NoOverrides_StillResolvesAndUsesGm()
    {
        const string script = @"use ""@midi""
section main {
    Sequence drums = | C2q C2q |
}
Song s = [main]
(midiOut s ""Synth"")
";
        var capture = RunWithCapture(script, "Synth");
        var notes = capture.Sent.Where(b => (b[0] & 0xF0) == 0x90 || (b[0] & 0xF0) == 0x80).ToList();
        Assert.NotEmpty(notes);
        Assert.All(notes, b => Assert.Equal(9, b[0] & 0x0F));
    }

    /// <summary>
    /// CR-03: the high-level midiOut path schedules NoteOn/NoteOff with REAL
    /// ms-aligned timing — every note has a non-zero duration (NoteOff later than
    /// NoteOn) and consecutive notes are spaced by their duration. The original
    /// code fired On then Off back-to-back with zero delay (all notes zero-length
    /// and simultaneous). Asserted via the ScheduleInspectOverride seam so the
    /// PLANNED offsets are checked with no real-time sleep.
    /// </summary>
    [Fact]
    public void MidiOut_SchedulesNonZeroNoteSpacing()
    {
        // 4/4 at 120 BPM: a quarter note = 500 ms. Two quarters → onsets at 0 and
        // 500 ms; each note's NoteOff is 500 ms after its NoteOn.
        const string script = @"use ""@midi""
section main {
    Sequence piano = | C4q E4q |
}
Song s = [main]
(midiOut s ""Synth"")
";
        System.Collections.Generic.List<double>? planned = null;
        var capture = new CaptureMidiBackend("Synth");
        RenderingDiagnostics.ResetForTesting();
        MidiFunctions.ResetForTesting();
        MidiFunctions.BackendOverride = capture;
        MidiFunctions.ScheduleInspectOverride = offsets => planned = new System.Collections.Generic.List<double>(offsets);
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(script, "<phase40-cr03>");
            Assert.True(ok, $"midiOut timing script failed: {stderr}");
        }
        finally
        {
            MidiFunctions.ResetForTesting();
        }

        Assert.NotNull(planned);

        // Reconstruct the (offset, isNoteOn, isNoteOff) sequence from the capture +
        // planned offsets — they are 1:1 in the same sorted order the seam fires.
        var sent = capture.Sent;
        Assert.Equal(planned!.Count, sent.Count);

        // Collect NoteOn / NoteOff offsets in order.
        double? firstOnMs = null, firstOffMs = null, secondOnMs = null;
        for (int i = 0; i < sent.Count; i++)
        {
            int status = sent[i][0] & 0xF0;
            if (status == 0x90)
            {
                if (firstOnMs == null) firstOnMs = planned[i];
                else if (secondOnMs == null) secondOnMs = planned[i];
            }
            else if (status == 0x80)
            {
                if (firstOffMs == null) firstOffMs = planned[i];
            }
        }

        Assert.NotNull(firstOnMs);
        Assert.NotNull(firstOffMs);
        Assert.NotNull(secondOnMs);

        // Non-zero note DURATION: the first NoteOff is a quarter (500 ms) after its
        // NoteOn — NOT zero-length.
        Assert.InRange(firstOffMs!.Value - firstOnMs!.Value, 400.0, 600.0);
        // Non-zero note SPACING: the second note's onset is a quarter after the first.
        Assert.InRange(secondOnMs!.Value - firstOnMs!.Value, 400.0, 600.0);
    }

    /// <summary>
    /// T-40-01: midiNoteOn with out-of-range channel/pitch/velocity is CLAMPED at
    /// the builtin boundary (channel→0..15, pitch/vel→0..127) + a [midi] advisory;
    /// no out-of-range byte reaches the capture seam; never throws.
    /// </summary>
    [Fact]
    public void MidiNoteOn_OutOfRange_ClampsAndAdvises()
    {
        // Flow is prefix-only — negative literals use (neg N), not `-5`.
        // pitch 200 → clamp to 127; velocity (neg 5) = -5 → clamp to 0.
        const string script = @"use ""@midi""
MidiDevice dev = (openMidiOutput ""Synth"")
(midiNoteOn dev 0 200 (neg 5))
";
        var originalErr = Console.Error;
        var sw = new System.IO.StringWriter();
        var capture = new CaptureMidiBackend("Synth");
        RenderingDiagnostics.ResetForTesting();
        MidiFunctions.BackendOverride = capture;
        Console.SetError(sw);
        try
        {
            using var runner = new FlowEngineRunner();
            // runner redirects Console too; restore ours afterward to read advisory.
            runner.RunSource(script, "<phase40-clamp>");
        }
        finally
        {
            MidiFunctions.BackendOverride = null;
            Console.SetError(originalErr);
        }

        // Exactly one note-on recorded; pitch clamped to 127, velocity clamped to 0.
        var noteOns = capture.Sent.Where(b => (b[0] & 0xF0) == 0x90).ToList();
        Assert.Single(noteOns);
        Assert.Equal(127, noteOns[0][1]);   // pitch 200 → 127
        Assert.Equal(0, noteOns[0][2]);     // velocity -5 → 0
        // No raw out-of-range byte ever recorded.
        Assert.All(capture.Sent, b => Assert.All(b.Skip(1), x => Assert.InRange(x, (byte)0, (byte)127)));
    }

    /// <summary>
    /// T-40-04: an oversized sysex payload is length-capped at the builtin
    /// boundary (65536 bytes) + a [midi] advisory — never handed unbounded to
    /// native. A 2-second sine (~88200 samples) exceeds the cap; the recorded
    /// sysex must be exactly capped.
    /// </summary>
    [Fact]
    public void MidiSysex_Oversized_LengthCapped()
    {
        const int SysexMaxBytes = 65536;
        const string script = @"use ""@audio""
use ""@midi""
MidiDevice dev = (openMidiOutput ""Synth"")
Buffer big = (createSineTone 440Hz 2.0 0.5)
(midiSysex dev big)
";
        var capture = new CaptureMidiBackend("Synth");
        RenderingDiagnostics.ResetForTesting();
        MidiFunctions.BackendOverride = capture;
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(script, "<phase40-sysex>");
            Assert.True(ok, $"sysex script failed: {stderr}");
        }
        finally
        {
            MidiFunctions.BackendOverride = null;
        }

        Assert.Single(capture.Sent);
        // WR-05: the DATA is capped at SysexMaxBytes, then framed with 0xF0/0xF7 →
        // total length is the cap + 2 framing bytes.
        Assert.Equal(SysexMaxBytes + 2, capture.Sent[0].Length);
        Assert.Equal(0xF0, capture.Sent[0][0]);
        Assert.Equal(0xF7, capture.Sent[0][^1]);
    }

    /// <summary>
    /// WR-05: a sysex payload is framed <c>0xF0 &lt;data...&gt; 0xF7</c> on the wire.
    /// The original BufferToSysexBytes emitted only the clamped data bytes (no
    /// envelope), so the message was invalid and devices rejected it.
    /// </summary>
    [Fact]
    public void MidiSysex_IsFramedWithF0F7()
    {
        const string script = @"use ""@audio""
use ""@midi""
MidiDevice dev = (openMidiOutput ""Synth"")
Buffer payload = (createSineTone 440Hz 0.001 0.5)
(midiSysex dev payload)
";
        var capture = new CaptureMidiBackend("Synth");
        RenderingDiagnostics.ResetForTesting();
        MidiFunctions.ResetForTesting();
        MidiFunctions.BackendOverride = capture;
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(script, "<phase40-sysex-frame>");
            Assert.True(ok, $"sysex frame script failed: {stderr}");
        }
        finally
        {
            MidiFunctions.ResetForTesting();
        }

        Assert.Single(capture.Sent);
        var msg = capture.Sent[0];
        Assert.True(msg.Length >= 3, "framed sysex must have at least F0 + 1 data + F7");
        Assert.Equal(0xF0, msg[0]);
        Assert.Equal(0xF7, msg[^1]);
        // No interior byte is a framing byte (data is 7-bit, < 0xF0).
        for (int i = 1; i < msg.Length - 1; i++)
            Assert.InRange(msg[i], (byte)0, (byte)0x7F);
    }

    /// <summary>
    /// MIDI-RT-01 module gate: a @midi builtin is unreachable WITHOUT
    /// `use "@midi"`. Two layers enforce this charitably: (1) the forward-decls
    /// live in midi.flow so without the import the name doesn't resolve
    /// ("Function 'midiPorts' not found"); (2) even if resolved, the
    /// RequireModuleActivated gate (MidiEnabled=false) raises
    /// "requires `use \"@midi\"`". Either path is a valid failure — the contract
    /// is "the call cannot succeed without the import", never a silent send.
    /// </summary>
    [Fact]
    public void MidiBuiltin_WithoutModule_RaisesActivationError()
    {
        const string script = @"(midiPorts)";
        RenderingDiagnostics.ResetForTesting();
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errCount) = runner.RunSource(script, "<phase40-gate>");
        Assert.True(!ok || errCount > 0, "expected a failure calling midiPorts without use \"@midi\"");
        // Accept either enforcement message.
        Assert.True(stderr.Contains("@midi") || stderr.Contains("midiPorts"),
            $"expected a midi-gate failure message, got: {stderr}");
    }

    /// <summary>
    /// SC1 / MIDI-RT-01: a `use "@midi"` + (midiPorts) smoke runs WITHOUT throwing
    /// on this librtmidi.so-absent box (charitable NullMidiBackend → empty ports).
    /// Uses the REAL backend (no override) to prove the lib-absent fallback path.
    /// </summary>
    [Fact]
    public void MidiPortsSmoke_LibAbsent_DoesNotThrow()
    {
        const string script = @"use ""@midi""
(midiPorts)
(print ""midi-ok"")
";
        RenderingDiagnostics.ResetForTesting();
        MidiFunctions.BackendOverride = null;   // real manager → NullMidiBackend here
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource(script, "<phase40-smoke>");
        Assert.True(ok, $"@midi smoke failed: {stderr}");
        Assert.Contains("midi-ok", stdout);
    }
}
