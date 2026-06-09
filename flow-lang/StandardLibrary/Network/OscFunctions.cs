using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Network;

/// <summary>
/// Phase 38 Plan 38-06 OSC-01 + OSC-02 — registration entry point for the
/// <c>@osc</c> stdlib module surface. Ships 5 surface builtins
/// (<c>oscSend</c> / <c>oscListen</c> / <c>oscStop</c> / <c>oscBundle</c> /
/// <c>oscSendBundle</c>) + 1 marker builtin (<c>__enableOscModule</c>) per
/// D-38-16 + RESEARCH §K + PATTERNS lines 580-674.
///
/// <para>
/// All 5 surface builtins gate on
/// <see cref="ExecutionContext.OscEnabled"/>. The gate flips <c>true</c>
/// when the <c>__enableOscModule</c> marker runs at import time
/// (per the trailing init call in <c>flow-lang/osc.flow</c>). Calling any
/// surface builtin without first importing the module raises a clear,
/// composer-facing error per the Phase 33 SFZ + Phase 39 notation-io
/// precedent.
/// </para>
///
/// <para>
/// Implementation notes:
/// <list type="bullet">
/// <item>Type-tag inference per D-38-13 is delegated to
/// <see cref="InferOscArgs"/> (Task 1 ship). Rug.Osc 1.2.5 encodes the
/// OSC 1.0 type-tag string directly from the boxed CLR argument types
/// — no separate tag-string assembly needed.</item>
/// <item>Server lifecycle per D-38-16 + Pitfall #5 (RESEARCH line 1445):
/// <c>oscListen</c> spawns a background <see cref="Task"/> with a
/// CancellationTokenSource; <c>oscStop</c> calls
/// <c>receiver.Dispose()</c> which forces the blocked
/// <c>Receive()</c> to throw, breaking the loop charitably.</item>
/// <item>Rate-limit gate per D-38-14 + RESEARCH §M: per-path
/// <see cref="_lastFireTimeMs"/> ConcurrentDictionary. Drop-newest
/// sample-and-hold at the 5ms (= 1/200Hz) window. No advisory on
/// individual drops (sample-and-hold IS the expected behavior).</item>
/// <item>Bundle DispatchPacket recursion per D-38-15 + RESEARCH §K
/// lines 1119-1147: depth cap 8 with one-shot stderr advisory on
/// overflow, timetag honored on receive (future-timetag bundles
/// dispatch on <see cref="Task.Delay"/>; immediate timetag = value 1
/// dispatches synchronously).</item>
/// </list>
/// </para>
/// </summary>
public static class OscFunctions
{
    // ===== Rate-limit gate state (D-38-14 / RESEARCH §M line 1213) =====
    //
    // Per-path `_lastFireTime` timestamp gate. Drop-newest sample-and-hold
    // at 200 Hz means: within a 5ms window per path, the FIRST incoming
    // message is dispatched to the handler; subsequent ones are dropped
    // silently. ConcurrentDictionary is the standard .NET concurrent-access
    // primitive — worst case two threads pass the gate on the same path in
    // the same 5ms window, which is acceptable per D-38-14 charitable
    // interpretation.

    private const int RateLimitWindowMs = 5;   // 1 / 200 Hz

    private static readonly ConcurrentDictionary<string, long> _lastFireTimeMs = new();

    // ===== Bundle nesting depth cap (D-38-15 / mirrors T-36-17 / D-39-19) =====
    private const int BundleDepthCap = 8;

    // ===== Foreground handler-dispatch queue (audit §5.3) =====
    //
    // The OSC receive loop runs on a ThreadPool thread (StartListener's
    // Task.Run). A composer's handler lambda mutates the SHARED, non-thread-safe
    // Interpreter/ExecutionContext (_recursionDepth, StrictMode, the
    // Stack<StackFrame> call stack, _returnValue). The foreground evaluator —
    // which keeps running after oscListen returns (subsequent statements, a
    // render, a live session) — is NOT lock-aware: it never takes an OSC lock,
    // so an OSC-side lock alone cannot stop it racing the listener thread and
    // corrupting the frame stack.
    //
    // Smallest correct fix: real user-proc handler invocations are NEVER run on
    // the listener thread. They are enqueued here and DRAINED synchronously on
    // the foreground evaluator thread — from each osc* builtin's own call site
    // (which the composer's foreground code invokes) and from the explicit
    // (oscPump) / oscStop drains. This guarantees ExecuteUserFunctionWithCaptures
    // only ever runs on the one evaluator thread, serialized-not-parallel with
    // the rest of the script. Latency tradeoff: a handler fires on the next
    // foreground osc* call (or pump), not the instant the packet arrives — an
    // acceptable cost for not corrupting the interpreter (a real-time MIDI/clock
    // sink would want a different design, but Flow's @osc surface is composer
    // scripting, not a hot audio path).
    //
    // Internal handlers (test stubs) and the HandlerInvokeOverride seam still
    // fire inline on the dispatch thread — they touch no shared interpreter
    // state, so the existing Phase 38 tests/seams are unaffected.
    private sealed record PendingHandlerInvocation(
        FunctionOverload Handler,
        IReadOnlyList<Value> Args,
        FlowLang.Runtime.ExecutionContext Context);

