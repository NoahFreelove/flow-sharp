using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Network;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0614;

/// <summary>
/// sweep-2026-06-14 (generative-improv-osc group) regression tests for two
/// OSC defects:
///
/// <list type="number">
///   <item><b>oscBundle could not wrap messages</b> — the referenced
///     <c>oscSendMessage</c> builtin never existed, so a composer could only
///     ever nest empty bundles. The fix adds <c>oscMsg(path, ...args)</c>,
///     a leaf-message constructor that produces a packet-carrying OscHandle.
///     This test sends <c>(oscBundle (oscMsg "/a" 1) (oscMsg "/b" 2))</c> over
///     UDP loopback and asserts both child messages dispatch through a real
///     listener.</item>
///   <item><b>Every clean oscStop logged a spurious receive error</b> —
///     disposing the Rug.Osc receiver to unblock <c>Receive()</c> throws a
///     GENERIC Exception ("The receiver socket has been disconnected") rather
///     than ObjectDisposedException, which fell through to the error-logging
///     catch. The fix breaks silently when cancellation is requested. This
///     test runs <c>oscListen</c> then <c>oscStop</c> and asserts stderr is
///     clean of the scary "[osc] receive error" line.</item>
/// </list>
/// </summary>
[Collection("FlowScripts")]
public class OscMsgBundleSweepTests : IDisposable
{
    public OscMsgBundleSweepTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = null;
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = null;
    }

    private static int FindFreeUdpPort()
    {
        using var probe = new UdpClient(0, AddressFamily.InterNetwork);
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    /// <summary>
    /// oscMsg builds a single-message packet; oscBundle wraps those leaf
    /// messages into a real OscBundle holding BOTH children. Before the fix
    /// this was impossible — oscBundle required every arg to carry a non-null
    /// PendingPacket, but the only producer (oscBundle itself) always wrapped
    /// an empty/other bundle. The referenced oscSendMessage builtin did not
    /// exist. We assert the constructed bundle structurally contains both
    /// OscMessages, then send it over loopback and confirm a child arrives.
    /// </summary>
    [Fact]
    public void OscBundle_OfOscMsg_WrapsBothMessages()
    {
        int port = FindFreeUdpPort();
        int hits = 0;

        OscFunctions.HandlerInvokeOverride = (_, args) =>
        {
            System.Threading.Interlocked.Increment(ref hits);
        };

        string script = $@"use ""@osc""
proc noop (Int: v)
    (print)
end proc
OscHandle listener = (oscListen {port} ""/a"" noop)
OscHandle bundle = (oscBundle (oscMsg ""/a"" 1) (oscMsg ""/b"" 2))
(oscSendBundle ""127.0.0.1"" {port} bundle)
";
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errCount) = runner.RunSource(script, "<sweep-osc-bundle>");
        Assert.True(ok && errCount == 0, $"script failed: errCount={errCount}\nstderr:\n{stderr}");

        // Structural assertion (the core of the bug): the bundle handle now
        // carries an OscBundle with BOTH leaf OscMessages — not an empty bundle.
        var bundleHandle = runner.GetVariable("bundle").As<OscHandleData>();
        var oscBundle = Assert.IsType<Rug.Osc.OscBundle>(bundleHandle.PendingPacket);
        var children = new System.Collections.Generic.List<Rug.Osc.OscMessage>();
        for (int i = 0; i < oscBundle.Count; i++)
            if (oscBundle[i] is Rug.Osc.OscMessage m) children.Add(m);
        Assert.Equal(2, children.Count);
        Assert.Equal("/a", children[0].Address);
        Assert.Equal("/b", children[1].Address);

        // Wire path: the listener on /a should receive its child over loopback.
        bool got = SpinUntil(() => hits >= 1, TimeSpan.FromSeconds(3));

        runner.RunSource("(oscStop listener)", "<sweep-osc-bundle-stop>");

        Assert.True(got, "bundle did not deliver the /a child over loopback");
    }

    /// <summary>
    /// A clean oscStop must NOT print "[osc] receive error ... socket has been
    /// disconnected" to stderr — that generic-exception line was the symptom
    /// of the spurious-log defect.
    /// </summary>
    [Fact]
    public void OscStop_CleanShutdown_DoesNotLogReceiveError()
    {
        int port = FindFreeUdpPort();

        string script = $@"use ""@osc""
proc noop (Int: v)
    (print)
end proc
OscHandle h = (oscListen {port} ""/test"" noop)
(oscStop h)
";
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errCount) = runner.RunSource(script, "<sweep-osc-stop>");
        Assert.True(ok && errCount == 0, $"script failed: errCount={errCount}\nstderr:\n{stderr}");

        Assert.DoesNotContain("receive error", stderr);
        Assert.DoesNotContain("socket has been disconnected", stderr);
    }

    private static bool SpinUntil(Func<bool> cond, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (cond()) return true;
            System.Threading.Thread.Sleep(25);
        }
        return cond();
    }
}
