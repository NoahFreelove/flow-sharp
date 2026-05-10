using System.Collections.Generic;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.Symbols;

/// <summary>
/// Per-URI snapshot of user-declared top-level symbols (procs, variables, sections).
/// Rebuilt on every successful parse via the <see cref="DocumentManager"/> onParse
/// callback — contrast with <see cref="StdlibSymbolIndex"/> which is built once at
/// startup.
///
/// Walks the AST only; does NOT execute the interpreter (D-01, RESEARCH
/// §Anti-Patterns). Recurses into proc bodies and section bodies to surface nested
/// declarations, and traverses MusicalContextStatement bodies.
///
/// Phase 17 (17-05). Consumed by CompletionHandler (17-05) and DefinitionHandler
/// (17-06).
/// </summary>
public sealed class UserSymbolIndex
{
    public enum SymbolKind { Proc, Variable, Section, Import }

    public sealed record Symbol(string Name, SymbolKind Kind);

    private readonly object _lock = new();
    private readonly Dictionary<DocumentUri, IReadOnlyList<Symbol>> _perDoc = new();

    /// <summary>Replaces the symbol snapshot for <paramref name="uri"/> from a fresh AST.</summary>
    public void Update(DocumentUri uri, FlowProgram ast)
    {
        var list = new List<Symbol>();
        Walk(ast.Statements, list);
        lock (_lock)
        {
            _perDoc[uri] = list;
        }
    }

    /// <summary>Clears the snapshot for <paramref name="uri"/> (call on didClose).</summary>
    public void Remove(DocumentUri uri)
    {
        lock (_lock)
        {
            _perDoc.Remove(uri);
        }
    }

    public IReadOnlyList<Symbol> For(DocumentUri uri)
    {
        lock (_lock)
        {
            return _perDoc.TryGetValue(uri, out var list)
                ? list
                : System.Array.Empty<Symbol>();
        }
    }

    public Symbol? Find(DocumentUri uri, string name)
    {
        foreach (var s in For(uri))
        {
            if (s.Name == name) return s;
        }
        return null;
    }

    public IEnumerable<CompletionItem> CompletionsFor(DocumentUri uri)
    {
        foreach (var s in For(uri))
        {
            yield return new CompletionItem
            {
                Label = s.Name,
                Kind = s.Kind switch
                {
                    SymbolKind.Proc => CompletionItemKind.Function,
                    SymbolKind.Variable => CompletionItemKind.Variable,
                    SymbolKind.Section => CompletionItemKind.Module,
                    SymbolKind.Import => CompletionItemKind.Module,
                    _ => CompletionItemKind.Variable,
                },
                SortText = $"3_{s.Name}", // user symbols after builtins + stdlib
            };
        }
    }

    private static void Walk(IReadOnlyList<Statement> stmts, List<Symbol> sink)
    {
        foreach (var s in stmts)
        {
            switch (s)
            {
                case ProcDeclaration pd:
                    sink.Add(new Symbol(pd.Name, SymbolKind.Proc));
                    Walk(pd.Body, sink);
                    break;
                case VariableDeclaration vd:
                    sink.Add(new Symbol(vd.Name, SymbolKind.Variable));
                    break;
                case SectionDeclaration sd:
                    sink.Add(new Symbol(sd.Name, SymbolKind.Section));
                    Walk(sd.Body, sink);
                    break;
                case MusicalContextStatement m:
                    Walk(m.Body, sink);
                    break;
                case ImportStatement:
                    // Import names themselves come via StdlibSymbolIndex — skip here.
                    break;
            }
        }
    }
}