    private static readonly ConcurrentQueue<PendingHandlerInvocation> _pendingHandlers = new();

    /// <summary>
    /// Test-only: clear the rate-limit gate state. Required so per-test
    /// isolation (xUnit Facts under
    /// <c>flow-lang.Tests/Integration/Phase38/</c>) doesn't carry residual
    /// per-path timestamps between Facts.
    /// </summary>
    public static void ResetForTesting()
    {
        _lastFireTimeMs.Clear();
        while (_pendingHandlers.TryDequeue(out _)) { }
    }

    /// <summary>Test-only: number of user-proc handler invocations currently
    /// queued for the foreground drain (audit §5.3 pinning test seam).</summary>
    public static int PendingHandlerCountForTesting => _pendingHandlers.Count;

    /// <summary>
    /// Test-only (audit §5.3): enqueue a user-proc handler invocation exactly as
    /// the production <see cref="InvokeHandler"/> does for the
    /// <c>context.Invoker</c> path — a thread-safe <c>ConcurrentQueue.Enqueue</c>
    /// and nothing else. Lets a background "listener" thread model the production
    /// receive loop (which only ever ENQUEUES; it never runs the proc nor reads
    /// live interpreter state) so the pinning stress test races the foreground
    /// evaluator without a test-harness-only data race.
    /// </summary>
    public static void EnqueueHandlerForTesting(
        FunctionOverload handler, IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
        => _pendingHandlers.Enqueue(new PendingHandlerInvocation(handler, args, context));

    /// <summary>
    /// Audit §5.3 — drain every queued user-proc handler invocation on the
    /// CALLING (foreground evaluator) thread, serialized-not-parallel with the
    /// rest of the script. Called at the head of every osc* builtin and exposed
    /// to composers as <c>(oscPump)</c> so a script that binds a listener and
    /// then loops (rather than calling further osc* builtins) can still flush
    /// pending handlers without racing the interpreter. Idempotent; an empty
    /// queue is a no-op. Returns the number of handlers drained (composer-
    /// visible count for <c>(oscPump)</c>).
    /// </summary>
    public static int DrainPendingHandlers()
    {
        int drained = 0;
        // Snapshot the count BEFORE the loop so a handler that enqueues another
        // packet (rare, but possible via a nested osc call) cannot livelock the
        // drain — anything enqueued during this pass waits for the next.
        int budget = _pendingHandlers.Count;
        while (budget-- > 0 && _pendingHandlers.TryDequeue(out var pending))
        {
            try
            {
                pending.Context.Invoker!.ExecuteUserFunctionWithCaptures(
                    pending.Handler.Declaration!, pending.Args, pending.Handler.CapturedVariables);
            }
            catch (Exception ex)
            {
                // Charitable per Pitfall #12 — a handler exception never kills
                // the drain; surface to stderr and continue.
                Console.Error.WriteLine($"[osc] handler error: {ex.Message}");
            }
            drained++;
        }
        return drained;
    }

    /// <summary>
    /// Wire the 6 OSC builtins (5 surface + 1 marker) into the registry.
    /// Idempotent. Called once per <see cref="FlowLang.Core.FlowEngine"/>
    /// instance at construction time. Mirrors
    /// <see cref="FlowLang.StandardLibrary.Notation.NotationIoBuiltins.Register"/>
    /// signature.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // ----- Marker: __enableOscModule -----
        //
        // Called by the trailing `(__enableOscModule)` line in
        // flow-lang/osc.flow. Flips ExecutionContext.OscEnabled = true so
        // the 5 surface builtins unlock. Composer never calls directly.
        var sigMarker = new FunctionSignature("__enableOscModule", System.Array.Empty<FlowType>());
        registry.Register("__enableOscModule", sigMarker, _ =>
        {
            context.OscEnabled = true;
            return Value.Void();
        });

        // ----- oscSend(String host, Int port, String path, ...args) -> Void -----
        //
        // Varargs: args after `path` are inferred per D-38-13. Per RESEARCH §K,
        // Rug.Osc accepts CLR-boxed args via OscMessage(string, params object[])
        // and encodes the OSC 1.0 type-tag string automatically.
        // Audit §2.9 follow-up: the trailing VoidType is the vararg ELEMENT slot
        // (explicit any-type wildcard) so host/port/path are validated as fixed
        // params while the heterogeneous payload stays unconstrained. Previously
        // the signature ended at `path`, which made String the implied vararg
        // element type — only the (pre-§2.9) skip-validation hole let mixed
        // Int/Double/Bool/Buffer payloads through.
        var sigSend = new FunctionSignature("oscSend",
            new FlowType[] { StringType.Instance, IntType.Instance, StringType.Instance, VoidType.Instance },
            IsVarArgs: true,
            ParameterNames: new[] { "host", "port", "path", "args" });
        registry.Register("oscSend", sigSend, args =>
        {
            RequireModuleActivated(context, "oscSend");
            DrainPendingHandlers();   // audit §5.3 — flush queued handlers on the foreground thread
            string host = args[0].As<string>();
            int port = args[1].As<int>();
            string path = args[2].As<string>();
            // Tail of args (slot 3+) are the OSC payload — infer types
            // charitably per D-38-13.
            var payload = new List<Value>(args.Count - 3);
            for (int i = 3; i < args.Count; i++) payload.Add(args[i]);
            var oscArgs = InferOscArgs(payload);
            SendOscMessage(host, port, path, oscArgs);
            return Value.Void();
        });

        // ----- oscListen(Int port, String path, Function handler) -> OscHandle -----
        //
        // Returns an OscHandle reference-identity Value per D-38-16. The
        // handler lambda is invoked once per matching incoming message at
        // rate-limit ≤ 200 Hz per path (drop-newest sample-and-hold).
        var sigListen = new FunctionSignature("oscListen",
            new FlowType[] { IntType.Instance, StringType.Instance, FunctionType.Instance },
            ParameterNames: new[] { "port", "path", "handler" });
        registry.Register("oscListen", sigListen, args =>
        {
            RequireModuleActivated(context, "oscListen");
            DrainPendingHandlers();   // audit §5.3
            int port = args[0].As<int>();
            string path = args[1].As<string>();
            var handler = args[2].As<FunctionOverload>();
            return StartListener(port, path, handler, context);
        });

        // ----- oscStop(OscHandle handle) -> Void -----
        var sigStop = new FunctionSignature("oscStop",
            new FlowType[] { OscHandleType.Instance },
            ParameterNames: new[] { "handle" });
        registry.Register("oscStop", sigStop, args =>
        {
            RequireModuleActivated(context, "oscStop");
            var handle = args[0].As<OscHandleData>();
            StopListener(handle);
            // audit §5.3 — drain AFTER stop so any handler that was already
            // queued before the stop still runs; the future-timetag fix (§5.10)
            // guarantees nothing NEW is enqueued for a stopped handle.
            DrainPendingHandlers();
            return Value.Void();
        });

        // ----- oscPump() -> Int (count of handlers drained) -----
        //
        // Audit §5.3: composer-facing flush of any queued OSC handler
        // invocations on the foreground evaluator thread. A script that binds a
        // listener and then loops (rather than calling further osc* builtins)
        // calls (oscPump) inside the loop to run pending handlers without
        // racing the interpreter. Returns the number drained.
        var sigPump = new FunctionSignature("oscPump", System.Array.Empty<FlowType>());
        registry.Register("oscPump", sigPump, _ =>
        {
            RequireModuleActivated(context, "oscPump");
            return Value.Int(DrainPendingHandlers());
        });

        // ----- oscBundle(...packets) -> OscHandle wrapping OscBundle -----
        //
        // Varargs: each arg must be either an OscHandle-wrapped OscMessage
        // (rare; bundles of messages typically constructed via oscSend's
        // building block) OR an OscHandle wrapping an OscBundle (recursive
        // nesting). For v1.5 simplicity we wrap the result as a SINGLE
        // OscHandleData with `Receiver=null` discriminator — the value
        // carries the OscBundle in a stash field. Future v1.6 may
        // introduce a dedicated OscBundleType.
        var sigBundle = new FunctionSignature("oscBundle",
            new FlowType[] { OscHandleType.Instance },
            IsVarArgs: true,
            ParameterNames: new[] { "packet" });
        registry.Register("oscBundle", sigBundle, args =>
        {
            RequireModuleActivated(context, "oscBundle");
            DrainPendingHandlers();   // audit §5.3
            var packets = new List<Rug.Osc.OscPacket>(args.Count);
            for (int i = 0; i < args.Count; i++)
            {
                var hd = args[i].As<OscHandleData>();
                if (hd.PendingPacket == null)
                    throw new ArgumentException(
                        $"[osc] oscBundle arg {i}: OscHandle must carry a packet built via oscSendMessage or oscBundle — got listener handle");
                packets.Add(hd.PendingPacket);
            }
            // OSC 1.0 immediate timetag = value 1 per spec; Rug.Osc 1.2.5
            // does not ship an OscTimeTag.Immediately static so we
            // construct it directly.
            var immediate = new Rug.Osc.OscTimeTag(1UL);
            // Cast to OscPacket[] explicitly so C# binds to the
            // (OscTimeTag, params OscPacket[]) ctor without trying to
            // re-wrap as nested params.
            var packetsArr = packets.ToArray();
            var bundle = new Rug.Osc.OscBundle(immediate, packetsArr);
            return Value.OscHandle(new OscHandleData
            {
                Port = 0,
                Path = string.Empty,
                Receiver = null,
                Cts = new CancellationTokenSource(),
                ListenerTask = Task.CompletedTask,
                PendingPacket = bundle,
            });
        });

        // ----- oscSendBundle(String host, Int port, OscHandle bundle) -> Void -----
        var sigSendBundle = new FunctionSignature("oscSendBundle",
            new FlowType[] { StringType.Instance, IntType.Instance, OscHandleType.Instance },
            ParameterNames: new[] { "host", "port", "bundle" });
        registry.Register("oscSendBundle", sigSendBundle, args =>
        {
            RequireModuleActivated(context, "oscSendBundle");
            DrainPendingHandlers();   // audit §5.3
            string host = args[0].As<string>();
            int port = args[1].As<int>();
            var hd = args[2].As<OscHandleData>();
            if (hd.PendingPacket is not Rug.Osc.OscBundle bundle)
                throw new ArgumentException(
                    "[osc] oscSendBundle: arg 2 must be an OscHandle built via (oscBundle ...) — got non-bundle handle");
            SendOscPacket(host, port, bundle);
            return Value.Void();
        });
    }

