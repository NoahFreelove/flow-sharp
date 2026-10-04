namespace Flow.Audio.Graph;

public readonly record struct GraphParameterValue(string NodeId, string ParameterId, double Value);

public readonly record struct StereoMeter(float PeakLeft, float PeakRight, double RmsLeft, double RmsRight);

/// <summary>Prepared stereo DAG. One audio owner calls Process/Reset; one control
/// producer calls TrySetParameter. No allocation, locks, user callbacks or IO in
/// valid Process calls. Inputs are contiguous bus blocks of the current frame count.
/// Live and offline hosts call this same processor.</summary>
public sealed class PreparedAudioGraph
{
    private sealed class Ramp(double value)
    {
        internal double Current = value, Target = value, Step;
        internal int Remaining;
        internal void Set(double value, int frames)
        {
            Target = value;
            Remaining = frames;
            Step = frames == 0 ? 0 : (Target - Current) / frames;
            if (frames == 0) Current = Target;
        }
        internal double Next()
        {
            if (Remaining > 0 && --Remaining == 0) Current = Target;
            else if (Remaining > 0) Current += Step;
            return Current;
        }
    }
    private sealed class Automation(GraphAutomationLane lane, int parameter, double initial)
    {
        internal readonly GraphAutomationLane Lane = lane;
        internal readonly int Parameter = parameter;
        internal readonly double Initial = initial;
        internal int Previous = -1;
        internal double Read(long frame)
        {
            while (Previous + 1 < Lane.Points.Count && Lane.Points[Previous + 1].Frame <= frame) Previous++;
            return Lane.ValueAtIndex(frame, Previous, Initial);
        }
        internal void Seek(long frame)
        {
            int low = 0, high = Lane.Points.Count;
            while (low < high) { int mid = low + (high - low) / 2; if (Lane.Points[mid].Frame <= frame) low = mid + 1; else high = mid; }
            Previous = low - 1;
        }
    }
    private sealed class Node(AudioGraphNode definition, int[] inputs, int samples, int sampleRate)
    {
        internal readonly AudioGraphNode Definition = definition;
        internal readonly int[] Inputs = inputs;
        internal readonly float[] Buffer = new float[samples];
        internal readonly DeviceDefinition Device = DeviceCatalog.Get(definition.DeviceId, definition.Version);
        internal readonly Ramp[] Parameters = DeviceCatalog.Get(definition.DeviceId, definition.Version).Parameters
            .Select(p => new Ramp(definition.Parameters[p.Id])).ToArray();
        internal readonly int DelayFrames = definition.DeviceId == "flow.delay" ? Math.Max(1, (int)Math.Round(definition.Parameters["timeMs"] * sampleRate / 1000, MidpointRounding.AwayFromZero)) : 0;
        internal float[] Delay = [];
        internal int DelayPosition;
        internal double PhaseLeft, PhaseRight, FilterLeft, FilterRight;
        internal GateEnvelope EnvelopeLeft, EnvelopeRight;
        internal PcmAsset? Sample;
        internal double SampleLeft, SampleRight;
        internal bool SampleGateLeft, SampleGateRight, SampleStartedLeft, SampleStartedRight;
        internal readonly int AttackFrames = definition.DeviceId == "flow.adsr" ? (int)Math.Ceiling(definition.Parameters["attackMs"] * sampleRate / 1000) : 0;
        internal readonly int DecayFrames = definition.DeviceId == "flow.adsr" ? (int)Math.Ceiling(definition.Parameters["decayMs"] * sampleRate / 1000) : 0;
        internal readonly int ReleaseFrames = definition.DeviceId == "flow.adsr" ? (int)Math.Ceiling(definition.Parameters["releaseMs"] * sampleRate / 1000) : 0;
        internal Automation[] Automation = [];
        internal StereoMeter Meter;
    }
    internal readonly record struct ParameterCommand(int Node, int Parameter, double Value);
    private readonly Node[] _nodes;
    private readonly Dictionary<string, int> _indices;
    private readonly int _output;
    private readonly ParameterCommand[] _commands;
    private ParameterCommand[]? _latestBatch;
    private int _head, _tail;
    private long _position;
    private readonly StereoMeter[] _publishedMeters;
    private long _meterSequence, _publishedFrame;
    public IReadOnlyList<string> MeterNodeIds { get; }
    public int SampleRate { get; }
    public int MaxBlockFrames { get; }
    public int InputBusCount { get; }
    public int ParameterCapacity => _commands.Length - 1;
    public long PreparedBufferBytes { get; }
    public long TailFrames { get; }

