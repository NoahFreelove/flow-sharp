using System;
using System.IO;
using System.Linq;
using FlowInterpreter;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-04 — REPL history file behaviour per UI-SPEC §"History File"
/// lines 293-302: persisted at <c>~/.config/flow/history</c>, 10k-entry cap with
/// rotation on append, mode 0600 on Linux/macOS. The PrettyPrompt Ctrl+R reverse
/// search itself is exercised by manual smoke (38-VALIDATION manual-only table);
/// these tests cover the file-side contract.
/// </summary>
[Collection("FlowScripts")]
public class ReplHistorySearchTests : IDisposable
{
    private string? _tempDir;

    public ReplHistorySearchTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        _tempDir = Path.Combine(Path.GetTempPath(), "flow-repl-history-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        if (_tempDir != null && Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private static string B64(string s) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s));

    /// <summary>
    /// Pointing the editor at a pre-populated history file MUST surface ALL entries
    /// via LoadHistory() in most-recent-first order. Quick 260610-gl4: the on-disk
    /// format is now base64-per-line (PrettyPrompt-compatible, single-owner), so the
    /// fixture is written base64-encoded.
    /// </summary>
    [Fact]
    public void LoadHistoryFromConfig_ReturnsEntries()
    {
        var historyFile = Path.Combine(_tempDir!, "history");
        File.WriteAllLines(historyFile, new[]
        {
            B64("(add 1 2)"),
            B64("Int x = 5"),
            B64("x -> (mul 3)"),
        });

        using var editor = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
            historyFilePath: historyFile);

        var entries = editor.LoadHistory();
        Assert.Equal(3, entries.Count);
        // Most-recent-first means the LAST written line is the FIRST returned.
        Assert.Equal("x -> (mul 3)", entries[0]);
        Assert.Equal("Int x = 5", entries[1]);
        Assert.Equal("(add 1 2)", entries[2]);
    }

    /// <summary>
    /// Quick 260610-gl4 Findings 5 + 6 — a history file that MIXES PrettyPrompt base64
    /// lines with the legacy manual-append plaintext lines (the exact corruption the
    /// composer found) MUST be repaired on editor construction: the original is backed
    /// up (*.corrupt-*.bak) and the file is rewritten with only the base64 lines that
    /// cleanly round-trip, so PrettyPrompt's loader sees a single-format file and
    /// Ctrl+R works. The plaintext pollution is dropped, real history preserved.
    /// </summary>
    [Fact]
    public void SanitizeHistoryFile_MixedBase64AndPlaintext_StripsPlaintextAndBacksUp()
    {
        var historyFile = Path.Combine(_tempDir!, "history");
        // Interleaved exactly like the composer's corrupt file: a base64 line from
        // PrettyPrompt followed by a plaintext line from the old manual append.
        File.WriteAllLines(historyFile, new[]
        {
            B64("(add 1 2)"),
            "(add 1 2)",            // plaintext pollution — NOT valid base64 round-trip
            B64("Int x = 5"),
            "Int x = 5",            // plaintext pollution
        });

        // Construction runs SanitizeHistoryFile.
        using var editor = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
            historyFilePath: historyFile);

        // The file now contains ONLY the two valid base64 lines.
        var onDisk = File.ReadAllLines(historyFile);
        Assert.Equal(2, onDisk.Length);
        Assert.Equal(B64("(add 1 2)"), onDisk[0]);
        Assert.Equal(B64("Int x = 5"), onDisk[1]);

        // A backup of the original mixed file was written.
        var backups = Directory.GetFiles(_tempDir!, "history.corrupt-*.bak");
        Assert.Single(backups);
        Assert.Equal(4, File.ReadAllLines(backups[0]).Length);

