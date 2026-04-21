using FlowLang.StandardLibrary;
using FlowLsp;
using FlowLsp.Handlers;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 plan 06 Task 2 Facts — HoverHandler pure-static helpers.
/// Exercises BuildHover (3-way lookup: BuiltIn → User → Stdlib) and
/// IdentifierAt (cursor-at-token extraction).
/// </summary>
public class HoverHandlerTests
{
    private static (BuiltInIndex bi, UserSymbolIndex ui, StdlibSymbolIndex si) Indices()
    {
        var reg = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(reg); // D-07 full coverage, audio-free
        return (new BuiltInIndex(reg), new UserSymbolIndex(), new StdlibSymbolIndex(new ParseSession()));
    }

    [Fact]
    public void BuiltIn_ShowsSignatureAndDoc()
    {
        var (bi, ui, si) = Indices();
        var hover = HoverHandler.BuildHover("print", bi, ui, si, DocumentUri.File("/t.flow"));
        Assert.NotNull(hover);
        Assert.NotNull(hover!.Contents.MarkupContent);
        Assert.Contains("print", hover.Contents.MarkupContent!.Value);
    }

    [Fact]
    public void UnknownIdentifier_ReturnsNull()
    {
        var (bi, ui, si) = Indices();
        Assert.Null(HoverHandler.BuildHover("xxyyzz", bi, ui, si, DocumentUri.File("/t.flow")));
    }

    [Fact]
    public void UserProc_AfterParse_ReturnsHover()
    {
        var (bi, ui, si) = Indices();
        var uri = DocumentUri.File("/t.flow");
        var r = LspFixtures.Parse("proc myProc ()\n  Int x = 5\nend proc");
        ui.Update(uri, r.Ast);
        var hover = HoverHandler.BuildHover("myProc", bi, ui, si, uri);
        Assert.NotNull(hover);
        Assert.NotNull(hover!.Contents.MarkupContent);
        Assert.Contains("myProc", hover.Contents.MarkupContent!.Value);
    }

    [Fact]
    public void IdentifierAt_ReturnsTokenAtCursor()
    {
        // "proc myHelper ()" — cursor on column 7 (inside "myHelper")
        var ident = HoverHandler.IdentifierAt("proc myHelper ()", new Position(0, 7));
        Assert.Equal("myHelper", ident);
    }

    [Fact]
    public void IdentifierAt_BetweenIdentifiers_ReturnsNullOrEmpty()
    {
        // "foo  bar" — cursor at column 4 (between the two spaces) — no identifier adjacent.
        var ident = HoverHandler.IdentifierAt("foo  bar", new Position(0, 4));
        // At column 4, line[3]=' ' (not ident) and line[4]=' ' (not ident). Returns null.
        Assert.True(string.IsNullOrEmpty(ident));
    }

    [Fact]
    public void EmptyIdentifier_ReturnsNull()
    {
        var (bi, ui, si) = Indices();
        Assert.Null(HoverHandler.BuildHover(null, bi, ui, si, DocumentUri.File("/t.flow")));
        Assert.Null(HoverHandler.BuildHover("", bi, ui, si, DocumentUri.File("/t.flow")));
    }
}