    public PreparedAudioGraph(AudioGraphDefinition graph, int sampleRate = 48000, int maxBlockFrames = 256,
        int parameterCapacity = 256, long maxBufferBytes = 64 * 1024 * 1024, IEnumerable<GraphAutomationLane>? automation = null, IReadOnlyDictionary<int, PcmAsset>? samples = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (sampleRate is < 1 or > 384000 || maxBlockFrames is < 1 or > 65536 || parameterCapacity is < 1 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        PreparedBufferBytes = checked((long)graph.Nodes.Count * maxBlockFrames * 2 * sizeof(float));
        foreach (var n in graph.Nodes.Where(n => n.DeviceId == "flow.delay" && !n.Bypassed))
            PreparedBufferBytes = checked(PreparedBufferBytes + (Math.Max(1L, (long)Math.Round(n.Parameters["timeMs"] * sampleRate / 1000, MidpointRounding.AwayFromZero)) * (int)n.Parameters["repeats"] + 1) * 8);
        if (maxBufferBytes < 1 || PreparedBufferBytes > maxBufferBytes) throw new ArgumentException("Graph exceeds prepared buffer budget");
        var sorted = graph.GetProcessingOrder();
        long sampleBytes = 0;
        var seenSamples = new HashSet<PcmAsset>(ReferenceEqualityComparer.Instance);
        foreach (var definition in sorted.Where(n => n.DeviceId == "flow.sample"))
        {
            if (samples is null || !samples.TryGetValue((int)definition.Parameters["asset"], out var asset) || asset is null || asset.Frames == 0)
                throw new ArgumentException("Missing or empty graph sample asset");
            if (seenSamples.Add(asset) && (sampleBytes += asset.Bytes) > 64 * 1024 * 1024)
                throw new ArgumentException("Graph sample assets exceed 64 MiB");
        }
        _indices = sorted.Select((n, i) => (n.Id, i)).ToDictionary(p => p.Id, p => p.i);
        _nodes = sorted.Select(n => new Node(n, n.Inputs.Select(i => _indices[i]).ToArray(), maxBlockFrames * 2, sampleRate)
        { Sample = n.DeviceId == "flow.sample" ? samples![(int)n.Parameters["asset"]] : null }).ToArray();
        var tails = new long[_nodes.Length];
        for (int i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            tails[i] = node.Inputs.Select(index => tails[index]).DefaultIfEmpty().Max();
            if (node.Definition.DeviceId == "flow.adsr") tails[i] = checked(tails[i] + node.ReleaseFrames);
            if (node.Definition.DeviceId == "flow.lowPass") tails[i] = checked(tails[i] + sampleRate * 2L);
            if (node.Definition.DeviceId == "flow.delay" && !node.Definition.Bypassed)
            {
                int length = checked(node.DelayFrames * (int)node.Definition.Parameters["repeats"]);
                node.Delay = new float[checked((length + 1) * 2)];
                tails[i] = checked(tails[i] + length);
            }
        }
        _publishedMeters = new StereoMeter[_nodes.Length];
        MeterNodeIds = Array.AsReadOnly(sorted.Select(n => n.Id).ToArray());
        _output = _indices[graph.OutputId];
        TailFrames = tails[_output];
        if (TailFrames > (long)sampleRate * 120) throw new ArgumentException("Graph exceeds 120-second tail budget");
        InputBusCount = sorted.Where(n => n.DeviceId == "flow.input").Select(n => (int)n.Parameters["bus"] + 1).DefaultIfEmpty().Max();
        SampleRate = sampleRate; MaxBlockFrames = maxBlockFrames;
        _commands = new ParameterCommand[parameterCapacity + 1];
        var lanes = (automation ?? []).Take(257).ToArray();
        if (lanes.Length > 256 || lanes.Sum(l => (long)l.Points.Count) > 100000) throw new ArgumentException("Automation budget exceeded");
        var seen = new HashSet<(string, string)>();
        foreach (var lane in lanes)
        {
            if (!seen.Add((lane.NodeId, lane.ParameterId)) || !_indices.TryGetValue(lane.NodeId, out int index))
                throw new ArgumentException("Duplicate automation lane or missing node");
            var node = _nodes[index];
            int parameter = -1;
            for (int i = 0; i < node.Device.Parameters.Count; i++) if (node.Device.Parameters[i].Id == lane.ParameterId) parameter = i;
            if (parameter < 0 || !node.Device.Parameters[parameter].Automatable) throw new ArgumentException("Parameter cannot be automated");
            foreach (var point in lane.Points) node.Device.Parameters[parameter].Validate(point.Value);
            node.Automation = node.Automation.Append(new Automation(lane, parameter, node.Definition.Parameters[lane.ParameterId])).ToArray();
        }
    }

    /// <summary>Bounded control-thread submission. Applies at the next nonempty block
    /// and ramps over the catalog duration. Invalid parameters fail before queuing.</summary>
    public bool TrySetParameter(string nodeId, string parameterId, double value)
    {
        var (index, parameter) = ValidateParameter(nodeId, parameterId, value);
        int next = (_tail + 1) % _commands.Length;
        if (next == Volatile.Read(ref _head)) return false;
        _commands[_tail] = new(index, parameter, value);
        Volatile.Write(ref _tail, next);
        return true;
    }

    /// <summary>Single control-producer latest-value mailbox per parameter. Bursts
    /// coalesce without queue backpressure. Applied after FIFO commands at the next
    /// nonempty block, with normal smoothing. Allocation occurs only on the caller.
    /// Use for continuous preview, including restoring the saved value on cancel.</summary>
    public void SetLatestParameter(string nodeId, string parameterId, double value) =>
        SetLatestParameters([new(nodeId, parameterId, value)]);

    /// <summary>Validate then publish a whole parameter batch atomically. Pending
    /// batches merge by target (latest value wins), so cancelling one gesture and
    /// starting another before audio runs cannot lose the restoration. One control
    /// producer; allocations/CAS retries occur here, never on the audio owner.</summary>
    public void SetLatestParameters(IEnumerable<GraphParameterValue> values)
    {
        var updates = values.Take(4097).ToArray();
        if (updates.Length is < 1 or > 4096) throw new ArgumentException("Parameter batch needs 1–4096 targets");
        var changes = new Dictionary<(int, int), ParameterCommand>();
        foreach (var update in updates)
        {
            var (node, parameter) = ValidateParameter(update.NodeId, update.ParameterId, update.Value);
            if (!changes.TryAdd((node, parameter), new(node, parameter, update.Value))) throw new ArgumentException("Duplicate parameter target");
        }
        while (true)
        {
            var observed = Volatile.Read(ref _latestBatch);
            var merged = observed?.ToDictionary(c => (c.Node, c.Parameter)) ?? new Dictionary<(int, int), ParameterCommand>();
            foreach (var change in changes) merged[change.Key] = change.Value;
            var next = merged.Values.ToArray();
            if (ReferenceEquals(Interlocked.CompareExchange(ref _latestBatch, next, observed), observed)) return;
        }
    }

    // Voice-pool control producer compiles once; all identical graphs share indices.
    internal ParameterCommand[] PrepareParameterBatch(IEnumerable<GraphParameterValue> values)
    {
        var batch = values.Take(4097).ToArray();
        if (batch.Length is < 1 or > 4096) throw new ArgumentException("Parameter batch needs 1–4096 targets");
        var seen = new HashSet<(int, int)>();
        return batch.Select(value =>
        {
            var target = ValidateParameter(value.NodeId, value.ParameterId, value.Value);
            if (!seen.Add(target)) throw new ArgumentException("Duplicate parameter target");
            return new ParameterCommand(target.Node, target.Parameter, value.Value);
        }).ToArray();
    }
    // Audio owner only; commands were validated against the identical prepared graph.
    internal void ApplyParameterBatch(ReadOnlySpan<ParameterCommand> commands)
    {
        foreach (var command in commands)
        {
            var node = _nodes[command.Node];
            if (node.Parameters[command.Parameter].Target == command.Value) continue;
            node.Parameters[command.Parameter].Set(command.Value,
                (int)Math.Ceiling(node.Device.Parameters[command.Parameter].SmoothingMilliseconds * SampleRate / 1000));
        }
    }

    private (int Node, int Parameter) ValidateParameter(string nodeId, string parameterId, double value)
    {
        int index = _indices[nodeId];
        var parameters = _nodes[index].Device.Parameters;
        int parameter = -1;
        for (int i = 0; i < parameters.Count; i++) if (parameters[i].Id == parameterId) parameter = i;
        if (parameter < 0 || !parameters[parameter].Automatable) throw new ArgumentException("Unknown or non-automatable parameter");
        if (_nodes[index].Automation.Any(l => l.Parameter == parameter))
            throw new InvalidOperationException("Prepared automation owns this parameter; prepare a changed lane to edit it");
        parameters[parameter].Validate(value);
        return (index, parameter);
    }

    /// <summary>Audio-owner access only. Other threads use TryReadMeters.</summary>
    public StereoMeter GetMeter(string nodeId) => _nodes[_indices[nodeId]].Meter;

    /// <summary>Any reader may copy a coherent completed-block snapshot into its own
    /// scratch buffer. Nonblocking, at most three attempts. On false, ignore buffer
    /// contents and retain the UI's previous snapshot. Order matches MeterNodeIds.</summary>
    public bool TryReadMeters(Span<StereoMeter> destination, out long frame)
    {
        if (destination.Length != _publishedMeters.Length) throw new ArgumentException("Meter buffer must match graph node count");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            long sequence = Volatile.Read(ref _meterSequence);
            if ((sequence & 1) != 0) continue;
            _publishedMeters.AsSpan().CopyTo(destination);
            long position = Volatile.Read(ref _publishedFrame);
            Thread.MemoryBarrier();
            if (Volatile.Read(ref _meterSequence) == sequence) { frame = position; return true; }
        }
        frame = 0; return false;
    }
    private void PublishMeters()
    {
        // A single audio owner writes. Fences bracket a short preallocated copy;
        // readers never block the audio owner or pin one of its processing buffers.
        Interlocked.Increment(ref _meterSequence);
        for (int i = 0; i < _nodes.Length; i++) _publishedMeters[i] = _nodes[i].Meter;
        _publishedFrame = _position;
        Interlocked.Increment(ref _meterSequence);
    }