    private static void RequireModuleActivated(FlowLang.Runtime.ExecutionContext context, string builtinName)
    {
        if (!context.OscEnabled)
            throw new System.InvalidOperationException(
                $"{builtinName} requires `use \"@osc\"`");
    }

    // ===== Type-tag inference (D-38-13 / RESEARCH §L) =====

    /// <summary>
    /// Phase 38 Plan 38-06 D-38-13 — charitable smallest-tag-that-fits OSC
    /// type-tag inference. Maps each Flow <see cref="Value"/> to the CLR
    /// type Rug.Osc 1.2.5's <c>OscMessage(string address, params object[] args)</c>
    /// constructor expects; Rug.Osc handles the OSC 1.0 type-tag string
    /// encoding from the boxed CLR types.
    ///
    /// <para>
    /// Mapping per 38-RESEARCH §L lines 1165-1175 + CONTEXT D-38-13:
    /// IntType→<c>int</c> (,i) — LongType→<c>long</c> (,h) —
    /// FloatType→<c>float</c> (,f) — DoubleType→<c>double</c> (,d) —
    /// StringType→<c>string</c> (,s) — SymbolType→<c>string</c> (,s; interned
    /// identity collapses to string on the wire per PATTERNS line 145) —
    /// BoolType→<c>bool</c> (,T / ,F) — BufferType→<c>byte[]</c> (,b blob).
    /// </para>
    ///
    /// <para>
    /// Unsupported types throw <see cref="ArgumentException"/> with the
    /// canonical "<c>[osc] unsupported arg type at index {i}: {Name} —
    /// use Int/Long/Float/Double/String/Symbol/Bool/Buffer</c>" message
    /// per 38-RESEARCH §L line 1197 + 38-PATTERNS line 651. Composer's
    /// escape hatch: explicit-cast at call site (e.g.
    /// <c>(oscSend host port "/x" (toLong 1) 1.5d)</c>).
    /// </para>
    /// </summary>
    public static object[] InferOscArgs(IReadOnlyList<Value> flowArgs)
    {
        var oscArgs = new object[flowArgs.Count];
        for (int i = 0; i < flowArgs.Count; i++)
        {
            var v = flowArgs[i];
            oscArgs[i] = v.Type switch
            {
                IntType => (int)v.Data!,
                LongType => (long)v.Data!,
                // Phase 26 (per Value.cs:25 + line 178 comment) — Float values
                // are double-backed; cast to float at the OSC wire boundary.
                FloatType => (float)(double)v.Data!,
                DoubleType => (double)v.Data!,
                StringType => (string)v.Data!,
                SymbolType => (string)v.Data!,
                BoolType => (bool)v.Data!,
                BufferType => AudioBufferToBlob((AudioBuffer)v.Data!),
                _ => throw new ArgumentException(
                    $"[osc] unsupported arg type at index {i}: {v.Type.Name} — " +
                    "use Int/Long/Float/Double/String/Symbol/Bool/Buffer")
            };
        }
        return oscArgs;
    }

