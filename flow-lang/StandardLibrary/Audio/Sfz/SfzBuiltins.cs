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
            [new DictType(VoidType.Instance, VoidType.Instance)],
            ParameterNames: ["instruments"]);
        registry.Register("__enableSfzModule", enableSig,
            args => EnableSfzModule(args, context));

        // loadSfz(Symbol) — Phase 26.1 SYM-01 strict separation from String
        // ensures the overload resolver picks the right body (Pitfall 12).
        var sigSym = new FunctionSignature("loadSfz", [SymbolType.Instance],
            ParameterNames: ["instrument"]);
        registry.Register("loadSfz", sigSym,
            args => LoadSfzSymbol(args, context));

        // loadSfz(String) — bypass-the-dict literal path.
        var sigStr = new FunctionSignature("loadSfz", [StringType.Instance],
            ParameterNames: ["path"]);
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
    /// Symbol overload — looks up <c>args[0]</c> (a Symbol Value) in
    /// <see cref="ExecutionContext.SfzInstruments"/>, resolves
    /// <c>sfz_root</c> via the Pitfall-2 cache, joins, parses, and wraps the
    /// result with <see cref="Value.Sfz"/>.
    ///
    /// Error paths (each is composer-facing and points at the fix):
    /// <list type="bullet">
    ///   <item><description>Gate off → "loadSfz requires `use \"@sfz\"`"</description></item>
    ///   <item><description>Unknown symbol → message listing all 19 supported symbols
    ///   + a "did you mean?" affordance via the sorted dump.</description></item>
    ///   <item><description>TBD placeholder (4 rows from the Plan 33-01 audit) → message
    ///   referencing "VSCO Community Edition" + the absolute-path overload.</description></item>
    ///   <item><description>sfz_root unconfigured → message naming
    ///   <c>~/.config/flow/config.toml</c> + the <c>sfz_root</c> key. Also fires
    ///   <see cref="RenderingDiagnostics.WarnOnce"/> with sentinel
    ///   <c>sfz:config:sfz_root_missing</c> before throwing.</description></item>
    ///   <item><description>File missing on disk → bubbles
    ///   <see cref="FileNotFoundException"/> from <see cref="File.ReadAllText"/>,
    ///   matching Phase 32 loadScala's contract.</description></item>
    /// </list>
    /// </summary>
    private static Value LoadSfzSymbol(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        if (!ctx.SfzEnabled)
            throw new InvalidOperationException(
                "loadSfz requires 'use \"@sfz\"' at the top of your script");

        var symbolValue = args[0];
        string symbolName = symbolValue.As<string>();

        // Symbol lookup — relies on the per-context SymbolInternTable
        // (Pitfall 1): __enableSfzModule populated ctx.SfzInstruments using the
        // SAME context's interned Symbol values, so reference equality holds.
        if (!ctx.SfzInstruments.TryGetValue(symbolValue, out var relativePath))
        {
            // Unknown symbol. Dump the entire 19-key supported set so the
            // composer's typo gets an immediate suggestion. Sort the keys
            // alphabetically for deterministic + scannable output.
            var supported = ctx.SfzInstruments.Keys
                .Select(k => $"#{k.As<string>()}")
                .OrderBy(s => s, StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"Unknown SFZ instrument symbol '#{symbolName}'. Supported symbols (19, " +
                $"from the @sfz GM orchestral dict): {string.Join(", ", supported)}. " +
                "See .planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md " +
                "for the full mapping.");
        }

        // TBD-placeholder detection — the 4 VSCO-CE-1.1.0 gaps from the audit
        // (#choir / #guitar / #harpsichord / #celeste). The relative path is
        // a known-missing _TBD_ filename rather than an empty string so
        // SymbolInternTable iteration order doesn't matter for the error path.
        if (relativePath.StartsWith(TbdPathPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"SFZ instrument '#{symbolName}' is not bundled with VSCO Community " +
                $"Edition 1.1.0. Use the absolute-path overload instead: " +
                $"(loadSfz \"/path/to/your/{symbolName}.sfz\"). " +
                "See .planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md " +
                "for the 4 TBD rows.");
        }

        // Resolve sfz_root with the Pitfall-2 first-read cache. After this
        // branch ctx.ResolvedSfzRoot is non-null (else we threw).
        string resolvedRoot = ResolveSfzRoot(ctx);

        // Join the relative path against the resolved root. Path.Combine
        // canonicalizes path separators per platform — no extra Windows
        // handling needed because the dict ships forward-slash + backslash
        // is normalized by SfzParser (Plan 33-04 handles the <control>
        // default_path cascade for inside-the-.sfz paths).
        string absolutePath = Path.Combine(resolvedRoot, relativePath);

        // Read + parse. File.ReadAllText throws FileNotFoundException with
        // a clear message naming the missing path — that bubbles to the
        // composer naturally.
        string content = File.ReadAllText(absolutePath);
        // Phase 44 Plan 44-06: thread ctx for strict-mode advisory elevation
        // in SfzParser. Non-strict path stays byte-identical (Pitfall 5).
        var sfzData = SfzParser.Parse(content, absolutePath,
            patchDescription: Path.GetFileNameWithoutExtension(absolutePath),
            strictCtx: ctx);

        // Phase 37 DRUM-01 W7 LOCK (revision pass 2/3) — dict-symbol drives
        // percussion routing, NOT filename. When the composer wrote
        // `loadSfz #drums`, they're loading a percussion patch by construction
        // — that intent is the source of truth, robust against filename
        // changes, VSCO-CE forks, and future custom-dict-symbol extensions.
        // SfzRenderer's #auto pitch-shift route gates on
        // SfzData.IsPercussion per D-37-14 / OQ3 / Pattern 11.
        bool isPercussion = symbolName == "drums";
        if (isPercussion)
        {
            sfzData = sfzData with { IsPercussion = true };
        }

        return Value.Sfz(sfzData);
    }

    /// <summary>
    /// String overload — bypass-the-dict literal-path entry point. No
    /// <c>sfz_root</c> resolution; the caller's path is used as-is (relative
    /// to the process cwd or absolute as written). Mirrors Phase 32
    /// <c>loadScala(String)</c>'s posture verbatim.
    /// </summary>
    private static Value LoadSfzString(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext ctx)
    {
        if (!ctx.SfzEnabled)
            throw new InvalidOperationException(
                "loadSfz requires 'use \"@sfz\"' at the top of your script");

        string path = args[0].As<string>();
        string content = File.ReadAllText(path);
        // Phase 44 Plan 44-06: thread ctx for strict-mode advisory elevation.
        var sfzData = SfzParser.Parse(content, path,
            patchDescription: Path.GetFileNameWithoutExtension(path),
            strictCtx: ctx);
        return Value.Sfz(sfzData);
    }

    /// <summary>
    /// Pitfall-2 first-read cache for <see cref="FlowConfig.Active.SfzRoot"/>.
    /// On the first <c>loadSfz(Symbol)</c> call within a given ExecutionContext,
    /// reads the singleton, validates non-null, caches on
    /// <see cref="ExecutionContext.ResolvedSfzRoot"/>, and returns. Subsequent
    /// calls hit the cache — singleton mutations between calls do NOT affect
    /// resolution (test-isolation fix; also makes a single render insulated
    /// from mid-script config edits).
    /// </summary>
    private static string ResolveSfzRoot(FlowLang.Runtime.ExecutionContext ctx)
    {
        if (ctx.ResolvedSfzRoot is not null) return ctx.ResolvedSfzRoot;

        string? fromConfig = FlowConfig.Active?.SfzRoot;
        if (string.IsNullOrEmpty(fromConfig))
        {
            // One-shot composer-facing advisory + throw. The advisory is
            // process-global per RenderingDiagnostics convention; the throw
            // is per-call so each script that forgets sfz_root sees the
            // error (the advisory just doesn't repeat the prose).
            // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    "[strict] [sfz] sfz_root not configured — populate ~/.config/flow/config.toml " +
                    "with a `sfz_root = \"/path/to/your/sfz/library\"` entry",
                    ctx.CurrentCallSite);
            }
            else
            {
                RenderingDiagnostics.WarnOnce(
                    sentinelKey: "sfz:config:sfz_root_missing",
                    message: "[sfz] sfz_root not configured — populate ~/.config/flow/config.toml " +
                             "with a `sfz_root = \"/path/to/your/sfz/library\"` entry");
            }
            throw new InvalidOperationException(
                "SFZ root directory not configured. Populate `sfz_root` in " +
                "~/.config/flow/config.toml — e.g. `sfz_root = \"$HOME/.flow/samples/VSCO-CE\"` — " +
                "then re-run. See the Phase 30 config docs for the full schema.");
        }

        ctx.ResolvedSfzRoot = fromConfig;
        return fromConfig;
    }
}
