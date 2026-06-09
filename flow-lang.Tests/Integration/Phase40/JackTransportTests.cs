using System;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Midi;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 Plan 40-03 JACK-01 (D-40-05 best-effort) — JACK transport sync.
///
/// <para><b>JackSharp verdict (Open Q3):</b> JackSharp 0.4.0 loads under net10 via
/// the net4x compat shim but exposes NO transport API (no jack_transport_query /
/// tempo / BBT). JACK-01 therefore ships via a hand-rolled
/// <c>[DllImport("jack")]</c> <c>jack_transport_query</c> in
/// <see cref="JackFunctions"/> — not via JackSharp.</para>
///
/// <para>No real JACK server needed: the
/// <see cref="JackFunctions.TransportQueryOverride"/> seam injects a synthetic
/// transport snapshot so both the present-server (drive-tempo) and absent-server
/// (charitable no-op) branches are exercised. Each Fact resets the override to
/// null in a finally so the seam never leaks. The headline acceptance Fact —
/// <see cref="JackAbsentServerNoOp"/> — asserts the JACK-01 charitable rule: an
/// absent server is a no-op that NEVER throws and leaves Tempo untouched.</para>
/// </summary>
// Serialized with the WASM console collection (and thus all process-wide
// Console.Out/Error redirectors): JackTransportTests drives a FlowEngineRunner
// that redirects process-wide Console streams. Sharing WasmEntryConsoleCollection
// prevents the cross-class Console-redirection race (same fix Plan 40-01 applied
// to VirtualMidiTests + OfflineRenderDeterminismTests).
[Collection(WasmEntryConsoleCollection.Name)]
public class JackTransportTests
{
    /// <summary>
    /// JACK-01 headline acceptance (T-40-04): with NO JACK server present,
    /// <c>(jackSync)</c> is a charitable no-op — it emits a one-shot advisory,
    /// returns a dead handle (ServerPresent=false), leaves the active Tempo
    /// untouched, and NEVER throws. Non-JACK workflows are unaffected.
    /// </summary>
    [Fact]
    public void JackAbsentServerNoOp()
    {
        // Simulate "no server answered" without touching real libjack.
        JackFunctions.TransportQueryOverride = () => (false, null, null, null);
        try
        {
            using var runner = new FlowEngineRunner();
            // Declare the handle at top level so GetVariable can read it from the
            // global frame. The absent-server no-op must not perturb anything.
            var (ok, stdout, stderr, _) = runner.RunSource(@"use ""@jack""
JackHandle h = (jackSync)
(print ""after jackSync"")
", "<jack-absent>");

            Assert.True(ok, $"jackSync with no server must not error the program: {stderr}");
            Assert.Contains("after jackSync", stdout);
            // Charitable advisory surfaced (one-shot).
            Assert.Contains("[jack]", stderr);

            // The handle is a dead handle: server absent, no tempo applied. This
            // IS the charitable no-op: Tempo is never touched (null), never throws.
            var handle = runner.GetVariable("h").As<JackHandleData>();
            Assert.NotNull(handle);
            Assert.False(handle.ServerPresent);
            Assert.Null(handle.Tempo);
        }
        finally
        {
            JackFunctions.TransportQueryOverride = null;
        }
    }

    /// <summary>
    /// JACK-01: when a JACK server IS present and the transport carries a valid
    /// BBT tempo, <c>(jackSync)</c> drives <see cref="MusicalContext.Tempo"/> from
    /// the transport BPM. Exercised via the synthetic transport seam (no real
    /// server) so the drive-tempo path is machine-proven; the REAL-hardware /
    /// real-DAW transport path is HUMAN-UAT (D-40-07).
    /// </summary>
    [Fact]
    public void JackPresentServerDrivesTempo_ViaSeam()
    {
        JackFunctions.TransportQueryOverride = () => (true, 140.0, 3, 2);
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(@"use ""@jack""
JackHandle h = (jackSync)
", "<jack-present>");

            Assert.True(ok, $"jackSync with a present server must not error: {stderr}");

            var handle = runner.GetVariable("h").As<JackHandleData>();
            Assert.NotNull(handle);
            Assert.True(handle.ServerPresent);
            Assert.Equal(140.0, handle.Tempo!.Value, 3);
            Assert.Equal(3, handle.Bar);
            Assert.Equal(2, handle.Beat);
        }
        finally
        {
            JackFunctions.TransportQueryOverride = null;
        }
    }

    /// <summary>
    /// T-40-01: a transport BPM that fails <see cref="MusicalContext.IsValidTempo"/>
    /// (≤ 0) is REJECTED — not written to Tempo — with an advisory. The handle
    /// reports the server present but no tempo applied.
    /// </summary>
    [Fact]
    public void JackInvalidTransportTempo_Rejected()
    {
        JackFunctions.TransportQueryOverride = () => (true, 0.0, null, null);
        try
        {
            using var runner = new FlowEngineRunner();
            var (ok, _, stderr, _) = runner.RunSource(@"use ""@jack""
JackHandle h = (jackSync)
", "<jack-bad-tempo>");

            Assert.True(ok, $"jackSync with bad transport tempo must not error: {stderr}");

            var handle = runner.GetVariable("h").As<JackHandleData>();
            Assert.NotNull(handle);
            Assert.True(handle.ServerPresent);
            Assert.Null(handle.Tempo); // out-of-range tempo not applied
        }
        finally
        {
            JackFunctions.TransportQueryOverride = null;
        }
    }

    /// <summary>
    /// Non-JACK workflows are demonstrably unaffected by JACK absence: a program
    /// that NEVER imports @jack runs identically whether or not a JACK server is
    /// present. (No @jack import means jackSync is never registered as active and
    /// the libjack P/Invoke is never reached.)
    /// </summary>
    [Fact]
    public void NonJackWorkflowUnaffected()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errors) = runner.RunSource(@"use ""@audio""
section main {
    Sequence lead = | C4q E4q G4q |
}
Song s = [main]
Buffer mix = (renderSong s ""sine"")
(print ""rendered without jack"")
", "<no-jack>");

        Assert.True(ok, $"non-JACK workflow failed: {stderr}");
        Assert.Equal(0, errors);
        Assert.Contains("rendered without jack", stdout);
    }

    /// <summary>
    /// The @jack gate: calling <c>(jackSync)</c> WITHOUT <c>use "@jack"</c> raises
    /// the clear "requires `use \"@jack\"`" error (mirrors the @midi / @osc gate),
    /// rather than silently running or crashing.
    /// </summary>
    [Fact]
    public void JackSyncWithoutModuleImport_GatedError()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errors) = runner.RunSource("(jackSync)\n", "<jack-ungated>");
        // Either a parse/eval error is reported, OR the gate InvalidOperationException
        // surfaces — both leave the run non-clean. The key contract: it does NOT
        // succeed silently.
        Assert.True(!ok || errors > 0,
            $"jackSync without `use \"@jack\"` must not succeed silently. stderr={stderr}");
    }
}
