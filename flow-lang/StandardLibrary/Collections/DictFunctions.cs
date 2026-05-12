using System.Collections.Generic;
using System.Linq;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

// NOTE: The namespace ends in `.Dict` (not `.Collections`) to avoid colliding
// with the existing static class `FlowLang.StandardLibrary.Collections`
// (Collections.cs). Same physical directory layout (`StandardLibrary/Collections/`)
// per CONTEXT § Architectural Responsibility Map; the directory is just a
// physical grouping.
namespace FlowLang.StandardLibrary.Dict;

/// <summary>
/// Phase 26.1 dict + tuple-unpack runtime implementations.
/// Wave 3 ships <see cref="Unpack"/> (TUP-11); Wave 4 (plan 26.1-05) extends
/// this class with the 14 dict ops. Mirrors the static-class shape of
/// <c>flow-lang/StandardLibrary/Collections.cs</c> (Each / Map / Filter).
/// </summary>
public static class DictFunctions
{
    /// <summary>
    /// <c>(unpack tuple func)</c> — first-class S-expression equivalent of the
    /// <c>~&gt;</c> operator. Mirrors Lisp's <c>(apply f args)</c>. Pattern S3
    /// (oscillator) for Function dispatch.
    /// </summary>
    public static Value Unpack(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var tup = args[0].As<IReadOnlyList<Value>>();
        var proc = args[1].As<FunctionOverload>();
        var positional = tup.ToList();
        return proc.IsInternal
            ? proc.Implementation!(positional)
            : context.Invoker!.ExecuteUserFunctionWithCaptures(
                proc.Declaration!, positional, proc.CapturedVariables);
    }

    // ===== Dict ops (DICT-01/02/03) — Wave 4 =====

    private static Value InvokeCallback(FlowLang.Runtime.ExecutionContext context, FunctionOverload cb, List<Value> args)
    {
        return cb.IsInternal
            ? cb.Implementation!(args)
            : context.Invoker!.ExecuteUserFunctionWithCaptures(cb.Declaration!, args, cb.CapturedVariables);
    }

    /// <summary>
    /// <c>(dict K V K V ...)</c> — flat interleaved constructor (DICT-02).
    /// Empty <c>(dict)</c> produces <c>Dict&lt;Void, Void&gt;</c>; the caller's annotation narrows it.
    /// </summary>
    public static Value Dict(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        if (args.Count % 2 != 0)
            throw new System.ArgumentException("(dict) requires an even number of args (K V pairs)");
        if (args.Count == 0)
        {
            var emptyType = new DictType(VoidType.Instance, VoidType.Instance);
            return Value.Dict(DictData.Empty(emptyType));
        }
        var keyType = args[0].Type;
        var valueType = args[1].Type;
        if (!keyType.IsHashable())
            throw new System.ArgumentException($"(dict) key type '{keyType.Name}' is not hashable");
        var type = new DictType(keyType, valueType);
        var data = DictData.Empty(type);
        for (int i = 0; i < args.Count; i += 2)
            data = data.WithSet(args[i], args[i + 1]);
        return Value.Dict(data);
    }

    /// <summary>
    /// <c>(dictTuple &lt;&lt;K,V&gt;&gt; &lt;&lt;K,V&gt;&gt; ...)</c> — tuple-pair constructor (DICT-02).
    /// Each arg must be a 2-tuple; K and V types inferred from <c>args[0]</c>'s element types.
    /// </summary>
    public static Value DictTuple(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        if (args.Count == 0)
        {
            var emptyType = new DictType(VoidType.Instance, VoidType.Instance);
            return Value.Dict(DictData.Empty(emptyType));
        }
        if (args[0].Type is not TupleType tt0 || tt0.ElementTypes.Count != 2)
            throw new System.ArgumentException("(dictTuple) requires Tuple<<K, V>> args");
        var keyType = tt0.ElementTypes[0];
        var valueType = tt0.ElementTypes[1];
        if (!keyType.IsHashable())
            throw new System.ArgumentException($"(dictTuple) key type '{keyType.Name}' is not hashable");
        var type = new DictType(keyType, valueType);
        var data = DictData.Empty(type);
        foreach (var pair in args)
        {
            var pairData = pair.As<IReadOnlyList<Value>>();
            if (pairData.Count != 2)
                throw new System.ArgumentException("(dictTuple) each tuple must have exactly 2 components");
            data = data.WithSet(pairData[0], pairData[1]);
        }
        return Value.Dict(data);
    }

    /// <summary><c>(get d k)</c> — returns the value or <see cref="Value.Void"/> when absent (Flow Nothing).</summary>
    public static Value Get(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        var key = args[1];
        return dict.Entries.TryGetValue(key, out var v) ? v : Value.Void();
    }

