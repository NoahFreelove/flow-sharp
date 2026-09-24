using System.Collections.Generic;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace FlowLsp.Symbols;

/// <summary>
/// Editor signatures discovered from module declarations, without registering
/// implementations or constructing audio/session objects. The registry constructor
/// remains a compatibility adapter for existing callers.
/// </summary>
public sealed class BuiltInIndex
{
    public sealed record Entry(string Name, IReadOnlyList<FunctionSignature> Signatures);

    private readonly IReadOnlyDictionary<string, Entry> _byName;

    public BuiltInIndex(InternalFunctionRegistry registry)
    {
        var dict = new Dictionary<string, Entry>();
        foreach (var kvp in registry.EnumerateSignatures())
        {
            dict[kvp.Key] = new Entry(kvp.Key, kvp.Value);
        }
        _byName = dict;
    }

    public BuiltInIndex(IEnumerable<FlowLang.Analysis.ModuleDescriptor> modules)
    {
        _byName = modules.SelectMany(m => m.Procedures)
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => new Entry(group.Key, group.Select(p => p.Signature).ToArray()),
                StringComparer.Ordinal);
    }

    /// <summary>Returns the index entry for <paramref name="name"/>, or null if unknown.</summary>
    public Entry? Find(string name) =>
        _byName.TryGetValue(name, out var e) ? e : null;

    /// <summary>All known built-in names — mainly for tests and stats.</summary>
    public IEnumerable<string> Names => _byName.Keys;

    /// <summary>Emits a CompletionItem per built-in. Detail is the first signature's ToString.</summary>
    public IEnumerable<CompletionItem> Items()
    {
        foreach (var (name, entry) in _byName)
        {
            var doc = BuiltInDocs.TryGet(name);
            var detail = entry.Signatures.Count > 0 ? entry.Signatures[0].ToString() : name;
            yield return new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Function,
                Detail = detail,
                Documentation = doc?.Summary is { Length: > 0 } s
                    ? new StringOrMarkupContent(s)
                    : null,
                SortText = $"1_{name}", // built-ins sort first
            };
        }
    }
}
