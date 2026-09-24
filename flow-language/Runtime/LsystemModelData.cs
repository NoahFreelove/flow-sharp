using System.Collections.Generic;

namespace FlowLang.Runtime;

/// <summary>
/// Phase 36 Plan 36-07 (GEN-02, D-36-06 + D-36-08) — first-class reference-identity
/// model produced by <c>(lsystemModel axiom rules)</c> and consumed by
/// <c>(lsystemGenerate model iterations)</c>.
///
/// <para>
/// <b>NOT a C# <c>record</c> by design (Pitfall 6 in <c>36-PATTERNS.md</c>).</b>
/// Mirrors the Plan 36-06 <see cref="MarkovModelData"/> precedent + Phase 32
/// <c>ResolvedTuning</c> + Phase 33 <c>SfzData</c>: two independently-built
/// L-system models on the same axiom + rules are DISTINCT values under default
/// equality (reference identity) but STRUCTURALLY EQUAL via the dedicated
/// <see cref="StructurallyEquals"/> method (backing the <c>(lsystemEqual a b)</c>
/// Flow builtin).
/// </para>
///
/// <para>
/// <b>Alphabet shape (D-36-08 Claude's-Discretion pick, justified in RESEARCH
/// §Pattern 3):</b> the alphabet is Phase 26.1 <c>Symbol</c> values
/// (<c>#A</c>, <c>#B</c>, ...). Rule keys are interned Symbol Values; rule
/// values are <see cref="IReadOnlyList{Value}"/> of Symbol Values. Terminal
/// symbols (not in the rules dict) pass through unchanged on every iteration —
/// canonical Lindenmayer semantics.
/// </para>
///
/// <para>
/// The <see cref="Iterations"/> field stores the iteration count captured at
/// train time. When the composer calls <c>(lsystemGenerate model iterations)</c>
/// they override that count; the field exists for diagnostics + structural
/// equality (two models trained with the same axiom + rules are not "the same"
/// model from the composer's POV if they were minted with different iteration
/// counts in mind).
/// </para>
/// </summary>
public class LsystemModelData
{
    /// <summary>The starting Symbol Value (e.g. <c>#A</c>).</summary>
    public Value Axiom { get; }

    /// <summary>
    /// Rule table mapping a Symbol Value to its rewrite-array of Symbol Values.
    /// Stored as <see cref="IReadOnlyDictionary{TKey,TValue}"/> at construction so
    /// the model is immutable post-construction (T-36-18 mitigation).
    /// </summary>
    public IReadOnlyDictionary<Value, IReadOnlyList<Value>> Rules { get; }

    /// <summary>
    /// Iteration count captured at train time. Bounded to <c>[0, 20]</c> by
    /// the training builtin (T-36-17 DoS guard — exponential growth cap).
    /// </summary>
    public int Iterations { get; }

    public LsystemModelData(
        Value axiom,
        IReadOnlyDictionary<Value, IReadOnlyList<Value>> rules,
        int iterations)
    {
        Axiom = axiom;
        Rules = rules;
        Iterations = iterations;
    }

    /// <summary>
    /// Structural compare for the <c>(lsystemEqual a b)</c> builtin. Two
    /// independently-built models with the same axiom + rules + iterations
    /// return <c>true</c> here even though <c>(eq m1 m2)</c> returns
    /// <c>false</c> (reference identity).
    ///
    /// <para>
    /// Axiom equality: Symbol Values intern to the same Value reference via
    /// <see cref="ExecutionContext.SymbolInternTable"/>, so reference equality
    /// suffices for Symbol-typed axioms. We fall back to underlying-data
    /// equality for safety (e.g. cross-context comparisons in tests).
    /// </para>
    ///
    /// <para>
    /// Rules equality: key-set equality (Symbol reference- OR data-equal) +
    /// per-key rewrite-array element-wise equality.
    /// </para>
    /// </summary>
    public bool StructurallyEquals(LsystemModelData? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Iterations != other.Iterations) return false;
        if (!ValuesEqual(Axiom, other.Axiom)) return false;
        if (Rules.Count != other.Rules.Count) return false;

        foreach (var kv in Rules)
        {
            // Find the matching key in the other dict. Symbol Values intern
            // per-context so reference equality usually works; but the test
            // harness may build two LsystemModelData instances from separate
            // contexts in which case we need data-level compare.
            IReadOnlyList<Value>? otherList = null;
            foreach (var otherKv in other.Rules)
            {
                if (ValuesEqual(kv.Key, otherKv.Key))
                {
                    otherList = otherKv.Value;
                    break;
                }
            }
            if (otherList is null) return false;
            if (kv.Value.Count != otherList.Count) return false;
            for (int i = 0; i < kv.Value.Count; i++)
                if (!ValuesEqual(kv.Value[i], otherList[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// Compare two Values for L-system-equality purposes: reference equality
    /// (covers the interned-Symbol common case) OR underlying-data equality
    /// (covers cross-context comparison and any non-Symbol future alphabets).
    /// </summary>
    private static bool ValuesEqual(Value a, Value b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Type != b.Type && a.Type.Name != b.Type.Name) return false;
        if (a.Data is null) return b.Data is null;
        return a.Data.Equals(b.Data);
    }
}
