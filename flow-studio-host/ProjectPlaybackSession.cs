using Flow.Audio;
using Flow.Platform.Linux;
using Flow.Studio.Engine;
using Flow.Studio.Model;
namespace Flow.Studio.Host;

/// <summary>Linux DAW host composition. Create and call from one control owner.
/// The document remains editable while audio is offline. Poll from the host update
/// loop; there are no hidden timers. Opening output never automatically plays.
/// Initial project preparation is synchronous, before any device is opened.</summary>
public sealed class ProjectPlaybackSession : IAsyncDisposable
{
    internal string ProjectDirectory { get; private set; } = "";
    private readonly ProjectPlaybackPreparationHost _preparation;
    private readonly PlaybackOutputSession _output;
    private Task? _shutdown;
    public ProjectDocument Document { get; }
    public ProjectPlaybackCoordinator Playback { get; }
    public ProjectMidiRecordingHost MidiRecording { get; }
    public ProjectMidiMonitoringHost MidiMonitoring { get; }
    internal SharedMidiInputHub MidiInputHub { get; }
    public PlaybackOutputState OutputState => _output.State;
    public Exception? OutputError => _output.LastError;
    public bool IsPreparing => _preparation.IsPreparing;

    public ProjectPlaybackSession(ProjectDocument document, string projectDirectory,
        int sampleRate = 48000, int blockFrames = 256, string? deviceSelector = null)
        : this(document, projectDirectory, sampleRate, blockFrames,
            queue => new PlaybackOutputSession(queue, deviceSelector)) { }

    internal ProjectPlaybackSession(ProjectDocument document, string projectDirectory,
        int sampleRate, int blockFrames, Func<QueuedSinePlayback, PlaybackOutputSession> outputFactory,
        Func<string, MidiPacketReceiver, IMidiInputConnection>? midiInputFactory = null)
    {
        Document = document;
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        Playback = new(document, projectDirectory, sampleRate, blockFrames);
        _output = outputFactory(Playback.Queue);
        _preparation = new(Playback, (request, cancellation) => ProjectPlaybackCoordinator.Prepare(request,
            projectDirectory, cancellation, request.MonitoredTrack.HasValue
                ? FlowLang.Hosting.GeneratorTuning.ResolveMidi(request.Snapshot.Context.Tuning) : null));
        MidiInputHub = new(midiInputFactory ?? MidiInputConnection.Open);
        MidiRecording = new(this);
        MidiMonitoring = new(this);
    }
    /// <summary>Call after committing an edit, undo/redo, or accepting generated output.</summary>
    public void RequestPreparation() { ThrowIfDisposed(); _preparation.Request(); }
    /// <summary>Monitoring-only click toggle, applied through prepared publication.
    /// Configure before arming a take; replacing playback during recording is a discontinuity.</summary>
    public bool SetMetronome(bool enabled)
    {
        ThrowIfDisposed();
        if (Playback.RequestedMetronomeEnabled == enabled) return false;
        if (MidiRecording.State is MidiRecordingState.Arming or MidiRecordingState.Recording or MidiRecordingState.Stopping)
            throw new InvalidOperationException("Set metronome before arming or after finishing the take");
        if (!Playback.SelectMetronome(enabled)) return false;
        RequestPreparation(); return true;
    }
    /// <summary>Prepare one selected monitoring track without changing the document.
    /// A native input host must wait for Playback.TryGetMonitor to succeed.</summary>
    public bool SetMonitoredTrack(Guid? trackId)
    {
        ThrowIfDisposed();
        if (!Playback.SelectMonitorTrack(trackId)) return false;
        RequestPreparation(); return true;
    }
    /// <summary>Commit one public control edit and schedule coherent playback preparation.
    /// Call once at gesture commit; live effect previews use ProjectMixerHost.</summary>
    public bool SetPluginParameter(Guid sourceId, string parameterId, double value)
    {
        ThrowIfDisposed();
        var before = Document.Snapshot;
        ProjectPluginCommands.SetParameter(Document, sourceId, parameterId, value);
        if (ReferenceEquals(before, Document.Snapshot)) return false;
        RequestPreparation(); return true;
    }
    public bool ResetPluginParameter(Guid sourceId, string parameterId)
    {
        ThrowIfDisposed();
        var before = Document.Snapshot;
        ProjectPluginCommands.ResetParameter(Document, sourceId, parameterId);
        if (ReferenceEquals(before, Document.Snapshot)) return false;
        RequestPreparation(); return true;
    }
    public bool ApplyPluginPreset(Guid sourceId, PluginPreset preset)
    {
        ThrowIfDisposed();
        if (!ProjectPluginCommands.ApplyPreset(Document, sourceId, preset)) return false;
        RequestPreparation(); return true;
    }
    public Guid DuplicatePlugin(Guid sourceId, Guid? instrumentTrack = null, bool copyAutomation = true)
    {
        ThrowIfDisposed();
        Guid id = ProjectPluginInstanceCommands.Duplicate(Document, sourceId, instrumentTrack, copyAutomation);
        RequestPreparation(); return id;
    }
    public void RemovePlugin(Guid sourceId)
    {
        ThrowIfDisposed();
        ProjectPluginInstanceCommands.Remove(Document, sourceId);
        RequestPreparation();
    }
    public bool SetEffects(IEnumerable<ProjectEffect> effects)
    {
        ThrowIfDisposed(); var before = Document.Snapshot;
        ProjectEffectCommands.Set(Document, effects);
        if (ReferenceEquals(before, Document.Snapshot)) return false;
        RequestPreparation(); return true;
    }
    public void Poll()
    {
        ThrowIfDisposed();
        _preparation.Poll();
        _output.Poll(); // May supply an offline boundary, only after callbacks are joined.
        Playback.TryPublish();
        if (_output.State != PlaybackOutputState.Running) Playback.CloseMonitor();
        MidiMonitoring.Poll();
        MidiRecording.Poll();
    }
    public bool Connect()
    {
        ThrowIfDisposed();
        bool connected = _output.Connect();
        Playback.TryPublish();
        if (connected && Playback.RequestedMonitorTrack.HasValue && !Playback.TryGetMonitor(out _) && !_preparation.IsPreparing)
            RequestPreparation();
        return connected;
    }
    public bool Disconnect()
    {
        ThrowIfDisposed();
        Playback.CloseMonitor();
        bool disconnected = _output.Disconnect();
        MidiMonitoring.Poll();
        MidiRecording.Poll();
        Playback.TryPublish();
        return disconnected;
    }
    public ValueTask DisposeAsync()
    {
        if (_shutdown is not null) return new(_shutdown);
        // A failed close must retain the entire live owner for explicit retry.
        // Never tear down shared playback while a callback might still use it.
        Playback.CloseMonitor();
        Playback.Queue.RequestStop();
        MidiRecording.Cancel();
        if (!MidiRecording.Stop(TimeSpan.FromSeconds(2)))
            throw new TimeoutException("MIDI input still owns its reader; retry shutdown.");
        if (!MidiMonitoring.Disconnect(TimeSpan.FromSeconds(2)))
            throw new TimeoutException("Monitor input still owns its callback; retry shutdown.");
        _output.Dispose();
        MidiRecording.Dispose();
        MidiMonitoring.Dispose();
        MidiInputHub.Dispose();
        _shutdown = _preparation.DisposeAsync().AsTask();
        return new(_shutdown);
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_shutdown is not null, this);
}
