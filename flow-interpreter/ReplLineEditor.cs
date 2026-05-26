using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLsp;
using FlowLsp.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using PrettyPrompt;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;
using CompletionItem = PrettyPrompt.Completion.CompletionItem;
using LspCompletionItem = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem;

namespace FlowInterpreter;

/// <summary>
/// Phase 38 Plan 38-04 — REPL line-editing surface that wraps PrettyPrompt 4.1.1
/// (D-38-11 readline gate winner; MPL-2.0; .NET 6+) with a <see cref="FlowPromptCallbacks"/>
/// that routes Tab-completion through the in-process
/// <c>flow-lsp/Handlers/CompletionHandler.BuildItems()</c> static helper per
/// D-38-12 SIMPLIFICATION FINDING (RESEARCH §G lines 854-929). The 4 symbol
/// indices (BuiltInIndex / StdlibSymbolIndex / KeywordIndex / UserSymbolIndex)
/// are constructed ONCE at editor construction so each Tab does not pay the
/// cold-load cost.
///
/// Persistent history at <c>~/.config/flow/history</c> per UI-SPEC lines 295-302
/// (10k cap with rotation-on-append; mode 0600 on Linux/macOS). PrettyPrompt's
/// built-in Ctrl+R reverse history search reads this same file via the
/// <c>persistentHistoryFilepath</c> ctor parameter — composer keybinding
/// (Ctrl+R) is therefore covered without any custom wiring.
/// </summary>
public sealed class ReplLineEditor : IDisposable
{
    private const int HistoryCap = 10_000; // UI-SPEC line 299

    private readonly string _historyFilePath;
    private readonly Prompt _prompt;
    private readonly FlowPromptCallbacks _callbacks;
    private readonly ParseSession _parser;
    private readonly BuiltInIndex _builtIns;
    private readonly StdlibSymbolIndex _stdlib;
    private readonly KeywordIndex _keywords;
    private readonly UserSymbolIndex _users;
    private bool _disposed;

    /// <summary>
    /// Constructs the editor. Defaults <paramref name="historyFilePath"/> to
    /// <c>~/.config/flow/history</c> per UI-SPEC line 297. Pass an explicit
    /// path for tests that want to redirect to a temp file.
    /// </summary>
    public ReplLineEditor(
        string promptText = "> ",
        string continuationPrompt = "... ",
        string? historyFilePath = null)
    {
        _historyFilePath = historyFilePath ?? DefaultHistoryFilePath();
        EnsureHistoryDirectoryExists(_historyFilePath);

        // RESEARCH §G lines 871-901 — instantiate the 4 indices ONCE at ctor time.
        // RegisterSignaturesOnly is the audio-free registry sweep (D-07 full coverage,
        // stubs throw NotSupportedException on invocation but expose every signature).
        var registry = new InternalFunctionRegistry();
        BuiltInFunctions.RegisterSignaturesOnly(registry);
        _builtIns = new BuiltInIndex(registry);
        _parser = new ParseSession();
        _stdlib = new StdlibSymbolIndex(_parser);
        _keywords = new KeywordIndex();
        _users = new UserSymbolIndex();

        _callbacks = new FlowPromptCallbacks(this);

        var config = new PromptConfiguration(
            prompt: new FormattedString(promptText));
        _prompt = new Prompt(
            persistentHistoryFilepath: _historyFilePath,
            callbacks: _callbacks,
            configuration: config);
    }

