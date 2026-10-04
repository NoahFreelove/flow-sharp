namespace Flow.Audio.Graph;

/// <summary>Independent prepared source cursors feed explicit graph input buses.
/// Construction transfers all sources exclusively. The same Read path serves live
/// callback transport and offline export. Graph tails are included in playback duration.
/// Seek/stop/loop reset graph transient state. With monitoring attached, pause freezes
/// arrangement/automation time while live voices and mixer DSP continue.</summary>
public sealed class PreparedGraphPlayback : IPreparedAudioPlayback, IPreparedMonitoringPlayback
{
    private readonly IPreparedAudioPlayback[] _sources;
    private readonly float[] _inputs;
    private readonly PreparedLiveInstrument?[] _monitors;
    private readonly float[] _monitorScratch;
    private int _monitoringEnabled;
    private IPreparedTimelineMonitor? _timelineMonitor;
    private float[] _timelineScratch = [];
    public bool MonitoringEnabled => Volatile.Read(ref _monitoringEnabled) != 0 || _timelineMonitor is not null;
    /// <summary>Preparation-only transfer, before publishing this playback.</summary>
    public void AttachTimelineMonitor(IPreparedTimelineMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        if (_timelineMonitor is not null || monitor.SampleRate != SampleRate || monitor.MaxBlockFrames < MaxBlockFrames)
            throw new ArgumentException("Provide one format-matched timeline monitor");
        _timelineScratch = new float[MaxBlockFrames * 2];
        monitor.Reset(PositionFrames); _timelineMonitor = monitor;
    }
    /// <summary>Control-owner permanent deactivation for this prepared generation.
    /// Stops audition while transport is frozen; ordinary arrangement rendering remains.</summary>
    public void DisableMonitoring() => Volatile.Write(ref _monitoringEnabled, 0);
    private readonly PreparedInstrumentControlGroup[] _instrumentControls;
    public PreparedAudioGraph Graph { get; }
    public int SampleRate => Graph.SampleRate;
    public int MaxBlockFrames => Graph.MaxBlockFrames;
    public long TotalFrames { get; }
    public long PositionFrames { get; private set; }

