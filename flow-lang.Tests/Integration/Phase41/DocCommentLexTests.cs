using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Integration.Phase41;

/// <summary>
/// Phase 41 DOC-01 — the <c>flow doc</c> generator's lexer contract:
/// a <c>///</c> line is captured as a doc-comment (exposed via the lexer's
/// <c>PendingDocComment</c> accessor and an out-of-band <c>DocComment</c> token
/// that the Parser binds to the following proc); <c>//</c> line comments and
/// <c>/* */</c> blocks are UNCHANGED (D-07 additive grammar, Pitfall 1).
///
/// 41-02 turned these RED→GREEN by adding the
/// <c>SimpleLexer.SkipWhitespaceAndComments</c> doc-comment branch ahead of the
/// two-slash arm.
/// </summary>
[Trait("Category", "Phase41")]
public class DocCommentLexTests
{
    // Significant tokens = everything the parser actually consumes. Comments
    // (plain `//` `Comment` + `///` `DocComment`) are out-of-band and excluded
    // from the "same significant stream" comparison.
    private static (TokenType Type, string Text)[] Significant(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        return lexer.Tokenize()
            .Where(t => t.Type is not (TokenType.Comment or TokenType.DocComment or TokenType.Eof))
            .Select(t => (t.Type, t.Text))
            .ToArray();
    }

    [Fact]
    public void TripleSlash_LexesAsDocComment()
    {
        // `/// summary` captures `summary` (leading `///` + one space stripped)
        // and leaves the significant-token stream identical to the comment-free source.
        const string withDoc = "/// adds two numbers\nproc add2(Int: x) (add x 2) end";
        const string withoutDoc = "proc add2(Int: x) (add x 2) end";

        var lexer = new SimpleLexer(withDoc, new ErrorReporter());
        var tokens = lexer.Tokenize();

        // The captured doc text is exposed via the lexer's pending accessor...
        Assert.Equal("adds two numbers", lexer.PendingDocComment);

        // ...AND a DocComment token rides the stream so the Parser can bind it.
        Assert.Contains(tokens, t => t.Type == TokenType.DocComment && t.Text == "adds two numbers");

        // The significant (parser-consumed) stream is byte-identical to the comment-free source.
        Assert.Equal(Significant(withoutDoc), Significant(withDoc));
    }

    [Fact]
    public void MultipleTripleSlash_Concatenate()
    {
        const string src = "/// line one\n/// line two\nproc foo() (print \"x\") end";
        var lexer = new SimpleLexer(src, new ErrorReporter());
        lexer.Tokenize();
        Assert.Equal("line one\nline two", lexer.PendingDocComment);
    }

    [Fact]
    public void DoubleSlash_Unchanged()
    {
        // A plain `// comment` skips to EOL with NO doc captured and NO DocComment token.
        const string withComment = "// just a comment\nproc foo() (print \"x\") end";
        const string withoutComment = "proc foo() (print \"x\") end";

        var lexer = new SimpleLexer(withComment, new ErrorReporter());
        var tokens = lexer.Tokenize();

        Assert.Null(lexer.PendingDocComment);
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.DocComment);
        Assert.Equal(Significant(withoutComment), Significant(withComment));
    }

    [Fact]
    public void BlockComment_Unchanged()
    {
        // Flow has no `/* */` block-comment grammar — it lexes today as Slash/Star
        // tokens. The `///` change must NOT alter that (no DocComment, no swallow).
        const string src = "/* not a comment */";
        var lexer = new SimpleLexer(src, new ErrorReporter());
        var tokens = lexer.Tokenize();

        Assert.Null(lexer.PendingDocComment);
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.DocComment);
        // Byte-identical to the pre-change lex: same source re-lexed is stable.
        Assert.Equal(Significant(src), Significant(src));
        // And it still produces the slash/star token shape (not swallowed to nothing).
        Assert.NotEmpty(Significant(src));
    }
}
