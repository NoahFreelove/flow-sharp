using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Daw;

/// <summary>Evaluation-local result bundle. The host detaches every value before publication.</summary>
public sealed class DawResultData
{
    public IReadOnlyList<(string Id, Value Content)> Outputs { get; }
    public DawResultData(IEnumerable<(string Id, Value Content)> outputs)
    {
        var array = outputs.Take(33).ToArray();
        if (array.Length is < 1 or > 32) throw new ArgumentException("A DAW result needs 1–32 outputs");
        var ids = new HashSet<(string, string)>();
        foreach (var (id, content) in array)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            if (!ids.Add((content.Type.Name, id))) throw new ArgumentException("Duplicate output identity in the same role");
        }
        Outputs = Array.AsReadOnly(array);
    }
    public static DawResultData From(Value? value)
    {
        if (value?.Data is DawResultData result) return result;
        if (value?.Data is DictData scores && scores.Type.KeyType is StringType && scores.Type.ValueType is SongType)
            return new(scores.Entries.Select(p => (p.Key.As<string>(), p.Value)));
        throw new ArgumentException("Generator entry must return Dict<String, Song> or DawResult; use dawResult");
    }
}