    /// <summary>
    /// Reads one composer-submitted line (paren-balanced multi-line input is
    /// driven by PrettyPrompt's soft-newline transform; see
    /// <see cref="FlowPromptCallbacks.TransformKeyPressAsync"/>).
    /// Returns null on Ctrl+D / cancellation.
    /// </summary>
    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var result = await _prompt.ReadLineAsync().ConfigureAwait(false);
        if (!result.IsSuccess) return null;
        return result.Text;
    }

    /// <summary>
    /// Appends a submitted entry to the on-disk history file. Triggers a rotation
    /// when the file exceeds <see cref="HistoryCap"/> entries (keeps the most-recent
    /// 10k per UI-SPEC line 299). Mode 0600 set on Linux/macOS per line 300.
    /// </summary>
    public void AppendHistory(string entry)
    {
        if (string.IsNullOrEmpty(entry)) return;
        EnsureHistoryDirectoryExists(_historyFilePath);

        var serialised = entry.Replace("\n", "\\n"); // UI-SPEC line 298: literal \n escape
        File.AppendAllLines(_historyFilePath, new[] { serialised });
        ApplyUnixPermissions(_historyFilePath);

        // Rotate when over cap — keep most-recent HistoryCap entries.
        if (CountLines(_historyFilePath) > HistoryCap)
        {
            var allLines = File.ReadAllLines(_historyFilePath);
            var kept = allLines.Skip(allLines.Length - HistoryCap).ToArray();
            File.WriteAllLines(_historyFilePath, kept);
            ApplyUnixPermissions(_historyFilePath);
        }
    }

    /// <summary>
    /// Reads the on-disk history in MOST-RECENT-FIRST order. Returns an empty list
    /// when the file does not yet exist (cold-start REPL session).
    /// </summary>
    public IReadOnlyList<string> LoadHistory()
    {
        if (!File.Exists(_historyFilePath)) return Array.Empty<string>();
        var lines = File.ReadAllLines(_historyFilePath);
        // Most-recent-first — reverse the on-disk append order.
        Array.Reverse(lines);
        return lines.Select(l => l.Replace("\\n", "\n")).ToArray();
    }

    /// <summary>
    /// Test seam — exposes the underlying completion pipeline so xUnit can exercise
    /// it without spawning a real PrettyPrompt session. Mirrors what
    /// FlowPromptCallbacks.GetCompletionItemsAsync does internally.
    /// </summary>
    public async Task<IReadOnlyList<CompletionItem>> GetCompletionItemsForTesting(
        string text, int caret, CancellationToken ct)
    {
        return await _callbacks.GetCompletionItemsAsyncForTesting(text, caret, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // PrettyPrompt's Prompt does not implement IDisposable in 4.1.1; nothing to release.
    }

    private static string DefaultHistoryFilePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // UI-SPEC line 297 — XDG-compatible, matches Phase 30 ~/.config/flow/config.toml.
        return Path.Combine(home, ".config", "flow", "history");
    }

    private static void EnsureHistoryDirectoryExists(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static void ApplyUnixPermissions(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // Charitable per D-v1.5-05: history is best-effort; do not block REPL.
        }
    }

    private static int CountLines(string path)
    {
        int count = 0;
        using var sr = new StreamReader(path);
        while (sr.ReadLine() is not null) count++;
        return count;
    }

    /// <summary>
    /// FlowPromptCallbacks routes Tab through CompletionHandler.BuildItems and
    /// drives multi-line continuation via the lexer-based paren-balance check
    /// that the existing Repl.cs:182-208 path uses (preserved per UI-SPEC line
    /// 257). PrettyPrompt's Shift+Enter soft-newline is the alternate path —
    /// composer can use either ergonomic style.
    /// </summary>
    private sealed class FlowPromptCallbacks : PromptCallbacks
    {
        private readonly ReplLineEditor _editor;

        public FlowPromptCallbacks(ReplLineEditor editor)
        {
            _editor = editor;
        }

        protected override Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(
            string text, int caret, TextSpan spanToBeReplaced, CancellationToken ct)
        {
            return GetCompletionItemsAsyncForTesting(text, caret, ct);
        }

        /// <summary>
        /// Internal-but-public for the editor's test seam. Drives the same pipeline
        /// the production callback uses so the unit suite covers the real path.
        /// </summary>
        public Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsyncForTesting(
            string text, int caret, CancellationToken ct)
        {
            // RESEARCH §G lines 891-901 — parse the input line, build a synthetic URI,
            // delegate to the LSP completion handler. The parser handles partial input
            // charitably (FlowEngine soft-failure model per Phase 17 D-06); BuildItems
            // accepts a null AST and falls through to the token-heuristic merge.
            var parseResult = _editor._parser.Parse(text, "<repl>");
            var uri = DocumentUri.From("file:///<repl>");
            var position = new Position(line: 0, character: caret);

            var lspItems = FlowLsp.Handlers.CompletionHandler.BuildItems(
                uri, text, parseResult.Ast, parseResult.Tokens, position,
                _editor._builtIns, _editor._users, _editor._stdlib, _editor._keywords);

            var converted = lspItems.Select(i => ConvertLspToPretty(i)).ToList();
            return Task.FromResult<IReadOnlyList<CompletionItem>>(converted);
        }

        protected override Task<KeyPress> TransformKeyPressAsync(
            string text, int caret, KeyPress keyPress, CancellationToken ct)
        {
            // Enter on unbalanced input → soft newline (continuation) per UI-SPEC line 251.
            if (keyPress.ConsoleKeyInfo.Key == ConsoleKey.Enter
                && (keyPress.ConsoleKeyInfo.Modifiers & ConsoleModifiers.Shift) == 0)
            {
                if (!ReplInputCompleteness.IsInputComplete(text))
                {
                    // PrettyPrompt's soft-newline shortcut is Shift+Enter; transform the
                    // bare Enter into one so the multi-line buffer continues per
                    // Repl.cs:117-119 / 182-208 contract.
                    var transformed = new KeyPress(new ConsoleKeyInfo(
                        keyChar: keyPress.ConsoleKeyInfo.KeyChar,
                        key: ConsoleKey.Enter,
                        shift: true, alt: false, control: false));
                    return Task.FromResult(transformed);
                }
            }
            return Task.FromResult(keyPress);
        }

        private static CompletionItem ConvertLspToPretty(LspCompletionItem lspItem)
        {
            var replacement = string.IsNullOrEmpty(lspItem.InsertText)
                ? lspItem.Label
                : lspItem.InsertText;
            var displayText = lspItem.Label;
            var description = ExtractDescription(lspItem);
            return new CompletionItem(
                replacementText: replacement,
                displayText: new FormattedString(displayText),
                getExtendedDescription: _ => Task.FromResult(new FormattedString(description)),
                filterText: lspItem.FilterText ?? lspItem.Label);
        }

        private static string ExtractDescription(LspCompletionItem lspItem)
        {
            var detail = lspItem.Detail ?? string.Empty;
            var doc = lspItem.Documentation;
            string docStr = string.Empty;
            if (doc != null)
            {
                if (doc.HasString) docStr = doc.String ?? string.Empty;
                else if (doc.HasMarkupContent) docStr = doc.MarkupContent?.Value ?? string.Empty;
            }
            if (!string.IsNullOrEmpty(detail) && !string.IsNullOrEmpty(docStr))
                return $"{detail}\n\n{docStr}";
            return !string.IsNullOrEmpty(detail) ? detail : docStr;
        }
    }
}

