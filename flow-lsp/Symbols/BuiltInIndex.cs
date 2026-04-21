using System.Collections.Generic;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace FlowLsp.Symbols;

/// <summary>
/// Snapshot of every built-in function registered in an
/// <see cref="InternalFunctionRegistry"/>, indexed by name. Built ONCE at
/// server startup from a registry populated via
/// <see cref="BuiltInFunctions.RegisterSignaturesOnly"/> — this gives D-07
/// "every built-in" coverage (core + audio + transforms + harmony) without
/// constructing or invoking any audio backend.
///
/// Phase 17 (17-05). Consumed by CompletionHandler (17-05), HoverHandler
/// and SignatureHelpHandler (17-06).
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