    public void Process(ReadOnlySpan<float> inputs, Span<float> output) => ProcessAt(inputs, output, _position);

    /// <summary>Render DSP while explicitly positioning project automation. A frozen
    /// project clock holds lane values, but oscillators, envelopes, delay and control
    /// smoothing continue. Repositioning lanes does not reset DSP state.</summary>
    public void ProcessAt(ReadOnlySpan<float> inputs, Span<float> output, long projectFrame, bool advanceTimeline = true)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames || inputs.Length != (long)output.Length * InputBusCount)
            throw new ArgumentException("Graph requires equal stereo bus blocks within its prepared frame limit");
        if (projectFrame < 0 || (advanceTimeline && projectFrame > long.MaxValue - output.Length / 2))
            throw new ArgumentOutOfRangeException(nameof(projectFrame));
        if (output.IsEmpty) return;
        if (projectFrame != _position)
            foreach (var node in _nodes)
                foreach (var lane in node.Automation) lane.Seek(projectFrame);
        _position = projectFrame;
        ApplyCommands();
        foreach (var node in _nodes)
        {
            var buffer = node.Buffer.AsSpan(0, output.Length);
            string kind = node.Definition.DeviceId;
            if (kind == "flow.input") inputs.Slice((int)node.Parameters[0].Current * output.Length, output.Length).CopyTo(buffer);
            else if (kind == "flow.value")
            {
                for (int i = 0; i < buffer.Length; i += 2)
                {
                    ApplyAutomation(node, _position + (advanceTimeline ? i / 2 : 0));
                    buffer[i] = buffer[i + 1] = (float)node.Parameters[0].Next();
                }
            }
            else
            {
                _nodes[node.Inputs[0]].Buffer.AsSpan(0, output.Length).CopyTo(buffer);
                if (kind == "flow.sum")
                    for (int input = 1; input < node.Inputs.Length; input++)
                        for (int i = 0; i < output.Length; i++) buffer[i] += _nodes[node.Inputs[input]].Buffer[i];
                else if (kind == "flow.multiply")
                {
                    var right = _nodes[node.Inputs[1]].Buffer;
                    for (int i = 0; i < buffer.Length; i++)
                        buffer[i] = (float)Math.Clamp((double)buffer[i] * right[i], -float.MaxValue, float.MaxValue);
                }
                else if (kind == "flow.sample")
                {
                    var gate = _nodes[node.Inputs[1]].Buffer;
                    for (int i = 0; i < buffer.Length; i += 2)
                    {
                        buffer[i] = ReadSample(node, buffer[i], gate[i], 0, ref node.SampleLeft, ref node.SampleGateLeft, ref node.SampleStartedLeft);
                        buffer[i + 1] = ReadSample(node, buffer[i + 1], gate[i + 1], 1, ref node.SampleRight, ref node.SampleGateRight, ref node.SampleStartedRight);
                    }
                }
                else if (kind is "flow.sineOsc" or "flow.sawOsc" or "flow.squareOsc")
                    for (int i = 0; i < buffer.Length; i += 2)
                    {
                        double frequencyLeft = Math.Clamp(float.IsNaN(buffer[i]) ? 0 : buffer[i], -SampleRate * .49, SampleRate * .49);
                        double frequencyRight = Math.Clamp(float.IsNaN(buffer[i + 1]) ? 0 : buffer[i + 1], -SampleRate * .49, SampleRate * .49);
                        buffer[i] = (float)(kind == "flow.sineOsc" ? Math.Sin(2 * Math.PI * node.PhaseLeft) :
                            kind == "flow.sawOsc" ? OscillatorWaveforms.Saw(node.PhaseLeft, frequencyLeft / SampleRate) :
                            OscillatorWaveforms.Square(node.PhaseLeft, frequencyLeft / SampleRate));
                        buffer[i + 1] = (float)(kind == "flow.sineOsc" ? Math.Sin(2 * Math.PI * node.PhaseRight) :
                            kind == "flow.sawOsc" ? OscillatorWaveforms.Saw(node.PhaseRight, frequencyRight / SampleRate) :
                            OscillatorWaveforms.Square(node.PhaseRight, frequencyRight / SampleRate));
                        node.PhaseLeft += frequencyLeft / SampleRate; node.PhaseLeft -= Math.Floor(node.PhaseLeft);
                        node.PhaseRight += frequencyRight / SampleRate; node.PhaseRight -= Math.Floor(node.PhaseRight);
                    }
                else if (kind == "flow.lowPass")
                {
                    var cutoff = _nodes[node.Inputs[1]].Buffer;
                    for (int i = 0; i < buffer.Length; i += 2)
                    {
                        double poleLeft = Math.Exp(-2 * Math.PI * Math.Clamp(float.IsNaN(cutoff[i]) ? 0 : cutoff[i], Math.Min(1, SampleRate * .49), SampleRate * .49) / SampleRate);
                        double poleRight = Math.Exp(-2 * Math.PI * Math.Clamp(float.IsNaN(cutoff[i + 1]) ? 0 : cutoff[i + 1], Math.Min(1, SampleRate * .49), SampleRate * .49) / SampleRate);
                        node.FilterLeft = (1 - poleLeft) * (float.IsFinite(buffer[i]) ? buffer[i] : 0) + poleLeft * node.FilterLeft;
                        node.FilterRight = (1 - poleRight) * (float.IsFinite(buffer[i + 1]) ? buffer[i + 1] : 0) + poleRight * node.FilterRight;
                        buffer[i] = (float)node.FilterLeft; buffer[i + 1] = (float)node.FilterRight;
                    }
                }
                else if (kind == "flow.adsr")
                    for (int i = 0; i < buffer.Length; i += 2)
                    {
                        bool leftGate = float.IsFinite(buffer[i]) && buffer[i] > 0;
                        bool rightGate = float.IsFinite(buffer[i + 1]) && buffer[i + 1] > 0;
                        double sustain = node.Definition.Parameters["sustain"];
                        buffer[i] = (float)node.EnvelopeLeft.Read(leftGate, node.AttackFrames, node.DecayFrames, sustain, node.ReleaseFrames);
                        buffer[i + 1] = (float)node.EnvelopeRight.Read(rightGate, node.AttackFrames, node.DecayFrames, sustain, node.ReleaseFrames);
                    }
                else if (kind == "flow.tanh")
                    for (int i = 0; i < buffer.Length; i++) buffer[i] = (float)Math.Tanh(buffer[i]);
                else if (kind == "flow.delay") ProcessDelay(node, buffer, _position, advanceTimeline);
                else
                    for (int i = 0; i < buffer.Length; i += 2)
                    {
                        ApplyAutomation(node, _position + (advanceTimeline ? i / 2 : 0));
                        double value = node.Parameters[0].Next();
                        if (node.Definition.Bypassed) continue;
                        if (kind == "flow.gain") { buffer[i] *= (float)value; buffer[i + 1] *= (float)value; }
                        else if (kind == "flow.pan")
                        {
                            float mono = (buffer[i] + buffer[i + 1]) * 0.5f;
                            StereoPanKernel.Gains((float)value, out float left, out float right);
                            buffer[i] = mono * left; buffer[i + 1] = mono * right;
                        }
                        else if (kind == "flow.drive")
                        { buffer[i] = (float)Math.Tanh(buffer[i] * value); buffer[i + 1] = (float)Math.Tanh(buffer[i + 1] * value); }
                    }
            }
            float peakL = 0, peakR = 0;
            double sumL = 0, sumR = 0;
            for (int i = 0; i < buffer.Length; i += 2)
            {
                peakL = Math.Max(peakL, Math.Abs(buffer[i])); peakR = Math.Max(peakR, Math.Abs(buffer[i + 1]));
                sumL += (double)buffer[i] * buffer[i]; sumR += (double)buffer[i + 1] * buffer[i + 1];
            }
            node.Meter = new(peakL, peakR, Math.Sqrt(sumL / (buffer.Length / 2)), Math.Sqrt(sumR / (buffer.Length / 2)));
        }
        _nodes[_output].Buffer.AsSpan(0, output.Length).CopyTo(output);
        if (advanceTimeline) _position = checked(_position + output.Length / 2);
        PublishMeters();
    }

    /// <summary>Audio-owner stop/seek reset. Apply pending parameter targets, finish
    /// their ramps immediately and clear buffers/meters. Monitoring hosts use ProcessAt
    /// with a frozen timeline on pause; other hosts may skip processing.</summary>
    public void Reset(long frame = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        _position = frame;
        ApplyCommands();
        foreach (var node in _nodes)
        {
            Array.Clear(node.Buffer);
            Array.Clear(node.Delay); node.DelayPosition = 0;
            node.PhaseLeft = node.PhaseRight = node.FilterLeft = node.FilterRight = 0;
            node.EnvelopeLeft = node.EnvelopeRight = default;
            node.SampleLeft = node.SampleRight = 0;
            node.SampleGateLeft = node.SampleGateRight = node.SampleStartedLeft = node.SampleStartedRight = false;
            node.Meter = default;
            foreach (var ramp in node.Parameters) ramp.Set(ramp.Target, 0);
            foreach (var lane in node.Automation) lane.Seek(frame);
            ApplyAutomation(node, frame);
        }
        PublishMeters();
    }
    private float ReadSample(Node node, float frequency, float gate, int channel, ref double position, ref bool wasGate, ref bool started)
    {
        bool on = float.IsFinite(gate) && gate > 0;
        if (on && !wasGate) { position = 0; started = true; }
        wasGate = on;
        var asset = node.Sample!;
        if (!started || position >= asset.Frames) return 0;
        long frame = (long)position; double fraction = position - frame;
        float value = (float)(asset.Sample(frame, channel) * (1 - fraction) + asset.Sample(frame + 1, channel) * fraction);
        double hz = float.IsFinite(frequency) ? Math.Clamp(frequency, 0, 100000) : 0;
        position = Math.Min(asset.Frames, position + hz / node.Definition.Parameters["rootHz"] * asset.SampleRate / SampleRate);
        return value;
    }

    private static void ProcessDelay(Node node, Span<float> buffer, long framePosition, bool advanceTimeline)
    {
        int repeats = (int)node.Parameters[1].Current;
        for (int i = 0; i < buffer.Length; i += 2)
        {
            ApplyAutomation(node, framePosition + (advanceTimeline ? i / 2 : 0));
            double feedback = node.Parameters[2].Next(), wet = node.Parameters[3].Next();
            if (node.Definition.Bypassed) continue;
            node.Delay[node.DelayPosition] = buffer[i]; node.Delay[node.DelayPosition + 1] = buffer[i + 1];
            double left = 0, right = 0, amplitude = 1;
            for (int repeat = 1; repeat <= repeats; repeat++)
            {
                int position = node.DelayPosition - repeat * node.DelayFrames * 2;
                if (position < 0) position += node.Delay.Length;
                left += node.Delay[position] * amplitude; right += node.Delay[position + 1] * amplitude;
                amplitude *= feedback;
            }
            buffer[i] = (float)(buffer[i] * (1 - wet) + left * wet);
            buffer[i + 1] = (float)(buffer[i + 1] * (1 - wet) + right * wet);
            node.DelayPosition += 2; if (node.DelayPosition == node.Delay.Length) node.DelayPosition = 0;
        }
    }
    private static void ApplyAutomation(Node node, long frame)
    {
        foreach (var lane in node.Automation) node.Parameters[lane.Parameter].Set(lane.Read(frame), 0);
    }
    private void ApplyCommands()
    {
        int boundary = Volatile.Read(ref _tail);
        while (_head != boundary)
        {
            var command = _commands[_head];
            Volatile.Write(ref _head, (_head + 1) % _commands.Length);
            var node = _nodes[command.Node];
            int frames = (int)Math.Ceiling(node.Device.Parameters[command.Parameter].SmoothingMilliseconds * SampleRate / 1000);
            node.Parameters[command.Parameter].Set(command.Value, frames);
        }
        if (Interlocked.Exchange(ref _latestBatch, null) is { } batch)
            foreach (var command in batch)
            {
                var node = _nodes[command.Node];
                int frames = (int)Math.Ceiling(node.Device.Parameters[command.Parameter].SmoothingMilliseconds * SampleRate / 1000);
                node.Parameters[command.Parameter].Set(command.Value, frames);
            }
    }
}