    // ===== Buffer blob header (audit §5.13) =====
    //
    // The pre-fix blob was a bare flatten of Frames × Channels floats with NO
    // metadata, so BlobToBuffer could only guess mono/44100 — a stereo 48 kHz
    // buffer came back as a double-length mono buffer at the wrong rate (silent
    // corruption of a well-formed input). We now prefix a tiny 12-byte header:
    //   bytes 0..3 : ASCII magic "FLO1"
    //   bytes 4..7 : channels   (int32, little-endian)
    //   bytes 8..11: sampleRate (int32, little-endian)
    // then the float payload (little-endian IEEE-754, Frames × Channels).
    // BlobToBuffer parses the header; a blob WITHOUT the magic (a foreign OSC
    // ,b blob from another app) still decodes — charitably as mono/44100 with a
    // one-shot advisory — so interop is preserved.
    private static readonly byte[] BlobMagic = { (byte)'F', (byte)'L', (byte)'O', (byte)'1' };
    private const int BlobHeaderBytes = 12; // 4 magic + 4 channels + 4 sampleRate

    /// <summary>
    /// Audit §5.13 — flatten an <see cref="AudioBuffer"/> to a <c>byte[]</c> blob
    /// for OSC <c>,b</c> transport, PREFIXED with a 12-byte header (magic +
    /// channels + sampleRate, little-endian) so the receive side can reconstruct
    /// the exact channel count + sample rate instead of guessing mono/44100.
    /// Payload is little-endian IEEE-754 (4 bytes per float, Frames × Channels).
    /// </summary>
    public static byte[] AudioBufferToBlob(AudioBuffer buf)
    {
        if (buf is null) throw new ArgumentNullException(nameof(buf));
        int payloadBytes = buf.Data.Length * 4;
        var blob = new byte[BlobHeaderBytes + payloadBytes];
        System.Buffer.BlockCopy(BlobMagic, 0, blob, 0, 4);
        // BitConverter is little-endian on every platform Flow targets; the
        // BlobToBuffer reader uses the matching ToInt32 so this round-trips.
        System.Buffer.BlockCopy(BitConverter.GetBytes(buf.Channels), 0, blob, 4, 4);
        System.Buffer.BlockCopy(BitConverter.GetBytes(buf.SampleRate), 0, blob, 8, 4);
        System.Buffer.BlockCopy(buf.Data, 0, blob, BlobHeaderBytes, payloadBytes);
        return blob;
    }

