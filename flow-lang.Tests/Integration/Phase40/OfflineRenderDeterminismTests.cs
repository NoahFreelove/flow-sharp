using System;
using System.Linq;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Midi;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 LINK-02 — the determinism invariant: offline render
/// (<c>renderSong</c> / <c>writeWav</c>) is byte-identical regardless of any
/// MIDI / sync state present. No MIDI path touches offline render. This gate is
/// writable + valuable NOW even though Ableton Link itself is deferred to
/// community/v1.6 (D-40-06) — it pins the "sync tempo is a play/loop/preview-only
/// input, never applied to writeWav/writeMidi" contract.
/// </summary>
// Serialized with the WASM console collection (and thus all RunFromJs callers):
// this class drives a FlowEngineRunner that redirects process-wide Console.Out/
// Error. Sharing WasmEntryConsoleCollection prevents the cross-class
// Console-redirection race with the Phase 48 WASM determinism tests.
[Collection(WasmEntryConsoleCollection.Name)]
public class OfflineRenderDeterminismTests
{
    // A pure synth render (sine — no disk samples) so the test is hermetic and
    // does not depend on the U-Iowa sample bundle / CWD.
    private const string RenderScript = @"use ""@audio""
section main {
    Sequence lead = | C4q E4q G4q C5q |
}
Song s = [main]
Buffer mix = (renderSong s ""sine"")
";

    // The SAME render, but with the @midi module imported + a (dead) device
    // opened FIRST. If any MIDI/sync state leaked into the render path, the PCM
    // would differ. On this lib-absent box openMidiOutput returns a dead handle —
    // exactly the charitable path we want to prove is render-inert.
    private const string RenderScriptWithMidi = @"use ""@audio""
use ""@midi""
MidiDevice dev = (openMidiOutput ""no-such-port"")
section main {
    Sequence lead = | C4q E4q G4q C5q |
}
Song s = [main]
Buffer mix = (renderSong s ""sine"")
";

    private static byte[] RenderToPcm(string script)
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(script, "<phase40-link02>");
        Assert.True(ok, $"render failed: {stderr}");
        var buf = runner.GetVariable("mix").As<AudioBuffer>();
        Assert.NotNull(buf);
        Assert.True(buf.Frames > 0, "LINK-02 render produced zero frames");

        // Serialize the float PCM to bytes for a byte-identical comparison.
        var bytes = new byte[buf.Data.Length * 4];
        System.Buffer.BlockCopy(buf.Data, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// LINK-02: writeWav/renderSong output is byte-identical with vs without any
    /// MIDI/sync state present. The @midi import + dead-device open before render
    /// MUST NOT perturb a single PCM byte.
    /// </summary>
    [Fact]
    public void OfflineRenderIgnoresSync()
    {
        var plain = RenderToPcm(RenderScript);
        var withMidi = RenderToPcm(RenderScriptWithMidi);

        Assert.Equal(plain.Length, withMidi.Length);
        Assert.True(plain.SequenceEqual(withMidi),
            "LINK-02 VIOLATED: renderSong output differs when @midi state is present — " +
            "a MIDI/sync path leaked into the deterministic offline render.");
    }

    /// <summary>
    /// Two-run cmp-clean within the plain path (the standard Flow determinism
    /// contract) — guards the test itself isn't measuring nondeterministic render.
    /// </summary>
    [Fact]
    public void OfflineRenderTwoRunsByteIdentical()
    {
        var a = RenderToPcm(RenderScript);
        var b = RenderToPcm(RenderScript);
        Assert.True(a.SequenceEqual(b), "render is not two-run byte-identical");
    }

    // The SAME render, but a (jackSync) drives a transport tempo of 240 BPM BEFORE
    // the section is defined. This is exactly the WR-01 (LINK-02) leak path: jackSync
    // resolves GetMusicalContext() and writes the transport BPM; the subsequent
    // `section main` then snapshots that resolved context for offline render. If the
    // sync tempo were written to MusicalContext.Tempo it would leak into renderSong
    // and change the PCM. With the fix, jackSync writes only the live-sync sink, so
    // the render is unperturbed.
    private const string RenderScriptWithSyncTempo = @"use ""@audio""
use ""@jack""
JackHandle sync = (jackSync)
section main {
    Sequence lead = | C4q E4q G4q C5q |
}
Song s = [main]
Buffer mix = (renderSong s ""sine"")
";

    /// <summary>
    /// WR-01 (LINK-02): a sync-driven transport tempo MUST NOT change writeWav/
    /// renderSong bytes. Drives jackSync (present-server seam @ 240 BPM) before the
    /// section is built — the precise path that used to leak the live tempo into the
    /// captured section context — and asserts the rendered PCM is byte-identical to
    /// the no-sync render. This is the regression that proves the determinism gap is
    /// closed in code, not just in the doc header.
    /// </summary>
    [Fact]
    public void SyncDrivenTempoDoesNotLeakIntoOfflineRender()
    {
        var plain = RenderToPcm(RenderScript);

        JackFunctions.TransportQueryOverride = () => (true, 240.0, 1, 1);
        byte[] withSync;
        try
        {
            withSync = RenderToPcm(RenderScriptWithSyncTempo);
        }
        finally
        {
            JackFunctions.TransportQueryOverride = null;
        }

        Assert.Equal(plain.Length, withSync.Length);
        Assert.True(plain.SequenceEqual(withSync),
            "LINK-02 VIOLATED: a sync-driven (jackSync 240 BPM) transport tempo leaked " +
            "into the deterministic offline render — renderSong PCM changed.");
    }
}
