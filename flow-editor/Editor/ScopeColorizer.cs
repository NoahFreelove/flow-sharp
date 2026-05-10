using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Document;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using System;
using System.Collections.Generic;

namespace FlowEditor.Editor;

/// <summary>
/// Renders semi-transparent background colors for proc and section scopes.
/// Each distinct scope gets a unique color from a pastel palette.
/// </summary>
public class ScopeColorizer : IBackgroundRenderer
{
    private readonly List<ScopeRegion> _scopes = new();

    // Soft pastel palette for scope backgrounds
    private static readonly Color[] ScopePalette = new[]
    {
        Color.Parse("#1a1b2e"),  // Deep blue-purple
        Color.Parse("#1e2a1e"),  // Deep green
        Color.Parse("#2a1e1e"),  // Deep red-brown
        Color.Parse("#1e2a2a"),  // Deep teal
        Color.Parse("#2a2a1e"),  // Deep olive
        Color.Parse("#2a1e2a"),  // Deep magenta
        Color.Parse("#1e1e2a"),  // Deep navy
        Color.Parse("#2a251e"),  // Deep amber
    };

    public KnownLayer Layer => KnownLayer.Background;

    public void UpdateSource(string source)
    {
        _scopes.Clear();

        try
        {
            var errorReporter = new ErrorReporter();
            var lexer = new SimpleLexer(source, errorReporter);
            var tokens = lexer.Tokenize();
            if (errorReporter.HasErrors) return;

            var parser = new Parser(tokens, errorReporter);
            var program = parser.Parse();
            if (errorReporter.HasErrors) return;

            int colorIndex = 0;
            foreach (var stmt in program.Statements)
            {
                CollectScopes(stmt, ref colorIndex);
            }
        }
        catch
        {
            // Parsing partial input may fail
        }
    }

    private void CollectScopes(object node, ref int colorIndex)
    {
        switch (node)
        {
            case ProcDeclaration proc:
                _scopes.Add(new ScopeRegion(
                    proc.Location.Line,
                    GetLastLine(proc.Body, proc.Location.Line),
                    $"proc:{proc.Name}",
                    ScopePalette[colorIndex % ScopePalette.Length]));
                colorIndex++;
                foreach (var stmt in proc.Body)
                    CollectScopes(stmt, ref colorIndex);
                break;

            case SectionDeclaration section:
                _scopes.Add(new ScopeRegion(
                    section.Location.Line,
                    GetLastLine(section.Body, section.Location.Line),
                    $"section:{section.Name}",
                    ScopePalette[colorIndex % ScopePalette.Length]));
                colorIndex++;
                foreach (var stmt in section.Body)
                    CollectScopes(stmt, ref colorIndex);
                break;

            case MusicalContextStatement ctx:
                _scopes.Add(new ScopeRegion(
                    ctx.Location.Line,
                    GetLastLine(ctx.Body, ctx.Location.Line),
                    $"context:{ctx.ContextType}",
                    ScopePalette[colorIndex % ScopePalette.Length]));
                colorIndex++;
                foreach (var stmt in ctx.Body)
                    CollectScopes(stmt, ref colorIndex);
                break;
        }
    }

    private static int GetLastLine(IReadOnlyList<Statement> body, int defaultLine)
    {
        if (body.Count == 0) return defaultLine + 1;
        // The last statement's line + 1 (for the closing brace/endproc)
        return body[^1].Location.Line + 1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_scopes.Count == 0 || textView.Document == null)
            return;

        foreach (var scope in _scopes)
        {
            int startLine = Math.Max(1, scope.StartLine);
            int endLine = Math.Min(textView.Document.LineCount, scope.EndLine);

            for (int lineNum = startLine; lineNum <= endLine; lineNum++)
            {
                var line = textView.Document.GetLineByNumber(lineNum);
                var visualLine = textView.GetVisualLine(lineNum);
                if (visualLine == null) continue;

                var y = visualLine.VisualTop - textView.ScrollOffset.Y;
                var height = visualLine.Height;

                if (y + height < 0 || y > textView.Bounds.Height)
                    continue;

                var rect = new Rect(0, y, textView.Bounds.Width, height);
                drawingContext.FillRectangle(new SolidColorBrush(scope.Color), rect);
            }
        }
    }

    private record ScopeRegion(int StartLine, int EndLine, string Name, Color Color);
}
