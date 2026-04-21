using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using FlowProgram = FlowLang.Ast.Program;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace FlowLsp.Handlers;

/// <summary>
/// Serves <c>textDocument/definition</c> for Flow buffers. Two resolution paths:
///   1. User symbols (procs, variables, sections) — walks the AST to find the
///      declaration Location, returns a Location pointing at it.
///   2. Stdlib imports (`use "@audio"`) — resolves the module name via
///      <see cref="ModuleLoader.ResolveStdlibPath"/> to the .flow file and returns
///      a Location at position 0,0.
/// Built-ins return null per D-09 (no user-level definition available for
/// C#-implemented built-ins).
///
/// Phase 17 (17-06).
/// </summary>
public sealed class DefinitionHandler : DefinitionHandlerBase
{
    private readonly DocumentManager _docs;
    private readonly ParseSession _parser;

    public DefinitionHandler(DocumentManager docs, ParseSession parser)
    {
        _docs = docs;
        _parser = parser;
    }

    /// <summary>
    /// Walks the AST looking for a ProcDeclaration, VariableDeclaration, or
    /// SectionDeclaration whose Name matches. Recurses into proc bodies, section
    /// bodies, and MusicalContextStatement bodies. Returns the 1-based source
    /// location (Line, Col) of the declaration, or null if not found.
    /// </summary>
    public static (int Line, int Col)? FindUserDeclaration(FlowProgram ast, string name)
    {
        (int, int)? found = null;
        Walk(ast.Statements);
        return found;

        void Walk(IReadOnlyList<Statement> stmts)
        {
            foreach (var s in stmts)
            {
                if (found is not null) return;
                switch (s)
                {
                    case ProcDeclaration pd when pd.Name == name:
                        found = (pd.Location.Line, pd.Location.Column);
                        return;
                    case VariableDeclaration vd when vd.Name == name:
                        found = (vd.Location.Line, vd.Location.Column);
                        return;
                    case SectionDeclaration sd when sd.Name == name:
                        found = (sd.Location.Line, sd.Location.Column);
                        return;
                    case ProcDeclaration pd:
                        Walk(pd.Body);
                        break;
                    case SectionDeclaration sd:
                        Walk(sd.Body);
                        break;
                    case MusicalContextStatement m:
                        Walk(m.Body);
                        break;
                }
            }
        }
    }

    public override Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri;
        var text = _docs.GetText(uri) ?? string.Empty;

        // Stdlib import jump: if the cursor sits inside a `use "@..."` string
        // literal on the current line, resolve to that stdlib file. WR-01 narrowed
        // the scan to require (a) the cursor is inside the `"@..."` token and
        // (b) a `use` keyword appears before the `"` on the same line — previously
        // any token on a line containing a `use "@..."` would jump to the stdlib.
        var lines = text.Split('\n');
        if (request.Position.Line < lines.Length)
        {
            var lineStr = lines[request.Position.Line];
            var atIdx = lineStr.IndexOf("\"@", System.StringComparison.Ordinal);
            if (atIdx >= 0)
            {
                var start = atIdx + 1;
                var end = lineStr.IndexOf('"', start + 1);
                var cursorCol = request.Position.Character;
                // Cursor must sit inside the `"@..."` span (inclusive of the
                // opening `"` and the position just after the closing `"`).
                var cursorInsideToken = end > start
                    && cursorCol >= atIdx
                    && cursorCol <= end + 1;
                // The prefix before the opening `"` must contain a `use` keyword
                // (word-boundary-sensitive) — a plain string literal like
                // `String s = "@notation"` should NOT trigger the stdlib jump.
                var prefix = atIdx > 0 ? lineStr.Substring(0, atIdx) : string.Empty;
                var hasUseBefore = HasUseKeywordBefore(prefix);
                if (cursorInsideToken && hasUseBefore)
                {
                    var mod = lineStr.Substring(start, end - start);
                    var resolved = ModuleLoader.ResolveStdlibPath(mod);
                    if (System.IO.File.Exists(resolved))
                    {
                        return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(
                            new Location
                            {
                                Uri = DocumentUri.File(resolved),
                                Range = new Range(new Position(0, 0), new Position(0, 0))
                            }));
                    }
                }
            }
        }

        var ident = HoverHandler.IdentifierAt(text, request.Position);
        if (string.IsNullOrEmpty(ident)) return Task.FromResult<LocationOrLocationLinks?>(null);

        var result = _parser.Parse(text, uri.GetFileSystemPath());
        var found = FindUserDeclaration(result.Ast, ident);
        if (found is null) return Task.FromResult<LocationOrLocationLinks?>(null);

        // Convert 1-based source location to 0-based LSP coordinates.
        var line = System.Math.Max(0, found.Value.Line - 1);
        var col = System.Math.Max(0, found.Value.Col - 1);
        return Task.FromResult<LocationOrLocationLinks?>(new LocationOrLocationLinks(
            new Location
            {
                Uri = uri,
                Range = new Range(new Position(line, col), new Position(line, col + ident.Length))
            }));
    }

    protected override DefinitionRegistrationOptions CreateRegistrationOptions(
        DefinitionCapability capability, ClientCapabilities clientCapabilities)
        => new() { DocumentSelector = TextDocumentSelector.ForLanguage("flow") };

    /// <summary>
    /// Returns true iff <paramref name="prefix"/> contains the keyword
    /// <c>use</c> surrounded by word boundaries (start-of-string or non-identifier
    /// char on either side). Rejects substrings like <c>misuse</c>, <c>abuser</c>.
    /// Exposed internally for the WR-01 Fact and reusable from the cursor-range
    /// gate in the main Handle path.
    /// </summary>
    public static bool HasUseKeywordBefore(string prefix)
    {
        int i = 0;
        while (true)
        {
            int idx = prefix.IndexOf("use", i, System.StringComparison.Ordinal);
            if (idx < 0) return false;
            bool leftOk = idx == 0 || !IsIdentChar(prefix[idx - 1]);
            int afterIdx = idx + 3;
            bool rightOk = afterIdx >= prefix.Length || !IsIdentChar(prefix[afterIdx]);
            if (leftOk && rightOk) return true;
            i = idx + 1;
        }

        static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
