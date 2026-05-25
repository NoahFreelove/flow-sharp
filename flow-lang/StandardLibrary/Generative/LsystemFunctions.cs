using System;
using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext
// — bare name is ambiguous under net10.0's implicit usings (Plan 36-05 / 36-06 precedent).
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Generative;

/// <summary>
/// Phase 36 Plan 36-07 (GEN-02, D-36-06 + D-36-08): the L-system primitive in
/// BOTH one-shot and train+generate-split shapes per D-36-06.
///
/// <para>
/// <b>Composer surface (five registered builtins):</b>
/// <code>
///   (lsystem axiom rules iterations)              ; one-shot → Array[Symbol]
///   (lsystemModel axiom rules)                    ; model → LsystemModel
///   (lsystemGenerate model iterations)            ; model + iteration count → Array[Symbol]
///   (lsystemToSequence expanded mapper)           ; Array[Symbol] + mapper → Sequence
///   (lsystemEqual a b)                            ; structural compare → Bool
/// </code>
/// </para>
///
/// <para>
/// <b>D-36-08 alphabet (Claude's-Discretion pick, justified in RESEARCH §Pattern 3):</b>
/// the L-system alphabet is Phase 26.1 <see cref="SymbolType"/>. Rule keys are
/// Symbol Values; rule values are Tuple-of-Symbol Values (composer-facing literal
/// is the Phase 26.1 tuple syntax <c>&lt;&lt;#A, #B&gt;&gt;</c>). Symbols are
/// interned per-context so the rules-dict lookup runs at <see cref="Value"/>
/// reference identity (hashable + cheap). Terminal symbols (not present as rule
/// keys) pass through unchanged on every iteration — canonical Lindenmayer
/// semantics.
/// </para>
///
/// <para>
/// <b>Charitable interpretation (D-v1.5-05 + Pitfall 2):</b> degenerate inputs
/// (empty rules, non-Symbol axiom, iteration count outside <c>[0, 20]</c>)
/// return a charitable empty/clamped result + emit a one-shot stderr advisory.
/// The iteration cap of 20 is the T-36-17 DoS guard per RESEARCH §Security
/// Domain — exponential growth at order 20 caps at roughly 2^20 ≈ 10^6
/// symbols which is already orders of magnitude beyond any musical use.
/// </para>
///
/// <para>
/// <b>No PRNG in this plan</b> — L-system rewriting is purely deterministic.
/// The source-grep gate <c>PrngRegistryNewRandomGateTests</c> from Plan 36-01
/// passes trivially for this file (zero <c>new Random(</c> occurrences). The
/// <c>// PRNG-SANCTIONED:</c> marker convention from Plan 36-06 is unused
/// here (would only apply if v1.6 adds stochastic rule overloading).
/// </para>
/// </summary>
public static class LsystemFunctions
{
    // ====================================================================
    // Constants — security guard
    // ====================================================================

    /// <summary>
    /// T-36-17 DoS guard per RESEARCH §Security Domain. Exponential growth at
    /// iteration 20 caps at ~2^20 ≈ 10^6 expanded symbols — orders of magnitude
    /// beyond any musical use, well short of OOM. Composers asking for higher
    /// iteration counts get a clamped result + a one-shot advisory.
    /// </summary>
    internal const int MaxIterations = 20;

