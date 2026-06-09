#if !FLOW_WEB
using System;
using System.Runtime.InteropServices;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;

namespace FlowLang.StandardLibrary.Midi;

/// <summary>
/// Phase 40 JACK-01 (D-40-05 best-effort) — registration entry point for the
/// <c>@jack</c> stdlib module. Ships a single builtin:
/// <list type="bullet">
///   <item><c>jackSync() → JackHandle</c> — query JACK transport position/tempo
///         and drive the LIVE-SYNC tempo sink
///         (<see cref="MusicalContext.SetLiveTempo"/>) + bar/beat when a JACK
///         server is present. WR-01/LINK-02: the live tempo NEVER touches
///         <see cref="MusicalContext.Tempo"/>, so it can never leak into the
///         deterministic offline render.</item>
/// </list>
///
/// <para><b>JackSharp verdict (Open Q3, Plan 40-03 Task 1):</b> JackSharp 0.4.0
/// loads under net10 via the net4x compat shim, BUT its public API exposes NO
/// transport surface (no <c>jack_transport_query</c> / tempo / BBT — only audio /
/// MIDI ports + connection control). It cannot satisfy JACK-01. Per the
/// best-effort fallback (D-40-05) we hand-roll a minimal
/// <c>[DllImport("jack")]</c> <c>jack_transport_query</c> here — transport state +
/// BPM only — instead of taking a dead JackSharp dependency.</para>
///
/// <para><b>Charitable absent-server (JACK-01 / T-40-04):</b> when no JACK server
/// is running (or <c>libjack.so.0</c> is absent), <c>(jackSync)</c> is a no-op:
/// it WarnOnce's <c>[jack] no JACK server — (jackSync) is a no-op</c>, leaves
/// <see cref="MusicalContext.Tempo"/> untouched, and returns a dead handle. It
/// NEVER throws — non-JACK workflows are completely unaffected by JACK absence.</para>
///
/// <para><b>Tempo validation (T-40-01):</b> a transport tempo is written to the
/// live-sync sink (<see cref="MusicalContext.SetLiveTempo"/>) only when the
/// BBT-valid bit is set AND the derived BPM passes
/// <see cref="MusicalContext.IsValidTempo"/>; out-of-range tempo is rejected, not
/// written.</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on Web (T-40-03), like every other
/// MIDI/JACK file. JACK is Linux-only native interop that can never run in a
/// browser sandbox.</para>
/// </summary>
public static class JackFunctions
{
    // ===== libjack P/Invoke (hand-rolled minimal transport surface) =====
    //
    // Only the four entry points needed for a one-shot transport query are bound.
    // The library name "jack" resolves to libjack.so.0 on Linux via the standard
    // .NET native-lib search (Pitfall 2 posture: the probe catches
    // DllNotFoundException and degrades charitably).
    private const string JackLib = "jack";