    // ===== Send helpers (oscSend / oscSendBundle bodies share these) =====

    private static void SendOscMessage(string host, int port, string path, object[] oscArgs)
    {
        var msg = new Rug.Osc.OscMessage(path, oscArgs);
        SendOscPacket(host, port, msg);
    }

    private static void SendOscPacket(string host, int port, Rug.Osc.OscPacket packet)
    {
        // Rug.Osc's OscSender(IPAddress, int) takes an IP. Resolve host
        // charitably — IPAddress.Parse first, then DNS fallback. For
        // localhost-loopback test paths this hits the IPAddress.Parse
        // branch unconditionally.
        IPAddress addr;
        if (!IPAddress.TryParse(host, out addr!))
        {
            IPHostEntry entry;
            try
            {
                entry = Dns.GetHostEntry(host);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[osc] oscSend: could not resolve host '{host}': {ex.Message}");
            }
            // Phase 44 review WR-03: Dns.GetHostEntry can return an
            // IPHostEntry with an empty AddressList (e.g. an IPv6-only
            // host with IPv6 disabled, or a misconfigured DNS server
            // returning success with no A records). The previous code's
            // entry.AddressList[0] would throw IndexOutOfRangeException
            // which the surrounding catch then rewrapped misleadingly as
            // "could not resolve host '...': Index was outside the bounds
            // of the array". Detect the empty case OUTSIDE the catch and
            // surface a composer-readable error.
            if (entry.AddressList.Length == 0)
                throw new InvalidOperationException(
                    $"[osc] oscSend: hostname '{host}' resolved but returned no IP addresses");
            addr = entry.AddressList[0];
        }
        // Rug.Osc 1.2.5 quirk: the 2-arg ctor (IPAddress, int port) binds
        // the sender's LOCAL port to the same value as the remote port,
        // which collides with a receiver bound to the same port on
        // loopback. The 3-arg (IPAddress, int localPort=0, int remotePort)
        // ctor lets the OS pick an ephemeral local port — see Plan 38-06
        // Task 2 SUMMARY for the verified loopback contract.
        using var sender = new Rug.Osc.OscSender(addr, 0, port);
        sender.Connect();
        sender.Send(packet);
    }

    // ===== Listener lifecycle (RESEARCH §K lines 1093-1147) =====

