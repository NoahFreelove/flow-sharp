#if !FLOW_WEB
using System.Linq;
using FlowLang.Audio;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Midi;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Integration.Phase40;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §5.6 — midiOut leaked a native librtmidi output device + open
/// ALSA port on EVERY call (the handle was never Closed and RtMidiOutputHandle has
/// no finalizer). The fix wraps dispatch in try/finally { handle.Close(); } and
/// flushes All-Notes-Off (CC123) per used channel before close. Asserted through
/// the CaptureMidiBackend seam (its handle exposes a Closed flag + records every
/// sent byte) so no real ALSA is needed.
///
/// <para>Shares the WASM console collection because the engine run redirects the
/// process-wide Console (mirrors VirtualMidiTests).</para>
/// </summary>
[Collection(WasmEntryConsoleCollection.Name)]
public class MidiOutHandleCloseTests
{
    private static CaptureMidiBackend RunWithCapture(string script, params string[] ports)
    {
        var capture = new CaptureMidiBackend(ports);
        RenderingDiagnostics.ResetForTesting();
        MidiFunctions.ResetForTesting();
        MidiFunctions.BackendOverride = capture;
        MidiFunctions.ScheduleInspectOverride = _ => { }; // no wall-clock sleeps
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(script, "<audit-5.6>");
            Assert.True(ok, $"@midi script failed: {stderr}");
        }
        finally
        {
            MidiFunctions.ResetForTesting();
        }
        return capture;
    }

    /// <summary>midiOut(Song) closes its handle and flushes All-Notes-Off on the
    /// used channel before closing.</summary>
    [Fact]
    public void MidiOutSong_ClosesHandle_AndFlushesAllNotesOff()
    {
        const string script = @"use ""@midi""
section main {
    Sequence piano = | C4q E4q |
}
Song s = [main]
(midiOut s ""Synth"")
";
        var capture = RunWithCapture(script, "Synth");

        // Handle closed — no leaked native device / ALSA port.
        Assert.True(capture.Handle.Closed, "midiOut must Close its output handle");

        // An All-Notes-Off (CC123 value 0) was sent on the piano channel (0).
        var cc123 = capture.Sent.Where(b => (b[0] & 0xF0) == 0xB0 && b[1] == 123).ToList();
        Assert.NotEmpty(cc123);
        Assert.All(cc123, b => Assert.Equal(0, b[2])); // value 0 per the GM spec
        Assert.Contains(cc123, b => (b[0] & 0x0F) == 0); // on channel 0
    }

    /// <summary>midiOut(bare Sequence) also closes its handle + flushes.</summary>
    [Fact]
    public void MidiOutBareSequence_ClosesHandle()
    {
        const string script = @"use ""@midi""
Sequence mel = | C4q E4q G4q |
(midiOut mel ""Synth"")
";
        var capture = RunWithCapture(script, "Synth");
        Assert.True(capture.Handle.Closed, "midiOut(Sequence) must Close its output handle");
        Assert.Contains(capture.Sent, b => (b[0] & 0xF0) == 0xB0 && b[1] == 123 && b[2] == 0);
    }

    /// <summary>A multi-channel arrangement flushes All-Notes-Off on EVERY used
    /// channel (drums→ch9 + piano→ch0) before closing.</summary>
    [Fact]
    public void MidiOutSong_MultiChannel_FlushesEveryUsedChannel()
    {
        const string script = @"use ""@midi""
section main {
    Sequence piano = | C4q E4q |
    Sequence drums = | C2q C2q |
}
Song s = [main]
(midiOut s ""Synth"")
";
        var capture = RunWithCapture(script, "Synth");
        Assert.True(capture.Handle.Closed);

        var offChannels = capture.Sent
            .Where(b => (b[0] & 0xF0) == 0xB0 && b[1] == 123 && b[2] == 0)
            .Select(b => b[0] & 0x0F)
            .ToHashSet();
        Assert.Contains(0, offChannels); // piano
        Assert.Contains(9, offChannels); // drums (GM percussion)
    }
}
#endif
