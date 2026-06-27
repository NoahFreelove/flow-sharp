using System;
using FlowLang.Audio;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 MIDI-RT-01 + MIDI-RT-04 — backend probe + charitable lifecycle.
/// These Facts exercise the manager + Null/Capture seams with NO real ALSA, so
/// they pass on this <c>librtmidi.so</c>-absent dev box (the manager falls back
/// to <see cref="NullMidiBackend"/> charitably).
/// </summary>
public class MidiBackendTests
{
    /// <summary>
    /// MIDI-RT-01: <see cref="MidiPlaybackManager.GetBackend"/> NEVER throws and
    /// always returns a usable backend. On a box without <c>librtmidi.so</c> it
    /// returns a <see cref="NullMidiBackend"/> whose <c>ListPorts()</c> is empty
    /// (charitable null/empty, never null reference).
    /// </summary>
    [Fact]
    public void MidiBackendEnumeratesPorts()
    {
        using var manager = new MidiPlaybackManager();

        IMidiBackend backend = manager.GetBackend();   // must not throw
        Assert.NotNull(backend);

        var ports = backend.ListPorts();
        Assert.NotNull(ports);                          // never null (MIDI-RT-01)

        // On this dev box librtmidi.so is absent → Null backend → empty ports.
        // If a future CI runner installs librtmidi.so, RtMidi enumerates a
        // (possibly empty) list — still non-null, still no throw. Either path is
        // a valid pass; the contract is "non-null + no throw".
        if (!manager.IsMidiAvailable())
        {
            Assert.Equal("Null", backend.Name);
            Assert.Empty(ports);
        }
    }

    /// <summary>
    /// MIDI-RT-04 / T-40-04: opening an absent port returns null (a dead handle)
    /// and NEVER throws — the hot-plug charitable rule. Exercised against both the
    /// production manager-selected backend AND the CaptureMidiBackend seam.
    /// </summary>
    [Fact]
    public void MidiHotPlugNeverThrows()
    {
        using var manager = new MidiPlaybackManager();
        var backend = manager.GetBackend();

        // Absent port → null, no throw.
        var handle = backend.OpenOutput("no-such-port-xyzzy");
        Assert.Null(handle);

        // Capture seam: same charitable contract.
        var capture = new CaptureMidiBackend("Virtual Raw MIDI");
        Assert.Null(capture.OpenOutput("definitely-not-present"));
        Assert.NotNull(capture.OpenOutput("Virtual Raw MIDI"));
    }

    /// <summary>
    /// MIDI-RT-04: a freshly-constructed <see cref="AudioBuffer"/> has a null
    /// <see cref="AudioBuffer.PlaybackStartTime"/> alignment origin; it becomes
    /// non-null only when playback begins (the seam is set in PlaySamples).
    /// </summary>
    [Fact]
    public void AudioBuffer_PlaybackStartTime_DefaultsNull()
    {
        var buf = new AudioBuffer(44100, 1, 44100);
        Assert.Null(buf.PlaybackStartTime);

        // The seam is settable (PlaySamples stamps it with Stopwatch.GetTimestamp()).
        buf.PlaybackStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Assert.NotNull(buf.PlaybackStartTime);
    }
}
