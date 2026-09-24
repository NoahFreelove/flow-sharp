using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary;

public static class Collections
{
    public static Value List(IReadOnlyList<Value> args) => CoreCollections.List(args);
    public static Value Len(IReadOnlyList<Value> args) => CoreCollections.Len(args);
    public static Value Head(IReadOnlyList<Value> args) => CoreCollections.Head(args);
    public static Value Tail(IReadOnlyList<Value> args) => CoreCollections.Tail(args);
    public static Value Last(IReadOnlyList<Value> args) => CoreCollections.Last(args);
    public static Value Init(IReadOnlyList<Value> args) => CoreCollections.Init(args);
    public static Value Empty(IReadOnlyList<Value> args) => CoreCollections.Empty(args);
    public static Value Sort(IReadOnlyList<Value> args) => CoreCollections.Sort(args);
    public static Value Reverse(IReadOnlyList<Value> args) => CoreCollections.Reverse(args);
    public static Value Take(IReadOnlyList<Value> args) => CoreCollections.Take(args);
    public static Value Drop(IReadOnlyList<Value> args) => CoreCollections.Drop(args);
    public static Value Range(IReadOnlyList<Value> args) => CoreCollections.Range(args);
    public static Value SliceArray(IReadOnlyList<Value> args) => CoreCollections.SliceArray(args);
    public static Value SliceSequence(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        if (args[1].Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {args[1].Type}");
        if (args[2].Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {args[2].Type}");

        int count = seq.Bars.Count;
        // DEFER-05: normalize negative indices Python-style (count + idx) BEFORE clamp.
        int rawStart = args[1].As<int>();
        int rawEnd   = args[2].As<int>();
        int normStart = rawStart < 0 ? rawStart + count : rawStart;
        int normEnd   = rawEnd   < 0 ? rawEnd   + count : rawEnd;
        // Phase 14 D-01 silent-clamp tradition preserved post-normalization (D-USER-D).
        int s = Math.Clamp(normStart, 0, count);
        int e = Math.Clamp(normEnd,   0, count);
        if (s >= e)
            return MusicValue.Sequence(new SequenceData());

        var result = new SequenceData();
        for (int i = s; i < e; i++)
            result.AddBar(seq.Bars[i]);
        return MusicValue.Sequence(result);
    }

    public static Value Append(IReadOnlyList<Value> args) => CoreCollections.Append(args);
    public static Value Prepend(IReadOnlyList<Value> args) => CoreCollections.Prepend(args);
    public static Value Concat(IReadOnlyList<Value> args) => CoreCollections.Concat(args);
    public static Value Contains(IReadOnlyList<Value> args) => CoreCollections.Contains(args);
    public static Value Each(IReadOnlyList<Value> args, ExecutionContext context) => CoreCollections.Each(args, context);
    public static Value Map(IReadOnlyList<Value> args, ExecutionContext context) => CoreCollections.Map(args, context);
    public static Value Filter(IReadOnlyList<Value> args, ExecutionContext context) => CoreCollections.Filter(args, context);
    public static Value Zip(IReadOnlyList<Value> args) => CoreCollections.Zip(args);
    public static Value Reduce(IReadOnlyList<Value> args, ExecutionContext context) => CoreCollections.Reduce(args, context);
}