    private static Value StartListener(int port, string path, FunctionOverload handler, FlowLang.Runtime.ExecutionContext context)
    {
        // Phase 44 review CR-01: snapshot the composer's strict bit AND
        // current call site at oscListen-call-time, BEFORE the Task.Run.
        // The intent (per the class XML doc at 350-358) is "treat
        // listener-bind failure as a [strict] event so composer can react"
        // — meaning "the composer's strict bit AT oscListen call time", not
        // "whatever the foreground thread happens to be doing seconds later
        // when an OSC packet arrives". The previous code read
        // context.CallerStrictMode from the background Task.Run body where
        // the value is unpredictable (per-dispatch snapshot owned by the
        // synchronous evaluator loop). Capture into immutable locals and
        // thread through every helper that runs on the listener thread.
        bool listenerStrict = context.CallerStrictMode;
        var listenerSite = context.CurrentCallSite;

        Rug.Osc.OscReceiver receiver;
        try
        {
            // Loopback bind — composer responsibility for non-loopback per
            // T-38-NET (CONTEXT line 244 "accept" disposition).
            receiver = new Rug.Osc.OscReceiver(port);
        }
        catch (Exception ex)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch. Treat
            // listener-bind failure as a [strict] handler exception event
            // so composer can react. Synchronous path — listenerStrict and
            // context.CallerStrictMode are still equal here (we have not
            // yet escaped to the background task).
            if (listenerStrict)
            {
                context.ErrorReporter.ReportError(
                    $"[strict] [osc] handler exception — bind failed on port {port}: {ex.Message} at {listenerSite}",
                    listenerSite);
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    $"osc-bind:{port}",
                    $"[osc] bind failed on port {port}: {ex.Message} — oscListen returned no handle");
            }
            // Return a sentinel "dead" handle — composer can still pass to
            // (oscStop) without crashing.
            return Value.OscHandle(new OscHandleData
            {
                Port = port,
                Path = path,
                Receiver = null,
                Cts = new CancellationTokenSource(),
                ListenerTask = Task.CompletedTask,
            });
        }

        var cts = new CancellationTokenSource();
        // Pitfall #5 per RESEARCH line 1445: Cts.Cancel() alone won't break
        // the blocked Receive() call. Register a callback that disposes the
        // receiver, which forces ObjectDisposedException in Receive() and
        // breaks the loop charitably. try/catch around Dispose for
        // idempotency.
        var receiverRef = receiver;
        cts.Token.Register(() => { try { receiverRef.Dispose(); } catch { } });

        var task = Task.Run(() =>
        {
            try { receiver.Connect(); }
            catch (Exception ex)
            {
                // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
                // CR-01: read the captured listenerStrict snapshot rather
                // than context.CallerStrictMode (which on this background
                // thread is whatever stale value the foreground last wrote).
                if (listenerStrict)
                {
                    context.ErrorReporter.ReportError(
                        $"[strict] [osc] handler exception — connect failed on port {port}: {ex.Message} at {listenerSite}",
                        listenerSite);
                }
                else
                {
                    RenderingDiagnostics.WarnOnce(
                        $"osc-bind:{port}",
                        $"[osc] connect failed on port {port}: {ex.Message}");
                }
                return;
            }
            while (!cts.IsCancellationRequested)
            {
                Rug.Osc.OscPacket packet;
                try
                {
                    packet = receiver.Receive();   // Blocking
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    // Charitable per Pitfall #12 "live session never dies mid-set"
                    // — log + continue. Non-dedup'd so flooding errors stay
                    // visible (this is the catastrophic path, not the normal
                    // sample-and-hold drop).
                    Console.Error.WriteLine($"[osc] receive error on port {port}: {ex.Message}");
                    continue;
                }
                DispatchPacket(packet, path, handler, context, 0, listenerStrict, listenerSite, cts.Token);
            }
        }, cts.Token);

        return Value.OscHandle(new OscHandleData
        {
            Port = port,
            Path = path,
            Receiver = receiver,
            Cts = cts,
            ListenerTask = task,
        });
    }

    private static void StopListener(OscHandleData handle)
    {
        try { handle.Cts.Cancel(); } catch { }
        // Receiver was disposed by the cts.Token.Register callback in
        // StartListener; second Dispose is idempotent but we wrap for safety.
        try { handle.Receiver?.Dispose(); } catch { }
        // Wait briefly for the task to drain; 1s cap per the plan's body.
        try { handle.ListenerTask.Wait(TimeSpan.FromSeconds(1)); }
        catch (AggregateException) { /* task may throw OCE; ignore */ }
        catch (Exception) { /* idempotent best-effort */ }
    }

    // ===== Dispatch (RESEARCH §K lines 1119-1147) =====

    /// <summary>
    /// Recursive packet dispatch. OscBundle bodies recurse to depth ≤ 8 per
    /// D-38-15 (DoS guard, mirrors Phase 36 T-36-17 + Phase 39 D-39-19).
    /// OscMessage bodies pass through the per-path rate-limit gate per
    /// D-38-14 + RESEARCH §M before invoking the Flow handler lambda.
    ///
    /// <para>Phase 44 review CR-01: <paramref name="listenerStrict"/> and
    /// <paramref name="listenerSite"/> are the captured strict-bit /
    /// call-site snapshot taken at <c>oscListen</c> call time. They MUST be
    /// threaded through every helper that runs on the background listener
    /// thread instead of reading <c>context.CallerStrictMode</c> /
    /// <c>context.CurrentCallSite</c> directly — those are per-dispatch
    /// snapshots owned by the synchronous evaluator loop and read stale on
    /// the listener thread.</para>
    /// </summary>
    private static void DispatchPacket(
        Rug.Osc.OscPacket packet,
        string targetPath,
        FunctionOverload handler,
        FlowLang.Runtime.ExecutionContext context,
        int depth,
        bool listenerStrict,
        SourceLocation listenerSite,
        CancellationToken cancel)
    {
        // Audit §5.10 — a stopped handle must not invoke handlers. If the
        // listener was already cancelled (oscStop), drop this packet entirely;
        // this also short-circuits a future-timetag continuation that fires
        // after the stop.
        if (cancel.IsCancellationRequested) return;

        if (depth > BundleDepthCap)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            // CR-01: use captured listenerStrict + listenerSite.
            if (listenerStrict)
            {
                context.ErrorReporter.ReportError(
                    $"[strict] [osc] bundle nesting depth > 8 at {targetPath} (depth={depth}) at {listenerSite}",
                    listenerSite);
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    $"osc-bundle-depth:{targetPath}",
                    $"[osc] bundle nesting depth exceeds 8 at {targetPath} — collapsing to flat dispatch");
            }
            return;
        }

        if (packet is Rug.Osc.OscBundle bundle)
        {
            // Honor timetag: future timetag → Task.Delay; immediate (value 1)
            // or past → dispatch synchronously.
            if (bundle.Timestamp.Value > 1UL)
            {
                DateTime when;
                try { when = bundle.Timestamp.ToDataTime(); }
                catch { when = DateTime.UtcNow; }
                var delay = when - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    // Audit §5.10 — thread the listener's CancellationToken into
                    // BOTH the Delay (so oscStop cancels the pending fire) AND
                    // the continuation (re-check on the off chance the token
                    // cancels between the delay completing and the continuation
                    // running). A cancelled Delay completes faulted/cancelled, so
                    // guard the continuation against ALL completion states and
                    // re-check the token before dispatching. Stop semantics win:
                    // a handle that was stopped must NOT invoke handlers, even
                    // for an already-received future-timetag bundle.
                    Task.Delay(delay, cancel).ContinueWith(t =>
                    {
                        if (t.IsCanceled || cancel.IsCancellationRequested) return;
                        DispatchBundleContents(bundle, targetPath, handler, context, depth + 1, listenerStrict, listenerSite, cancel);
                    }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                    return;
                }
            }
            DispatchBundleContents(bundle, targetPath, handler, context, depth + 1, listenerStrict, listenerSite, cancel);
            return;
        }

        if (packet is Rug.Osc.OscMessage msg && msg.Address == targetPath)
        {
            InvokeHandlerWithRateLimit(targetPath, handler, msg, context, listenerStrict, listenerSite);
        }
    }

    private static void DispatchBundleContents(
        Rug.Osc.OscBundle bundle,
        string targetPath,
        FunctionOverload handler,
        FlowLang.Runtime.ExecutionContext context,
        int depth,
        bool listenerStrict,
        SourceLocation listenerSite,
        CancellationToken cancel)
    {
        for (int i = 0; i < bundle.Count; i++)
        {
            DispatchPacket(bundle[i], targetPath, handler, context, depth, listenerStrict, listenerSite, cancel);
        }
    }

    private static void InvokeHandlerWithRateLimit(
        string path,
        FunctionOverload handler,
        Rug.Osc.OscMessage msg,
        FlowLang.Runtime.ExecutionContext context,
        bool listenerStrict,
        SourceLocation listenerSite)
    {
        var nowMs = Environment.TickCount64;
        var lastMs = _lastFireTimeMs.GetOrAdd(path, 0L);
        if (lastMs > 0 && nowMs - lastMs < RateLimitWindowMs)
            return;  // Drop-newest, sample-and-hold per D-38-14
        _lastFireTimeMs[path] = nowMs;

        // Translate the OscMessage's args back to Flow Values (inverse of
        // InferOscArgs). Rug.Osc enumerates via IEnumerable<object>.
        var rugArgs = new List<object?>();
        foreach (var a in msg) rugArgs.Add(a);
        var flowArgs = new List<Value>(rugArgs.Count);
        for (int i = 0; i < rugArgs.Count; i++)
            flowArgs.Add(RugOscArgToFlowValue(rugArgs[i]));

        try
        {
            InvokeHandler(handler, flowArgs, context);
        }
        catch (Exception ex)
        {
            // Charitable per Pitfall #12 — handler exceptions never kill
            // the listener loop; surface to stderr and continue.
            Console.Error.WriteLine($"[osc] handler error at {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Test-only seam: invoke the handler lambda. Default path uses
    /// <c>context.Invoker</c> per the
    /// <see cref="Collections.Each"/> precedent (StandardLibrary/Collections.cs:325).
    /// Tests that exercise the dispatch loop without a full FlowEngine can
    /// override via <see cref="HandlerInvokeOverride"/>.
    /// </summary>
    private static void InvokeHandler(FunctionOverload handler, IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        if (HandlerInvokeOverride != null)
        {
            HandlerInvokeOverride(handler, args);
            return;
        }
        if (handler.IsInternal)
        {
            // Internal handlers touch no shared interpreter state — safe to run
            // inline on the dispatch thread (keeps test stubs synchronous).
            handler.Implementation!(args);
        }
        else
        {
            // Audit §5.3: a user proc mutates the shared, non-thread-safe
            // ExecutionContext (call stack, _returnValue, strict bit). NEVER run
            // it on the listener/dispatch thread — enqueue for the foreground
            // evaluator thread to drain via DrainPendingHandlers (osc* call
            // sites + (oscPump)). Serialized-not-parallel with the rest of the
            // script; see the _pendingHandlers field doc for the tradeoff.
            _pendingHandlers.Enqueue(new PendingHandlerInvocation(handler, args, context));
        }
    }

    /// <summary>
    /// Test-only seam: when non-null, replaces the default
    /// <c>context.Invoker</c>-based lambda invocation with a callback.
    /// Used by the Phase 38 OSC tests to assert handler invocation
    /// without spinning up a full FlowEngine. Always restore to null in
    /// test Dispose/Reset code.
    /// </summary>
    public static Action<FunctionOverload, IReadOnlyList<Value>>? HandlerInvokeOverride { get; set; }

    /// <summary>
    /// Inverse of <see cref="InferOscArgs"/> — translate a Rug.Osc-boxed
    /// arg back to a Flow Value. Used when a received OscMessage's args
    /// are passed to the composer's handler lambda.
    /// </summary>
    public static Value RugOscArgToFlowValue(object? arg) => arg switch
    {
        null => Value.Void(),
        int i => Value.Int(i),
        long l => Value.Long(l),
        float f => Value.Float(f),
        double d => Value.Double(d),
        string s => Value.String(s),
        bool b => Value.Bool(b),
        byte[] bytes => BlobToBuffer(bytes),
        _ => throw new InvalidOperationException(
            $"[osc] received unsupported Rug.Osc arg type: {arg.GetType().Name}")
    };

    /// <summary>
    /// Audit §5.13 — reconstruct an <see cref="AudioBuffer"/> from an OSC <c>,b</c>
    /// blob. A blob written by <see cref="AudioBufferToBlob"/> carries a 12-byte
    /// header (magic + channels + sampleRate) so channel count + rate round-trip
    /// exactly. A blob WITHOUT the magic (a foreign OSC blob from another app)
    /// still decodes — charitably as mono/44100 with a one-shot advisory — so
    /// interop is preserved per the charitable-interpretation philosophy.
    /// </summary>
    private static Value BlobToBuffer(byte[] bytes)
    {
        // Header present? Require the full 12-byte header AND the magic match.
        bool hasHeader = bytes.Length >= BlobHeaderBytes
            && bytes[0] == BlobMagic[0] && bytes[1] == BlobMagic[1]
            && bytes[2] == BlobMagic[2] && bytes[3] == BlobMagic[3];

        if (hasHeader)
        {
            int channels = BitConverter.ToInt32(bytes, 4);
            int sampleRate = BitConverter.ToInt32(bytes, 8);
            int payloadBytes = bytes.Length - BlobHeaderBytes;
            int sampleCount = payloadBytes / 4;

            // Defend charitably against a malformed/hostile header rather than
            // throwing on the listener thread: clamp degenerate channels/rate
            // and only honor a channel count the payload actually divides into.
            if (channels < 1 || (sampleCount % channels) != 0)
            {
                RenderingDiagnostics.WarnOnce(
                    "osc-blob-bad-channels",
                    $"[osc] blob header declared channels={channels} incompatible with {sampleCount} samples — decoding as mono");
                channels = 1;
            }
            if (sampleRate < 1)
            {
                RenderingDiagnostics.WarnOnce(
                    "osc-blob-bad-rate",
                    $"[osc] blob header declared sampleRate={sampleRate} — decoding at 44100 Hz");
                sampleRate = 44100;
            }

            int frames = sampleCount / channels;
            var buf = new AudioBuffer(frames, channels, sampleRate);
            System.Buffer.BlockCopy(bytes, BlobHeaderBytes, buf.Data, 0, sampleCount * 4);
            return Value.Buffer(buf);
        }

        // Headerless (foreign) blob — charitable mono/44100 fallback + advisory.
        RenderingDiagnostics.WarnOnce(
            "osc-blob-no-header",
            "[osc] received a Buffer blob without Flow channel/rate metadata — decoding as mono at 44100 Hz");
        int monoFrames = bytes.Length / 4;
        var monoBuf = new AudioBuffer(monoFrames, 1, 44100);
        System.Buffer.BlockCopy(bytes, 0, monoBuf.Data, 0, monoFrames * 4);
        return Value.Buffer(monoBuf);
    }

    /// <summary>
    /// Test-only seam: directly exercise the dispatch path against a
    /// synthesized packet without binding a UDP socket. Used by
    /// OscRateLimitTests + OscBundleTests + OscBundleDepthCapTests so
    /// CI doesn't depend on real socket lifetimes.
    /// </summary>
    public static void DispatchPacketForTesting(
        Rug.Osc.OscPacket packet,
        string targetPath,
        FunctionOverload handler,
        FlowLang.Runtime.ExecutionContext context,
        CancellationToken cancel = default)
    {
        // Phase 44 review CR-01: tests still invoke dispatch on the caller
        // thread, so the synchronous context.CallerStrictMode /
        // context.CurrentCallSite read here is correct (no background thread
        // to race the foreground). Production listeners route through
        // StartListener which captures these into immutable locals before
        // the Task.Run boundary.
        //
        // Audit §5.10: the optional cancel token lets a test feed a cancelled
        // listener token so a post-oscStop future-timetag bundle is dropped.
        DispatchPacket(packet, targetPath, handler, context, 0,
            context.CallerStrictMode, context.CurrentCallSite, cancel);
    }
}