/// <summary>
/// Lexer-based paren-balance completeness check extracted from
/// Repl.cs:182-208 so BOTH the legacy <c>Console.ReadLine</c> path AND the new
/// PrettyPrompt-driven path share a single implementation. Preserves backslash-
/// at-EOL detection (Repl.cs:117-119) for the secondary continuation surface.
///
/// Phase 38 Plan 38-04.
/// </summary>
public static class ReplInputCompleteness
{
    public static bool IsInputComplete(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return true;

        // Backslash-at-EOL continuation (Repl.cs:117-119 preservation per UI-SPEC line 256).
        if (input.TrimEnd().EndsWith("\\"))
            return false;

        // Internal procs have no body — single-line by construction.
        if (input.TrimStart().StartsWith("internal proc"))
            return true;

        // Tokenize via the real lexer and count block + proc nesting depth
        // (Repl.cs:182-208 preserved verbatim).
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(input, reporter, "<repl>");
        var tokens = lexer.Tokenize();

        int blockDepth = 0;
        int procDepth = 0;
        int parenDepth = 0;
        int bracketDepth = 0;
        foreach (var token in tokens)
        {
            if (token.Type == TokenType.LBrace) blockDepth++;
            else if (token.Type == TokenType.RBrace) blockDepth--;
            else if (token.Type == TokenType.Proc) procDepth++;
            else if (token.Type == TokenType.EndProc) procDepth--;
            else if (token.Type == TokenType.LParen) parenDepth++;
            else if (token.Type == TokenType.RParen) parenDepth--;
            else if (token.Type == TokenType.LBracket) bracketDepth++;
            else if (token.Type == TokenType.RBracket) bracketDepth--;
        }

        // Phase 38 Plan 38-04 — multi-line continuation honours brace AND paren AND
        // bracket nesting (S-expression call form `(add 1` and chord/song bracket
        // literals like `[intro verse` are common composer continuation points).
        return blockDepth <= 0 && procDepth <= 0 && parenDepth <= 0 && bracketDepth <= 0;
    }
}
