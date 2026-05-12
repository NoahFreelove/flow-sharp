using System.Collections.Generic;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Runtime;

/// <summary>
/// Runtime data shape for Dict values (Phase 26.1 DICT-02). Wraps
/// <see cref="OrderedDictionary{TKey,TValue}"/> (.NET 9+) for insertion-order
/// preservation. Immutable update primitives (<see cref="WithSet"/> / <see cref="WithRemove"/> /
/// <see cref="Merge"/>) each return a NEW <see cref="DictData"/> copy — Flow's record-style data model.
/// </summary>
public sealed class DictData
{
    public OrderedDictionary<Value, Value> Entries { get; }
    public DictType Type { get; }

    public DictData(DictType type, OrderedDictionary<Value, Value> entries)
    {
        Type = type;
        Entries = entries;
    }

    public static DictData Empty(DictType type)
        => new(type, new OrderedDictionary<Value, Value>(new DictKeyComparer(type.KeyType)));

    public DictData WithSet(Value k, Value v)
    {
        var copy = new OrderedDictionary<Value, Value>(Entries.Count + 1, new DictKeyComparer(Type.KeyType));
        foreach (var kv in Entries) copy[kv.Key] = kv.Value;
        copy[k] = v;
        return new DictData(Type, copy);
    }

    public DictData WithRemove(Value k)
    {
        var comparer = new DictKeyComparer(Type.KeyType);
        var copy = new OrderedDictionary<Value, Value>(Entries.Count, comparer);
        foreach (var kv in Entries)
            if (!comparer.Equals(kv.Key, k))
                copy[kv.Key] = kv.Value;
        return new DictData(Type, copy);
    }

    public DictData Merge(DictData other)
    {
        // Last-write-wins per CONTEXT § Claude's Discretion (D-claude-merge).
        // Insertion order: this's keys preserved (updated values stay at original position),
        // then other's keys not present in this appended in their order.
        var copy = new OrderedDictionary<Value, Value>(Entries.Count + other.Entries.Count, new DictKeyComparer(Type.KeyType));
        foreach (var kv in Entries) copy[kv.Key] = kv.Value;
        foreach (var kv in other.Entries) copy[kv.Key] = kv.Value;
        return new DictData(Type, copy);
    }
}
