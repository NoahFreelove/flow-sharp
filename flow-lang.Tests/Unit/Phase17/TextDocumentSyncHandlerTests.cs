using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Diagnostics;
using FlowLsp;
using FlowLsp.Handlers;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 WR-02 regression Facts — TextDocumentSyncHandler close path.
///
/// Verifies that didClose fans out to (a) DocumentManager.Close, (b) a final
/// empty-diagnostics publish, AND (c) UserSymbolIndex.Remove. The symbol-index
/// remove was previously missing, leaking per-URI snapshots in long-running
/// sessions (WR-02).
/// </summary>
public class TextDocumentSyncHandlerTests
{
    private sealed class RecordingDiagnosticsPublisher : IDiagnosticsPublisher
    {
        public readonly List<(DocumentUri Uri, int ErrorCount)> Calls = new();

        public void Publish(DocumentUri uri, IReadOnlyList<FlowError> errors)
            => Calls.Add((uri, errors.Count));
    }

    private static (TextDocumentSyncHandler h, DocumentManager dm, RecordingDiagnosticsPublisher diag, UserSymbolIndex users)
        MakeHandler()
    {
        var diag = new RecordingDiagnosticsPublisher();
        var users = new UserSymbolIndex();
        // Simple DocumentManager: parse via the onParse callback wiring but we
        // do not drive the debounce in these Facts — only Close + accessor paths.
        var dm = new DocumentManager((uri, text, ct) => Task.CompletedTask);
        var h = new TextDocumentSyncHandler(dm, diag, users);
        return (h, dm, diag, users);
    }

    [Fact]
    public async Task CloseDocument_CallsUserSymbolIndexRemove()
    {
        var (h, dm, diag, users) = MakeHandler();
        var uri = DocumentUri.File("/wr02.flow");

        // Pre-seed the UserSymbolIndex as if a parse had completed.
        var parseResult = LspFixtures.Parse("proc myHelper ()\n  Int x = 1\nend proc");
        users.Update(uri, parseResult.Ast);
        Assert.NotEmpty(users.For(uri));

        // Drive didClose.
        var p = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri }
        };
        await h.Handle(p, CancellationToken.None);

        // Post-condition: symbol snapshot for this URI is GONE.
        Assert.Empty(users.For(uri));
    }

    [Fact]
    public async Task CloseDocument_PublishesEmptyDiagnostics()
    {
        var (h, _, diag, _) = MakeHandler();
        var uri = DocumentUri.File("/wr02-diag.flow");

        var p = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri }
        };
        await h.Handle(p, CancellationToken.None);

        // Exactly one publish, empty, for this uri.
        Assert.Single(diag.Calls);
        Assert.Equal(uri, diag.Calls[0].Uri);
        Assert.Equal(0, diag.Calls[0].ErrorCount);
    }

    [Fact]
    public async Task CloseDocument_UserSymbolIndexRemoveIsIdempotent()
    {
        // WR-02 guard: closing twice must not throw even if the second close
        // finds no entry to remove. Exercises UserSymbolIndex.Remove idempotence.
        var (h, _, _, users) = MakeHandler();
        var uri = DocumentUri.File("/wr02-idem.flow");

        var p = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri }
        };
        await h.Handle(p, CancellationToken.None);
        await h.Handle(p, CancellationToken.None); // second close — must not throw.

        Assert.Empty(users.For(uri));
    }

    [Fact]
    public async Task CloseDocument_OtherUriUsersUntouched()
    {
        // Scope check: Remove(a) must not affect Remove(b).
        var (h, _, _, users) = MakeHandler();
        var uriA = DocumentUri.File("/a.flow");
        var uriB = DocumentUri.File("/b.flow");

        var ast = LspFixtures.Parse("proc foo ()\nend proc").Ast;
        users.Update(uriA, ast);
        users.Update(uriB, ast);

        await h.Handle(new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uriA }
        }, CancellationToken.None);

        Assert.Empty(users.For(uriA));
        Assert.NotEmpty(users.For(uriB));
    }
}
