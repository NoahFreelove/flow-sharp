using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Sfz;

/// <summary>
/// Phase 33 Plan 33-05 — registration entry point for the SFZ surface.
/// Mirrors <see cref="FlowLang.StandardLibrary.Audio.Tuning.ScalaBuiltins"/>
/// (Phase 32 Plan 32-04) shape, with three additions per CONTEXT D-09 / D-10:
///
/// <list type="number">
///   <item><description><c>__enableSfzModule(Dict&lt;Symbol, String&gt;)</c> — internal marker
///   called once at <c>use "@sfz"</c> import time (last expression in
///   <c>flow-lang/sfz.flow</c>). Flips <see cref="ExecutionContext.SfzEnabled"/>
///   to <c>true</c> and copies the dict entries into
///   <see cref="ExecutionContext.SfzInstruments"/>. Returns <see cref="Value.Void"/>.</description></item>
///
///   <item><description><c>loadSfz(Symbol)</c> — looks up the symbol in
///   <see cref="ExecutionContext.SfzInstruments"/>, joins the relative path with
///   <see cref="ExecutionContext.ResolvedSfzRoot"/> (cached from
///   <c>FlowConfig.Active.SfzRoot</c> per Pitfall 2), parses via
///   <see cref="SfzParser.Parse"/>, returns <see cref="Value.Sfz"/>.</description></item>
///
///   <item><description><c>loadSfz(String)</c> — bypasses the dict; parses the literal
///   path directly (composer-side absolute or relative path).</description></item>
/// </list>
///
/// <para><b>SfzEnabled gating (D-10).</b> Both <c>loadSfz</c> overloads are
/// registered unconditionally at <see cref="FlowLang.Core.FlowEngine"/> startup.
/// On each call, they check <see cref="ExecutionContext.SfzEnabled"/>; if false,
/// they throw <see cref="InvalidOperationException"/> with a message containing
/// <c>use "@sfz"</c> so the SPEC-1 acceptance criterion is testable through
/// the FlowEngineRunner (the lexer / parser / overload resolver paths are
/// unchanged — only the runtime call gate matters).</para>
///
/// <para><b>sfz_root caching (Pitfall 2).</b> <see cref="FlowConfig.Active"/> is
/// mutable singleton state — test isolation pollutes it. To prevent
/// test-order-dependent flakes and to make a single render deterministic,
/// the first <c>loadSfz(Symbol)</c> call within a given
/// <see cref="ExecutionContext"/> reads <c>FlowConfig.Active.SfzRoot</c> ONCE
/// and caches the value on
/// <see cref="ExecutionContext.ResolvedSfzRoot"/>. Subsequent calls in the same
/// context use the cached value — script-time config edits do not affect an
/// in-flight render.</para>
///
/// <para><b>TBD-placeholder detection.</b> Per the Plan 33-01 VSCO-CE 1.1.0
/// audit, 4 of the 19 GM symbols (<c>#choir</c>, <c>#guitar</c>,
/// <c>#harpsichord</c>, <c>#celeste</c>) point at a known-missing
/// <c>_TBD_*</c> placeholder filename. <see cref="LoadSfzSymbol"/> recognizes
/// the <c>_TBD_</c> prefix and emits a specifically-worded "not bundled with
/// VSCO Community Edition" error pointing the composer at the absolute-path
/// overload, rather than the generic FileNotFoundException the underlying
/// <see cref="File.ReadAllText"/> would throw.</para>
///
/// <para><b>Advisory dedup.</b> The missing <c>sfz_root</c> error fires
/// <see cref="RenderingDiagnostics.WarnOnce"/> with a process-global sentinel
/// <c>sfz:config:sfz_root_missing</c> BEFORE throwing — composers see the
/// guidance once on stderr regardless of how many times the script attempts
/// <c>loadSfz</c>.</para>
/// </summary>
public static class SfzBuiltins
{
    /// <summary>
    /// Prefix used by <c>flow-lang/sfz.flow</c> for the 4 GM symbols not
    /// bundled with VSCO Community Edition (<c>#choir</c>, <c>#guitar</c>,
    /// <c>#harpsichord</c>, <c>#celeste</c>). Detected at lookup time so the
    /// resulting error message references VSCO-CE and the absolute-path
    /// overload — not the raw FileNotFoundException.
    /// </summary>
    private const string TbdPathPrefix = "_TBD_";

