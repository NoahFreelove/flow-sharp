using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Model;
namespace Flow.Studio.Engine;

public sealed class PlaybackPreparation
{
    public ProjectSnapshot Snapshot { get; }
    public int SampleRate { get; }
    public int BlockFrames { get; }
    public Guid? MonitoredTrack { get; }
    public bool MetronomeEnabled { get; }
    internal PlaybackPreparation(ProjectSnapshot snapshot, int sampleRate, int blockFrames, Guid? monitoredTrack = null, bool metronomeEnabled = false)
    { Snapshot = snapshot; SampleRate = sampleRate; BlockFrames = blockFrames; MonitoredTrack = monitoredTrack; MetronomeEnabled = metronomeEnabled; }
}
public sealed class PreparedProjectPlayback
{
    public PlaybackPreparation Request { get; }
    internal PreparedArrangement Arrangement { get; }
    public IReadOnlyList<ArrangementDiagnostic> Diagnostics => Arrangement.Diagnostics;
    public IReadOnlyList<string> AssetDiagnostics { get; }
    internal PreparedProjectPlayback(PlaybackPreparation request, PreparedArrangement arrangement, IReadOnlyList<string> assetDiagnostics)
    { Request = request; Arrangement = arrangement; AssetDiagnostics = assetDiagnostics; }
}
/// <summary>Opaque, control-owner parameter gesture. Preview never changes history.</summary>
public sealed class PlaybackParameterPreview
{
    internal PreparedAudioGraph Graph { get; }
    internal ProjectSnapshot ExpectedSnapshot { get; set; }
    internal double Original { get; }
    internal bool Frozen { get; set; }
    public bool IsActive { get; internal set; } = true;
    public Guid? PluginSourceId { get; internal set; }
    internal PluginParameter? PublicParameter { get; set; }
    internal PluginParameterTarget[]? Targets { get; set; }
    internal PreparedInstrumentControlGroup? InstrumentControls { get; set; }
    internal void Publish(double value)
    {
        if (InstrumentControls is { } controls)
            controls.SetLatestParameters(Targets!.Select(t => new GraphParameterValue(t.NodeId, t.DeviceParameterId, value)));
        else if (Targets is null) Graph.SetLatestParameter(NodeId, ParameterId, value);
        else Graph.SetLatestParameters(Targets.Select(t => new GraphParameterValue(t.NodeId, t.DeviceParameterId, value)));
    }
    public string NodeId { get; }
    public string ParameterId { get; }
    public double Value { get; internal set; }
    internal PlaybackParameterPreview(PreparedAudioGraph graph, ProjectSnapshot snapshot, string node, string parameter, double value)
    { Graph = graph; ExpectedSnapshot = snapshot; NodeId = node; ParameterId = parameter; Original = Value = value; }
}
public enum ProjectPlaybackStatus { Active, Preparing, Ready, AwaitingAudio, Failed, Stale }

/// <summary>Control-thread document-to-audio publication. Hosts run Prepare on their
/// bounded worker and marshal Complete/Fail back to the control thread. No worker
/// touches the document or audio cursor. This coordinator does not own a device.</summary>
public sealed class ProjectPlaybackCoordinator
{
    private readonly ProjectDocument _document;
    private PlaybackPreparation? _latest;
    private PreparedProjectPlayback? _ready, _submitted;
    private PreparedProjectPlayback _active;
    private long _submittedGeneration;
    private PlaybackParameterPreview? _preview;
    private PlaybackMonitorEndpoint? _monitor;
    public Guid? RequestedMonitorTrack { get; private set; }
    public bool RequestedMetronomeEnabled { get; private set; }
    public bool SelectMetronome(bool enabled)
    {
        if (RequestedMetronomeEnabled == enabled) return false;
        RequestedMetronomeEnabled = enabled;
        _latest = null; _ready = null; Status = ProjectPlaybackStatus.Stale;
        return true;
    }

    /// <summary>Control-owner selection only; follow a change with a preparation
    /// request. Selection is host state, not a project edit or undo action.</summary>
    public bool SelectMonitorTrack(Guid? trackId)
    {
        if (trackId.HasValue && !_document.Snapshot.Routing.Tracks.Any(t => t.Id == trackId.Value))
            throw new ArgumentException("Unknown monitor track", nameof(trackId));
        if (RequestedMonitorTrack == trackId) return false;
        RequestedMonitorTrack = trackId;
        CloseMonitor();
        _latest = null; _ready = null; Status = ProjectPlaybackStatus.Stale;
        return true;
    }

