using FlowLang.Diagnostics;
using FlowLsp.Symbols;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace FlowLsp.Handlers;

/// <summary>
/// Wires LSP didOpen / didChange / didClose events into <see cref="DocumentManager"/>.
///
/// D-03: FULL text sync — no incremental parser. Each didChange receives the full
/// document text (last ContentChange). The DocumentManager debounces and eventually
/// fires the onParse callback Program.cs wires to <see cref="ParseSession"/>.
///
/// On didClose:
///   1. DocumentManager.Close cancels any pending debounced parse;
///   2. publish empty diagnostics to clear stale squiggles;
///   3. WR-02: UserSymbolIndex.Remove(uri) drops the per-URI symbol snapshot so
///      long-running sessions do not grow unbounded.
/// </summary>
public sealed class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly DocumentManager _docs;
    private readonly IDiagnosticsPublisher _diagnostics;
    private readonly UserSymbolIndex _users;

    private static readonly TextDocumentSelector Selector =
        TextDocumentSelector.ForLanguage("flow");

    public TextDocumentSyncHandler(
        DocumentManager docs,
        IDiagnosticsPublisher diagnostics,
        UserSymbolIndex users)
    {
        _docs = docs;
        _diagnostics = diagnostics;
        _users = users;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        => new(uri, "flow");

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken ct)
    {
        _docs.Update(request.TextDocument.Uri, request.TextDocument.Text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken ct)
    {
        // FULL sync per D-03 — take the last ContentChange's Text as the full buffer.
        var last = request.ContentChanges.LastOrDefault();
        if (last is not null)
            _docs.Update(request.TextDocument.Uri, last.Text);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri;
        _docs.Close(uri);
        // Clear diagnostics on close (empty publish — that is how LSP clears squiggles).
        _diagnostics.Publish(uri, Array.Empty<FlowError>());
        // WR-02: drop per-URI user symbol snapshot so long-running sessions do
        // not leak stale per-doc dictionary entries.
        _users.Remove(uri);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken ct)
        => Unit.Task; // No-op: re-parse is already covered by didChange.

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = Selector,
        Change = TextDocumentSyncKind.Full,
        Save = new SaveOptions { IncludeText = false }
    };
}