    // jack_client_open(name, options, &status, ...) — varargs in C; we never pass
    // the optional server-name arg so a fixed 3-arg signature is ABI-correct.
    [DllImport(JackLib, EntryPoint = "jack_client_open", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr jack_client_open(
        [MarshalAs(UnmanagedType.LPStr)] string clientName,
        int options,
        out int status);

    [DllImport(JackLib, EntryPoint = "jack_client_close", CallingConvention = CallingConvention.Cdecl)]
    private static extern int jack_client_close(IntPtr client);

    // jack_transport_query(client, &pos) → jack_transport_state_t (enum/int).
    [DllImport(JackLib, EntryPoint = "jack_transport_query", CallingConvention = CallingConvention.Cdecl)]
    private static extern int jack_transport_query(IntPtr client, ref JackPositionT pos);

    /// <summary>
    /// C ABI mirror of <c>jack_position_t</c> (jack/transport.h). Sequential
    /// layout, field-for-field. Only the BBT block (<c>valid</c> .. <c>bpm</c>) is
    /// read; the trailing audio/video/padding fields are present for size fidelity
    /// so the struct the native side fills matches our managed buffer. The
    /// <c>valid</c> field's <see cref="JackPositionBbt"/> bit (0x10) gates whether
    /// <c>beats_per_minute</c> / bar / beat carry meaningful values.
    ///
    /// <para><b>CR-01 ABI correctness (memory safety):</b> <c>jack_transport_query</c>
    /// writes a full native <c>jack_position_t</c> into the address of the pinned
    /// managed instance. The managed mirror MUST be ≥ the native struct or the
    /// native side overruns the managed buffer (heap/stack corruption). Two fixes
    /// vs. the original under-sized mirror:
    /// <list type="bullet">
    ///   <item><c>tick_double</c> is a native <c>double</c> (8 bytes), NOT an
    ///   <c>int</c> — the original declared it as <c>int</c>, costing 4 bytes.</item>
    ///   <item>The reserved tail is <c>int32_t padding[7]</c> (28 bytes), NOT 5
    ///   ints (20 bytes) — the original was 8 bytes short there.</item>
    /// </list>
    /// On top of matching the canonical layout exactly, an extra
    /// <see cref="reserved_overalloc"/> 64-byte buffer is appended so a FUTURE
    /// JACK ABI bump that grows the struct still cannot re-introduce the overrun.
    /// <c>JackStructSizeTests</c> asserts the managed marshalled size is ≥ the
    /// canonical native size.</para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct JackPositionT
    {
        public ulong unique_1;          // jack_unique_t
        public ulong usecs;             // jack_time_t
        public uint frame_rate;         // jack_nframes_t
        public uint frame;              // jack_nframes_t
        public int valid;               // jack_position_bits_t (enum → int)
        public int bar;                 // BBT
        public int beat;                // BBT
        public int tick;                // BBT
        public double bar_start_tick;   // BBT
        public float beats_per_bar;     // BBT
        public float beat_type;         // BBT
        public double ticks_per_beat;   // BBT
        public double beats_per_minute; // BBT — the field JACK-01 reads
        public double frame_time;       // JackPositionTimecode
        public double next_time;        // JackPositionTimecode
        public uint bbt_offset;         // JackBBTFrameOffset
        public float audio_frames_per_video_frame; // JackAudioVideoRatio
        public uint video_offset;       // JackVideoFrameOffset
        public double tick_double;      // JackTickDouble (later JACK) — FIX: double, not int
        // FIX: int32_t padding[7] (28 bytes), not 5 ints — match the canonical tail.
        public int padding0;
        public int padding1;
        public int padding2;
        public int padding3;
        public int padding4;
        public int padding5;
        public int padding6;
        public ulong unique_2;          // jack_unique_t
        // Future-proofing: 64 extra reserved bytes (8 × ulong) so a later JACK ABI
        // that appends fields cannot overrun the managed buffer. Never read.
        public ulong reserved_overalloc0;
        public ulong reserved_overalloc1;
        public ulong reserved_overalloc2;
        public ulong reserved_overalloc3;
        public ulong reserved_overalloc4;
        public ulong reserved_overalloc5;
        public ulong reserved_overalloc6;
        public ulong reserved_overalloc7;
    }

    /// <summary>
    /// CR-01 test seam: the marshalled size (bytes) of the managed
    /// <see cref="JackPositionT"/> mirror. Exposed so <c>JackStructSizeTests</c>
    /// can assert it is ≥ the canonical native <c>jack_position_t</c> size without
    /// reflecting a private nested struct.
    /// </summary>
    public static int JackPositionTMarshalSize => Marshal.SizeOf<JackPositionT>();

    /// <summary><c>JackPositionBBT</c> bit in <c>jack_position_bits_t</c> — set when
    /// bar/beat/tick + beats_per_minute are valid.</summary>
    private const int JackPositionBbt = 0x10;

    /// <summary><c>JackNullOption</c> — no special client-open flags.</summary>
    private const int JackNullOption = 0;

    /// <summary>
    /// Test-only seam: when set, <see cref="QueryTransport"/> returns this snapshot
    /// instead of touching real libjack — so JackTransportTests can exercise both
    /// the present-server (drive-tempo) and absent-server (no-op) branches with NO
    /// real JACK server. Always restore to null in test teardown.
    /// </summary>
    public static Func<(bool present, double? bpm, int? bar, int? beat)>? TransportQueryOverride { get; set; }

    /// <summary>
    /// Wire the @jack builtins into the registry. Called once per FlowEngine at
    /// construction (inside the <c>#if !FLOW_WEB</c> guard at the OSC/@midi register
    /// site), beside <see cref="MidiFunctions.Register"/>. Gates on
    /// <see cref="ExecutionContext.JackEnabled"/>.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // ----- Marker: __enableJackModule -----
        var sigMarker = new FunctionSignature("__enableJackModule", System.Array.Empty<FlowType>());
        registry.Register("__enableJackModule", sigMarker, _ =>
        {
            context.JackEnabled = true;
            return Value.Void();
        });

        // ----- jackSync() -> JackHandle -----
        var sigSync = new FunctionSignature("jackSync", System.Array.Empty<FlowType>());
        registry.Register("jackSync", sigSync, _ =>
        {
            RequireModuleActivated(context, "jackSync");
            var (present, bpm, bar, beat) = QueryTransport();

            if (!present)
            {
                RenderingDiagnostics.WarnOnce(
                    "jack-absent",
                    "[jack] no JACK server — (jackSync) is a no-op (transport tempo unchanged)");
                return Value.JackHandle(new JackHandleData
                {
                    ServerPresent = false,
                    Tempo = null,
                    Bar = null,
                    Beat = null,
                });
            }

            // Server present — drive the LIVE-SYNC tempo sink from the transport
            // BPM, but ONLY when it passes IsValidTempo (T-40-01).
            //
            // WR-01 (LINK-02): write the live-tempo sink, NOT MusicalContext.Tempo.
            // GetMusicalContext returns the cached resolved snapshot that sections
            // capture for offline render; mutating its .Tempo would leak the live
            // transport tempo into writeWav/writeMidi. SetLiveTempo is Interlocked
            // (WR-02) and consumed only by the realtime clock master, never by the
            // deterministic render path. The handle still records the applied tempo
            // so the composer can read it back.
            double? appliedTempo = null;
            if (bpm.HasValue && MusicalContext.IsValidTempo(bpm.Value))
            {
                var mctx = context.GetMusicalContext();
                mctx.SetLiveTempo(bpm.Value);
                appliedTempo = bpm.Value;
            }
            else if (bpm.HasValue)
            {
                RenderingDiagnostics.WarnOnce(
                    "jack-bad-tempo",
                    $"[jack] transport BPM {bpm.Value:0.##} is out of range — not applied to Tempo");
            }

            return Value.JackHandle(new JackHandleData
            {
                ServerPresent = true,
                Tempo = appliedTempo,
                Bar = bar,
                Beat = beat,
            });
        });
    }

    /// <summary>
    /// One-shot JACK transport query. Returns whether a JACK server answered, and
    /// (if so + BBT-valid) the transport BPM + bar/beat. NEVER throws: any native
    /// failure (libjack absent, no server, ABI surprise) is caught and reported as
    /// "no server present" so the caller degrades charitably (JACK-01 / T-40-04).
    /// </summary>
    private static (bool present, double? bpm, int? bar, int? beat) QueryTransport()
    {
        if (TransportQueryOverride != null)
            return TransportQueryOverride();

        IntPtr client = IntPtr.Zero;
        try
        {
            // A short-lived passive client; JackNullOption means "fail if no
            // server is running" (no auto-start). status is filled but we only
            // need the client-handle nullity to know if a server answered.
            client = jack_client_open("flow-jacksync", JackNullOption, out _);
            if (client == IntPtr.Zero)
                return (false, null, null, null);

            var pos = default(JackPositionT);
            // The return value is the transport state; the BBT data we want is in
            // `pos`. We don't gate on rolling/stopped state — a stopped transport
            // still carries a valid tempo for tempo-following.
            _ = jack_transport_query(client, ref pos);

            if ((pos.valid & JackPositionBbt) == 0)
            {
                // Server present but no BBT/tempo info — present, but no tempo.
                return (true, null, null, null);
            }

            double bpm = pos.beats_per_minute;
            int? bar = pos.bar > 0 ? pos.bar : (int?)null;
            int? beat = pos.beat > 0 ? pos.beat : (int?)null;
            return (true, bpm > 0 ? bpm : (double?)null, bar, beat);
        }
        catch (DllNotFoundException)
        {
            // libjack.so.0 absent — charitable no-op (Pitfall 2 posture).
            return (false, null, null, null);
        }
        catch (Exception)
        {
            // Any other native surprise (ABI / EntryPointNotFound / etc.) — never
            // let a live session die on a JACK query (T-40-04).
            return (false, null, null, null);
        }
        finally
        {
            if (client != IntPtr.Zero)
            {
                try { jack_client_close(client); } catch { /* best-effort */ }
            }
        }
    }

    private static void RequireModuleActivated(FlowLang.Runtime.ExecutionContext context, string builtinName)
    {
        if (!context.JackEnabled)
            throw new System.InvalidOperationException($"{builtinName} requires `use \"@jack\"`");
    }
}
#endif
