using FlowLang.Lexing;
using FlowLsp.Semantic;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace FlowLsp.Handlers;

/// <summary>
/// Serves <c>textDocument/semanticTokens/full</c> for Flow buffers.
///
/// D-04 hybrid: this handler provides lexer-precise coloring that layers on
/// top of the TextMate grammar baseline (plan 17-02). The TM grammar paints
/// immediately at editor-open; this handler refines once the server emits
/// the semantic tokens response.
///
/// D-05 standard scopes only: legend entries come from
/// <see cref="SemanticTokenType"/> static properties (Keyword, Type, Variable,
/// Function, Macro, ...). No Flow-specific scopes are invented.
///
/// Thin wrapper pattern: all encoding logic lives in the pure
/// <see cref="SemanticTokensEncoder"/> (no OmniSharp dependencies), so the
/// encoding can be unit-tested without standing up an LSP transport. This
/// handler's sole responsibility is plumbing — fetch buffer text, parse,
/// push mapped tokens into OmniSharp's <see cref="SemanticTokensBuilder"/>.
/// </summary>
public sealed class SemanticTokensHandler : SemanticTokensHandlerBase
{
    private readonly DocumentManager _docs;
    private readonly ParseSession _parser;

    private static readonly TextDocumentSelector Selector =
        TextDocumentSelector.ForLanguage("flow");

    public SemanticTokensHandler(DocumentManager docs, ParseSession parser)
    {
        _docs = docs;
        _parser = parser;
    }

    /// <summary>
    /// OmniSharp calls this for <c>textDocument/semanticTokens/full</c> (and the
    /// delta/range endpoints fall through to full via the base class). Fetches
    /// the current buffer text from <see cref="DocumentManager"/>, re-parses to
    /// get a fresh token list, then Pushes each mapped token into the builder.
    ///
    /// Using <see cref="SemanticTokensBuilder.Push(int, int, int, SemanticTokenType?, System.Collections.Generic.IEnumerable{SemanticTokenModifier})"/>
    /// delegates the 5-tuple delta math to OmniSharp — functionally equivalent
    /// to calling <see cref="SemanticTokensEncoder.EncodeTokens"/> and emitting
    /// the ints directly, but keeps us on the supported OmniSharp API surface.
    /// </summary>
    protected override Task Tokenize(
        SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier,
        CancellationToken cancellationToken)
    {
        var uri = identifier.TextDocument.Uri;
        var text = _docs.GetText(uri) ?? string.Empty;
        var result = _parser.Parse(text, uri.GetFileSystemPath());
        foreach (var t in result.Tokens)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var idx = SemanticTokensEncoder.MapTokenType(t.Type);
            if (idx is null) continue;
            if (t.Text == null || t.Text.Length == 0) continue;
            int line = System.Math.Max(0, t.Location.Line - 1);
            int col = System.Math.Max(0, t.Location.Column - 1);
            // Cast the mods argument explicitly — without it, `null` is ambiguous
            // between the (SemanticTokenType?, params SemanticTokenModifier[]) and
            // (string, params string[]) overloads. Empty modifier array = no modifiers.
            builder.Push(
                line,
                col,
                t.Text.Length,
                SemanticTokensEncoder.Legend[idx.Value],
                System.Array.Empty<SemanticTokenModifier>());
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Per-URI cache stub. The base class consults this to decide between full
    /// and delta responses; returning a fresh document backed by our
    /// registration options tells it "always compute full." Delta support is
    /// a future refinement (RESEARCH §Pitfalls: delta caching would require
    /// persisting prior token arrays — for v1, full is cheap enough).
    /// </summary>
    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params,
        CancellationToken cancellationToken)
        => Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = Selector,
        Legend = new SemanticTokensLegend
        {
            TokenTypes = SemanticTokensEncoder.Legend,
            TokenModifiers = SemanticTokensEncoder.ModifierLegend,
        },
        Full = new SemanticTokensCapabilityRequestFull { Delta = false },
        Range = false,
    };
}
