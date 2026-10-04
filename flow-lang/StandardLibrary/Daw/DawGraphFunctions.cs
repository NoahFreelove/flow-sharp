using Flow.Audio.Graph;
using Flow.Studio.Model;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Daw;

public static class DawGraphFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        DawProjectFunctions.Register(registry);
        registry.Register("dawPluginSample", new FunctionSignature("dawPluginSample", [StringType.Instance]),
            _ => throw new InvalidOperationException("Packaged sample access requires a declared plugin build"));
        registry.Register("dawPluginCurve", new FunctionSignature("dawPluginCurve", [StringType.Instance, StringType.Instance,
            StringType.Instance, new DictType(DoubleType.Instance, DoubleType.Instance), BoolType.Instance]), args =>
        {
            var points = args[3].As<DictData>().Entries.Select(p => new MusicalAutomationPoint(p.Key.As<double>(), p.Value.As<double>())).OrderBy(p => p.Quarter);
            return new Value(new ProjectAutomationLane(Guid.Parse(args[0].As<string>()), Guid.Parse(args[1].As<string>()),
                "", args[2].As<string>(), points, args[4].As<bool>() ? AutomationShape.Linear : AutomationShape.Step,
                AutomationTargetKind.PluginParameter), DawAutomationType.Instance);
        });
        registry.Register("dawCurve", new FunctionSignature("dawCurve", [StringType.Instance, StringType.Instance,
            StringType.Instance, StringType.Instance, new DictType(DoubleType.Instance, DoubleType.Instance), BoolType.Instance]), args =>
        {
            var points = args[4].As<DictData>().Entries.Select(p => new MusicalAutomationPoint(p.Key.As<double>(), p.Value.As<double>())).OrderBy(p => p.Quarter);
            return new Value(new ProjectAutomationLane(Guid.Parse(args[0].As<string>()), Guid.Parse(args[1].As<string>()),
                args[2].As<string>(), args[3].As<string>(), points, args[5].As<bool>() ? AutomationShape.Linear : AutomationShape.Step), DawAutomationType.Instance);
        });
        registry.Register("dawAudio", new FunctionSignature("dawAudio", [BufferType.Instance]), args =>
        {
            var buffer = args[0].As<AudioBuffer>();
            if (buffer.Channels != 2) throw new ArgumentException("DAW audio assets require stereo; convert channel layout explicitly");
            return new Value(new Flow.Audio.PcmAsset(buffer.Data, buffer.SampleRate), DawAudioType.Instance);
        });
        registry.Register("dawSine", new FunctionSignature("dawSine", [IntType.Instance, MillisecondType.Instance, MillisecondType.Instance]), args =>
        {
            var settings = new Flow.Audio.SineVoiceSettings(args[0].As<int>(), args[1].As<double>(), args[2].As<double>());
            settings.Validate();
            return new Value(settings, DawInstrumentType.Instance);
        });
        registry.Register("dawSampler", new FunctionSignature("dawSampler", [DawAudioType.Instance, DoubleType.Instance, IntType.Instance, MillisecondType.Instance, MillisecondType.Instance]), args =>
        {
            var settings = new Flow.Audio.SineVoiceSettings(args[2].As<int>(), args[3].As<double>(), args[4].As<double>(),
                args[0].As<Flow.Audio.PcmAsset>(), args[1].As<double>());
            settings.Validate(); return new Value(settings, DawInstrumentType.Instance);
        });
        var graph = AudioGraphType.Instance;
        registry.Register("dawSample", new FunctionSignature("dawSample", [StringType.Instance, graph, graph, IntType.Instance, DoubleType.Instance]), args =>
            Wrap(AudioGraphDefinition.Sample(args[0].As<string>(), args[1].As<AudioGraphDefinition>(), args[2].As<AudioGraphDefinition>(), args[3].As<int>(), args[4].As<double>())));
        registry.Register("dawSampleInstrument", new FunctionSignature("dawSampleInstrument", [graph, IntType.Instance, new DictType(IntType.Instance, DawAudioType.Instance)]), args =>
        {
            var samples = new GraphSampleSet(args[2].As<DictData>().Entries.Select(p => new KeyValuePair<int, Flow.Audio.PcmAsset>(p.Key.As<int>(), p.Value.As<Flow.Audio.PcmAsset>())));
            var settings = new Flow.Audio.SineVoiceSettings(VoiceLimit: args[1].As<int>(), VoiceGraph: args[0].As<AudioGraphDefinition>(), GraphSamples: samples);
            settings.Validate(); return new Value(settings, DawInstrumentType.Instance);
        });
        registry.Register("dawGraphInstrument", new FunctionSignature("dawGraphInstrument", [graph, IntType.Instance]), args =>
        {
            var settings = new Flow.Audio.SineVoiceSettings(VoiceLimit: args[1].As<int>(), VoiceGraph: args[0].As<AudioGraphDefinition>());
            settings.Validate(); return new Value(settings, DawInstrumentType.Instance);
        });
        registry.Register("dawDelay", new FunctionSignature("dawDelay", [StringType.Instance, graph, MillisecondType.Instance, IntType.Instance, DoubleType.Instance, DoubleType.Instance]), args =>
            Wrap(args[1].As<AudioGraphDefinition>().Then(args[0].As<string>(), "flow.delay", new Dictionary<string, double>
            { ["timeMs"] = args[2].As<double>(), ["repeats"] = args[3].As<int>(), ["feedback"] = args[4].As<double>(), ["wet"] = args[5].As<double>() })));
        registry.Register("dawProcess", new FunctionSignature("dawProcess", [graph, BufferType.Instance,
            new ArrayType(DawAutomationType.Instance), new DictType(DoubleType.Instance, DoubleType.Instance)]), args =>
        {
            var buffer = args[1].As<AudioBuffer>();
            var tempo = new ProjectTempoMap(args[3].As<DictData>().Entries.Select(p => new TempoChange(p.Key.As<double>(), p.Value.As<double>())).OrderBy(p => p.Quarter));
            return Process(args[0].As<AudioGraphDefinition>(), [buffer], args[2].As<List<Value>>().Select(v =>
                MusicalAutomationCompiler.Lower(v.As<ProjectAutomationLane>(), tempo, buffer.SampleRate)).ToArray());
        });
        registry.Register("dawProcess", new FunctionSignature("dawProcess", [graph, new ArrayType(BufferType.Instance),
            new ArrayType(DawAutomationType.Instance), new DictType(DoubleType.Instance, DoubleType.Instance)]), args =>
        {
            var buffers = args[1].As<List<Value>>().Select(v => v.As<AudioBuffer>()).ToArray();
            if (buffers.Length is < 1 or > 64) throw new ArgumentException("Provide 1–64 audio buses");
            var tempo = new ProjectTempoMap(args[3].As<DictData>().Entries.Select(p => new TempoChange(p.Key.As<double>(), p.Value.As<double>())).OrderBy(p => p.Quarter));
            return Process(args[0].As<AudioGraphDefinition>(), buffers, args[2].As<List<Value>>().Select(v =>
                MusicalAutomationCompiler.Lower(v.As<ProjectAutomationLane>(), tempo, buffers[0].SampleRate)).ToArray());
        });
        registry.Register("dawProcess", new FunctionSignature("dawProcess", [graph, BufferType.Instance]),
            args => Process(args[0].As<AudioGraphDefinition>(), [args[1].As<AudioBuffer>()]));
        registry.Register("dawProcess", new FunctionSignature("dawProcess", [graph, new ArrayType(BufferType.Instance)]),
            args => Process(args[0].As<AudioGraphDefinition>(), args[1].As<List<Value>>().Select(v => v.As<AudioBuffer>()).ToArray()));
        registry.Register("dawInput", new FunctionSignature("dawInput", [StringType.Instance, IntType.Instance]),
            args => Wrap(AudioGraphDefinition.Input(args[0].As<string>(), args[1].As<int>())));
        registry.Register("dawValue", new FunctionSignature("dawValue", [StringType.Instance, DoubleType.Instance]),
            args => Wrap(AudioGraphDefinition.Value(args[0].As<string>(), args[1].As<double>())));
        registry.Register("dawMultiply", new FunctionSignature("dawMultiply", [StringType.Instance, graph, graph]),
            args => Wrap(AudioGraphDefinition.Multiply(args[0].As<string>(), args[1].As<AudioGraphDefinition>(), args[2].As<AudioGraphDefinition>())));
        registry.Register("dawTanh", new FunctionSignature("dawTanh", [StringType.Instance, graph]),
            args => Wrap(args[1].As<AudioGraphDefinition>().Then(args[0].As<string>(), "flow.tanh")));
        registry.Register("dawSineOsc", new FunctionSignature("dawSineOsc", [StringType.Instance, graph]),
            args => Wrap(args[1].As<AudioGraphDefinition>().Then(args[0].As<string>(), "flow.sineOsc")));
        registry.Register("dawSawOsc", new FunctionSignature("dawSawOsc", [StringType.Instance, graph]),
            args => Wrap(args[1].As<AudioGraphDefinition>().Then(args[0].As<string>(), "flow.sawOsc")));
        registry.Register("dawSquareOsc", new FunctionSignature("dawSquareOsc", [StringType.Instance, graph]),
            args => Wrap(args[1].As<AudioGraphDefinition>().Then(args[0].As<string>(), "flow.squareOsc")));
        registry.Register("dawLowPass", new FunctionSignature("dawLowPass", [StringType.Instance, graph, graph]),
            args => Wrap(AudioGraphDefinition.LowPass(args[0].As<string>(), args[1].As<AudioGraphDefinition>(), args[2].As<AudioGraphDefinition>())));
        registry.Register("dawAdsr", new FunctionSignature("dawAdsr", [StringType.Instance, graph,
            MillisecondType.Instance, MillisecondType.Instance, DoubleType.Instance, MillisecondType.Instance]), args =>
            Wrap(args[1].As<AudioGraphDefinition>().Then(args[0].As<string>(), "flow.adsr", new Dictionary<string, double>
            {
                ["attackMs"] = args[2].As<double>(), ["decayMs"] = args[3].As<double>(),
                ["sustain"] = args[4].As<double>(), ["releaseMs"] = args[5].As<double>()
            })));
        registry.Register("dawDevice", new FunctionSignature("dawDevice",
            [StringType.Instance, StringType.Instance, IntType.Instance, graph, new DictType(StringType.Instance, DoubleType.Instance), BoolType.Instance]), args =>
                Wrap(args[3].As<AudioGraphDefinition>().Then(args[0].As<string>(), args[1].As<string>(),
                    args[4].As<DictData>().Entries.ToDictionary(p => p.Key.As<string>(), p => p.Value.As<double>()),
                    args[2].As<int>(), args[5].As<bool>())));
        registry.Register("dawMix", new FunctionSignature("dawMix", [StringType.Instance, graph, graph]),
            args => Wrap(AudioGraphDefinition.Mix(args[0].As<string>(), args[1].As<AudioGraphDefinition>(), args[2].As<AudioGraphDefinition>())));
        registry.Register("dawSum", new FunctionSignature("dawSum", [StringType.Instance, new ArrayType(graph)]),
            args => Wrap(AudioGraphDefinition.Sum(args[0].As<string>(), args[1].As<List<Value>>().Select(v => v.As<AudioGraphDefinition>()))));
        foreach (var (name, device, parameter) in new[] { ("dawGain", "flow.gain", "gain"), ("dawPan", "flow.pan", "pan"), ("dawDrive", "flow.drive", "drive") })
        {
            registry.Register(name, new FunctionSignature(name, [StringType.Instance, graph, DoubleType.Instance]), args =>
                Wrap(args[1].As<AudioGraphDefinition>().Then(args[0].As<string>(), device,
                    new Dictionary<string, double> { [parameter] = args[2].As<double>() })));
        }
    }
    private static Value Process(AudioGraphDefinition definition, AudioBuffer[] inputs, GraphAutomationLane[]? automation = null)
    {
        if (inputs.Length is < 1 or > 64) throw new ArgumentException("Provide 1–64 stereo input buses");
        int frames = inputs[0].Frames;
        int sampleRate = inputs[0].SampleRate;
        if (inputs.Any(b => b.Channels != 2 || b.Frames != frames || b.SampleRate != sampleRate))
            throw new ArgumentException("Graph buses must be stereo with matching frame count and sample rate");
        var graph = new PreparedAudioGraph(definition, sampleRate, 256, automation: automation);
        int totalFrames = checked((int)(frames + graph.TailFrames));
        if ((long)totalFrames * 2 * sizeof(float) > 256 * 1024 * 1024)
            throw new ArgumentException("Graph output exceeds the 256 MiB buffer budget; use streaming host rendering");
        if (graph.InputBusCount != inputs.Length) throw new ArgumentException("Input count does not match graph buses");
        var output = new AudioBuffer(totalFrames, 2, sampleRate);
        var block = new float[inputs.Length * 256 * 2];
        for (int offset = 0; offset < totalFrames; offset += 256)
        {
            SessionServices.Current?.ThrowIfCancelled();
            int count = Math.Min(256, totalFrames - offset);
            for (int bus = 0; bus < inputs.Length; bus++)
            {
                var input = block.AsSpan(bus * count * 2, count * 2); input.Clear();
                int available = Math.Max(0, Math.Min(count, frames - offset));
                if (available > 0) inputs[bus].Data.AsSpan(offset * 2, available * 2).CopyTo(input);
            }
            graph.Process(block.AsSpan(0, inputs.Length * count * 2), output.Data.AsSpan(offset * 2, count * 2));
        }
        return MusicValue.Buffer(output);
    }

    private static Value Wrap(AudioGraphDefinition graph) => new(graph, AudioGraphType.Instance);
}
