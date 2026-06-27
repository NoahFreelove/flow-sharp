using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Harmony;
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

        // Quick 260610-gl4 Findings 5 + 6 — single-owner history persistence.
        // PrettyPrompt persists its own base64-per-line history to this same file
        // (via persistentHistoryFilepath) and our old Repl loop ALSO manually
        // appended plaintext lines, so the file ended up with INTERLEAVED base64 +
        // plaintext pairs. PrettyPrompt's loader keeps only lines that base64-decode,
        // so the plaintext pollution (and any plaintext line that happens to be valid
        // base64) corrupted the loaded history — killing Ctrl+R reverse-search and
        // up-arrow recall. Repl.cs no longer manual-appends; PrettyPrompt is the sole
        // writer. Here we sanitize a pre-existing mixed/corrupt file BEFORE PrettyPrompt
        // reads it: back it up and strip every line that does not cleanly base64-decode.
        SanitizeHistoryFile(_historyFilePath);

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
    /// Appends a submitted entry to the on-disk history file. Quick 260610-gl4: the
    /// on-disk format is now ONE base64-encoded UTF-8 line per entry, byte-compatible
    /// with PrettyPrompt's <c>SavePersistentHistoryAsync</c> so the file stays
    /// single-format and PrettyPrompt's Ctrl+R reverse-search can read every line.
    /// (The production REPL loop no longer calls this — PrettyPrompt auto-saves on
    /// submit; it remains as a tested utility + a manual-import surface.) Triggers a
    /// rotation when the file exceeds <see cref="HistoryCap"/> entries (keeps the
    /// most-recent 10k per UI-SPEC line 299). Mode 0600 set on Linux/macOS per line 300.
    /// </summary>
    public void AppendHistory(string entry)
    {
        if (string.IsNullOrEmpty(entry)) return;
        EnsureHistoryDirectoryExists(_historyFilePath);

        File.AppendAllLines(_historyFilePath, new[] { Base64Encode(entry) });
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
    /// Reads the on-disk history in MOST-RECENT-FIRST order. Quick 260610-gl4: lines
    /// are base64-decoded (PrettyPrompt's format). Any line that does not cleanly
    /// decode is skipped charitably (D-v1.5-05) — a corrupt entry never throws. Returns
    /// an empty list when the file does not yet exist (cold-start REPL session).
    /// </summary>
    public IReadOnlyList<string> LoadHistory()
    {
        if (!File.Exists(_historyFilePath)) return Array.Empty<string>();
        var lines = File.ReadAllLines(_historyFilePath);
        // Most-recent-first — reverse the on-disk append order.
        Array.Reverse(lines);
        var result = new List<string>(lines.Length);
        foreach (var l in lines)
        {
            if (TryBase64Decode(l, out var decoded))
                result.Add(decoded);
        }
        return result;
    }

    /// <summary>
    /// Quick 260610-gl4 Findings 5 + 6 — on startup, repair a history file that mixes
    /// PrettyPrompt base64 lines with the legacy manual-append plaintext lines (or is
    /// otherwise corrupt). If EVERY non-empty line already base64-decodes, the file is
    /// clean and left untouched (no spurious backups). Otherwise the original is copied
    /// to <c>&lt;path&gt;.corrupt-&lt;timestamp&gt;.bak</c> and the file is rewritten with only
    /// the lines that cleanly decode — preserving real history while dropping the
    /// plaintext pollution that was poisoning PrettyPrompt's loader. Fully charitable:
    /// any IO failure is swallowed (the REPL must never refuse to start over history).
    /// </summary>
    internal static void SanitizeHistoryFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path);

            bool allClean = true;
            var kept = new List<string>(lines.Length);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) { allClean = false; continue; }
                if (TryBase64Decode(line, out _))
                    kept.Add(line);
                else
                    allClean = false;
            }

            if (allClean) return; // nothing to repair

            // Back up the original mixed/corrupt file before rewriting.
            var backup = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak";
            try { File.Copy(path, backup, overwrite: false); } catch { /* best-effort */ }

            File.WriteAllLines(path, kept);
            ApplyUnixPermissions(path);
        }
        catch
        {
            // Charitable per D-v1.5-05 — a history repair failure must not block REPL startup.
        }
    }

    private static string Base64Encode(string s) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s));

    /// <summary>
    /// Mirrors PrettyPrompt's internal TryBase64Decode: a line is "clean" only if it
    /// base64-decodes AND round-trips back to the same string (rejects coincidental
    /// base64-shaped plaintext like a bare 4-char token).
    /// </summary>
    private static bool TryBase64Decode(string line, out string decoded)
    {
        decoded = string.Empty;
        if (string.IsNullOrEmpty(line)) return false;
        try
        {
            var bytes = Convert.FromBase64String(line);
            decoded = System.Text.Encoding.UTF8.GetString(bytes);
            // Round-trip guard — re-encoding must reproduce the exact on-disk line.
            return Convert.ToBase64String(bytes) == line;
        }
        catch
        {
            return false;
        }
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

    /// <summary>
    /// Quick 260610-gl4 test seam — exposes the <c>ShouldOpenCompletionWindowAsync</c>
    /// override (Findings 1 + 3) so xUnit can pin the auto-open trigger without a TTY.
    /// </summary>
    public bool ShouldOpenCompletionWindowForTesting(string text, int caret)
        => _callbacks.ShouldOpenCompletionWindowForTesting(text, caret);

    /// <summary>
    /// Quick 260610-gl4 test seam — exposes the <c>GetSpanToReplaceByCompletionAsync</c>
    /// override (Finding 2) as a (start, length) pair so xUnit can pin the span widening
    /// across a leading <c>@</c> without a TTY.
    /// </summary>
    public (int Start, int Length) GetSpanToReplaceForTesting(string text, int caret)
        => _callbacks.GetSpanToReplaceForTesting(text, caret);

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
    /// Quick 260610-gl4 Finding 5 — pure reverse-search over the on-disk history.
    /// PrettyPrompt 4.1.1 has NO Ctrl+R reverse-search keybinding (its history
    /// navigation is Up/DownArrow only; the completion trigger is Ctrl+Space), so the
    /// composer's Ctrl+R genuinely did nothing. We wire Ctrl+R ourselves (see
    /// <see cref="FlowPromptCallbacks.GetKeyPressCallbacks"/>); this helper does the
    /// matching. Most-recent-first, case-insensitive substring, returns the first
    /// entry containing <paramref name="query"/>, or null when none match / query is
    /// empty. Extracted as a pure static so xUnit can pin it without a TTY.
    /// </summary>
    public string? ReverseSearchHistory(string query)
    {
        if (string.IsNullOrEmpty(query)) return null;
        foreach (var entry in LoadHistory()) // already most-recent-first
        {
            if (entry.Contains(query, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
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

        /// <summary>
        /// Quick 260610-gl4 Finding 5 — register Ctrl+R reverse history search.
        /// PrettyPrompt 4.1.1 ships NO reverse-search binding, so we add one here via
        /// the documented key-press-callback surface. On Ctrl+R we render an
        /// incremental <c>(reverse-i-search)`query':</c> prompt and read the search
        /// term key-by-key (PrettyPrompt has handed us control), matching against the
        /// on-disk history most-recent-first. Enter submits the matched entry; Escape /
        /// Ctrl+C / Ctrl+G aborts (returns null → composer stays on the current line).
        /// Charitable: any failure aborts the search rather than crashing the REPL.
        /// </summary>
        protected override IEnumerable<(KeyPressPattern Pattern, KeyPressCallbackAsync Callback)> GetKeyPressCallbacks()
        {
            yield return (
                new KeyPressPattern(ConsoleModifiers.Control, ConsoleKey.R),
                (text, caret, ct) => Task.FromResult<KeyPressCallbackResult?>(RunReverseSearch()));
        }

        /// <summary>
        /// Interactive reverse-i-search loop. Returns a non-null
        /// <see cref="KeyPressCallbackResult"/> (which PrettyPrompt SUBMITS) when the
        /// composer accepts a match with Enter, or null to abort. Writes the prompt +
        /// running match directly to the console — PrettyPrompt is paused inside the
        /// callback so direct Console IO is safe.
        /// </summary>
        private KeyPressCallbackResult? RunReverseSearch()
        {
            try
            {
                var query = new System.Text.StringBuilder();
                string? match = null;
                while (true)
                {
                    // (reverse-i-search)`query': match
                    Console.Write($"\r\x1b[K(reverse-i-search)`{query}': {match ?? string.Empty}");
                    var key = Console.ReadKey(intercept: true);

                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.Write("\r\x1b[K");
                        return match is not null ? new KeyPressCallbackResult(match, null) : null;
                    }
                    if (key.Key == ConsoleKey.Escape
                        || (key.Modifiers.HasFlag(ConsoleModifiers.Control)
                            && (key.Key == ConsoleKey.C || key.Key == ConsoleKey.G)))
                    {
                        Console.Write("\r\x1b[K");
                        return null;
                    }
                    if (key.Key == ConsoleKey.Backspace)
                    {
                        if (query.Length > 0) query.Remove(query.Length - 1, 1);
                    }
                    else if (!char.IsControl(key.KeyChar))
                    {
                        query.Append(key.KeyChar);
                    }
                    match = _editor.ReverseSearchHistory(query.ToString());
                }
            }
            catch
            {
                // Charitable — abort the search, never crash the session.
                try { Console.Write("\r\x1b[K"); } catch { }
                return null;
            }
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
            // Audit 0609 §5.1 fix: the caret is a 0-based offset into the FULL
            // (possibly multi-line) buffer, but the LSP CompletionHandler indexes
            // the cursor as `lines[Position.Line]` + a per-line `Substring(0,
            // Position.Character)` (CompletionHandler.cs:387-393). Hard-coding
            // line:0 fed the whole-buffer offset as a first-line character →
            // wrong line / out-of-range character for any continuation line
            // (`use "` and note-stream context detection silently mis-fired on
            // multi-line input). Convert the offset to a real (line, character)
            // pair instead.
            var position = ReplCaretPosition.CaretToPosition(text, caret);

            var lspItems = FlowLsp.Handlers.CompletionHandler.BuildItems(
                uri, text, parseResult.Ast, parseResult.Tokens, position,
                _editor._builtIns, _editor._users, _editor._stdlib, _editor._keywords);

            var converted = lspItems.Select(i => ConvertLspToPretty(i)).ToList();
            return Task.FromResult<IReadOnlyList<CompletionItem>>(converted);
        }

        /// <summary>
        /// Quick 260610-gl4 Findings 1 + 3 — controls when the completion window
        /// auto-opens (Tab always force-opens via CompletionPane regardless of this).
        ///
        /// PrettyPrompt's stock heuristic only auto-opens after <c>(</c> / <c>.</c>, or
        /// a letter that immediately follows whitespace, or a single non-space char at
        /// caret==1. On a fresh first prompt line the heuristic mis-fires (composer saw
        /// "completion dead on the first line"), and after accepting a completion +
        /// pressing Backspace it never reopened (Finding 3). We widen the trigger:
        /// open whenever the char to the LEFT of the caret is something a Flow
        /// identifier/path can grow from — a letter/digit/underscore, <c>(</c>,
        /// <c>.</c>, <c>@</c> (stdlib module path), or a <c>"</c> that opens a
        /// <c>use "</c> string. Backspace is included so deleting a char reopens the
        /// list against the now-shorter prefix. Charitable: never throws; closed
        /// (returns false) only when there is genuinely no identifier context.
        /// </summary>
        protected override Task<bool> ShouldOpenCompletionWindowAsync(
            string text, int caret, KeyPress keyPress, CancellationToken ct)
        {
            if (caret <= 0 || caret > text.Length)
                return Task.FromResult(false);

            // Quick 260610-gl4 — never auto-open completion on a REPL meta-command line
            // (`:help`, `:quit`, `:strict on`, ...). Otherwise the completion window
            // intercepts Enter to COMMIT an item instead of submitting the command, so
            // `:help createSineTone` never executes. Detect the leading ':' on the
            // current (last) physical line of the buffer.
            int lineStart = text.LastIndexOf('\n', caret - 1) + 1;
            if (lineStart < text.Length && text[lineStart] == ':')
                return Task.FromResult(false);

            char left = text[caret - 1];

            // Identifier growth — letters/digits/underscore, plus the punctuation that
            // begins a completable token in Flow.
            bool openable =
                char.IsLetterOrDigit(left) || left == '_' ||
                left == '(' || left == '.' || left == '@';

            // Inside an open `use "..."` string the next char the composer types is a
            // module path — open the window so @std/@audio/... surface immediately,
            // including right after the opening quote (left == '"').
            if (!openable)
            {
                var pos = ReplCaretPosition.CaretToPosition(text, caret);
                if (FlowLsp.Handlers.CompletionHandler.IsInsideUseStringLiteral(text, pos))
                    openable = true;
            }

            return Task.FromResult(openable);
        }

        /// <summary>
        /// Quick 260610-gl4 Finding 2 — determines the span the accepted completion
        /// REPLACES, which PrettyPrompt also uses to FILTER the candidate list against
        /// what the composer has typed. PrettyPrompt's stock implementation only walks
        /// over <c>[A-Za-z0-9_]</c>, so after <c>use "@aud</c> the span is just
        /// <c>aud</c> — and the module-path completion items (ReplacementText
        /// <c>@audio</c>) do NOT start with <c>aud</c>, so the live window showed
        /// NOTHING even though the in-process unit test (which calls BuildItems
        /// directly, bypassing this span/filter layer) passed. We extend the span
        /// LEFT across a leading <c>@</c> so the module path matches and replaces
        /// cleanly. The trailing edge keeps the stock word-char walk.
        /// </summary>
        protected override Task<TextSpan> GetSpanToReplaceByCompletionAsync(
            string text, int caret, CancellationToken ct)
        {
            static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

            int start = caret;
            while (start > 0 && IsWordChar(text[start - 1]))
                start--;
            // Swallow a leading '@' so `@audio` matches a typed `@aud` (stdlib module
            // path completion). Only the single sigil — the opening `use "` quote is
            // NOT part of the replaced span (the inserted @module sits after it).
            if (start > 0 && text[start - 1] == '@')
                start--;

            int end = caret;
            while (end < text.Length && IsWordChar(text[end]))
                end++;

            return Task.FromResult(TextSpan.FromBounds(start, end));
        }

        /// <summary>Test seam mirroring <see cref="ShouldOpenCompletionWindowAsync"/>.</summary>
        public bool ShouldOpenCompletionWindowForTesting(string text, int caret)
            => ShouldOpenCompletionWindowAsync(
                   text, caret,
                   new KeyPress(new ConsoleKeyInfo('\0', ConsoleKey.NoName, false, false, false)),
                   CancellationToken.None)
               .GetAwaiter().GetResult();

        /// <summary>Test seam mirroring <see cref="GetSpanToReplaceByCompletionAsync"/>.</summary>
        public (int Start, int Length) GetSpanToReplaceForTesting(string text, int caret)
        {
            var span = GetSpanToReplaceByCompletionAsync(text, caret, CancellationToken.None)
                .GetAwaiter().GetResult();
            return (span.Start, span.Length);
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
        // Note-stream state. The earlier sweep-0614 `pipeCount % 2 == 0` parity check
        // was WRONG: the lexer emits one TokenType.Pipe per `|` INCLUDING bar
        // separators, so a single-line N-bar stream `| bar1 | ... | barN |` carries
        // N+1 pipes. A 4-bar stream is 5 pipes = odd = wrongly judged "incomplete" →
        // the REPL froze forever in continuation mode. (It also false-positived the
        // other way: `| C4 | D4` is 2 pipes = even = wrongly "complete".) This scan
        // mirrors Parser.NoteStream.IsEndOfNoteStream(): a pipe seen while not in a
        // stream OPENS it; a pipe seen while in a stream is the CLOSING pipe unless
        // the next token can continue a note stream (then it is a bar separator). A
        // still-open stream at end of buffer means the composer is mid-stream →
        // request continuation.
        bool inStream = false;
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Type == TokenType.LBrace) blockDepth++;
            else if (token.Type == TokenType.RBrace) blockDepth--;
            else if (token.Type == TokenType.Proc) procDepth++;
            else if (token.Type == TokenType.EndProc) procDepth--;
            else if (token.Type == TokenType.LParen) parenDepth++;
            else if (token.Type == TokenType.RParen) parenDepth--;
            else if (token.Type == TokenType.LBracket) bracketDepth++;
            else if (token.Type == TokenType.RBracket) bracketDepth--;
            else if (token.Type == TokenType.Pipe)
            {
                if (!inStream)
                {
                    // Opening pipe.
                    inStream = true;
                }
                else if (i + 1 < tokens.Count)
                {
                    // Inside a stream with a following token: bar separator if the
                    // next token continues the stream, otherwise the closing pipe.
                    if (!ContinuesNoteStream(tokens[i + 1]))
                        inStream = false;
                }
                // else: pipe is the very last token — ambiguous mid-typing, leave
                // inStream = true so the REPL requests continuation.
            }
        }

        // Phase 38 Plan 38-04 — multi-line continuation honours brace AND proc AND
        // paren AND bracket nesting (S-expression call form `(add 1` and chord/song
        // bracket literals like `[intro verse` are common composer continuation
        // points). A still-open note stream (inStream) also requests continuation.
        return blockDepth <= 0 && procDepth <= 0 && parenDepth <= 0
               && bracketDepth <= 0 && !inStream;
    }

    /// <summary>
    /// Returns true when <paramref name="next"/> — the token immediately AFTER a pipe
    /// inside a note stream — can continue that stream (so the pipe is a bar
    /// separator, not the closing pipe). Mirrors the INVERSE of
    /// Parser.NoteStream.IsEndOfNoteStream() (Parser.NoteStream.cs:556): a token that
    /// would NOT end the stream means the stream continues. TryParseDynamicMarking is
    /// parser-private and intentionally omitted — the duration-letter set (c) and the
    /// lowercase-identifier rule (d) already cover the common dynamic-marked
    /// (`p`/`mp`/`mf`/`f`/`ff`) and single-letter overlaps without a parser-private
    /// dependency.
    /// </summary>
    private static bool ContinuesNoteStream(Token next)
    {
        var type = next.Type;
        if (type is TokenType.NoteLiteral or TokenType.Underscore
            or TokenType.LBracket or TokenType.Pipe or TokenType.ChordLiteral
            or TokenType.LParen or TokenType.GreaterThan or TokenType.LBrace)
            return true;

        if (type == TokenType.Identifier)
        {
            var text = next.Text;
            // (a) roman numeral inside the stream (`| I IV V |`).
            if (ScaleDatabase.IsRomanNumeral(text))
                return true;
            // (b) articulation mark.
            if (text is "stacc" or "ten" or "marc" or "leg" or "cresc" or "decresc")
                return true;
            // (c) duration letter.
            if (text is "w" or "h" or "q" or "e" or "s" or "t")
                return true;
            // (d) lowercase-initial identifier (variable ref) that is NOT one of the
            // duration letters already covered by (c).
            if (text.Length > 0 && char.IsLower(text[0])
                && text is not ("w" or "h" or "q" or "e" or "s" or "t"))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Audit 0609 §5.1 — pure offset→(line, character) conversion for the REPL
/// completion callback. PrettyPrompt hands <c>GetCompletionItemsAsync</c> a caret
/// that is a 0-based offset into the FULL (possibly multi-line) input buffer, but
/// the LSP <c>CompletionHandler</c> consumes an LSP-style <see cref="Position"/>
/// — a 0-based (line, character-within-line) pair it indexes as
/// <c>lines[Position.Line].Substring(0, Position.Character)</c>
/// (CompletionHandler.cs:387-393). The previous wiring passed
/// <c>new Position(line: 0, character: caret)</c>, which is only correct for
/// single-line input; on a continuation line the whole-buffer offset overran the
/// first physical line and the <c>use "</c> / note-stream context detection
/// silently mis-fired.
///
/// Newlines are <c>'\n'</c> (the REPL joins continuation lines with <c>"\n"</c>;
/// see <see cref="Repl"/> ReadCompleteInput and PrettyPrompt soft-newlines). The
/// newline character itself is treated as the last column of the line it
/// terminates — a caret sitting exactly on a <c>'\n'</c> maps to the end of that
/// line, matching how a composer perceives "just after the visible text".
/// Extracted as a pure static so xUnit can pin it without a TTY.
/// </summary>
public static class ReplCaretPosition
{
    /// <summary>
    /// Converts a 0-based <paramref name="caret"/> offset into
    /// <paramref name="text"/> to an LSP <see cref="Position"/>. The caret is
    /// clamped to <c>[0, text.Length]</c> charitably (D-v1.5-05) — an
    /// out-of-range caret never throws; it maps to the buffer start/end.
    /// </summary>
    public static Position CaretToPosition(string text, int caret)
    {
        if (string.IsNullOrEmpty(text))
            return new Position(line: 0, character: 0);

        // Charitable clamp — never throw on a stray caret.
        if (caret < 0) caret = 0;
        if (caret > text.Length) caret = text.Length;

        int line = 0;
        int lineStart = 0;
        for (int i = 0; i < caret; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return new Position(line: line, character: caret - lineStart);
    }
}
