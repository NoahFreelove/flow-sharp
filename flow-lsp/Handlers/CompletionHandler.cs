using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Lexing;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using FlowProgram = FlowLang.Ast.Program;

namespace FlowLsp.Handlers;

/// <summary>
/// Completion handler. Merges 5 sources:
///   1. BuiltInIndex — every built-in function (core + audio + transforms + harmony)
///      registered via <see cref="FlowLang.StandardLibrary.BuiltInFunctions.RegisterSignaturesOnly"/>
///      (D-07).
///   2. StdlibSymbolIndex — top-level procs in the 6 stdlib .flow modules.
///   3. UserSymbolIndex — procs/variables/sections in the current buffer.
///   4. KeywordIndex — Flow keywords + type names.
///   5. SnippetTemplates — 5 block-construct snippets (tempo, key, timesig, proc, section).
///
/// Gated by context:
/// - Inside a `use "..."` string literal → only stdlib path completions.
/// - Default → all 5 sources merged.
///
/// Note-stream context-aware completion (D-11) ships in plan 17-06; BuildItems
/// extends with more params there. This handler owns the use-string gating +
/// default merge only.
///
/// Phase 17 (17-05).
/// </summary>
public sealed class CompletionHandler : CompletionHandlerBase
{
    private readonly DocumentManager _docs;
    private readonly ParseSession _parser;
    private readonly BuiltInIndex _builtIns;
    private readonly UserSymbolIndex _users;
    private readonly StdlibSymbolIndex _stdlib;
    private readonly KeywordIndex _keywords;

    public CompletionHandler(
        DocumentManager docs,
        ParseSession parser,
        BuiltInIndex builtIns,
        UserSymbolIndex users,
        StdlibSymbolIndex stdlib,
        KeywordIndex keywords)
    {
        _docs = docs;
        _parser = parser;
        _builtIns = builtIns;
        _users = users;
        _stdlib = stdlib;
        _keywords = keywords;
    }

    /// <summary>
    /// 5 block-construct snippets — rendered as CompletionItem with InsertTextFormat.Snippet
    /// so VSCode expands placeholders (`${1:120}` etc.).
    /// </summary>
    public static IEnumerable<CompletionItem> SnippetTemplates()
    {
        yield return Snip("tempo", "tempo ${1:120} {\n\t$0\n}");
        yield return Snip("key", "key ${1:Cmajor} {\n\t$0\n}");
        yield return Snip("timesig", "timesig ${1:4}/${2:4} {\n\t$0\n}");
        yield return Snip("proc", "proc ${1:name} ()\n\t$0\nend proc");
        yield return Snip("section", "section ${1:name} {\n\t$0\n}");

        static CompletionItem Snip(string label, string body) => new()
        {
            Label = label,
            Kind = CompletionItemKind.Snippet,
            InsertText = body,
            InsertTextFormat = InsertTextFormat.Snippet,
            Detail = $"({label} block)",
            SortText = $"0_{label}", // snippets sort first in their trigger group
        };
    }

    /// <summary>
    /// Pure static inner — compute completion items for a given URI + cursor context.
    /// Exposed for tests so the L1 unit suite doesn't need an OmniSharp facade.
    ///
    /// Plan 17-06 extended the signature with <paramref name="ast"/> and
    /// <paramref name="tokens"/> so the note-stream branch (D-11) can walk the cached
    /// ParseResult.Tokens + AST to detect both "inside stream" (AST-based) and the
    /// enclosing key block (token-scan with brace-depth tracking).
    /// </summary>
    public static IEnumerable<CompletionItem> BuildItems(
        DocumentUri uri,
        string text,
        FlowProgram? ast,
        IReadOnlyList<Token>? tokens,
        Position cursor,
        BuiltInIndex builtIns,
        UserSymbolIndex users,
        StdlibSymbolIndex stdlib,
        KeywordIndex keywords)
    {
        // D-11 note-stream branch: context-aware completion INSIDE `| ... |`.
        // Replaces the default merge — we do NOT want proc/variable/keyword names
        // polluting note-stream completion per D-11.
        if (ast is not null
            && FlowLsp.NoteStream.NoteStreamContext.IsInsideNoteStream(ast, text, cursor))
        {
            // FindEnclosingKey needs the tokens; if for some reason they weren't provided
            // (e.g. a synthetic test path), fall back to null (no key detected).
            var key = tokens is not null
                ? FlowLsp.NoteStream.NoteStreamContext.FindEnclosingKey(ast, tokens, text, cursor)
                : null;
            return key is not null ? RomanNumeralItems(key) : DefaultNoteStreamItems();
        }

        if (IsInsideUseStringLiteral(text, cursor))
            return stdlib.UseStringPathItems();

        return builtIns.Items()
            .Concat(stdlib.Items())
            .Concat(users.CompletionsFor(uri))
            .Concat(keywords.Items())
            .Concat(SnippetTemplates());
    }

