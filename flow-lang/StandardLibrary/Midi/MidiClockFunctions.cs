#if !FLOW_WEB
using FlowLang.Audio;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Midi;

/// <summary>
/// Phase 40 CLOCK-01/02 (D-40-03) — registration entry point for the MIDI clock
/// surface of the <c>@midi</c> stdlib module. Ships:
/// <list type="bullet">
///   <item><c>clockMaster(MidiDevice device) → ClockHandle</c> — emit 24 PPQN +
///         start/stop tied to the active <see cref="MusicalContext.Tempo"/>
///         (CLOCK-01).</item>
///   <item><c>clockSlave(String port) → ClockHandle</c> — receive 24 PPQN, derive
///         BPM with an 8-pulse settle, drive <see cref="MusicalContext.Tempo"/>
///         (CLOCK-02).</item>
///   <item><c>clockStop(ClockHandle handle) → Void</c> — stop the master thread /
///         slave listener cleanly (≤ 1s, never hangs).</item>
/// </list>
///
/// <para>Models <see cref="FlowLang.StandardLibrary.Network.OscFunctions"/>'s
/// <c>oscListen</c>/<c>oscStop</c> handle pair: <c>clockMaster</c>/<c>clockSlave</c>
/// return reference-identity <c>ClockHandle</c> Values; <c>clockStop</c> takes a
/// handle and runs the <c>StopListener</c>-style teardown (Cancel → join/dispose →
/// Wait(1s) charitably). All gate on <see cref="ExecutionContext.MidiEnabled"/>.</para>
///
/// <para><b>Charitable (T-40-04):</b> a clockMaster on a dead device or a
/// clockSlave on an absent port returns a dead handle + WarnOnce, never throwing
/// — a live session never dies.</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on Web (T-40-03).</para>
/// </summary>
public static class MidiClockFunctions
{
    /// <summary>
    /// Wire the clock builtins into the registry. Called once per FlowEngine at
    /// construction (inside the <c>#if !FLOW_WEB</c> guard at the OSC/@midi
    /// register site), beside <see cref="MidiFunctions.Register"/>.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // ----- clockMaster(MidiDevice device) -> ClockHandle -----
        var sigMaster = new FunctionSignature("clockMaster",
            new FlowType[] { MidiDeviceType.Instance },
            ParameterNames: new[] { "device" });
        registry.Register("clockMaster", sigMaster, args =>
        {
            RequireModuleActivated(context, "clockMaster");
            var dev = args[0].As<MidiDeviceData>();
            // Snapshot the active musical context (tempo + timesig) at start. The
            // master re-reads its .Tempo at each bar boundary (CLOCK-01); pass the
            // resolved snapshot so the active tempo seeds the first bar.
            var mctx = context.GetMusicalContext();
            var clock = MidiClock.StartMaster(mctx, dev.Handle);
            if (dev.Handle == null)
            {
                RenderingDiagnostics.WarnOnce(
                    "clock-master-dead",
                    "[clock] clockMaster — device handle is dead (absent port / librtmidi.so absent); clock runs but bytes go nowhere");
            }
            return Value.ClockHandle(new ClockHandleData
            {
                Mode = ClockMode.Master,
                Clock = clock,
            });
        });

        // ----- clockSlave(String port) -> ClockHandle -----
        var sigSlave = new FunctionSignature("clockSlave",
            new FlowType[] { StringType.Instance },
            ParameterNames: new[] { "port" });
        registry.Register("clockSlave", sigSlave, args =>
        {
            RequireModuleActivated(context, "clockSlave");
            string port = args[0].As<string>();
            // WR-01 (LINK-02): the slave drives the LIVE-SYNC tempo sink
            // (MusicalContext.SetLiveTempo), NOT MusicalContext.Tempo. We still pass
            // the resolved snapshot so the slave + a co-running master share one
            // instance's live-tempo channel; SetLiveTempo is Interlocked (WR-02) and
            // is invisible to SongRenderer/writeWav/writeMidi, so a sync-driven tempo
            // can never perturb the deterministic offline render.
            var mctx = context.GetMusicalContext();
            var clock = MidiClock.StartSlave(mctx, port);
            return Value.ClockHandle(new ClockHandleData
            {
                Mode = ClockMode.Slave,
                Clock = clock,
            });
        });

        // ----- clockStop(ClockHandle handle) -> Void -----
        var sigStop = new FunctionSignature("clockStop",
            new FlowType[] { ClockHandleType.Instance },
            ParameterNames: new[] { "handle" });
        registry.Register("clockStop", sigStop, args =>
        {
            RequireModuleActivated(context, "clockStop");
            var handle = args[0].As<ClockHandleData>();
            try { handle.Clock.Stop(); } catch { /* idempotent best-effort */ }
            return Value.Void();
        });
    }

    private static void RequireModuleActivated(FlowLang.Runtime.ExecutionContext context, string builtinName)
    {
        if (!context.MidiEnabled)
            throw new System.InvalidOperationException($"{builtinName} requires `use \"@midi\"`");
    }
}
#endif
