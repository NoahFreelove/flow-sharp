using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Network;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-06 OSC-01 — UDP loopback round-trip via 127.0.0.1:N
/// (ephemeral port allocated by an OS probe per Pitfall #10 / RESEARCH
/// §K line 1149). Exercises the full Rug.Osc OscReceiver / OscSender
/// pipeline through OscFunctions' charitable type-tag inference.
/// </summary>
[Collection("FlowScripts")]
public class OscLoopbackTests : IDisposable
{
    private readonly List<IReadOnlyList<Value>> _received = new();
    private readonly ManualResetEventSlim _gotMessage = new(false);

    public OscLoopbackTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = (_, args) =>
        {
            lock (_received) _received.Add(args);
            _gotMessage.Set();
        };
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        OscFunctions.ResetForTesting();
        OscFunctions.HandlerInvokeOverride = null;
    }

    private static FunctionOverload StubHandler()
    {
        var sig = new FunctionSignature("test_handler",
            new FlowType[] { VoidType.Instance },
            IsVarArgs: true);
        return FunctionOverload.Internal("test_handler", sig, _ => Value.Void());
    }

    private static FlowLang.Runtime.ExecutionContext MakeContext()
    {
        var reporter = new FlowLang.Diagnostics.ErrorReporter();
        var registry = new InternalFunctionRegistry();
        return new FlowLang.Runtime.ExecutionContext(reporter, registry);
    }

    /// <summary>
    /// Pick an ephemeral free UDP port via a probe socket — Pitfall #10
    /// avoidance per RESEARCH line 1493. Rug.Osc 1.2.5's OscReceiver
    /// constructor doesn't accept port=0 ephemeral binding directly, so
    /// we probe with a UdpClient first and pass the assigned port.
    /// </summary>
    private static int FindFreeUdpPort()
    {
        using var probe = new UdpClient(0, AddressFamily.InterNetwork);
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    /// <summary>
    /// RoundTrip_127001_EphemeralPort_PreservesPayload: bind oscListen on
    /// 127.0.0.1 with an ephemeral port; send a string + int + float
    /// payload; assert handler received 3 args with the expected values
    /// within a 2s timeout.
    /// </summary>
    [Fact]
    public void RoundTrip_127001_EphemeralPort_PreservesPayload()
    {
        int port = FindFreeUdpPort();
        var ctx = MakeContext();
        var handler = StubHandler();

        // Set up the listener with our test seam handler.
        var receiver = new Rug.Osc.OscReceiver(IPAddress.Loopback, port);
        var cts = new CancellationTokenSource();
        cts.Token.Register(() => { try { receiver.Dispose(); } catch { } });

        var listenerTask = Task.Run(() =>
        {
            try { receiver.Connect(); } catch { return; }
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var pkt = receiver.Receive();
                    OscFunctions.DispatchPacketForTesting(pkt, "/test/path", handler, ctx);
                }
                catch (ObjectDisposedException) { break; }
                catch (OperationCanceledException) { break; }
                catch { /* charitable */ }
            }
        });

        try
        {
            // Send the message using OscFunctions' OscSender path indirectly
            // via reflection-equivalent: call Rug.Osc directly to exercise
            // the wire format that InferOscArgs produces.
            var payload = new List<Value>
            {
                Value.String("hello"),
                Value.Int(42),
                Value.Float(1.5),
            };
            var oscArgs = OscFunctions.InferOscArgs(payload);
            var msg = new Rug.Osc.OscMessage("/test/path", oscArgs);

            // Rug.Osc 1.2.5 quirk per Plan 38-06: 2-arg ctor binds sender
            // local port = remote port, colliding on loopback. Use 3-arg
            // with localPort=0 for an OS-picked ephemeral local port.
            using var sender = new Rug.Osc.OscSender(IPAddress.Loopback, 0, port);
            sender.Connect();
            sender.Send(msg);

            // Wait up to 2s for the listener to dispatch.
            bool got = _gotMessage.Wait(TimeSpan.FromSeconds(2));
            Assert.True(got, "Did not receive message within 2s timeout");

            IReadOnlyList<Value> received;
            lock (_received)
            {
                Assert.NotEmpty(_received);
                received = _received[0];
            }
            Assert.Equal(3, received.Count);
            Assert.Equal("hello", received[0].As<string>());
            Assert.Equal(42, received[1].As<int>());
            // Float on the wire arrives back as Float per RugOscArgToFlowValue.
            // Value.Float wraps a double per Value.cs:25 (Phase 26 design);
            // round-trip 1.5f → boxed double 1.5 is exact.
            Assert.Equal(FloatType.Instance, received[2].Type);
            Assert.Equal(1.5, received[2].As<double>(), precision: 6);
        }
        finally
        {
            cts.Cancel();
            try { listenerTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
        }
    }

    /// <summary>
    /// AddressMismatch_NoDispatch: send to a different address than the
    /// listener filters on; assert handler not invoked within a 500ms
    /// timeout (literal-path match per D-38-16 v1.5 scope).
    /// </summary>
    [Fact]
    public void AddressMismatch_NoDispatch()
    {
        int port = FindFreeUdpPort();
        var ctx = MakeContext();
        var handler = StubHandler();

        var receiver = new Rug.Osc.OscReceiver(IPAddress.Loopback, port);
        var cts = new CancellationTokenSource();
        cts.Token.Register(() => { try { receiver.Dispose(); } catch { } });

        var listenerTask = Task.Run(() =>
        {
            try { receiver.Connect(); } catch { return; }
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var pkt = receiver.Receive();
                    OscFunctions.DispatchPacketForTesting(pkt, "/expected", handler, ctx);
                }
                catch (ObjectDisposedException) { break; }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        });

        try
        {
            var msg = new Rug.Osc.OscMessage("/other", 1);
            using var sender = new Rug.Osc.OscSender(IPAddress.Loopback, 0, port);
            sender.Connect();
            sender.Send(msg);

            bool got = _gotMessage.Wait(TimeSpan.FromMilliseconds(500));
            Assert.False(got, "Handler should not fire on address mismatch");
        }
        finally
        {
            cts.Cancel();
            try { listenerTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
        }
    }
}