    /// <summary><c>(getOr d k default)</c> — fallback when absent.</summary>
    public static Value GetOr(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        var key = args[1];
        var fallback = args[2];
        return dict.Entries.TryGetValue(key, out var v) ? v : fallback;
    }

    /// <summary><c>(set d k v)</c> — returns NEW dict with k → v.</summary>
    public static Value Set(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        return Value.Dict(dict.WithSet(args[1], args[2]));
    }

    /// <summary><c>(remove d k)</c> — returns NEW dict without k.</summary>
    public static Value Remove(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        return Value.Dict(dict.WithRemove(args[1]));
    }

    /// <summary><c>(has d k)</c> — Bool.</summary>
    public static Value Has(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        return Value.Bool(dict.Entries.ContainsKey(args[1]));
    }

    /// <summary><c>(keys d)</c> — Array[K] in insertion order.</summary>
    public static Value Keys(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        var ks = new List<Value>(dict.Entries.Count);
        foreach (var kv in dict.Entries) ks.Add(kv.Key);
        return Value.Array(ks, dict.Type.KeyType);
    }

    /// <summary><c>(values d)</c> — Array[V] in insertion order.</summary>
    public static Value Values(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        var vs = new List<Value>(dict.Entries.Count);
        foreach (var kv in dict.Entries) vs.Add(kv.Value);
        return Value.Array(vs, dict.Type.ValueType);
    }

    /// <summary><c>(size d)</c> — Int.</summary>
    public static Value Size(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        return Value.Int(dict.Entries.Count);
    }

    /// <summary>
    /// <c>(merge d1 d2)</c> — last-write-wins per CONTEXT § Claude's Discretion.
    /// Insertion order: d1 keys preserved (updated values stay at original position),
    /// then d2 keys not in d1 appended in d2's order.
    /// </summary>
    public static Value Merge(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var d1 = args[0].As<DictData>();
        var d2 = args[1].As<DictData>();
        return Value.Dict(d1.Merge(d2));
    }

    /// <summary>
    /// <c>(each d cb)</c> — invokes cb(K, V) per entry in insertion order.
    /// CONTEXT § Specifics block 6 specifies cb is invoked with TWO unpacked
    /// positional args (key, value) — the dict-side does the unpacking internally
    /// so the user writes <c>(fn Symbol k, Int v =&gt; ...)</c> 2-arg lambda (Pitfall 6).
    /// </summary>
    public static Value Each(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        var cb = args[1].As<FunctionOverload>();
        foreach (var kv in dict.Entries)
        {
            InvokeCallback(context, cb, new List<Value> { kv.Key, kv.Value });
        }
        return Value.Void();
    }

    /// <summary>
    /// <c>(map d cb)</c> — V-only transform; returns <c>Dict&lt;K, V'&gt;</c> where V' is
    /// the callback's return type. If the callback returns heterogeneous types,
    /// the new value type degrades to <see cref="VoidType"/> (mirrors <c>Collections.Map</c>).
    /// Key-remap is deferred (RESEARCH § Open Questions Q2 RESOLVED).
    /// </summary>
    public static Value Map(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        var cb = args[1].As<FunctionOverload>();
        FlowType? newValueType = null;
        var pairs = new List<(Value, Value)>(dict.Entries.Count);
        foreach (var kv in dict.Entries)
        {
            Value mapped = InvokeCallback(context, cb, new List<Value> { kv.Key, kv.Value });
            if (newValueType == null)
                newValueType = mapped.Type;
            else if (!mapped.Type.Equals(newValueType))
                newValueType = VoidType.Instance;
            pairs.Add((kv.Key, mapped));
        }
        var newType = new DictType(dict.Type.KeyType, newValueType ?? dict.Type.ValueType);
        var data = DictData.Empty(newType);
        foreach (var (k, v) in pairs) data = data.WithSet(k, v);
        return Value.Dict(data);
    }

    /// <summary>
    /// <c>(filter d pred)</c> — returns <c>Dict&lt;K, V&gt;</c> with entries where pred(K, V)
    /// returns true. Insertion order preserved.
    /// </summary>
    public static Value Filter(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var dict = args[0].As<DictData>();
        var pred = args[1].As<FunctionOverload>();
        var data = DictData.Empty(dict.Type);
        foreach (var kv in dict.Entries)
        {
            Value result = InvokeCallback(context, pred, new List<Value> { kv.Key, kv.Value });
            if (result.As<bool>()) data = data.WithSet(kv.Key, kv.Value);
        }
        return Value.Dict(data);
    }
}