    /// <summary>Only acknowledged, selected endpoints are offered to an input host.
    /// Failed preparations retain the last working endpoint unless selection changed.</summary>
    public bool TryGetMonitor(out PlaybackMonitorEndpoint? monitor)
    {
        var candidate = _monitor;
        if (candidate is not null && candidate.TrackId == RequestedMonitorTrack && candidate.IsActive &&
            _document.Snapshot.Routing.Tracks.Any(t => t.Id == candidate.TrackId))
        { monitor = candidate; return true; }
        monitor = null; return false;
    }

    private static void ClosePreparedMonitor(PreparedProjectPlayback prepared)
    {
        foreach (var monitor in prepared.Arrangement.Monitors.Values) monitor.CloseAdmission();
        prepared.Arrangement.Playback.DisableMonitoring();
    }
    private void CloseActiveMonitor()
    {
        _monitor?.Close(); _monitor = null;
        ClosePreparedMonitor(_active);
    }
    public void CloseMonitor()
    {
        CloseActiveMonitor();
        if (_submitted is not null) ClosePreparedMonitor(_submitted);
    }
    /// <summary>Transport/device access. Replacement and retired-source collection
    /// belong to this coordinator. A serialized output-session owner may also
    /// reclaim retirement during a joined offline reset; other callers must not
    /// replace sources or collect retirement independently.</summary>
    public QueuedSinePlayback Queue { get; }
    public ProjectSnapshot ActiveSnapshot { get; private set; }
    public ProjectPlaybackStatus Status { get; private set; } = ProjectPlaybackStatus.Active;
    public string? Error { get; private set; }
    public IReadOnlyList<ArrangementDiagnostic> ActiveDiagnostics => _active.Diagnostics;
    public IReadOnlyList<string> ActiveAssetDiagnostics => _active.AssetDiagnostics;
    public IReadOnlyList<string> ActiveMeterNodeIds => _active.Arrangement.Playback.Graph.MeterNodeIds;
    /// <summary>Control/UI reader API for the acknowledged graph. On false, discard
    /// the scratch buffer and retain the last accepted display snapshot.</summary>
    public bool TryReadActiveMeters(Span<StereoMeter> meters, out long frame) =>
        _active.Arrangement.Playback.Graph.TryReadMeters(meters, out frame);

