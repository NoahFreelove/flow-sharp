namespace Flow.Audio;

/// <summary>Frame-addressed note event with source provenance. EndFrame is note-off;
/// the instrument's bounded release follows it. Frequencies retain custom tuning.</summary>
public sealed record ScheduledNote(Guid ClipId, Guid NoteId, long StartFrame, long EndFrame,
    double FrequencyHz, double Velocity = 0.63, double Gain = 1, double Pan = 0,
    Guid PlacementId = default, int RepeatIndex = 0);

// Historical public name retained for source compatibility; Sample selects the sampler kernel.
public sealed record SineVoiceSettings(int VoiceLimit = 64, double AttackMilliseconds = 5, double ReleaseMilliseconds = 20,
    PcmAsset? Sample = null, double RootFrequencyHz = 440, Flow.Audio.Graph.AudioGraphDefinition? VoiceGraph = null, Flow.Audio.Graph.GraphSampleSet? GraphSamples = null)
{
    /// <summary>Legacy sine/sampler tail. Graph tails are sample-rate-dependent and
    /// exposed by the prepared playback duration instead.</summary>
    public double TailSeconds => VoiceGraph is null ? ReleaseMilliseconds / 1000
        : throw new InvalidOperationException("Prepare the voice graph to determine its tail");
    public void Validate()
    {
        NoteInstrumentCatalog.Sine.Parameters[0].Validate(VoiceLimit);
        NoteInstrumentCatalog.Sine.Parameters[1].Validate(AttackMilliseconds);
        NoteInstrumentCatalog.Sine.Parameters[2].Validate(ReleaseMilliseconds);
        if (!double.IsFinite(RootFrequencyHz) || RootFrequencyHz is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(RootFrequencyHz));
        if (VoiceGraph is not null && (AttackMilliseconds != 5 || ReleaseMilliseconds != 20 || RootFrequencyHz != 440))
            throw new ArgumentException("Graph instruments own envelope and tuning; legacy settings must remain at defaults");
        if (VoiceGraph is { } graph && ((long)graph.Nodes.Count * VoiceLimit > 4096 ||
            graph.Nodes.Any(n => n.DeviceId == "flow.input" && n.Parameters["bus"] > 2)))
            throw new ArgumentException("Voice graph exceeds node budget or uses an unsupported input bus");
        var requiredSlots = VoiceGraph?.Nodes.Where(n => n.DeviceId == "flow.sample").Select(n => (int)n.Parameters["asset"]).Distinct().Order().ToArray() ?? [];
        var suppliedSlots = GraphSamples?.Assets.Keys.Order().ToArray() ?? [];
        if (!requiredSlots.SequenceEqual(suppliedSlots)) throw new ArgumentException("Graph sample bindings must match all referenced slots exactly");
        if (Sample is not null && VoiceGraph is not null) throw new ArgumentException("Choose a sample or a voice graph");
        if (Sample is not null && Sample.Frames == 0) throw new ArgumentException("Sampler requires nonempty audio");
    }
}

/// <summary>Prepared, bounded-polyphony sine/sampler or independent graph instrument. Starts/reconstructs held
/// notes at phase zero on seek; no prefix render or release-tail reconstruction.
/// Oldest active voice is stolen deterministically. One owner calls Read/Seek.</summary>
public sealed class PreparedNotePlayback : IPreparedAudioPlayback
{
    private struct Voice
    {
        internal bool Active;
        internal int LiveKey;
        internal long Started, Off, End, NaturalEnd;
        internal double Frequency, Amplitude, Gain;
        internal float Left, Right;
    }
    private readonly Flow.Audio.Graph.PreparedAudioGraph[]? _graphs;
    private readonly float[]? _graphInputs, _graphOutput;
    private Flow.Audio.Graph.PreparedAudioGraph.ParameterCommand[]? _pendingParameters;
    private Flow.Audio.Graph.PreparedAudioGraph.ParameterCommand[] _controlParameters = [];