        // LoadHistory now decodes cleanly, most-recent-first.
        var entries = editor.LoadHistory();
        Assert.Equal(new[] { "Int x = 5", "(add 1 2)" }, entries);
    }

    /// <summary>
    /// A history file that is ALREADY clean (every line base64) must be left untouched —
    /// no spurious *.bak files on every REPL launch.
    /// </summary>
    [Fact]
    public void SanitizeHistoryFile_AllBase64_LeavesFileUntouchedNoBackup()
    {
        var historyFile = Path.Combine(_tempDir!, "history");
        var clean = new[] { B64("(add 1 2)"), B64("Int x = 5") };
        File.WriteAllLines(historyFile, clean);

        using var editor = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
            historyFilePath: historyFile);

        Assert.Equal(clean, File.ReadAllLines(historyFile));
        Assert.Empty(Directory.GetFiles(_tempDir!, "history.corrupt-*.bak"));
    }

    /// <summary>
    /// Quick 260610-gl4 Finding 5 — Ctrl+R reverse-search matching. PrettyPrompt 4.1.1
    /// has no reverse-search binding, so we wired one; this pins the matching helper:
    /// most-recent-first, case-insensitive substring, null on no-match/empty-query.
    /// </summary>
    [Fact]
    public void ReverseSearchHistory_FindsMostRecentCaseInsensitiveSubstring()
    {
        var historyFile = Path.Combine(_tempDir!, "history");
        File.WriteAllLines(historyFile, new[]
        {
            B64("(print \"alpha\")"),
            B64("Int x = 5"),
            B64("(print \"beta\")"),   // most recent of the two prints
        });

        using var editor = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
            historyFilePath: historyFile);

        // Most-recent-first: "print" matches the LAST-written print line first.
        Assert.Equal("(print \"beta\")", editor.ReverseSearchHistory("print"));
        // Case-insensitive (use an unambiguous substring — "x = " only occurs in the Int line).
        Assert.Equal("Int x = 5", editor.ReverseSearchHistory("X = "));
        // No match / empty query → null (search stays open / aborts charitably).
        Assert.Null(editor.ReverseSearchHistory("zzz"));
        Assert.Null(editor.ReverseSearchHistory(""));
    }

    /// <summary>
    /// On Linux/macOS the history file MUST be created with mode 0600 per UI-SPEC line 300
    /// (composer may type secrets into the REPL; the file is private to the user).
    /// On Windows this test is a no-op (mode bits don't apply).
    /// </summary>
    [Fact]
    public void HistoryFile_OnLinuxMacOS_Has0600Permissions()
    {
        var historyFile = Path.Combine(_tempDir!, "history");

        using var editor = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
            historyFilePath: historyFile);

        editor.AppendHistory("(print \"hello\")");

        Assert.True(File.Exists(historyFile), "AppendHistory should have created the file");

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return; // No-op on Windows (UI-SPEC line 300 scopes 0600 to Linux/macOS)
        }

        var mode = File.GetUnixFileMode(historyFile);
        var expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(expected, mode);
    }

    /// <summary>
    /// AppendHistory of a single entry MUST be visible in the subsequent LoadHistory.
    /// Regression guard: AppendHistory writes to disk immediately (no buffering),
    /// which is required for the manual Ctrl+R flow to see entries from the same session.
    /// </summary>
    [Fact]
    public void AppendHistory_PersistsToFileImmediately()
    {
        var historyFile = Path.Combine(_tempDir!, "history");

        using (var editor = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
                   historyFilePath: historyFile))
        {
            editor.AppendHistory("Int y = 42");
        }

        // Quick 260610-gl4: on-disk format is base64-per-line (single-owner, PrettyPrompt-
        // compatible). The raw line is the base64 encoding; LoadHistory decodes it back.
        var lines = File.ReadAllLines(historyFile);
        Assert.Contains(B64("Int y = 42"), lines);

        using var reader = new ReplLineEditor(promptText: "> ", continuationPrompt: "... ",
            historyFilePath: historyFile);
        Assert.Contains("Int y = 42", reader.LoadHistory());
    }
}