    /// <summary>Begin only against the acknowledged, unchanged project. One gesture
    /// at a time; structural and automation-owned parameters cannot be previewed.</summary>
    public bool TryBeginParameterPreview(string nodeId, string parameterId, out PlaybackParameterPreview? preview)
    {
        preview = null;
        ReconcilePreview();
        if (_preview is not null || _submitted is not null || _latest is not null || _ready is not null ||
            !ReferenceEquals(ActiveSnapshot, _document.Snapshot)) return false;
        var binding = ActiveSnapshot.Routing.GraphBinding ?? throw new InvalidOperationException("No selected graph");
        var source = ActiveSnapshot.Sources.Values.Single(s => s.Bindings.Any(b => b.Id == binding && b.Available));
        var layer = source.Bindings.Single(b => b.Id == binding).Output.LayerId;
        var definition = source.Result.GraphLayers.Single(l => l.Id == layer).Graph;
        if (source.Plugin is { } plugin)
            definition = plugin.ValidateEffect(source.Result, Queue.SampleRate, Queue.MaxBlockFrames).ApplyValues(source.PluginValues);
        var node = definition.Nodes.Single(n => n.Id == nodeId);
        double value = node.Parameters[parameterId];
        var graph = _active.Arrangement.Playback.Graph;
        graph.SetLatestParameter(nodeId, parameterId, value); // Shared validation before ownership changes.
        _preview = preview = new(graph, ActiveSnapshot, nodeId, parameterId, value);
        return true;
    }
    public bool TryBeginPluginParameterPreview(Guid sourceId, string parameterId, out PlaybackParameterPreview? preview)
    {
        preview = null; ReconcilePreview();
        if (_preview is not null || _submitted is not null || _latest is not null || _ready is not null ||
            !ReferenceEquals(ActiveSnapshot, _document.Snapshot)) return false;
        var source = ActiveSnapshot.Sources[sourceId];
        var plugin = source.Plugin ?? throw new InvalidOperationException("Source is not a declared plugin");
        PreparedInstrumentControlGroup? instrumentControls = null;
        Guid? effectBinding = null;
        if (plugin.Manifest.Kind == FlowPluginKind.Instrument)
        {
            var binding = source.Bindings.SingleOrDefault(b => b.Available && b.Output.Role == GeneratedRole.Instrument);
            if (binding is null || !_active.Arrangement.InstrumentControls.TryGetValue(binding.Id, out instrumentControls)) return false;
        }
        else if (!source.Bindings.Any(b => b.Id == ActiveSnapshot.Routing.GraphBinding && b.Available))
        {
            effectBinding = source.Bindings.Where(b => b.Available && ActiveSnapshot.Routing.Effects.Any(e => e.Binding == b.Id && !e.Bypassed))
                .Select(b => (Guid?)b.Id).SingleOrDefault();
            if (!effectBinding.HasValue) return false;
        }
        var parameter = plugin.Manifest.Parameters.SingleOrDefault(p => p.Id == parameterId)
            ?? throw new ArgumentException("Unknown public parameter");
        if (parameter.RequiresRebuild) throw new InvalidOperationException("Parameter requires preparation; live preview is unavailable");
        double value = source.PluginValues.TryGetValue(parameterId, out double saved) ? saved : parameter.Default;
        var candidate = new PlaybackParameterPreview(_active.Arrangement.Playback.Graph, ActiveSnapshot, "", parameterId, value)
        { InstrumentControls = instrumentControls, PluginSourceId = sourceId, PublicParameter = parameter,
            Targets = plugin.Targets.Where(t => t.ParameterId == parameterId).Select(t => effectBinding.HasValue
                ? t with { NodeId = _active.Arrangement.EffectNodes[(effectBinding.Value, t.NodeId)] } : t).ToArray() };
        candidate.Publish(value); // Validate every target/automation owner before acquiring the gesture.
        _preview = preview = candidate; return true;
    }
    public bool TryUpdateParameterPreview(PlaybackParameterPreview preview, double value)
    {
        ReconcilePreview();
        if (!ReferenceEquals(preview, _preview) || preview.Frozen) return false;
        if (preview.PublicParameter is { } parameter) _ = parameter.ToNormalized(value);
        preview.Publish(value);
        preview.Value = value; return true;
    }
    public bool FreezeParameterPreview(PlaybackParameterPreview preview)
    {
        ReconcilePreview();
        if (!ReferenceEquals(preview, _preview) || preview.Frozen) return false;
        preview.Frozen = true; return true;
    }
    /// <summary>Called by the committing control owner immediately after its captured
    /// action is accepted. Keep the audible preview until replacement acknowledges.</summary>
    public void AcceptParameterPreview(PlaybackParameterPreview preview)
    {
        if (!ReferenceEquals(preview, _preview) || !preview.Frozen) return;
        preview.ExpectedSnapshot = _document.Snapshot;
    }
    public void CancelParameterPreview(PlaybackParameterPreview preview)
    {
        if (!ReferenceEquals(preview, _preview)) return;
        preview.Publish(preview.Original);
        preview.IsActive = false; _preview = null;
    }
    private void ReconcilePreview()
    {
        if (_preview is not { } preview) return;
        if (!ReferenceEquals(preview.Graph, _active.Arrangement.Playback.Graph))
        { preview.IsActive = false; _preview = null; }
        else if (!ReferenceEquals(preview.ExpectedSnapshot, _document.Snapshot)) CancelParameterPreview(preview);
    }