    private readonly PcmAsset? _sample;
    private readonly double _rootFrequency;
    private readonly ScheduledNote[] _notes;
    private readonly Voice[] _voices;
    private readonly long[] _maxEnd;
    private readonly int _leafCount, _attack, _release;
    private int _next, _held;
    private long _liveProjectFrame;
    public int SampleRate { get; }
    public int MaxBlockFrames { get; }
    public long TotalFrames { get; }
    public long PositionFrames { get; private set; }
    public long StolenVoices { get; private set; }
    public IReadOnlyList<ScheduledNote> Notes { get; }

    public PreparedNotePlayback(IEnumerable<ScheduledNote> notes, int sampleRate, int maxBlockFrames,
        long minimumFrames = 0, SineVoiceSettings? settings = null, int maxNotes = 100_000, int maxOnsetsPerFrame = 1024, IEnumerable<Flow.Audio.Graph.GraphAutomationLane>? graphAutomation = null)
    {
        settings ??= new();
        settings.Validate();
        _sample = settings.Sample; _rootFrequency = settings.RootFrequencyHz;
        if (sampleRate is < 1 or > 384000 || maxBlockFrames is < 1 or > 65536 || minimumFrames < 0 || maxNotes < 1 || maxOnsetsPerFrame < 1)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        // Take at most one extra item so an unbounded enumerable cannot allocate indefinitely.
        var authored = notes.Take(checked(maxNotes + 1)).ToArray();
        if (authored.Length > maxNotes) throw new ArgumentException("Prepared note budget exceeded");
        foreach (var note in authored)
        {
            if (note is null || note.StartFrame < 0 || note.EndFrame <= note.StartFrame || !double.IsFinite(note.FrequencyHz) || note.FrequencyHz <= 0 ||
                !double.IsFinite(note.Velocity) || note.Velocity is < 0 or > 1 || !double.IsFinite(note.Gain) || note.Gain is < 0 or > 16 ||
                !double.IsFinite(note.Pan) || note.Pan is < -1 or > 1) throw new ArgumentException("Invalid scheduled note");
        }
        _notes = authored.OrderBy(n => n.StartFrame).ToArray();
        int onsets = 0; long previous = -1;
        foreach (var note in _notes)
        {
            onsets = note.StartFrame == previous ? onsets + 1 : 1;
            previous = note.StartFrame;
            if (onsets > maxOnsetsPerFrame) throw new ArgumentException("Simultaneous note-on budget exceeded");
        }
        SampleRate = sampleRate; MaxBlockFrames = maxBlockFrames;
        _attack = (int)Math.Ceiling(settings.AttackMilliseconds * sampleRate / 1000);
        _release = (int)Math.Ceiling(settings.ReleaseMilliseconds * sampleRate / 1000);
        var lanes = (graphAutomation ?? []).Take(257).ToArray();
        if (lanes.Length > 256 || (lanes.Length != 0 && settings.VoiceGraph is null)) throw new ArgumentException("Invalid voice automation");
        if (settings.VoiceGraph is { } definition)
        {
            // Bus 0 = Hz, 1 = gate, 2 = velocity, each broadcast to stereo.
            if ((long)definition.Nodes.Count * settings.VoiceLimit > 4096)
                throw new ArgumentException("Voice graph node budget exceeded");
            _graphs = new Flow.Audio.Graph.PreparedAudioGraph[settings.VoiceLimit];
            long remainingBytes = 64 * 1024 * 1024 - (long)maxBlockFrames * 8 * sizeof(float);
            _graphInputs = new float[maxBlockFrames * 6];
            _graphOutput = new float[maxBlockFrames * 2];
            for (int i = 0; i < _graphs.Length; i++)
            {
                var prepared = new Flow.Audio.Graph.PreparedAudioGraph(definition, sampleRate, maxBlockFrames, maxBufferBytes: remainingBytes, automation: lanes, samples: settings.GraphSamples?.Assets);
                if (prepared.InputBusCount > 3) throw new ArgumentException("Voice graph supports only pitch, gate and velocity buses");
                remainingBytes -= prepared.PreparedBufferBytes;
                _graphs[i] = prepared;
            }
            _release = checked((int)_graphs[0].TailFrames);
        }
        TotalFrames = Math.Max(minimumFrames, _notes.Select(n => checked(n.EndFrame + _release)).DefaultIfEmpty(0).Max());
        _voices = new Voice[settings.VoiceLimit];
        _leafCount = 1;
        while (_leafCount < _notes.Length) _leafCount *= 2;
        _maxEnd = new long[_leafCount * 2];
        for (int i = 0; i < _notes.Length; i++) _maxEnd[_leafCount + i] = _notes[i].EndFrame;
        for (int i = _leafCount - 1; i > 0; i--) _maxEnd[i] = Math.Max(_maxEnd[i * 2], _maxEnd[i * 2 + 1]);
        Notes = Array.AsReadOnly(_notes);
    }

