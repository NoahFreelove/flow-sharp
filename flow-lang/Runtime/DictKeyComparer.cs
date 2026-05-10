using System.Collections.Generic;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// IEqualityComparer&lt;Value&gt; for Dict keys (Phase 26.1 DICT-03 NaN-key special-case).
/// Float NaN-equals-NaN ONLY when key type is FloatType — scoped to Dict; <see cref="Utils.StrictEquals"/>
/// is NEVER touched so general <c>(equals nan nan)</c> continues to follow IEEE 754 (Pitfall 3).
/// Symbol uses <see cref="object.ReferenceEquals"/> on Data (interned string object identity).
/// Tuple recurses per-position using element-typed sub-comparers.
/// </summary>
public sealed class DictKeyComparer : IEqualityComparer<Value>
{
    private readonly FlowType _keyType;
    public DictKeyComparer(FlowType keyType) => _keyType = keyType;

    public bool Equals(Value? a, Value? b)
    {
        if (a is null || b is null) return ReferenceEquals(a, b);
        if (_keyType is FloatType && a.Data is double ad && b.Data is double bd)
        {
            if (double.IsNaN(ad) && double.IsNaN(bd)) return true;
            return ad == bd;
        }
        if (_keyType is SymbolType) return ReferenceEquals(a.Data, b.Data);
        if (_keyType is TupleType tt && a.Data is IReadOnlyList<Value> al && b.Data is IReadOnlyList<Value> bl)
        {
            if (al.Count != bl.Count) return false;
            for (int i = 0; i < al.Count; i++)
            {
                var sub = new DictKeyComparer(tt.ElementTypes[i]);
                if (!sub.Equals(al[i], bl[i])) return false;
            }
            return true;
        }
        return Utils.StrictEquals(a, b);
    }

    public int GetHashCode(Value v)
    {
        if (v.Data is double d && double.IsNaN(d)) return 0;
        if (v.Data is IReadOnlyList<Value> tup && _keyType is TupleType tt)
        {
            var hc = new HashCode();
            for (int i = 0; i < tup.Count; i++)
            {
                var sub = new DictKeyComparer(tt.ElementTypes[i]);
                hc.Add(sub.GetHashCode(tup[i]));
            }
            return hc.ToHashCode();
        }
        return v.Data?.GetHashCode() ?? 0;
    }
}