    public ProjectPlaybackCoordinator(ProjectDocument document, string projectDirectory, int sampleRate = 48000, int blockFrames = 256)
    {
        _document = document;
        // Initial construction is preparation work and must precede opening the device.
        var initial = Prepare(new(document.Snapshot, sampleRate, blockFrames), projectDirectory);
        _active = initial;
        Queue = new(initial.Arrangement.Playback); ActiveSnapshot = document.Snapshot;
    }
    public PlaybackPreparation BeginPreparation()
    {
        ReconcilePreview();
        if (RequestedMonitorTrack is { } selected && !_document.Snapshot.Routing.Tracks.Any(t => t.Id == selected))
            SelectMonitorTrack(null);
        _latest = new(_document.Snapshot, Queue.SampleRate, Queue.MaxBlockFrames, RequestedMonitorTrack, RequestedMetronomeEnabled);
        _ready = null; Error = null; Status = ProjectPlaybackStatus.Preparing;
        return _latest;
    }
    public static PreparedProjectPlayback Prepare(PlaybackPreparation request, string projectDirectory, CancellationToken cancellation = default, Flow.Music.Model.MidiPitchMap? midiPitchMap = null)
    {
        var assets = ProjectAudioAssets.Resolve(request.Snapshot, projectDirectory, cancellation: cancellation);
        var arrangement = ProjectCompiler.Prepare(request.Snapshot, request.SampleRate, request.BlockFrames, cancellation, assets.Assets, request.MonitoredTrack, midiPitchMap: midiPitchMap);
        cancellation.ThrowIfCancellationRequested();
        if (request.MetronomeEnabled)
            arrangement.Playback.AttachTimelineMonitor(new PreparedMetronome(request.Snapshot.Context.Tempo,
                request.Snapshot.Context.Meter, request.SampleRate, request.BlockFrames));
        return new(request, arrangement, assets.Diagnostics);
    }
    public bool Complete(PreparedProjectPlayback result)
    {
        if (!ReferenceEquals(result.Request, _latest)) return false;
        if (!ReferenceEquals(result.Request.Snapshot, _document.Snapshot) || result.Request.MonitoredTrack != RequestedMonitorTrack || result.Request.MetronomeEnabled != RequestedMetronomeEnabled)
        { _latest = null; Status = ProjectPlaybackStatus.Stale; return false; }
        _ready = result; _latest = null; Status = ProjectPlaybackStatus.Ready; Error = null;
        return true;
    }
    public bool Fail(PlaybackPreparation request, string error)
    {
        if (!ReferenceEquals(request, _latest)) return false;
        _latest = null; _ready = null; Status = ProjectPlaybackStatus.Failed; Error = error;
        if (_preview is { } preview) CancelParameterPreview(preview);
        return true;
    }
    /// <summary>Call from the control thread while workers/audio run. A false result
    /// leaves a ready build available for retry unless its document became stale.
    /// Publication always follows document commit and never changes history.</summary>
    public bool TryPublish()
    {
        if (_submitted is not null && Queue.Generation >= _submittedGeneration)
        {
            CloseActiveMonitor();
            _active = _submitted;
            if (_active.Request.MonitoredTrack is { } selected && selected == RequestedMonitorTrack &&
                _document.Snapshot.Routing.Tracks.Any(t => t.Id == selected))
                _monitor = new(selected, _submittedGeneration, _active.Arrangement.Monitors[selected], Queue, _active.Arrangement.Playback);
            else
            {
                foreach (var monitor in _active.Arrangement.Monitors.Values) monitor.CloseAdmission();
                _active.Arrangement.Playback.DisableMonitoring();
            }
            ActiveSnapshot = _submitted.Request.Snapshot; _submitted = null;
            if (Status == ProjectPlaybackStatus.AwaitingAudio) Status = ProjectPlaybackStatus.Active;
        }
        if (RequestedMonitorTrack is { } selectedTrack && !_document.Snapshot.Routing.Tracks.Any(t => t.Id == selectedTrack))
            SelectMonitorTrack(null);
        ReconcilePreview();
        // Ownership returned by the audio boundary is reclaimed on this thread.
        Queue.TryTakeRetired(out _);
        // Do not overwrite the identity of an unacknowledged publication, even if
        // the audio owner finishes installing it during this control-thread call.
        if (_submitted is not null) return false;
        if (_ready is null) return false;
        if (!ReferenceEquals(_ready.Request.Snapshot, _document.Snapshot) || _ready.Request.MonitoredTrack != RequestedMonitorTrack || _ready.Request.MetronomeEnabled != RequestedMetronomeEnabled)
        { _ready = null; Status = ProjectPlaybackStatus.Stale; return false; }
        long generation = Queue.Generation;
        if (!Queue.TryReplace(_ready.Arrangement.Playback, PlaybackReplacementMode.PreserveTransport)) return false;
        CloseActiveMonitor();
        _submitted = _ready; _submittedGeneration = checked(generation + 1); _ready = null;
        Status = ProjectPlaybackStatus.AwaitingAudio; return true;
    }
}