    /// <summary>Single control producer; validates and coalesces a whole update before
    /// publication. Every voice receives it at the next nonempty Read or Seek.
    /// Newly started/stolen voices retain the latest targets when their state resets.</summary>
    public void SetLatestParameters(IEnumerable<Flow.Audio.Graph.GraphParameterValue> values)
    {
        if (_graphs is null) throw new InvalidOperationException("Live graph controls require graph voices");
        var updates = _graphs[0].PrepareParameterBatch(values);
        var merged = _controlParameters.ToDictionary(c => (c.Node, c.Parameter));
        foreach (var update in updates) merged[(update.Node, update.Parameter)] = update;
        if (merged.Count > 4096) throw new ArgumentException("Voice control target budget exceeded");
        var next = merged.Values.ToArray();
        _controlParameters = next;
        Volatile.Write(ref _pendingParameters, next);
    }
    internal Flow.Audio.Graph.PreparedAudioGraph.ParameterCommand[] PrepareVoiceParameters(IEnumerable<Flow.Audio.Graph.GraphParameterValue> values)
    {
        if (_graphs is null) throw new InvalidOperationException("Live graph controls require graph voices");
        return _graphs[0].PrepareParameterBatch(values);
    }
    internal void ApplyVoiceParameterBatch(Flow.Audio.Graph.PreparedAudioGraph.ParameterCommand[] batch)
    {
        foreach (var graph in _graphs!) graph.ApplyParameterBatch(batch);
    }
    private void ApplyVoiceParameters()
    {
        var batch = Interlocked.Exchange(ref _pendingParameters, null);
        if (batch is null) return;
        foreach (var graph in _graphs!) graph.ApplyParameterBatch(batch);
    }

    public int Read(Span<float> output) => ReadCore(output, true);
    internal void BeginLiveBlock(long projectFrame) { ApplyVoiceParameters(); _liveProjectFrame = projectFrame; }
    internal int ReadLive(Span<float> output, long projectFrame, bool advanceTimeline) => ReadCore(output, false, projectFrame, advanceTimeline);

