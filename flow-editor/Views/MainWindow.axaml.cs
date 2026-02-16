using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using FlowEditor.Editor;
using FlowEditor.Services;
using System;
using System.IO;

namespace FlowEditor.Views;

public partial class MainWindow : Window
{
    private readonly EditorService _editorService;
    private readonly PlaybackService _playbackService;
    private readonly FlowSyntaxHighlighter _syntaxHighlighter;
    private readonly ScopeColorizer _scopeColorizer;
    private readonly PlaybackHighlighter _playbackHighlighter;
    private string? _currentFilePath;
    private bool _isDirty;

    public MainWindow()
    {
        InitializeComponent();

        _editorService = new EditorService();
        _playbackService = new PlaybackService();
        _syntaxHighlighter = new FlowSyntaxHighlighter();
        _scopeColorizer = new ScopeColorizer();
        _playbackHighlighter = new PlaybackHighlighter();

        SetupEditor();
        SetupButtons();
        SetupPlayback();
    }

    private void SetupEditor()
    {
        CodeEditor.TextArea.TextView.LineTransformers.Add(_syntaxHighlighter);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_scopeColorizer);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_playbackHighlighter);

        CodeEditor.TextChanged += OnTextChanged;

        // Default sample content
        CodeEditor.Text = @"Note: Welcome to Flow Editor
Note: Write your Flow music code here!

use ""@std""
use ""@audio""

tempo 120 {
    timesig 4/4 {
        key Cmajor {

section intro {
    Sequence melody = | C4 E4 G4 C5 |
}

section verse {
    Sequence melody = | E4 D4 C4 D4 | E4 E4 E4h |
}

Song song = [intro verse*2]
Buffer output = (renderSong song ""piano"")
(play output)

        }
    }
}
";
    }

    private void SetupButtons()
    {
        NewButton.Click += OnNewClick;
        OpenButton.Click += OnOpenClick;
        SaveButton.Click += OnSaveClick;
        RunButton.Click += OnRunClick;
        StopButton.Click += OnStopClick;
    }

    private void SetupPlayback()
    {
        _playbackService.PositionChanged += OnPlaybackPositionChanged;
        _playbackService.PlaybackFinished += OnPlaybackFinished;
        _playbackService.PlaybackError += OnPlaybackError;
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        _isDirty = true;
        UpdateTitle();

        // Debounced re-highlighting
        _syntaxHighlighter.UpdateSource(CodeEditor.Text);
        _scopeColorizer.UpdateSource(CodeEditor.Text);
        CodeEditor.TextArea.TextView.Redraw();
    }

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        CodeEditor.Text = "";
        _currentFilePath = null;
        _isDirty = false;
        FileNameLabel.Text = "untitled.flow";
        UpdateTitle();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var storage = StorageProvider;
        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Flow Script",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Flow Scripts") { Patterns = new[] { "*.flow" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (result.Count > 0)
        {
            var file = result[0];
            var path = file.TryGetLocalPath();
            if (path != null)
            {
                var content = await File.ReadAllTextAsync(path);
                CodeEditor.Text = content;
                _currentFilePath = path;
                _isDirty = false;
                FileNameLabel.Text = Path.GetFileName(path);
                UpdateTitle();
            }
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_currentFilePath != null)
        {
            await File.WriteAllTextAsync(_currentFilePath, CodeEditor.Text);
            _isDirty = false;
            UpdateTitle();
            return;
        }

        var storage = StorageProvider;
        var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Flow Script",
            DefaultExtension = "flow",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Flow Scripts") { Patterns = new[] { "*.flow" } }
            }
        });

        if (result != null)
        {
            var path = result.TryGetLocalPath();
            if (path != null)
            {
                await File.WriteAllTextAsync(path, CodeEditor.Text);
                _currentFilePath = path;
                _isDirty = false;
                FileNameLabel.Text = Path.GetFileName(path);
                UpdateTitle();
            }
        }
    }

    private async void OnRunClick(object? sender, RoutedEventArgs e)
    {
        ErrorOutput.Text = "";
        RunButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        PlaybackStatusLabel.Text = "Running...";

        var source = CodeEditor.Text;
        var fileName = _currentFilePath ?? "untitled.flow";

        try
        {
            var result = await _editorService.ExecuteWithTimeline(source, fileName);

            if (result.Errors.Count > 0)
            {
                ErrorOutput.Text = string.Join("\n", result.Errors);
                PlaybackStatusLabel.Text = "Error";
                RunButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                return;
            }

            // Update context info
            if (result.ContextInfo != null)
            {
                ContextInfoLabel.Text = result.ContextInfo;
            }

            if (result.Buffer != null && result.Timeline != null)
            {
                var entries = result.Timeline.Entries;
                var noteEntries = new System.Collections.Generic.List<FlowLang.Audio.TimelineEntry>();
                foreach (var te in entries)
                    if (!te.ScopeName.StartsWith("section:") || te.SourceLength < 50)
                        noteEntries.Add(te);

                ErrorOutput.Text = $"Timeline: {entries.Count} entries ({noteEntries.Count} note-level)";
                if (noteEntries.Count > 0)
                {
                    var first = noteEntries[0];
                    ErrorOutput.Text += $"\n  First: L{first.SourceStart.Line}:C{first.SourceStart.Column} len={first.SourceLength} t={first.StartSeconds:F2}-{first.EndSeconds:F2}s scope={first.ScopeName}";
                }

                PlaybackStatusLabel.Text = "Playing";
                _playbackHighlighter.SetTimeline(result.Timeline);
                _playbackService.Play(result.Buffer, result.Timeline);
            }
            else
            {
                ErrorOutput.Text = "Script executed successfully (no audio output).";
                PlaybackStatusLabel.Text = "Done";
                RunButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            ErrorOutput.Text = $"Error: {ex.Message}";
            PlaybackStatusLabel.Text = "Error";
            RunButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        _playbackService.Stop();
        _playbackHighlighter.ClearHighlights();
        CodeEditor.TextArea.TextView.Redraw();
        PlaybackStatusLabel.Text = "Stopped";
        PlaybackTimeLabel.Text = "00:00";
        RunButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    private void OnPlaybackPositionChanged(double seconds)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var ts = TimeSpan.FromSeconds(seconds);
            PlaybackTimeLabel.Text = $"{ts.Minutes:D2}:{ts.Seconds:D2}";

            _playbackHighlighter.UpdatePosition(seconds);
            CodeEditor.TextArea.TextView.Redraw();
        });
    }

    private void OnPlaybackFinished()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _playbackHighlighter.ClearHighlights();
            CodeEditor.TextArea.TextView.Redraw();
            PlaybackStatusLabel.Text = "Stopped";
            RunButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        });
    }

    private void OnPlaybackError(string error)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ErrorOutput.Text = error;
            PlaybackStatusLabel.Text = "Error";
            RunButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        });
    }

    private void UpdateTitle()
    {
        var dirty = _isDirty ? " *" : "";
        var file = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "untitled.flow";
        Title = $"Flow Editor - {file}{dirty}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _playbackService.Dispose();
        _editorService.Dispose();
        base.OnClosed(e);
    }
}
