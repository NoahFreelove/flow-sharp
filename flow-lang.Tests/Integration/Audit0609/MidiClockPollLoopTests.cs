#if !FLOW_WEB
using System;
using FlowLang.Audio;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §5.5 — the RtMidiInputBridge poll loop must not (a) trust a
/// stale <c>size</c> on a native error, nor (b) dispatch an oversize message via
/// <c>Array.Copy(buf, chunk, n)</c> with <c>n &gt; buf.Length</c> — that throws
/// ArgumentException, escapes the inner try, and KILLS the clock-slave poll
/// thread (a &gt; 512-byte sysex on the clock-in port; sysex is un-ignored at
/// MidiClock.cs:548). The per-iteration decision is now factored into the pure
/// <see cref="RtMidiInputBridge.ClassifyPollResult"/> helper so it is testable
/// without real <c>librtmidi</c>; these Facts pin its behavior + prove the loop
/// body (which uses Math.Min) cannot throw on an oversize message.
/// </summary>
public class MidiClockPollLoopTests
{
    private const int Buf = 512; // LibRtMidi.NameBufferSize

    [Fact]
    public void NegativeDelta_BacksOff_DoesNotTrustSize()
    {
        // librtmidi returns -1 on error WITHOUT writing *size — size stays at the
        // input capacity (512). The loop must back off, NOT dispatch 512 stale
        // bytes in a 100% busy spin.
        var (action, len) = RtMidiInputBridge.ClassifyPollResult(delta: -1.0, size: Buf, bufLength: Buf);
        Assert.Equal(RtMidiInputBridge.PollAction.Error, action);
        Assert.Equal(0, len);
    }

    [Fact]
    public void EmptyQueue_Waits()
    {
        var (action, len) = RtMidiInputBridge.ClassifyPollResult(delta: 0.0, size: 0, bufLength: Buf);
        Assert.Equal(RtMidiInputBridge.PollAction.Wait, action);
        Assert.Equal(0, len);
    }

    [Fact]
    public void NormalMessage_Dispatches_ReturnedLength()
    {
        var (action, len) = RtMidiInputBridge.ClassifyPollResult(delta: 0.0, size: 3, bufLength: Buf);
        Assert.Equal(RtMidiInputBridge.PollAction.Dispatch, action);
        Assert.Equal(3, len);
    }

    [Fact]
    public void FullBuffer_Dispatches_NotDropped()
    {
        // Exactly buffer-sized is copyable — dispatch all of it.
        var (action, len) = RtMidiInputBridge.ClassifyPollResult(delta: 0.0, size: Buf, bufLength: Buf);
        Assert.Equal(RtMidiInputBridge.PollAction.Dispatch, action);
        Assert.Equal(Buf, len);
    }

    [Fact]
    public void OversizeSysex_IsDropped_NotDispatched_WithAdvisory()
    {
        RenderingDiagnostics.ResetForTesting();
        var originalErr = Console.Error;
        var sw = new System.IO.StringWriter();
        Console.SetError(sw);
        try
        {
            // A 900-byte sysex on a 512-byte poll buffer: librtmidi skipped the
            // memcpy but still reports size=900. The helper must say Wait (drop),
            // so the loop never runs Array.Copy(buf, chunk, 900).
            var (action, len) = RtMidiInputBridge.ClassifyPollResult(delta: 0.0, size: 900, bufLength: Buf);
            Assert.Equal(RtMidiInputBridge.PollAction.Wait, action);
            Assert.Equal(0, len);
        }
        finally
        {
            Console.SetError(originalErr);
            RenderingDiagnostics.ResetForTesting();
        }
        Assert.Contains("[clock]", sw.ToString());
        Assert.Contains("oversize", sw.ToString());
    }

    /// <summary>
    /// Mirror the actual poll-loop body: for every classification the loop takes,
    /// the dispatch branch only runs Array.Copy when len ∈ [0, bufLength]. Drive
    /// the helper with an oversize size and confirm the body cannot throw (the
    /// pre-fix Array.Copy(buf, chunk, 900) would throw ArgumentException).
    /// </summary>
    [Fact]
    public void PollBodyShape_OversizeMessage_DoesNotThrow()
    {
        var buf = new byte[Buf];
        var ex = Record.Exception(() =>
        {
            var (action, len) = RtMidiInputBridge.ClassifyPollResult(delta: 0.0, size: 900, bufLength: buf.Length);
            if (action == RtMidiInputBridge.PollAction.Dispatch)
            {
                var chunk = new byte[len];
                Array.Copy(buf, chunk, len); // would throw if len > buf.Length
            }
        });
        Assert.Null(ex);
    }
}
#endif
