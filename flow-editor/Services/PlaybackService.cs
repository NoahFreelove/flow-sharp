using FlowLang.Audio;
using FlowLang.StandardLibrary.Audio;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FlowEditor.Services;

/// <summary>
/// Manages audio playback on a background thread with position tracking.
/// Uses a Stopwatch to report playback position in real-time.
/// </summary>
public class PlaybackService : IDisposable
{
    private CancellationTokenSource? _cts;
    private Timer? _positionTimer;
    private Stopwatch? _stopwatch;
    private AudioPlaybackManager? _audioManager;
    private bool _disposed;

    public event Action<double>? PositionChanged;
    public event Action? PlaybackFinished;
    public event Action<string>? PlaybackError;

    public void Play(AudioBuffer buffer, TimelineMap timeline)
    {
        Stop();

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _stopwatch = Stopwatch.StartNew();
        _positionTimer = new Timer(_ =>
        {
            if (_stopwatch != null && _stopwatch.IsRunning)
            {
                PositionChanged?.Invoke(_stopwatch.Elapsed.TotalSeconds);
            }
        }, null, 0, 50);

        Task.Run(() =>
        {
            try
            {
                _audioManager = new AudioPlaybackManager();
                if (!_audioManager.IsAudioAvailable())
                {
                    PlaybackError?.Invoke("No audio backend available. Install PipeWire or PulseAudio.");
                    return;
                }

                var backend = _audioManager.GetBackend();
                var playbackCt = _audioManager.StartPlayback();

                // Link our cancellation to the manager's
                ct.Register(() => _audioManager.StopPlayback());

                backend.Play(buffer.Data, buffer.SampleRate, buffer.Channels, playbackCt);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                PlaybackError?.Invoke($"Playback error: {ex.Message}");
            }
            finally
            {
                _stopwatch?.Stop();
                _positionTimer?.Dispose();
                _positionTimer = null;

                if (!ct.IsCancellationRequested)
                {
                    PlaybackFinished?.Invoke();
                }
            }
        }, ct);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _stopwatch?.Stop();
        _stopwatch = null;

        _positionTimer?.Dispose();
        _positionTimer = null;

        try
        {
            _audioManager?.StopPlayback();
            _audioManager?.Dispose();
        }
        catch { }
        _audioManager = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
    }
}
