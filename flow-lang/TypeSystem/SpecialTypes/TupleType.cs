using System.Linq;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Tuple type with per-position element types and arity (Phase 26.1 TUP-09).
/// Empty <c>Tuple&lt;&lt;&gt;&gt;</c> and singleton <c>Tuple&lt;&lt;T&gt;&gt;</c> are valid arities.
/// <para>
/// <see cref="IsHashable"/> recurses to all elements — Tuple-of-hashables is a valid
/// Dict key (Wave 4 TUP-09 + DICT-01 contract).
/// </para>
/// <para>
/// <see cref="AnyArity"/> sentinel matches any tuple via <see cref="IsCompatibleWith"/>
/// (mirrors <c>ArrayType(VoidType.Instance)</c> precedent). Required by Wave 3
/// <c>(unpack)</c> registration so the builtin accepts any tuple arity.
/// </para>
/// </summary>
public sealed class TupleType : FlowType
{
    public IReadOnlyList<FlowType> ElementTypes { get; }
    public bool IsAnyArity { get; }

    public TupleType(IReadOnlyList<FlowType> elementTypes)
    {
        ElementTypes = elementTypes ?? throw new ArgumentNullException(nameof(elementTypes));
        IsAnyArity = false;
    }

    private TupleType(IReadOnlyList<FlowType> elementTypes, bool isAnyArity)
    {
        ElementTypes = elementTypes;
        IsAnyArity = isAnyArity;
    }

    /// <summary>
    /// Sentinel matching any tuple type via <see cref="IsCompatibleWith"/> — used
    /// for <c>(unpack)</c> registration in Wave 3 (TUP-11).
    /// </summary>
    public static TupleType AnyArity { get; } = new(System.Array.Empty<FlowType>(), isAnyArity: true);

    public override string Name => IsAnyArity
        ? "Tuple<<*>>"
        : $"Tuple<<{string.Join(", ", ElementTypes.Select(t => t.Name))}>>";

    public override bool Equals(FlowType? other)
    {
        if (other is not TupleType t) return false;
        // sweep-0614: Equals must be a pure STRUCTURAL-equality relation
        // consistent with GetHashCode (the AnyArity sentinel hashes
        // "__AnyArity__"; a concrete tuple hashes its element types). The old
        // `if (IsAnyArity || t.IsAnyArity) return true;` short-circuit made
        // AnyArity.Equals(Tuple<<Int>>) == true while their hash codes differ,
        // violating the .NET invariant "equal objects must hash equally" — a
        // latent hazard for any future Dictionary<FlowType,_>/HashSet<FlowType>.
        // AnyArity WILDCARD matching lives in IsCompatibleWith/CanConvertTo,
        // which is the path dispatch actually uses, so removing it here does
        // not affect overload resolution. Two AnyArity instances stay equal
        // (both have empty ElementTypes → fall through the structural loop).
        if (IsAnyArity != t.IsAnyArity) return false;
        if (ElementTypes.Count != t.ElementTypes.Count) return false;
        for (int i = 0; i < ElementTypes.Count; i++)
            if (!ElementTypes[i].Equals(t.ElementTypes[i])) return false;
        return true;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(GetType());
        // AnyArity hashes the same regardless of (empty) ElementTypes — its semantics
        // is "match any tuple" so two AnyArity instances should collide in any hash set.
        if (IsAnyArity)
        {
            hc.Add("__AnyArity__");
            return hc.ToHashCode();
        }
        foreach (var et in ElementTypes) hc.Add(et);
        return hc.ToHashCode();
    }

    public override bool IsCompatibleWith(FlowType target)
    {
        if (target is not TupleType tt) return false;
        if (IsAnyArity || tt.IsAnyArity) return true;
        if (ElementTypes.Count != tt.ElementTypes.Count) return false;
        for (int i = 0; i < ElementTypes.Count; i++)
            if (!ElementTypes[i].IsCompatibleWith(tt.ElementTypes[i])) return false;
        return true;
    }

    public override bool CanConvertTo(FlowType target) => IsCompatibleWith(target);

    public override int GetSpecificity()
    {
        if (IsAnyArity) return 50;
        return ElementTypes.Sum(e => e.GetSpecificity()) + 60;
    }

    public override bool IsHashable() => !IsAnyArity && ElementTypes.All(e => e.IsHashable());
}
