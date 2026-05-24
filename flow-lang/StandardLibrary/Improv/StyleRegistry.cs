using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext —
// the bare name is ambiguous under net10.0's implicit usings.
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Improv;

/// <summary>
/// Phase 36 Plan 36-11 (IMPROV-01 / D-36-12) — XDG-conventions style-pack
/// discovery + the <c>(registerStyle #name pack)</c> / <c>(listStyles)</c>
/// builtins backing the <c>@improv</c> stdlib module.
///
/// <para>
/// <b>Load order (Pitfall 8):</b> at FlowEngine init, <see cref="LoadAtEngineInit"/>
/// loads shipped packs from <c>flow-lang/improv/styles/*.flow</c> FIRST, then
/// user packs from <c>~/.config/flow/styles/*.flow</c> SECOND. Last-write-wins
/// for same-named packs (user pack overrides shipped). When a user pack
/// overrides a shipped pack, a one-shot stderr advisory fires keyed by
/// <c>improv:override:{symbolName}</c>.
/// </para>
///
/// <para>
/// <b>Charitable interpretation:</b> a malformed style pack file (parse error,
/// missing <c>(registerStyle ...)</c> call, or any runtime exception during
/// load) emits a one-shot stderr advisory and continues — FlowEngine init
/// MUST NOT abort due to a user's typo in their style pack.
/// </para>
///
/// <para>
/// <b>Rule-pack Dict shape contract:</b> see <c>flow-lang/improv/styles/README.md</c>.
/// The composer-facing shape (inline-dict form — heterogeneous values sidestep
/// Flow's variable-declaration type-annotation requirement) is:
/// <code>
///   (registerStyle #name
///     (dict
///       #beat_weights (dict
///         #strong (dict #chord_tone w_ct  #scale_tone w_st  #chromatic_passing w_cp)
///         #weak   (dict #chord_tone w_ct  #scale_tone w_st  #chromatic_passing w_cp))
///       #interval_transitions (dict
///         #step_up w_su  #step_down w_sd
///         #leap_up w_lu  #leap_down w_ld
///         #chromatic w_chrom  #repeat w_rep)
///       #rhythmic_template &lt;&lt;e e e e e e e e&gt;&gt;
///       #articulation_distribution (dict
///         #downbeat   #legato
///         #offbeat    #accent
///         #syncopated #marcato)))
/// </code>
/// </para>
/// </summary>
public static class StyleRegistry
{
    /// <summary>
    /// Wires the <c>registerStyle</c> + <c>listStyles</c> C# builtins into the
    /// registry. Called from <c>FlowEngine</c> engine init BEFORE the
    /// interpreter is fully constructed (the .flow pack files need these
    /// builtins to be resolvable when they parse). The actual pack discovery
    /// + load step runs AFTER the interpreter is wired — see
    /// <see cref="LoadShippedAndUserPacks"/>.
    /// </summary>
    public static void RegisterBuiltinsOnly(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        RegisterBuiltins(registry, context);
    }

    /// <summary>
    /// Scans the shipped + user XDG-config style-pack directories and runs
    /// each <c>.flow</c> file through the given engine's
    /// <see cref="FlowLang.Core.FlowEngine.Execute"/>. Last-write-wins per
    /// Pitfall 8; user packs override shipped packs and emit a one-shot
    /// stderr advisory. Called from <c>FlowEngine</c> engine init AFTER the
    /// interpreter + module loader are fully constructed.
    /// </summary>
    public static void LoadShippedAndUserPacks(
        FlowLang.Core.FlowEngine engine,
        ExecutionContext context)
    {
        // Suppress override advisories during shipped-pack load — only
        // user-vs-shipped collisions are composer-visible overrides; back-to-back
        // FlowEngine init re-loads of the same shipped pack do NOT count.
        context.SuppressStyleOverrideAdvisory = true;
        try
        {
            LoadShippedPacks(engine, context);
        }
        finally
        {
            context.SuppressStyleOverrideAdvisory = false;
        }
        LoadUserPacks(engine, context);
    }

    // ====================================================================
    // Builtins
    // ====================================================================

    private static void RegisterBuiltins(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        // registerStyle(Symbol, Dict) — the Dict is wildcard-typed
        // (Dict<Void, Void>) to accept any composer-supplied
        // Dict<Symbol, Value> shape without forcing them to match a specific
        // generic instantiation at the call site.
        var registerSig = new FunctionSignature("registerStyle",
            [SymbolType.Instance, new DictType(VoidType.Instance, VoidType.Instance)],
            ParameterNames: ["name", "pack"]);
        registry.Register("registerStyle", registerSig,
            args => RegisterStyle(args, context));

        // listStyles() — returns Array[Symbol] of registered style names in
        // insertion order. Composers can call this to audit which packs
        // loaded at engine init (Pitfall 8 — "did my pack actually load?").
        var listSig = new FunctionSignature("listStyles",
            Array.Empty<FlowType>(),
            ParameterNames: Array.Empty<string>());
        registry.Register("listStyles", listSig,
            args => ListStyles(context));
    }

