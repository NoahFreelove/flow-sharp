using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace FlowLsp.Symbols;

/// <summary>
/// Static keyword list for completion. Covers general control-flow keywords,
/// musical-context keywords, and type keywords. The 5 block-construct snippet
/// templates live on CompletionHandler — these entries are the plain identifier
/// completions that complement them.
///
/// Phase 17 (17-05). Pairs with BuiltInIndex + StdlibSymbolIndex + UserSymbolIndex
/// to provide the default-context completion source set.
/// </summary>
public sealed class KeywordIndex
{
    public static readonly string[] Names = new[]
    {
        // General keywords
        "proc", "use", "return", "internal", "lazy", "fn",
        "section", "for", "while", "break", "continue", "in", "progression",
        // Musical-context keywords
        "tempo", "timesig", "key", "swing", "dynamics", "rit", "accel", "pickup", "pan", "gain",
    };

    public static readonly string[] Types = new[]
    {
        "Int", "Float", "Long", "Double", "String", "Bool", "Number", "Note", "Buf", "Void",
        "Buffer", "Sequence", "Chord", "Song", "Section", "MusicalNote",
        "Beat", "Bar", "TimeSignature", "NoteValue", "Semitone", "Cent",
        "Millisecond", "Second", "Decibel", "Envelope", "OscillatorState", "Voice", "Track",
        "Lazy", "Function",
    };

    public IEnumerable<CompletionItem> Items()
    {
        foreach (var n in Names)
            yield return new CompletionItem
            {
                Label = n,
                Kind = CompletionItemKind.Keyword,
                SortText = $"4_{n}", // keywords sort after builtins and user symbols
            };
        foreach (var t in Types)
            yield return new CompletionItem
            {
                Label = t,
                Kind = CompletionItemKind.TypeParameter,
                SortText = $"5_{t}",
            };
    }
}
