using FlowLsp.NoteStream;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 plan 06 Task 1 Facts — NoteStreamContext token-scan walker.
/// Asserts IsInsideNoteStream (AST-based) and FindEnclosingKey (token-scan over
/// ParseResult.Tokens with brace-depth tracking). The regression Fact
/// CursorAfterClosedKeyBlock_FindEnclosingKey_ReturnsNull is the discriminator
/// that fails against a naive line-heuristic and passes only with the token-scan.
/// </summary>
public class NoteStreamContextTests
{
    [Fact]
    public void CursorInsideStream_NoKey_ReturnsNullKey()
    {
        var source = "proc main ()\n  | C4 D4 E4 |\nend proc";
        var result = LspFixtures.Parse(source);
        // Cursor on line 1 column 8 — inside the `| C4 D4 E4 |` stream.
        Assert.True(NoteStreamContext.IsInsideNoteStream(result.Ast, source, new Position(1, 8)));
        Assert.Null(NoteStreamContext.FindEnclosingKey(result.Ast, result.Tokens, source, new Position(1, 8)));
    }

    [Fact]
    public void CursorInsideStreamWithKey_ReturnsKeyName()
    {
        var source = "tempo 120 {\n  key Cmajor {\n    | I IV V7 |\n  }\n}";
        var result = LspFixtures.Parse(source);
        // Cursor on line 2 (the note-stream line) at column 10.
        var key = NoteStreamContext.FindEnclosingKey(result.Ast, result.Tokens, source, new Position(2, 10));
        Assert.NotNull(key);
        Assert.Contains("major", key!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NestedKeys_DeeperWins()
    {
        var source = "key Cmajor {\n  key Dminor {\n    | i iv v |\n  }\n}";
        var result = LspFixtures.Parse(source);
        // Cursor inside the Dminor block.
        var key = NoteStreamContext.FindEnclosingKey(result.Ast, result.Tokens, source, new Position(2, 10));
        Assert.NotNull(key);
        Assert.Contains("minor", key!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CursorAtFileStart_NoStream_ReturnsFalse()
    {
        var source = "proc main () { }";
        var result = LspFixtures.Parse(source);
        Assert.False(NoteStreamContext.IsInsideNoteStream(result.Ast, source, new Position(0, 0)));
    }

    /// <summary>
    /// Regression Fact for the block-exit bug. A naive line-heuristic
    /// (`cursor.Line >= stmt.Location.Line - 1`) would incorrectly report "inside key"
    /// for a cursor AFTER the key's closing `}`. The token-scan implementation must
    /// return null here.
    /// </summary>
    [Fact]
    public void CursorAfterClosedKeyBlock_FindEnclosingKey_ReturnsNull()
    {
        // Line 0: proc main () {
        // Line 1:   key Cmajor {
        // Line 2:     | I IV V |
        // Line 3:   }
        // Line 4:   | C4 D4 |    cursor here, outside the key block but inside proc main
        // Line 5: }
        var source = "proc main () {\n  key Cmajor {\n    | I IV V |\n  }\n  | C4 D4 |\n}";
        var result = LspFixtures.Parse(source);
        var cursor = new Position(4, 6);
        // The key block closed on line 3; the cursor is NOT inside it anymore.
        Assert.Null(NoteStreamContext.FindEnclosingKey(result.Ast, result.Tokens, source, cursor));
    }

    /// <summary>
    /// Block-exit edge case: cursor AFTER a sibling closed `key` but still inside an outer
    /// `proc` body. FindEnclosingKey must return null; IsInsideNoteStream must still detect
    /// the outer stream at the cursor position.
    /// </summary>
    [Fact]
    public void CursorAfterClosedKey_InSiblingStream_IsStreamTrueKeyNull()
    {
        var source = "proc main () {\n  key Cmajor {\n    | I IV V |\n  }\n  | C4 D4 |\n}";
        var result = LspFixtures.Parse(source);
        var cursor = new Position(4, 6);
        Assert.True(NoteStreamContext.IsInsideNoteStream(result.Ast, source, cursor));
        Assert.Null(NoteStreamContext.FindEnclosingKey(result.Ast, result.Tokens, source, cursor));
    }

    // === WR-05 regression guards — unclosed mid-edit stream handling ===

    /// <summary>
    /// WR-05 primary regression Fact: when the user is mid-edit with an
    /// UNCLOSED note stream (no closing `|`), but the enclosing block IS
    /// closed so the parser still produces a NoteStreamExpression in the AST,
    /// IsInsideNoteStream must still return true so completion surfaces
    /// note-stream items rather than the default proc/keyword set.
    ///
    /// Previous FindMatchingCloseStream implementation initialized
    /// `lastPipe = startOffset` and never re-assigned it when no closing `|`
    /// was found — so `cursorOffset <= endOffset` failed for any cursor
    /// position past the opening `|`, and completion surfaced the default
    /// proc/keyword set exactly when the user was actively typing in the
    /// stream.
    /// </summary>
    [Fact]
    public void StreamContainsOffset_OnUnclosedMidEditStream_ReturnsTrue()
    {
        // Mid-edit buffer: user typed `| C4 D4` inside a key block that is
        // closed on the following line. The parser produces a
        // MusicalContextStatement containing a NoteStreamExpression with no
        // matching `|`.
        var source = "key Cmajor {\n  | C4 D4\n}";
        var result = LspFixtures.Parse(source);
        // Cursor on line 1, col 8 — right after `D4`, before the newline.
        var cursor = new Position(1, 8);
        Assert.True(NoteStreamContext.IsInsideNoteStream(result.Ast, source, cursor));
        var key = NoteStreamContext.FindEnclosingKey(result.Ast, result.Tokens, source, cursor);
        Assert.NotNull(key);
        Assert.Contains("major", key!, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// WR-05 variant: unclosed note stream inside a proc body. Exercises the
    /// EOF-fallback branch of FindMatchingCloseStream (no `}` terminator
    /// encountered before the cursor, since the cursor is inside the stream
    /// and the `end proc` terminator comes after).
    /// </summary>
    [Fact]
    public void StreamContainsOffset_UnclosedStreamInProc_ReturnsTrue()
    {
        var source = "proc main ()\n  | C4 D4\nend proc";
        var result = LspFixtures.Parse(source);
        // Cursor right after `D4` on line 1.
        var cursor = new Position(1, 8);
        Assert.True(NoteStreamContext.IsInsideNoteStream(result.Ast, source, cursor));
    }

    /// <summary>
    /// WR-05 regression on plain unclosed-at-file-level streams. Exercises the
    /// EOF fallback in FindMatchingCloseStream (source.Length branch).
    /// </summary>
    [Fact]
    public void StreamContainsOffset_PlainUnclosedStream_ReturnsTrue()
    {
        var source = "| C4 D4 ";
        var result = LspFixtures.Parse(source);
        // Cursor at end of buffer.
        var cursor = new Position(0, source.Length);
        Assert.True(NoteStreamContext.IsInsideNoteStream(result.Ast, source, cursor));
    }
}
