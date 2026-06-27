using System.Threading;
using System.Threading.Tasks;
using FlowLang.StandardLibrary;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace FlowLsp.Handlers;

/// <summary>
/// Serves <c>textDocument/hover</c> for Flow buffers. 3-way lookup:
///   1. BuiltInIndex → signature(s) + BuiltInDocs summary (fallback signature-only if
///      the doc entry is missing).
///   2. UserSymbolIndex → symbol kind + name (declared type fidelity is tracked in
///      UserSymbolIndex as Kind per plan 17-05; richer per-proc-signature rendering
///      is a future refinement).
///   3. StdlibSymbolIndex → module-qualified proc signature.
///
/// All static helpers are public so the Fact suite can exercise them without the
/// OmniSharp transport (mirrors plan 17-05's CompletionHandler pattern).
/// Phase 17 (17-06).
/// </summary>
public sealed class HoverHandler : HoverHandlerBase
{
    private readonly DocumentManager _docs;
    private readonly BuiltInIndex _builtIns;
    private readonly UserSymbolIndex _users;
    private readonly StdlibSymbolIndex _stdlib;

    public HoverHandler(DocumentManager docs, BuiltInIndex builtIns,
        UserSymbolIndex users, StdlibSymbolIndex stdlib)
    {
        _docs = docs;
        _builtIns = builtIns;
        _users = users;
        _stdlib = stdlib;
    }

    /// <summary>
    /// 3-way lookup for hover content. Returns null when <paramref name="identifier"/>
    /// is null/empty or is not known to any index.
    /// </summary>
    public static Hover? BuildHover(string? identifier, BuiltInIndex builtIns,
        UserSymbolIndex users, StdlibSymbolIndex stdlib, DocumentUri uri)
    {
        if (string.IsNullOrEmpty(identifier)) return null;

        var b = builtIns.Find(identifier);
        if (b is not null)
        {
            var doc = BuiltInDocs.TryGet(identifier);
            // Phase 31 SPEC-3 (D-01/D-02): LSP-side renderer emits U+2026 for varargs.
            // FunctionSignature.ToString() still emits ASCII "..." for runtime use
            // (Phase 24 D-04 — zero flow-lang touch for LSP-only work).
            var signature = b.Signatures.Count > 0 ? LspMappings.FormatSignature(b.Signatures[0]) : identifier;
            var summary = doc?.Summary is { Length: > 0 } s ? s : "*(no documentation)*";
            var md = $"```flow\n{signature}\n```\n\n{summary}";
            return new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(
                    new MarkupContent { Kind = MarkupKind.Markdown, Value = md })
            };
        }

        var u = users.Find(uri, identifier);
        if (u is not null)
        {
            var md = $"```flow\n{u.Kind.ToString().ToLowerInvariant()} {u.Name}\n```\n\nUser-declared symbol in current document.";
            return new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(
                    new MarkupContent { Kind = MarkupKind.Markdown, Value = md })
            };
        }

        var sp = stdlib.Find(identifier);
        if (sp is not null)
        {
            var md = $"```flow\nproc {sp.Name}(...)\n```\n\nStandard library proc from `@{sp.Module}`.";
            return new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(
                    new MarkupContent { Kind = MarkupKind.Markdown, Value = md })
            };
        }

        return null;
    }

    /// <summary>
    /// Returns the contiguous identifier token surrounding the cursor, or null if the
    /// cursor sits on non-identifier characters. Walks left and right from the cursor
    /// column, collecting letters, digits, and underscores.
    /// </summary>
    public static string? IdentifierAt(string text, Position pos)
    {
        var lines = text.Split('\n');
        if (pos.Line >= lines.Length) return null;
        var line = lines[pos.Line];
        if (pos.Character > line.Length) return null;

        int start = pos.Character;
        while (start > 0 && IsIdentChar(line[start - 1])) start--;
        int end = pos.Character;
        while (end < line.Length && IsIdentChar(line[end])) end++;
        if (end <= start) return null;
        return line.Substring(start, end - start);
    }

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    public override Task<Hover?> Handle(HoverParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri;
        var text = _docs.GetText(uri) ?? string.Empty;
        var ident = IdentifierAt(text, request.Position);
        return Task.FromResult(BuildHover(ident, _builtIns, _users, _stdlib, uri));
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability, ClientCapabilities clientCapabilities)
        => new() { DocumentSelector = TextDocumentSelector.ForPattern("**/*.flow") };
}
