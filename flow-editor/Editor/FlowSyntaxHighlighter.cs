using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using System;
using System.Collections.Generic;

namespace FlowEditor.Editor;

/// <summary>
/// Syntax highlighter for Flow language using SimpleLexer tokenization.
/// Implements AvaloniaEdit's DocumentColorizingTransformer.
/// </summary>
public class FlowSyntaxHighlighter : DocumentColorizingTransformer
{
    private List<Token> _tokens = new();
    private string _lastSource = "";

    // Color palette (Catppuccin Mocha inspired)
    private static readonly IBrush KeywordBrush = new SolidColorBrush(Color.Parse("#cba6f7"));     // Purple - keywords
    private static readonly IBrush MusicKeywordBrush = new SolidColorBrush(Color.Parse("#94e2d5")); // Teal - tempo/key/timesig
    private static readonly IBrush TypeBrush = new SolidColorBrush(Color.Parse("#89b4fa"));         // Blue - type keywords
    private static readonly IBrush StringBrush = new SolidColorBrush(Color.Parse("#a6e3a1"));       // Green - strings
    private static readonly IBrush NumberBrush = new SolidColorBrush(Color.Parse("#fab387"));       // Peach - numbers
    private static readonly IBrush NoteBrush = new SolidColorBrush(Color.Parse("#f9e2af"));         // Yellow - notes/chords
    private static readonly IBrush OperatorBrush = new SolidColorBrush(Color.Parse("#f38ba8"));     // Red - operators
    private static readonly IBrush CommentBrush = new SolidColorBrush(Color.Parse("#6c7086"));      // Gray - comments
    private static readonly IBrush BoolBrush = new SolidColorBrush(Color.Parse("#fab387"));         // Peach - booleans
    private static readonly IBrush PipeBrush = new SolidColorBrush(Color.Parse("#f9e2af"));         // Yellow - | delimiters
    private static readonly IBrush SectionBrush = new SolidColorBrush(Color.Parse("#f5c2e7"));      // Pink - section

    public void UpdateSource(string source)
    {
        if (source == _lastSource)
            return;
        _lastSource = source;

        try
        {
            var errorReporter = new ErrorReporter();
            var lexer = new SimpleLexer(source, errorReporter);
            _tokens = lexer.Tokenize();
        }
        catch
        {
            // Tokenization can fail on partial input - that's OK
        }
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_tokens.Count == 0)
            return;

        int lineNumber = line.LineNumber;
        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;

        foreach (var token in _tokens)
        {
            if (token.Location.Line != lineNumber)
                continue;
            if (token.Type == TokenType.Eof)
                continue;

            int tokenStart = lineStart + (token.Location.Column - 1);
            int tokenEnd = tokenStart + token.Text.Length;

            // Clamp to line bounds
            if (tokenStart >= lineEnd || tokenEnd <= lineStart)
                continue;
            tokenStart = Math.Max(tokenStart, lineStart);
            tokenEnd = Math.Min(tokenEnd, lineEnd);

            if (tokenStart >= tokenEnd)
                continue;

            var brush = GetBrushForToken(token.Type);
            if (brush == null)
                continue;

            ChangeLinePart(tokenStart, tokenEnd, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
                if (token.Type == TokenType.Comment)
                {
                    element.TextRunProperties.SetTypeface(
                        new Typeface(element.TextRunProperties.Typeface.FontFamily, FontStyle.Italic));
                }
            });
        }
    }

    private static IBrush? GetBrushForToken(TokenType type)
    {
        return type switch
        {
            // Keywords
            TokenType.Proc or TokenType.EndProc or TokenType.Return or
            TokenType.Use or TokenType.Internal or TokenType.Lazy or TokenType.Fn
                => KeywordBrush,

            // Music context keywords
            TokenType.Tempo or TokenType.Timesig or TokenType.Key or
            TokenType.Swing or TokenType.Dynamics or TokenType.Rit or
            TokenType.Accel or TokenType.Pickup
                => MusicKeywordBrush,

            // Section
            TokenType.Section => SectionBrush,

            // Type keywords
            TokenType.Void or TokenType.Int or TokenType.Float or
            TokenType.Long or TokenType.Double or TokenType.String or
            TokenType.Bool or TokenType.Number or TokenType.Note or
            TokenType.Buf
                => TypeBrush,

            // Literals
            TokenType.IntLiteral or TokenType.FloatLiteral or
            TokenType.SemitoneLiteral or TokenType.CentLiteral or
            TokenType.TimeLiteral or TokenType.DecibelLiteral
                => NumberBrush,

            TokenType.StringLiteral => StringBrush,
            TokenType.BoolLiteral => BoolBrush,

            // Music literals
            TokenType.NoteLiteral or TokenType.ChordLiteral
                => NoteBrush,

            // Operators
            TokenType.Arrow or TokenType.FatArrow or TokenType.Plus or
            TokenType.Minus or TokenType.Star or TokenType.Slash or
            TokenType.LessThan or TokenType.GreaterThan or TokenType.Assign
                => OperatorBrush,

            // Pipe delimiters (note streams)
            TokenType.Pipe => PipeBrush,

            // Comments
            TokenType.Comment => CommentBrush,

            _ => null
        };
    }
}
