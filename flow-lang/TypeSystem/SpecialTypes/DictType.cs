using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Generic Dict&lt;K, V&gt; with insertion-order preservation (Phase 26.1 DICT-01).
/// Key type MUST be hashable per <see cref="FlowType.IsHashable"/> — enforced at parse-time
/// (TypeParser) and defensively in this constructor (PATTERNS § E1).
/// <para>
/// <see cref="IsHashable"/> returns <c>false</c>: dicts are not themselves dict keys.
/// </para>
/// <para>
/// VoidType is exempt from the IsHashable defensive check because the dict-side `(get)`,
/// `(set)`, `(each)`, `(map)`, `(filter)` registrations use a wildcard
/// <c>new DictType(VoidType.Instance, VoidType.Instance)</c> for overload-resolution
/// dispatch. The wildcard is internal; user-facing source paths run through TypeParser
/// where VoidType is not a valid type-annotation token at the K position.
/// </para>
/// </summary>
public sealed class DictType : FlowType
{
    public FlowType KeyType { get; }
    public FlowType ValueType { get; }

    public DictType(FlowType keyType, FlowType valueType)
    {
        KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        // Defensive — TypeParser is the primary gate; programmatic construction
        // also rejects non-hashable keys EXCEPT for the VoidType wildcard
        // sentinel used by the dict-side overload registrations.
        if (!keyType.IsHashable() && !(keyType is VoidType))
            throw new ArgumentException(
                $"Dict key type '{keyType.Name}' is not hashable. Allowed: Int, Long, Float, " +
                $"String, Symbol, Note, Chord, Tuple-of-hashables.");
    }

    public override string Name => $"Dict<{KeyType.Name}, {ValueType.Name}>";

    public override bool Equals(FlowType? other)
    {
        return other is DictType dt
            && KeyType.Equals(dt.KeyType)
            && ValueType.Equals(dt.ValueType);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), KeyType, ValueType);

    public override bool IsCompatibleWith(FlowType target)
    {
        if (target is not DictType targetDict) return false;
        // VoidType wildcard — same convention as ArrayType
        if (KeyType is VoidType || targetDict.KeyType is VoidType
         || ValueType is VoidType || targetDict.ValueType is VoidType) return true;
        return KeyType.IsCompatibleWith(targetDict.KeyType)
            && ValueType.IsCompatibleWith(targetDict.ValueType);
    }

    public override bool CanConvertTo(FlowType target) => IsCompatibleWith(target);

    public override int GetSpecificity() => KeyType.GetSpecificity() + ValueType.GetSpecificity() + 70;

    public override bool IsHashable() => false;
}