    private int ReadCore(Span<float> output, bool applyParameters, long? projectFrame = null, bool advanceTimeline = true)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames) throw new ArgumentException("Invalid stereo block");
        output.Clear();
        int frames = (int)Math.Min(output.Length / 2, TotalFrames - PositionFrames);
        if (_graphs is not null)
        {
            if (frames > 0 && applyParameters) ApplyVoiceParameters();
            ReadGraphs(output, frames, projectFrame, advanceTimeline);
            PositionFrames += frames;
            return frames;
        }
        for (int i = 0; i < frames; i++)
        {
            long frame = PositionFrames + i;
            while (_next < _notes.Length && _notes[_next].StartFrame <= frame)
                Start(_notes[_next++], frame);
            for (int v = 0; v < _voices.Length; v++)
            {
                ref var voice = ref _voices[v];
                if (!voice.Active) continue;
                if (frame >= voice.End || frame >= voice.NaturalEnd) { voice.Active = false; continue; }
                long age = frame - voice.Started;
                double envelope = _attack == 0 ? 1 : Math.Min(1, age / (double)_attack);
                if (frame >= voice.Off)
                {
                    double atOff = _attack == 0 ? 1 : Math.Min(1, (voice.Off - voice.Started) / (double)_attack);
                    envelope = atOff * (voice.End - frame) / _release;
                }
                if (_sample is null)
                {
                    float sample = (float)(0.3 * voice.Amplitude * envelope * Math.Sin(2 * Math.PI * voice.Frequency * (age / (double)SampleRate)));
                    output[i * 2] += sample * voice.Left; output[i * 2 + 1] += sample * voice.Right;
                }
                else
                {
                    double position = age * (voice.Frequency / _rootFrequency) * _sample.SampleRate / SampleRate;
                    if (position >= _sample.Frames) { voice.Active = false; continue; }
                    long sourceFrame = (long)position; double fraction = position - sourceFrame;
                    float left = (float)(_sample.Sample(sourceFrame, 0) * (1 - fraction) + _sample.Sample(sourceFrame + 1, 0) * fraction);
                    float right = (float)(_sample.Sample(sourceFrame, 1) * (1 - fraction) + _sample.Sample(sourceFrame + 1, 1) * fraction);
                    // Preserve the recorded stereo image at center; pan attenuates the opposite side.
                    float center = (float)Math.Sqrt(2);
                    output[i * 2] += left * (float)(voice.Amplitude * envelope) * Math.Min(1, voice.Left * center);
                    output[i * 2 + 1] += right * (float)(voice.Amplitude * envelope) * Math.Min(1, voice.Right * center);
                }
            }
        }
        PositionFrames += frames;
        return frames;
    }

    // Event-delimited segments keep gates constant and preserve voice summation order.
    // Scratch buffers are shared sequentially by all independently owned voice graphs.
    private void ReadGraphs(Span<float> output, int frames, long? projectFrame, bool advanceTimeline)
    {
        int offset = 0;
        while (offset < frames)
        {
            long frame = PositionFrames + offset;
            while (_next < _notes.Length && _notes[_next].StartFrame <= frame)
                Start(_notes[_next++], frame);
            long boundary = PositionFrames + frames;
            if (_next < _notes.Length) boundary = Math.Min(boundary, _notes[_next].StartFrame);
            for (int v = 0; v < _voices.Length; v++)
            {
                ref var voice = ref _voices[v];
                if (!voice.Active) continue;
                if (frame >= voice.End) { voice.Active = false; continue; }
                boundary = Math.Min(boundary, voice.End);
                if (voice.Off > frame) boundary = Math.Min(boundary, voice.Off);
            }
            int samples = checked((int)(boundary - frame) * 2);
            var destination = output.Slice(offset * 2, samples);
            var rendered = _graphOutput.AsSpan(0, samples);
            for (int v = 0; v < _voices.Length; v++)
            {
                ref var voice = ref _voices[v];
                if (!voice.Active) continue;
                var graph = _graphs![v];
                var inputs = _graphInputs.AsSpan(0, samples * graph.InputBusCount);
                if (graph.InputBusCount > 0) inputs[..samples].Fill((float)voice.Frequency);
                if (graph.InputBusCount > 1) inputs.Slice(samples, samples).Fill(frame < voice.Off ? 1 : 0);
                if (graph.InputBusCount > 2) inputs.Slice(samples * 2, samples).Fill((float)voice.Amplitude);
                if (projectFrame is { } timeline)
                    graph.ProcessAt(inputs, rendered, timeline + (advanceTimeline ? offset : 0), advanceTimeline);
                else graph.Process(inputs, rendered);
                float center = (float)Math.Sqrt(2);
                float left = Math.Min(1, voice.Left * center), right = Math.Min(1, voice.Right * center);
                for (int i = 0; i < samples; i += 2)
                {
                    destination[i] += rendered[i] * (float)voice.Gain * left;
                    destination[i + 1] += rendered[i + 1] * (float)voice.Gain * right;
                }
            }
            offset += samples / 2;
        }
    }

    private void Start(ScheduledNote note, long frame) =>
        StartVoice(note.FrequencyHz, note.Velocity, note.Gain, note.Pan, note.EndFrame, frame, -1);

    private void StartVoice(double frequency, double velocity, double gain, double pan, long off, long frame, int liveKey, long? projectFrame = null)
    {
        int slot = -1;
        for (int i = 0; i < _voices.Length; i++)
            if (!_voices[i].Active || _voices[i].End <= frame || _voices[i].NaturalEnd <= frame) { slot = i; break; }
        if (slot < 0)
        {
            slot = 0;
            for (int i = 1; i < _voices.Length; i++) if (_voices[i].Started < _voices[slot].Started) slot = i;
            StolenVoices++;
        }
        Flow.Audio.Graph.StereoPanKernel.Gains((float)pan, out float left, out float right);
        long naturalEnd = long.MaxValue;
        if (_sample is not null)
        {
            double length = Math.Ceiling(_sample.Frames / (frequency / _rootFrequency * _sample.SampleRate / SampleRate));
            if (length < long.MaxValue - (double)frame) naturalEnd = frame + Math.Max(1, (long)length);
        }
        _graphs?[slot].Reset(projectFrame ?? frame);
        _voices[slot] = new() { NaturalEnd = naturalEnd, Active = true, Started = frame, Off = off, End = off > long.MaxValue - _release ? long.MaxValue : off + _release, LiveKey = liveKey,
            Frequency = frequency, Amplitude = _graphs is null ? velocity * gain : velocity, Gain = gain, Left = left, Right = right };
    }

    // Audio-owner entry points for live MIDI. Reuse the exact scheduled voice
    // kernels, graph reset, controls, stealing and release behavior. No note objects
    // or graphs are constructed here. The wrapper owns key validation/admission.
    internal void LiveNoteOn(int key, double frequency, double velocity)
    {
        LiveNoteOff(key);
        StartVoice(frequency, velocity, 1, 0, long.MaxValue, PositionFrames, key, _liveProjectFrame);
    }
    internal void LiveNoteOff(int key)
    {
        for (int i = 0; i < _voices.Length; i++)
        {
            ref var voice = ref _voices[i];
            if (!voice.Active || voice.LiveKey != key || voice.Off != long.MaxValue) continue;
            voice.Off = PositionFrames;
            voice.End = PositionFrames > long.MaxValue - _release ? long.MaxValue : PositionFrames + _release;
        }
    }
    internal void LivePanic(int channel = -1)
    {
        for (int i = 0; i < _voices.Length; i++)
            if (_voices[i].LiveKey >= 0 && (channel < 0 || _voices[i].LiveKey / 128 == channel))
                _voices[i].Active = false;
    }

    public void Seek(long frame)
    {
        if (frame < 0 || frame > TotalFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        ApplyVoiceParameters();
        Array.Clear(_voices);
        if (_graphs is not null) foreach (var graph in _graphs) graph.Reset(frame);
        PositionFrames = frame;
        int low = 0, high = _notes.Length;
        while (low < high) { int mid = low + (high - low) / 2; if (_notes[mid].StartFrame < frame) low = mid + 1; else high = mid; }
        _next = low;
        _held = 0;
        if (frame < TotalFrames) RestoreHeld(1, 0, _leafCount, low, frame);
    }

    // Interval max tree prunes expired notes. Visit newer notes first, at most the
    // voice budget, so seeks don't scan an entire long project on the callback.
    private void RestoreHeld(int node, int from, int to, int before, long frame)
    {
        if (_held == _voices.Length || from >= before || _maxEnd[node] <= frame) return;
        if (to - from == 1) { Start(_notes[from], frame); _held++; return; }
        int middle = from + (to - from) / 2;
        RestoreHeld(node * 2 + 1, middle, to, before, frame);
        RestoreHeld(node * 2, from, middle, before, frame);
    }
    public void Reset() => Seek(0);
}
