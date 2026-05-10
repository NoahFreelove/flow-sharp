using FlowLang.Audio;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlowEditor.Services;

/// <summary>
/// Result of executing a Flow script in the editor.
/// </summary>
public class ExecutionResult
{
    public List<string> Errors { get; init; } = new();
    public AudioBuffer? Buffer { get; init; }
    public TimelineMap? Timeline { get; init; }
    public string? ContextInfo { get; init; }
}

/// <summary>
/// Orchestrates script execution with timeline capture.
/// Intercepts renderSong calls to produce TimelineMap alongside AudioBuffer.
/// </summary>
public class EditorService : IDisposable
{
    private FlowEngine? _engine;
    private bool _disposed;

    public async Task<ExecutionResult> ExecuteWithTimeline(string source, string fileName)
    {
        return await Task.Run(() => ExecuteInternal(source, fileName));
    }

    private ExecutionResult ExecuteInternal(string source, string fileName)
    {
        var errors = new List<string>();
        string? contextInfo = null;

        // Holder to capture timeline data from intercepted renderSong calls
        var capture = new CaptureHolder();

        try
        {
            _engine?.Dispose();
            var errorReporter = new ErrorReporter();
            _engine = new FlowEngine(errorReporter);

            // Replace renderSong with timeline-aware interceptor
            var renderSig = new FunctionSignature(
                "renderSong",
                new FlowType[] { SongType.Instance, StringType.Instance });

            _engine.Context.InternalRegistry.ReplaceAll("renderSong", renderSig, args =>
            {
                var song = args[0].As<SongData>();
                string synthType = (string)args[1].Data!;

                var (buffer, timelineMap) = SongRenderer.RenderSongWithTimeline(song, synthType);
                capture.Buffer = buffer;
                capture.Timeline = timelineMap;

                return Value.Buffer(buffer);
            });

            // Replace play overloads with no-ops (we handle playback ourselves)
            var playBufferSig = new FunctionSignature("play", new FlowType[] { BufferType.Instance });
            var playSeqSig = new FunctionSignature("play", new FlowType[] { SequenceType.Instance });
            _engine.Context.InternalRegistry.ReplaceAll("play", playBufferSig, args =>
            {
                // Capture the buffer if renderSong wasn't used
                if (capture.Buffer == null && args[0].Data is AudioBuffer buf)
                {
                    capture.Buffer = buf;
                }
                return Value.Void();
            });
            _engine.Context.InternalRegistry.Register("play", playSeqSig, args =>
            {
                return Value.Void();
            });

            var success = _engine.Execute(source, fileName);

            foreach (var error in errorReporter.Errors)
            {
                errors.Add(error.ToString());
            }

            // Extract musical context info
            try
            {
                var ctx = _engine.Context.GetMusicalContext();
                var parts = new List<string>();
                if (ctx.Tempo.HasValue) parts.Add($"Tempo: {ctx.Tempo}");
                if (ctx.Key != null) parts.Add($"Key: {ctx.Key}");
                if (ctx.TimeSignature != null) parts.Add($"Time: {ctx.TimeSignature}");
                if (parts.Count > 0) contextInfo = string.Join("  |  ", parts);
            }
            catch { }
        }
        catch (Exception ex)
        {
            errors.Add($"Execution error: {ex.Message}");
        }

        return new ExecutionResult
        {
            Errors = errors,
            Buffer = capture.Buffer,
            Timeline = capture.Timeline,
            ContextInfo = contextInfo
        };
    }

    public void StopAudio()
    {
        _engine?.StopAudio();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _engine?.Dispose();
        }
    }

    private class CaptureHolder
    {
        public AudioBuffer? Buffer;
        public TimelineMap? Timeline;
    }
}
