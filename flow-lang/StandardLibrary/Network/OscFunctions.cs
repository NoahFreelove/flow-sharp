using System;
using System.Collections.Generic;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Network;

/// <summary>
/// Phase 38 Plan 38-06 OSC-01 + OSC-02 — registration entry point for the
/// <c>@osc</c> stdlib module surface. Task 1 ships the type-tag inference
/// helper + Buffer-to-blob converter (no Rug.Osc dispatch yet);
/// Task 2 fills in the 5 surface builtins
/// (<c>oscSend</c> / <c>oscListen</c> / <c>oscStop</c> / <c>oscBundle</c> /
/// <c>oscSendBundle</c>) and wires <c>__enableOscModule</c> alongside.
///
/// <para>
/// All 5 surface builtins gate on
/// <see cref="ExecutionContext.OscEnabled"/>. Mirrors the Phase 33
/// <see cref="FlowLang.StandardLibrary.Audio.Sfz.SfzBuiltins"/> +
/// Phase 39 <see cref="FlowLang.StandardLibrary.Notation.NotationIoBuiltins"/>
/// pattern (registration always; runtime gate enforces module activation
/// per CONTEXT D-38-13 inheritance from D-10).
/// </para>
/// </summary>
public static class OscFunctions
{
    /// <summary>
    /// Wire the 6 OSC builtins (5 surface + 1 marker) into the registry.
    /// Idempotent. Called once per <see cref="FlowLang.Core.FlowEngine"/>
    /// instance at construction time. Task 1 ships an empty stub —
    /// Task 2 will register the 5 surface builtins.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // Task 2 will populate this method with __enableOscModule + 5 builtins.
        // Task 1 ships only the InferOscArgs helper for the TDD RED gate on
        // OscTypeTagInferenceTests.
    }

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

    /// <summary>
    /// Phase 38 Plan 38-06 — flatten an <see cref="AudioBuffer"/> to a
    /// <c>byte[]</c> blob suitable for OSC <c>,b</c> (blob) transport.
    /// Encodes each 32-bit float sample in little-endian IEEE 754 order
    /// (4 bytes per sample × Frames × Channels). The composer-side receiver
    /// is responsible for inverse-decoding.
    /// </summary>
    public static byte[] AudioBufferToBlob(AudioBuffer buf)
    {
        if (buf is null) throw new ArgumentNullException(nameof(buf));
        var blob = new byte[buf.Data.Length * 4];
        Buffer.BlockCopy(buf.Data, 0, blob, 0, blob.Length);
        return blob;
    }
}