    /// <summary>
    /// (registerStyle #name pack) — stores the (Symbol → Dict) entry into
    /// <see cref="ExecutionContext.StyleRegistry"/>. Last-write-wins; if a
    /// pack with the same Symbol is already registered AND the override has
    /// not yet been advised this process, a one-shot stderr advisory fires.
    /// </summary>
    private static Value RegisterStyle(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var symbolValue = args[0];
        var pack = args[1].As<DictData>();

        string symbolName = symbolValue.As<string>();

        // Override detection — emit ONCE per (style-name) per process so a
        // composer who runs the same script back-to-back doesn't get spammed.
        // Suppression: the shipped-pack phase of LoadShippedAndUserPacks sets
        // ctx.SuppressStyleOverrideAdvisory=true so re-loading the same pack
        // in a back-to-back FlowEngine doesn't fire a spurious "user overrides
        // shipped" warning (the composer-facing override is user-vs-shipped,
        // not shipped-vs-shipped).
        if (!ctx.SuppressStyleOverrideAdvisory
            && ctx.StyleRegistry.ContainsKey(symbolValue)
            && !ctx.StyleOverrideAdvisoriesEmitted.Contains(symbolName))
        {
            RenderingDiagnostics.WarnOnce(
                $"improv:override:{symbolName}",
                $"[improv] user style '#{symbolName}' overrides shipped pack");
            ctx.StyleOverrideAdvisoriesEmitted.Add(symbolName);
        }

        ctx.StyleRegistry[symbolValue] = pack;
        return Value.Void();
    }

    /// <summary>
    /// (listStyles) — returns the registered Symbol names as an Array[Symbol]
    /// in registration (insertion) order. Symbol keys are reference-equal
    /// to the original Symbol Values so the returned array's entries are
    /// the same interned instances composers see when they write #jazz etc.
    /// </summary>
    private static Value ListStyles(ExecutionContext ctx)
    {
        var symbols = new List<Value>(ctx.StyleRegistry.Count);
        foreach (var key in ctx.StyleRegistry.Keys)
            symbols.Add(key);
        return Value.Array(symbols, SymbolType.Instance);
    }

    // ====================================================================
    // Pack loading
    // ====================================================================

    /// <summary>
    /// Loads packs shipped with the FlowEngine — sits alongside the stdlib
    /// .flow files at <c>{AppContext.BaseDirectory}/improv/styles/*.flow</c>
    /// after the build copies them via the csproj <c>None Update</c> entries.
    /// </summary>
    private static void LoadShippedPacks(FlowLang.Core.FlowEngine engine, ExecutionContext ctx)
    {
        string shippedDir = Path.Combine(AppContext.BaseDirectory, "improv", "styles");
        LoadDir(engine, ctx, shippedDir, source: "shipped");
    }

    /// <summary>
    /// Loads user-supplied packs from XDG-conventional
    /// <c>~/.config/flow/styles/*.flow</c>. Honors the <c>HOME</c> env var so
    /// tests can redirect to a temp dir without touching the real user config.
    /// </summary>
    private static void LoadUserPacks(FlowLang.Core.FlowEngine engine, ExecutionContext ctx)
    {
        // Environment.GetFolderPath(SpecialFolder.UserProfile) honors $HOME on
        // Linux and %USERPROFILE% on Windows; SetEnvironmentVariable("HOME", ...)
        // in tests redirects this exact call (verified by StyleRegistryTests).
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userHome))
            return; // No home dir available (e.g., containerized run) — nothing to load.

        string userDir = Path.Combine(userHome, ".config", "flow", "styles");
        LoadDir(engine, ctx, userDir, source: "user");
    }

    /// <summary>
    /// Iterates <c>dir/*.flow</c> in deterministic alphabetical order and
    /// runs each through the engine. Per the threat model (T-36-27), user
    /// .flow files have no broader privilege than any other Flow code — the
    /// rule-pack convention is documented but NOT enforced (loader is
    /// charitable about non-<c>(registerStyle ...)</c> top-level statements).
    ///
    /// <para>
    /// Any exception during a single pack's load fires a one-shot stderr
    /// advisory and CONTINUES — one composer's malformed pack does not
    /// abort FlowEngine init.
    /// </para>
    /// </summary>
    private static void LoadDir(
        FlowLang.Core.FlowEngine engine,
        ExecutionContext ctx,
        string dir,
        string source)
    {
        if (!Directory.Exists(dir))
            return;

        string[] files;
        try
        {
            files = Directory.GetFiles(dir, "*.flow")
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex)
        {
            RenderingDiagnostics.WarnOnce(
                $"styleRegistry:enumerate-fail:{dir}",
                $"[improv] failed to enumerate style packs in '{dir}': {ex.Message}");
            return;
        }

        foreach (var file in files)
        {
            try
            {
                string source_ = File.ReadAllText(file);
                bool ok = engine.Execute(source_, file);
                if (!ok)
                {
                    RenderingDiagnostics.WarnOnce(
                        $"styleRegistry:executeErr:{file}",
                        $"[improv] style pack '{file}' reported errors during load (see prior diagnostics)");
                }
            }
            catch (Exception ex)
            {
                RenderingDiagnostics.WarnOnce(
                    $"styleRegistry:loadFail:{file}",
                    $"[improv] failed to load style pack '{file}': {ex.Message}");
            }
        }
    }
}