    /// <summary>
    /// Wire <c>__enableSfzModule(Dict)</c>, <c>loadSfz(Symbol)</c>, and
    /// <c>loadSfz(String)</c> into the internal function registry. Called
    /// from <see cref="FlowLang.Core.FlowEngine"/>'s startup path alongside
    /// the existing <c>ScalaBuiltins.Register(registry)</c> call.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // __enableSfzModule(Dict) — internal marker called from sfz.flow.
        // Uses DictType(VoidType, VoidType) wildcard like the existing
        // (set)/(get)/(remove)/etc. registrations so the actual concrete type
        // Dict<Symbol, String> matches via VoidType-wildcard compatibility.
        var enableSig = new FunctionSignature("__enableSfzModule",
            [new DictType(VoidType.Instance, VoidType.Instance)]);
        registry.Register("__enableSfzModule", enableSig,
            args => EnableSfzModule(args, context));

        // loadSfz(Symbol) — Phase 26.1 SYM-01 strict separation from String
        // ensures the overload resolver picks the right body (Pitfall 12).
        var sigSym = new FunctionSignature("loadSfz", [SymbolType.Instance]);
        registry.Register("loadSfz", sigSym,
            args => LoadSfzSymbol(args, context));

        // loadSfz(String) — bypass-the-dict literal path.
        var sigStr = new FunctionSignature("loadSfz", [StringType.Instance]);
        registry.Register("loadSfz", sigStr,
            args => LoadSfzString(args, context));
    }

    /// <summary>
    /// Marker builtin called once at <c>use "@sfz"</c> import time. Iterates
    /// the supplied Dict entries (expected shape: Symbol → String) and copies
    /// each pair into <see cref="ExecutionContext.SfzInstruments"/>, then
    /// flips <see cref="ExecutionContext.SfzEnabled"/> to <c>true</c>.
    /// Returns <see cref="Value.Void"/>.
    ///
    /// Idempotent — re-imports just re-populate the same dict and re-flip the
    /// already-true gate; Phase 26.1 SymbolInternTable guarantees the Symbol
    /// Value keys remain reference-equal across the copy.
    /// </summary>
    private static Value EnableSfzModule(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        var dict = args[0].As<DictData>();
        foreach (var kv in dict.Entries)
        {
            // Defensive: the dict could in principle contain non-String values
            // if a composer constructed (dict #foo 42) — that would type-error
            // at parse time anyway via Dict<Symbol, String>, but we guard at
            // runtime too because the wildcard DictType signature doesn't
            // enforce the value type.
            if (kv.Value.Data is string path)
            {
                ctx.SfzInstruments[kv.Key] = path;
            }
        }
        ctx.SfzEnabled = true;
        return Value.Void();
    }

    /// <summary>
    /// Symbol overload — full body lands in Task 2. For now: error out
    /// clearly so the registration is callable and the unit-test scaffolding
    /// can probe the signature.
    /// </summary>
    private static Value LoadSfzSymbol(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        if (!ctx.SfzEnabled)
            throw new InvalidOperationException(
                "loadSfz requires 'use \"@sfz\"' at the top of your script");
        // TODO Task 2: full body.
        throw new NotImplementedException(
            "loadSfz(Symbol) body is shipped in Plan 33-05 Task 2");
    }

    /// <summary>
    /// String overload — full body lands in Task 2. For now: error out
    /// clearly so the registration is callable.
    /// </summary>
    private static Value LoadSfzString(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        if (!ctx.SfzEnabled)
            throw new InvalidOperationException(
                "loadSfz requires 'use \"@sfz\"' at the top of your script");
        // TODO Task 2: full body.
        throw new NotImplementedException(
            "loadSfz(String) body is shipped in Plan 33-05 Task 2");
    }
}