    /// <summary>
    /// Roman numeral completions inside a `key &lt;name&gt; { | ... | }` context.
    /// Major keys surface I/ii/iii/IV/V/V7/vi/vii°; minor keys surface i/ii°/III/iv/v/V7/VI/VII.
    /// Key mode is detected by case-insensitive "minor" substring in the key name.
    /// </summary>
    public static IEnumerable<CompletionItem> RomanNumeralItems(string keyName)
    {
        var isMinor = keyName.IndexOf("minor", StringComparison.OrdinalIgnoreCase) >= 0;
        var numerals = isMinor
            ? new[] { "i", "ii\u00b0", "III", "iv", "v", "V7", "VI", "VII" }
            : new[] { "I", "ii", "iii", "IV", "V", "V7", "vi", "vii\u00b0" };
        foreach (var n in numerals)
        {
            // Use HarmonyFunctions/ScaleDatabase to resolve Detail to the actual chord
            // symbol (e.g. "I" in Cmajor → "C"). Falls back to the plain key label if
            // the resolver returns null for a given numeral.
            string detail;
            try
            {
                var resolved = FlowLang.StandardLibrary.Harmony.ScaleDatabase.ResolveRomanNumeral(n, keyName);
                detail = resolved is not null
                    ? $"{resolved.Root}{resolved.Quality} (in {keyName})"
                    : $"Roman numeral in {keyName}";
            }
            catch
            {
                detail = $"Roman numeral in {keyName}";
            }
            yield return new CompletionItem
            {
                Label = n,
                Kind = CompletionItemKind.Constant,
                Detail = detail,
            };
        }
    }

    /// <summary>
    /// Default note-stream completions when NO key context is active: note letters,
    /// duration suffixes, and the rest character. Chord literals and octave-numbered
    /// notes (C4, D4) are surfaced as generic labels — users typically type these
    /// directly rather than via completion, but having them in the list aids discovery.
    /// </summary>
    public static IEnumerable<CompletionItem> DefaultNoteStreamItems()
    {
        // Note letters (both bare and with common octaves for discovery)
        foreach (var l in new[] { "C", "D", "E", "F", "G", "A", "B" })
        {
            yield return new CompletionItem { Label = l, Kind = CompletionItemKind.Variable, Detail = "Note letter" };
        }
        foreach (var l in new[] { "C4", "D4", "E4", "F4", "G4", "A4", "B4" })
        {
            yield return new CompletionItem { Label = l, Kind = CompletionItemKind.Variable, Detail = "Note (octave 4)" };
        }
        // Duration suffixes
        foreach (var (d, desc) in new[] {
            ("q","quarter"), ("h","half"), ("w","whole"), ("e","eighth"), ("s","sixteenth") })
        {
            yield return new CompletionItem { Label = d, Kind = CompletionItemKind.Value, Detail = desc };
        }
        // Rest
        yield return new CompletionItem { Label = "_", Kind = CompletionItemKind.Value, Detail = "Rest" };
    }

    /// <summary>
    /// True iff the cursor sits inside an unclosed `"..."` literal preceded by
    /// the <c>use</c> keyword on the same line. Simple scanner — exact enough
    /// for the common case where the user types <c>use "</c> and pauses.
    ///
    /// WR-04 fix: require a word boundary on BOTH sides of the matched
    /// <c>use</c> so substrings like <c>misuse</c>, <c>abuser</c>, <c>houses</c>
    /// do not trigger stdlib-path completion. Identifier chars on either side
    /// of the three-letter match disqualify it.
    /// </summary>
    public static bool IsInsideUseStringLiteral(string text, Position cursor)
    {
        var lines = text.Split('\n');
        if (cursor.Line >= lines.Length) return false;

        var line = lines[cursor.Line];
        if (cursor.Character > line.Length) return false;

        var prefix = line.Substring(0, cursor.Character);
        int useIdx = FindLastStandaloneUse(prefix);
        if (useIdx < 0) return false;

        var afterUse = prefix.Substring(useIdx);
        int quoteCount = 0;
        foreach (var c in afterUse)
        {
            if (c == '"') quoteCount++;
        }
        return quoteCount % 2 == 1; // odd → cursor inside an open string
    }

    /// <summary>
    /// Returns the rightmost index of the three-letter sequence <c>use</c> in
    /// <paramref name="prefix"/> where neither neighbor is an identifier char
    /// (letter, digit, or underscore). Returns <c>-1</c> when no standalone
    /// <c>use</c> keyword exists. Rejects <c>misuse</c>, <c>abuser</c>,
    /// <c>houses</c>, <c>used</c>, <c>x_use</c>.
    /// </summary>
    private static int FindLastStandaloneUse(string prefix)
    {
        int from = prefix.Length;
        while (true)
        {
            int idx = prefix.LastIndexOf("use", from - 1, StringComparison.Ordinal);
            if (idx < 0) return -1;
            bool leftOk = idx == 0 || !IsIdentChar(prefix[idx - 1]);
            int afterIdx = idx + 3;
            bool rightOk = afterIdx >= prefix.Length || !IsIdentChar(prefix[afterIdx]);
            if (leftOk && rightOk) return idx;
            from = idx;
            if (from <= 0) return -1;
        }

        static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }

    public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var uri = request.TextDocument.Uri;
        var text = _docs.GetText(uri) ?? "";
        // Per-request re-parse — v1 correctness over completion cache optimization.
        // A future DocumentManager per-URI ParseResult cache could avoid this (tracked
        // in 17-06 SUMMARY as a candidate optimization, not required for correctness).
        var result = _parser.Parse(text, uri.GetFileSystemPath());
        var items = BuildItems(uri, text, result.Ast, result.Tokens, request.Position,
            _builtIns, _users, _stdlib, _keywords);
        var list = new CompletionList(items.ToArray(), isIncomplete: false);
        return Task.FromResult(list);
    }

    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        => Task.FromResult(request);

    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("flow"),
            TriggerCharacters = new Container<string>(".", "@", "\"", " "),
            ResolveProvider = false,
        };
}
