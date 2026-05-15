using System.Collections.Generic;
using System.IO;
using FlowLang.Ast.Statements;
using FlowLang.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace FlowLsp.Symbols;

/// <summary>
/// Snapshot of every top-level <see cref="ProcDeclaration"/> in the 6 stdlib
/// modules: @std, @audio, @collections, @bars, @notation, @composition.
/// Built ONCE at startup via <see cref="ParseSession"/> — stdlib files don't
/// change during an LSP session, so per-keystroke reparsing is wasted work.
///
/// Phase 17 (17-05). Resolves module paths via
/// <see cref="ModuleLoader.ResolveStdlibPath"/> (the public helper added in
/// plan 17-05 Task 1).
/// </summary>
public sealed class StdlibSymbolIndex
{
    /// <summary>
    /// The 6 stdlib module names per D-07 (matches std.flow's `use "@collections"` +
    /// `use "@bars"` plus the feature-specific modules loaded by the REPL and by
    /// convention user scripts).
    /// </summary>
    public static readonly string[] ModuleNames = new[]
        { "std", "audio", "collections", "bars", "notation", "composition" };

    public sealed record StdProc(string Name, string Module, string FilePath);

    private readonly Dictionary<string, StdProc> _byName = new();

    public StdlibSymbolIndex(ParseSession parser)
    {
        foreach (var mod in ModuleNames)
        {
            var path = ModuleLoader.ResolveStdlibPath(mod);
            if (!File.Exists(path))
                continue; // Pitfall 6 — file must ship beside the binary (CopyToOutputDirectory).

            string source;
            try
            {
                source = File.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            var result = parser.Parse(source, path);
            foreach (var stmt in result.Ast.Statements)
            {
                if (stmt is ProcDeclaration pd && !_byName.ContainsKey(pd.Name))
                {
                    _byName[pd.Name] = new StdProc(pd.Name, mod, path);
                }
            }
        }
    }

    public StdProc? Find(string name) =>
        _byName.TryGetValue(name, out var p) ? p : null;

    /// <summary>
    /// Returns every stdlib proc declared in <paramref name="moduleName"/>
    /// (e.g. "harmony", "audio", "std"). Phase 31 reverse-lookup helper consumed
    /// by <c>UnusedImportAnalyzer</c> (Plan 31-02) to determine whether a
    /// <c>use "@harmony"</c> actually has any referenced procs, and by
    /// <c>CompletionHandler.FilterByImports</c> (Plan 31-04) to drop suggestions
    /// from non-imported modules. Linear walk over the ~100-entry stdlib proc
    /// table — bounded; no caching needed.
    /// </summary>
    public IEnumerable<StdProc> ProcsForModule(string moduleName)
    {
        foreach (var p in _byName.Values)
        {
            if (p.Module == moduleName)
                yield return p;
        }
    }

    /// <summary>
    /// CompletionItems for every discovered stdlib proc. Used in the default
    /// completion merge alongside BuiltInIndex.Items(), UserSymbolIndex.CompletionsFor(uri),
    /// KeywordIndex.Items(), and the snippet templates.
    /// </summary>
    public IEnumerable<CompletionItem> Items()
    {
        foreach (var p in _byName.Values)
        {
            yield return new CompletionItem
            {
                Label = p.Name,
                Kind = CompletionItemKind.Function,
                Detail = $"(stdlib: @{p.Module})",
                SortText = $"2_{p.Name}", // stdlib after builtins, before user + keywords
            };
        }
    }

    /// <summary>
    /// CompletionItems for the 6 module path strings — returned when the cursor is
    /// inside a `use "..."` literal. These completions are DISTINCT from Items()
    /// (no built-ins, no user symbols, no keywords leak in).
    /// </summary>
    public IEnumerable<CompletionItem> UseStringPathItems()
    {
        foreach (var mod in ModuleNames)
        {
            yield return new CompletionItem
            {
                Label = $"@{mod}",
                Kind = CompletionItemKind.Module,
                InsertText = $"@{mod}",
                Detail = "Flow standard library module",
            };
        }
    }
}
