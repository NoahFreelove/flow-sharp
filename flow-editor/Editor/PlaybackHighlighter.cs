using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using FlowLang.Audio;
using FlowLang.Core;
using System;
using System.Collections.Generic;

namespace FlowEditor.Editor;

/// <summary>
/// Renders glowing highlight overlays on source code regions that are
/// currently playing during audio playback.
/// </summary>
public class PlaybackHighlighter : IBackgroundRenderer
{
    private TimelineMap? _timeline;
    private IReadOnlyList<TimelineEntry> _activeEntries = Array.Empty<TimelineEntry>();

    private static readonly Color NoteHighlightColor = Color.Parse("#80f9e2af");   // Semi-transparent yellow
    private static readonly Color SectionHighlightColor = Color.Parse("#20cba6f7"); // Faint purple for section

    public KnownLayer Layer => KnownLayer.Selection;

    public void SetTimeline(TimelineMap timeline)
    {
        _timeline = timeline;
    }

    public void UpdatePosition(double seconds)
    {
        if (_timeline == null)
        {
            _activeEntries = Array.Empty<TimelineEntry>();
            return;
        }
        _activeEntries = _timeline.GetActiveAt(seconds);
    }

    public void ClearHighlights()
    {
        _timeline = null;
        _activeEntries = Array.Empty<TimelineEntry>();
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_activeEntries.Count == 0 || textView.Document == null)
            return;

        foreach (var entry in _activeEntries)
        {
            bool isSection = entry.ScopeName.StartsWith("section:");
            var color = isSection ? SectionHighlightColor : NoteHighlightColor;
            var brush = new SolidColorBrush(color);

            int line = entry.SourceStart.Line;
            if (line < 1 || line > textView.Document.LineCount)
                continue;

            var docLine = textView.Document.GetLineByNumber(line);
            var visualLine = textView.GetVisualLine(line);
            if (visualLine == null)
                continue;

            // For note-level entries, highlight just the token area
            if (!isSection)
            {
                int col = Math.Max(0, entry.SourceStart.Column - 1);
                int startOffset = docLine.Offset + col;
                int endOffset = Math.Min(startOffset + Math.Max(entry.SourceLength, 1), docLine.EndOffset);

                if (startOffset >= endOffset)
                {
                    // Fallback: highlight entire line
                    var y = visualLine.VisualTop - textView.ScrollOffset.Y;
                    var rect = new Rect(0, y, textView.Bounds.Width, visualLine.Height);
                    drawingContext.FillRectangle(brush, rect);
                    continue;
                }

                var rects = BackgroundGeometryBuilder.GetRectsForSegment(
                    textView, new TextSegment { StartOffset = startOffset, EndOffset = endOffset });
                foreach (var r in rects)
                {
                    var inflated = r.Inflate(new Thickness(1, 0));
                    drawingContext.FillRectangle(brush, inflated);
                }
            }
            else
            {
                // Section: highlight whole line
                var y = visualLine.VisualTop - textView.ScrollOffset.Y;
                var rect = new Rect(0, y, textView.Bounds.Width, visualLine.Height);
                drawingContext.FillRectangle(brush, rect);
            }
        }
    }
}
