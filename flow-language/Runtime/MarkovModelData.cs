using System.Collections.Generic;
using System.Collections.Immutable;

namespace FlowLang.Runtime;

/// <summary>
/// Phase 36 Plan 36-06 (GEN-01, D-36-06) — first-class reference-identity model
/// produced by <c>(markovTrain corpus order)</c> and consumed by
/// <c>(markovGenerate model length [seed])</c>.
///
/// <para>
/// <b>NOT a C# <c>record</c> by design (Pitfall 6 in <c>36-PATTERNS.md</c>).</b>
/// Composer mental-model contract per CLAUDE.md Music Types Quick Reference is
/// reference identity: two independently-trained models on the same corpus +
/// order are DISTINCT values for <c>(eq m1 m2)</c> but STRUCTURALLY EQUAL via
/// the dedicated <c>(markovEqual m1 m2)</c> builtin. Mirrors the Phase 32
/// <c>ResolvedTuning</c> + Phase 33 <c>SfzData</c> precedent — those are also
/// plain classes wrapped in dedicated Flow types
/// (<c>TuningType</c> / <c>SfzType</c>) with reference equality.
/// </para>
///
/// <para>
/// <b>Transition shape:</b> the key is an <see cref="ImmutableArray{T}"/> of
/// <c>order</c> integer states. In the default <c>"pitch"</c> feature mode,
/// each state is a MIDI pitch (0..127). In the optional <c>"pitch+duration"</c>
/// mode (D-36-07), each state is packed via
/// <c>(pitch &lt;&lt; 20) | duration_quarter_units</c>:
/// 12 bits for the MIDI pitch (low) and 20 bits for the duration in
/// quarter-note units (high) — sufficient for a 16-bar whole-note at quarter
/// granularity. Documented in this plan's SUMMARY.
/// </para>
///
/// <para>
/// The transitions table maps each <c>order</c>-length prefix to the list of
/// observed next-states with non-zero weights. Weights are normalised at
/// generation time via cumulative-weight roulette (see
/// <c>MarkovFunctions.GenerateMarkov</c>); training stores the raw counts.
/// </para>
/// </summary>
public class MarkovModelData
{
    /// <summary>Markov order in [1, 3] per GEN-01 (clamped by training builtin).</summary>
    public int Order { get; }

    /// <summary>
    /// Prefix → list of (next-state, weight) pairs. The key uses
    /// <see cref="ImmutableArray{T}"/> so the dictionary's structural equality
    /// works via <see cref="MarkovModelData.PrefixComparer"/>.
    /// </summary>
    public IReadOnlyDictionary<ImmutableArray<int>, IReadOnlyList<(int State, double Weight)>> Transitions { get; }

    /// <summary>
    /// All distinct states observed in the corpus, in first-seen order.
    /// Used for cold-start generation when the seeded prefix has no transitions
    /// table entry (e.g. when the corpus is shorter than <c>Order</c>).
    /// </summary>
    public IReadOnlyList<int> StateAlphabet { get; }

    /// <summary>
    /// D-36-07 feature mode: <c>"pitch"</c> (default) or <c>"pitch+duration"</c>.
    /// Drives how integer states are unpacked back into <see cref="TypeSystem.SpecialTypes.MusicalNoteData"/>
    /// at generation time.
    /// </summary>
    public string FeatureMode { get; }

    public MarkovModelData(
        int order,
        IReadOnlyDictionary<ImmutableArray<int>, IReadOnlyList<(int State, double Weight)>> transitions,
        IReadOnlyList<int> stateAlphabet,
        string featureMode)
    {
        Order = order;
        Transitions = transitions;
        StateAlphabet = stateAlphabet;
        FeatureMode = featureMode;
    }

    /// <summary>
    /// Structural compare for the <c>(markovEqual a b)</c> builtin. Two
    /// independently-trained models on the same corpus + order +
    /// <see cref="FeatureMode"/> return <c>true</c> here even though
    /// <c>(eq m1 m2)</c> returns <c>false</c> (reference identity).
    ///
    /// <para>
    /// Equality of <see cref="Transitions"/> is by key-set + per-key value
    /// list content (the list is ordered — same observation order at training
    /// produces same list order). <see cref="StateAlphabet"/> equality is
    /// element-wise in observed order.
    /// </para>
    /// </summary>
    public bool StructurallyEquals(MarkovModelData? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Order != other.Order) return false;
        if (!string.Equals(FeatureMode, other.FeatureMode, System.StringComparison.Ordinal))
            return false;
        if (StateAlphabet.Count != other.StateAlphabet.Count) return false;
        for (int i = 0; i < StateAlphabet.Count; i++)
            if (StateAlphabet[i] != other.StateAlphabet[i]) return false;

        if (Transitions.Count != other.Transitions.Count) return false;
        foreach (var kv in Transitions)
        {
            if (!other.Transitions.TryGetValue(kv.Key, out var otherList)) return false;
            if (kv.Value.Count != otherList.Count) return false;
            for (int i = 0; i < kv.Value.Count; i++)
            {
                if (kv.Value[i].State != otherList[i].State) return false;
                if (kv.Value[i].Weight != otherList[i].Weight) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// <see cref="IEqualityComparer{T}"/> for <see cref="ImmutableArray{T}"/>
    /// of int so the <see cref="Transitions"/> dictionary uses structural
    /// (element-wise) equality on the prefix key. Without this the dict
    /// would use reference identity on the underlying int[] backing field
    /// and every <c>training-time prefix-build</c> would produce a fresh
    /// non-matching key.
    /// </summary>
    public sealed class PrefixComparer : IEqualityComparer<ImmutableArray<int>>
    {
        public static readonly PrefixComparer Instance = new();

        public bool Equals(ImmutableArray<int> a, ImmutableArray<int> b)
        {
            if (a.IsDefault || b.IsDefault) return a.IsDefault == b.IsDefault;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public int GetHashCode(ImmutableArray<int> obj)
        {
            if (obj.IsDefault) return 0;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < obj.Length; i++)
                    hash = hash * 31 + obj[i];
                return hash;
            }
        }
    }
}