    // ====================================================================
    // Registration entry point
    // ====================================================================

    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        ExecutionContext context)
    {
        // Wildcard Dict<Void, Void> for overload-resolution dispatch — mirrors
        // the Dict op registrations in BuiltInFunctions.cs:1119. Rules-dict args
        // come in as Dict<Symbol, Tuple<<Symbol, ...>>> (composer-facing) or
        // Dict<Symbol, Array[Symbol]> (programmatic); we dispatch on the runtime
        // shape inside the impl.
        var dictWildcard = new DictType(VoidType.Instance, VoidType.Instance);

        // ---- lsystem (one-shot) ----
        var oneShotSig = new FunctionSignature("lsystem",
            [SymbolType.Instance, dictWildcard, IntType.Instance],
            ParameterNames: ["axiom", "rules", "iterations"]);
        registry.Register("lsystem", oneShotSig, args => LsystemOneShot(args, context));

        // ---- lsystemModel (train) ----
        var modelSig = new FunctionSignature("lsystemModel",
            [SymbolType.Instance, dictWildcard],
            ParameterNames: ["axiom", "rules"]);
        registry.Register("lsystemModel", modelSig, args => LsystemModelTrain(args, context));

        // ---- lsystemGenerate ----
        var generateSig = new FunctionSignature("lsystemGenerate",
            [LsystemModelType.Instance, IntType.Instance],
            ParameterNames: ["model", "iterations"]);
        registry.Register("lsystemGenerate", generateSig,
            args => LsystemGenerate(args, context));

        // ---- lsystemToSequence ----
        // Symbol-array + composer-supplied mapper(Symbol => MusicalNote) → Sequence.
        // Composer's mapper can return a rest (musicalNote rest helper) for
        // any Symbol they want to drop from the output sequence.
        var toSeqSig = new FunctionSignature("lsystemToSequence",
            [new ArrayType(SymbolType.Instance), FunctionType.Instance],
            ParameterNames: ["expanded", "mapper"]);
        registry.Register("lsystemToSequence", toSeqSig,
            args => LsystemToSequence(args, context));

        // ---- lsystemEqual (structural compare) ----
        var equalSig = new FunctionSignature("lsystemEqual",
            [LsystemModelType.Instance, LsystemModelType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("lsystemEqual", equalSig, args => LsystemEqual(args));
    }

    // ====================================================================
    // lsystem (one-shot)
    // ====================================================================

    private static Value LsystemOneShot(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var axiom = args[0];
        var rulesDict = args[1].As<DictData>();
        int requested = args[2].As<int>();
        int iterations = ClampIterationsWithAdvisory(requested, ctx, "lsystem");

        var rules = NormalizeRules(rulesDict, ctx);
        var expanded = ExpandAxiom(axiom, rules, iterations);
        return Value.Array(expanded, SymbolType.Instance);
    }

    // ====================================================================
    // lsystemModel
    // ====================================================================

    private static Value LsystemModelTrain(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var axiom = args[0];
        var rulesDict = args[1].As<DictData>();

        // The model captures `iterations = 0` by default — composers override
        // at generation time via (lsystemGenerate model iterations). The field
        // exists so two-model structural compare can include iteration intent.
        var rules = NormalizeRules(rulesDict, ctx);
        var model = new LsystemModelData(axiom, rules, iterations: 0);
        return Value.LsystemModel(model);
    }

    // ====================================================================
    // lsystemGenerate
    // ====================================================================

    private static Value LsystemGenerate(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var model = args[0].As<LsystemModelData>();
        int requested = args[1].As<int>();
        int iterations = ClampIterationsWithAdvisory(requested, ctx, "lsystemGenerate");

        var expanded = ExpandAxiom(model.Axiom, model.Rules, iterations);
        return Value.Array(expanded, SymbolType.Instance);
    }

    // ====================================================================
    // lsystemToSequence
    // ====================================================================

    private static Value LsystemToSequence(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        var expanded = args[0].As<IReadOnlyList<Value>>();
        var mapper = args[1].As<FunctionOverload>();

        var result = new SequenceData();
        var notes = new List<MusicalNoteData>(expanded.Count);
        foreach (var sym in expanded)
        {
            var lambdaResult = InvokeCallback(ctx, mapper, new List<Value> { sym });
            // The composer's mapper can return:
            //   - a MusicalNote (typical): append to the bar
            //   - a Note literal (e.g. C4q): the parser binds the duration; convert
            //   - a rest: append as rest
            //   - anything else: charitable advisory + skip
            if (lambdaResult.Data is MusicalNoteData note)
            {
                notes.Add(note);
            }
            else
            {
                // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
                if (ctx.CallerStrictMode)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [lsystemToSequence] mapper returned non-Note ({lambdaResult.Type.Name}) at {ctx.CurrentCallSite}",
                        ctx.CurrentCallSite);
                    continue;
                }
                RenderingDiagnostics.WarnOnce(
                    $"lsystemToSequence:non-note-result:{ctx.CurrentCallSite}",
                    $"[lsystemToSequence] mapper returned {lambdaResult.Type.Name} at "
                    + $"{ctx.CurrentCallSite}; expected MusicalNote — skipped");
            }
        }

        if (notes.Count > 0)
        {
            var timeSig = new TimeSignatureData(4, 4);
            result.AddBar(new BarData(notes, timeSig));
        }
        return Value.Sequence(result);
    }

    // ====================================================================
    // lsystemEqual (structural compare)
    // ====================================================================

    private static Value LsystemEqual(IReadOnlyList<Value> args)
    {
        var a = args[0].As<LsystemModelData>();
        var b = args[1].As<LsystemModelData>();
        return Value.Bool(a.StructurallyEquals(b));
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    /// <summary>
    /// Normalises the composer's rules dict into a Symbol → Array[Symbol] table.
    /// Accepts both tuple-shaped values (<c>&lt;&lt;#A, #B&gt;&gt;</c> — the
    /// canonical composer surface) and array-shaped values. Non-Symbol keys or
    /// non-Symbol rule values emit a charitable advisory and are dropped.
    /// </summary>
    private static Dictionary<Value, IReadOnlyList<Value>> NormalizeRules(
        DictData rulesDict, ExecutionContext ctx)
    {
        var result = new Dictionary<Value, IReadOnlyList<Value>>();
        foreach (var kv in rulesDict.Entries)
        {
            var key = kv.Key;
            var valExpr = kv.Value;

            if (key.Type is not SymbolType)
            {
                // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
                if (ctx.CallerStrictMode)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [lsystem] malformed rule — rule key has type {key.Type.Name} (expected Symbol) at {ctx.CurrentCallSite}",
                        ctx.CurrentCallSite);
                    continue;
                }
                RenderingDiagnostics.WarnOnce(
                    $"lsystem:non-symbol-key:{ctx.CurrentCallSite}:{key.Type.Name}",
                    $"[lsystem] rule key has type {key.Type.Name} at "
                    + $"{ctx.CurrentCallSite}; expected Symbol — rule dropped");
                continue;
            }

            // Rule value is either a Tuple of Symbols or an Array of Symbols.
            if (valExpr.Data is IReadOnlyList<Value> components)
            {
                var symbols = new List<Value>(components.Count);
                foreach (var c in components)
                {
                    if (c.Type is SymbolType)
                    {
                        symbols.Add(c);
                    }
                    else
                    {
                        // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
                        if (ctx.CallerStrictMode)
                        {
                            ctx.ErrorReporter.ReportError(
                                $"[strict] [lsystem] rule symbol not in alphabet — rule value contains {c.Type.Name} (expected Symbol) at {ctx.CurrentCallSite}",
                                ctx.CurrentCallSite);
                            continue;
                        }
                        RenderingDiagnostics.WarnOnce(
                            $"lsystem:non-symbol-value:{ctx.CurrentCallSite}:{c.Type.Name}",
                            $"[lsystem] rule value contains {c.Type.Name} at "
                            + $"{ctx.CurrentCallSite}; expected Symbol — element dropped");
                    }
                }
                result[key] = symbols;
            }
            else
            {
                // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
                if (ctx.CallerStrictMode)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] [lsystem] malformed rule — rule value has type {valExpr.Type.Name} (expected Tuple or Array of Symbols) at {ctx.CurrentCallSite}",
                        ctx.CurrentCallSite);
                    continue;
                }
                RenderingDiagnostics.WarnOnce(
                    $"lsystem:non-list-value:{ctx.CurrentCallSite}:{valExpr.Type.Name}",
                    $"[lsystem] rule value has type {valExpr.Type.Name} at "
                    + $"{ctx.CurrentCallSite}; expected Tuple or Array of Symbols — rule dropped");
            }
        }
        return result;
    }

    /// <summary>
    /// Iterates the L-system: starts with <paramref name="axiom"/> as a single-
    /// element list, then for each iteration walks the current list and either
    /// substitutes (when the symbol has a rule) or passes through (terminal
    /// symbol — canonical Lindenmayer semantics per CONTEXT D-36-08).
    /// </summary>
    private static List<Value> ExpandAxiom(
        Value axiom,
        IReadOnlyDictionary<Value, IReadOnlyList<Value>> rules,
        int iterations)
    {
        var current = new List<Value> { axiom };
        for (int i = 0; i < iterations; i++)
        {
            var next = new List<Value>(current.Count * 2);
            foreach (var sym in current)
            {
                // Rule lookup: symbols intern per-context so reference identity
                // suffices for the common case; we also accept underlying-data
                // equality (string body of the Symbol Value) for cross-context
                // robustness (e.g. unit tests building their own Values).
                var expansion = LookupRule(rules, sym);
                if (expansion != null)
                {
                    foreach (var s in expansion) next.Add(s);
                }
                else
                {
                    next.Add(sym);
                }
            }
            current = next;
        }
        return current;
    }

    /// <summary>
    /// Lookup with reference- and data-equality fallback. The interned-Symbol
    /// case (composers writing <c>#A</c> in their rules) hits the
    /// <see cref="IReadOnlyDictionary{TKey,TValue}.ContainsKey"/> fast path;
    /// the data-equality walk covers cross-context comparisons.
    /// </summary>
    private static IReadOnlyList<Value>? LookupRule(
        IReadOnlyDictionary<Value, IReadOnlyList<Value>> rules,
        Value sym)
    {
        if (rules.TryGetValue(sym, out var hit)) return hit;
        // Cross-context fallback: compare by underlying string body.
        if (sym.Data is string symBody)
        {
            foreach (var kv in rules)
            {
                if (kv.Key.Data is string keyBody && keyBody == symBody)
                    return kv.Value;
            }
        }
        return null;
    }

    private static int ClampIterationsWithAdvisory(int requested, ExecutionContext ctx, string siteName)
    {
        if (requested < 0)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{siteName}] iterations clamped to [0, {MaxIterations}] — got {requested} (< 0) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return 0;
            }
            RenderingDiagnostics.WarnOnce(
                $"{siteName}:iterations-negative:{ctx.CurrentCallSite}:{requested}",
                $"[{siteName}] iterations {requested} < 0 at {ctx.CurrentCallSite}; "
                + "clamped to 0 (axiom passes through unchanged)");
            return 0;
        }
        if (requested > MaxIterations)
        {
            // Phase 44 Plan 44-07 Pattern S3: strict-mode branch.
            if (ctx.CallerStrictMode)
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [{siteName}] iterations clamped to [0, {MaxIterations}] — got {requested} (> {MaxIterations}) at {ctx.CurrentCallSite}",
                    ctx.CurrentCallSite);
                return MaxIterations;
            }
            RenderingDiagnostics.WarnOnce(
                $"{siteName}:iterations-cap:{ctx.CurrentCallSite}:{requested}",
                $"[{siteName}] iterations {requested} > {MaxIterations} at {ctx.CurrentCallSite}; "
                + $"clamped to {MaxIterations} (DoS guard — exponential growth cap)");
            return MaxIterations;
        }
        return requested;
    }

    /// <summary>
    /// Invokes a composer-supplied <see cref="FunctionOverload"/>. Mirrors
    /// <c>DictFunctions.InvokeCallback</c> at <c>DictFunctions.cs:41-46</c> and
    /// <c>PatternFunctions.InvokeCallback</c> at <c>PatternFunctions.cs:106-115</c>.
    /// </summary>
    private static Value InvokeCallback(ExecutionContext context, FunctionOverload cb, List<Value> args)
    {
        return cb.IsInternal
            ? cb.Implementation!(args)
            : context.Invoker!.ExecuteUserFunctionWithCaptures(
                cb.Declaration!, args, cb.CapturedVariables);
    }
}
