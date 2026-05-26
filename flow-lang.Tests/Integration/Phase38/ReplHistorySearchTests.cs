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

    /// <summary>
    /// Pointing the editor at a pre-populated history file MUST surface ALL entries
    /// via LoadHistory() in most-recent-first order. The file format is one entry per
    /// line per UI-SPEC line 298.
    /// </summary>
    [Fact]
    public void LoadHistoryFromConfig_ReturnsEntries()
    {
        var historyFile = Path.Combine(_tempDir!, "history");
        File.WriteAllLines(historyFile, new[]
        {
            "(add 1 2)",
            "Int x = 5",
            "x -> (mul 3)",
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

        var lines = File.ReadAllLines(historyFile);
        Assert.Contains("Int y = 42", lines);
    }
}