    public PreparedGraphPlayback(PreparedAudioGraph graph, IEnumerable<IPreparedAudioPlayback> sources,
        long maxInputBufferBytes = 32 * 1024 * 1024, IEnumerable<PreparedInstrumentControlGroup>? instrumentControls = null,
        IReadOnlyDictionary<int, PreparedLiveInstrument>? monitors = null, long minimumFrames = 0)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumFrames);
        var array = sources.ToArray();
        if (array.Length != graph.InputBusCount || array.Length == 0 || array.Any(s => s is null) ||
            array.Distinct(ReferenceEqualityComparer.Instance).Count() != array.Length)
            throw new ArgumentException("Provide one independently owned source per input bus", nameof(sources));
        if (array.Any(s => s.SampleRate != graph.SampleRate || s.MaxBlockFrames < graph.MaxBlockFrames))
            throw new ArgumentException("Sources must match sample rate and support the graph block size", nameof(sources));
        _monitors = new PreparedLiveInstrument?[array.Length];
        if (monitors is not null)
        {
            if (monitors.Count > array.Length || monitors.Keys.Any(k => k < 0 || k >= array.Length) ||
                monitors.Values.Any(m => m is null || m.SampleRate != graph.SampleRate || m.MaxBlockFrames < graph.MaxBlockFrames) ||
                monitors.Values.Distinct(ReferenceEqualityComparer.Instance).Count() != monitors.Count)
                throw new ArgumentException("Provide independently owned, format-matched monitors on valid input buses");
            foreach (var pair in monitors) _monitors[pair.Key] = pair.Value;
        }
        _monitoringEnabled = _monitors.Any(m => m is not null) ? 1 : 0;
        long samples = checked((long)array.Length * graph.MaxBlockFrames * 2);
        if (maxInputBufferBytes < 1 || (samples + (MonitoringEnabled ? graph.MaxBlockFrames * 2L : 0)) * sizeof(float) > maxInputBufferBytes || samples > int.MaxValue)
            throw new ArgumentException("Graph input buffers exceed preparation budget");
        _instrumentControls = (instrumentControls ?? []).Take(65).ToArray();
        if (_instrumentControls.Length > 64 || _instrumentControls.Any(c => c is null) ||
            _instrumentControls.Distinct().Count() != _instrumentControls.Length)
            throw new ArgumentException("Invalid instrument control groups");
        _inputs = new float[(int)samples];
        _monitorScratch = MonitoringEnabled ? new float[graph.MaxBlockFrames * 2] : [];
        _sources = array;
        Graph = graph;
        TotalFrames = Math.Max(minimumFrames, checked(array.Max(s => s.TotalFrames) + graph.TailFrames));
        Reset();
    }
    public int Read(Span<float> output)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames)
            throw new ArgumentException("Output must be stereo within the prepared block limit", nameof(output));
        if (output.IsEmpty) return 0;
        int frames = (int)Math.Min(output.Length / 2, TotalFrames - PositionFrames);
        int samples = frames * 2;
        if (frames > 0)
        {
            Render(output[..samples], PositionFrames, true, true);
            PositionFrames += frames;
        }
        output[samples..].Clear();
        return frames;
    }
    /// <summary>Monitoring and mixer tails continue, but no arrangement source is
    /// read. The caller decides whether the project automation clock advances.</summary>
    public void ReadMonitoring(Span<float> output, long projectFrame, bool advanceTimeline)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames || projectFrame < 0 ||
            (advanceTimeline && projectFrame > long.MaxValue - output.Length / 2))
            throw new ArgumentException("Invalid monitoring block or clock");
        if (output.IsEmpty) return;
        if (!MonitoringEnabled) { output.Clear(); return; }
        if (Volatile.Read(ref _monitoringEnabled) == 0)
        { output.Clear(); RenderTimeline(output, projectFrame, advanceTimeline); return; }
        Render(output, projectFrame, advanceTimeline, false);
    }
    private void Render(Span<float> output, long projectFrame, bool advanceTimeline, bool arrangement)
    {
        foreach (var controls in _instrumentControls) controls.Apply();
        int samples = output.Length;
        for (int bus = 0; bus < _sources.Length; bus++)
        {
            var input = _inputs.AsSpan(bus * samples, samples);
            if (arrangement) _sources[bus].Read(input);
            else input.Clear();
            if (_monitors[bus] is { } monitor)
            {
                var live = _monitorScratch.AsSpan(0, samples);
                monitor.ReadAt(live, projectFrame, advanceTimeline);
                for (int i = 0; i < samples; i++) input[i] += live[i];
            }
        }
        Graph.ProcessAt(_inputs.AsSpan(0, _sources.Length * samples), output, projectFrame, advanceTimeline);
        RenderTimeline(output, projectFrame, advanceTimeline);
    }

    private void RenderTimeline(Span<float> output, long frame, bool advancing)
    {
        if (_timelineMonitor is not { } monitor) return;
        var samples = _timelineScratch.AsSpan(0, output.Length);
        monitor.ReadAt(samples, frame, advancing);
        for (int i = 0; i < output.Length; i++) output[i] += samples[i];
    }

    public void Seek(long frame)
    {
        if (frame < 0 || frame > TotalFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        foreach (var controls in _instrumentControls) controls.Apply();
        foreach (var source in _sources) source.Seek(Math.Min(frame, source.TotalFrames));
        foreach (var monitor in _monitors) monitor?.Reset(frame);
        Graph.Reset(frame);
        _timelineMonitor?.Reset(frame);
        PositionFrames = frame;
    }
    public void Reset() => Seek(0);
}
