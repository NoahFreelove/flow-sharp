using System.Globalization;
using System.Text.Json;
using Flow.Music.Model;
using Flow.Studio.Model;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
namespace FlowLang.StandardLibrary.Daw;

public static class DawProjectFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        var project = DawProjectType.Instance; var str = StringType.Instance; var number = DoubleType.Instance;
        registry.Register("dawProjectTiming", new FunctionSignature("dawProjectTiming", [project, new DictType(number, number),
            new DictType(IntType.Instance, IntType.Instance), new DictType(IntType.Instance, IntType.Instance)]), args =>
        {
            var tempo = new ProjectTempoMap(args[1].As<DictData>().Entries.Select(p => new TempoChange(p.Key.As<double>(), p.Value.As<double>())).OrderBy(p => p.Quarter));
            var nums = args[2].As<DictData>().Entries.ToDictionary(p => p.Key.As<int>(), p => p.Value.As<int>());
            var dens = args[3].As<DictData>().Entries.ToDictionary(p => p.Key.As<int>(), p => p.Value.As<int>());
            if (!nums.Keys.ToHashSet().SetEquals(dens.Keys)) throw new ArgumentException("Meter maps must have matching bar keys");
            var meter = new ProjectMeterMap(nums.OrderBy(p => p.Key).Select(p => new MeterChange(p.Key, p.Value, dens[p.Key])));
            return Wrap(ProjectTimingCommands.WithTiming(args[0].As<ProjectSnapshot>(), tempo, meter));
        });
        foreach (var unit in new FlowType[] { MillisecondType.Instance, SecondType.Instance })
            foreach (bool relative in new[] { false, true })
            {
                string name = relative ? "dawRelativeOffsetMs" : "dawSetOffsetMs";
                double scale = unit == SecondType.Instance ? 1000 : 1;
                registry.Register(name, new FunctionSignature(name, [DawClipType.Instance, unit]), args =>
                {
                    var offset = new TimeOffset(args[1].As<double>() * scale);
                    object clip = args[0].Data switch
                    {
                        ScoreClip score => relative ? ClipOperations.RelativeOffsetMs(score, offset) : ClipOperations.SetOffsetMs(score, offset),
                        AudioClip audio => relative ? ClipOperations.RelativeOffsetMs(audio, offset) : ClipOperations.SetOffsetMs(audio, offset),
                        _ => throw new ArgumentException("Unknown clip value")
                    };
                    return new Value(clip, DawClipType.Instance);
                });
            }
        registry.Register("dawProjectTuning", new FunctionSignature("dawProjectTuning", [project, str, str, str, str]), args =>
        {
            string scale = args[3].As<string>(), map = args[4].As<string>();
            return Wrap(FlowLang.Hosting.GeneratorTuning.WithTuning(args[0].As<ProjectSnapshot>(),
                new(args[1].As<string>(), args[2].As<string>(), scale.Length == 0 ? null : scale, map.Length == 0 ? null : map)));
        });
        registry.Register("dawAssetGrants", new FunctionSignature("dawAssetGrants", [project, str, new ArrayType(str)]), args =>
            Wrap(ProjectAssetGrantCommands.WithGrants(args[0].As<ProjectSnapshot>(), Guid.Parse(args[1].As<string>()),
                args[2].As<List<Value>>().Select(v => Guid.Parse(v.As<string>())))));
        registry.Register("dawRenderSettings", new FunctionSignature("dawRenderSettings", [project, IntType.Instance, IntType.Instance]), args =>
        {
            var p = args[0].As<ProjectSnapshot>();
            return Wrap(new(p.Arrangement, p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation,
                new ProjectRenderSettings(args[1].As<int>(), args[2].As<int>())));
        });
        registry.Register("dawScoreClip", new FunctionSignature("dawScoreClip", [str, str, str, str, number, number, number, number]), args =>
            new Value(new ScoreClip(Guid.Parse(args[0].As<string>()), Guid.Parse(args[1].As<string>()), Guid.Parse(args[2].As<string>()),
                args[3].As<string>(), args[4].As<double>(), args[5].As<double>(), args[6].As<double>(), new(args[7].As<double>())), DawClipType.Instance));
        registry.Register("dawAudioClip", new FunctionSignature("dawAudioClip", [str, str, str, number, str, str, IntType.Instance, number]), args =>
            new Value(new AudioClip(Guid.Parse(args[0].As<string>()), Guid.Parse(args[1].As<string>()), Guid.Parse(args[2].As<string>()),
                args[3].As<double>(), long.Parse(args[4].As<string>(), CultureInfo.InvariantCulture), long.Parse(args[5].As<string>(), CultureInfo.InvariantCulture),
                args[6].As<int>(), new(args[7].As<double>())), DawClipType.Instance));
        registry.Register("dawClipEnvelope", new FunctionSignature("dawClipEnvelope", [DawClipType.Instance, number, str, str, str, str]), args =>
        {
            var clip = args[0].As<AudioClip>();
            var envelope = new AudioClipEnvelope(long.Parse(args[2].As<string>(), CultureInfo.InvariantCulture),
                long.Parse(args[3].As<string>(), CultureInfo.InvariantCulture), args[1].As<double>(),
                long.Parse(args[4].As<string>(), CultureInfo.InvariantCulture), long.Parse(args[5].As<string>(), CultureInfo.InvariantCulture));
            return new Value(ProjectClipEnvelopeCommands.WithEnvelope(clip, envelope), DawClipType.Instance);
        });
        registry.Register("dawArrange", new FunctionSignature("dawArrange", [project, new ArrayType(DawClipType.Instance)]), args =>
        {
            var p = args[0].As<ProjectSnapshot>(); var clips = args[1].As<List<Value>>();
            if (clips.Count > 100000) throw new ArgumentException("Arrangement clip budget exceeded");
            var scores = new List<ScoreClip>(); var audio = new List<AudioClip>();
            foreach (var clip in clips)
            {
                if (clip.Data is ScoreClip score) scores.Add(score);
                else if (clip.Data is AudioClip sample) audio.Add(sample);
                else throw new ArgumentException("Unknown clip value");
            }
            return Wrap(new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter, scores, audio),
                p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation, p.RenderSettings));
        });
        registry.Register("dawRelativeOffsetMs", new FunctionSignature("dawRelativeOffsetMs", [DawClipType.Instance, number]), args =>
        {
            var offset = new TimeOffset(args[1].As<double>());
            object moved = args[0].Data switch
            {
                ScoreClip score => ClipOperations.RelativeOffsetMs(score, offset),
                AudioClip audio => ClipOperations.RelativeOffsetMs(audio, offset),
                _ => throw new ArgumentException("Unknown clip value")
            };
            return new Value(moved, DawClipType.Instance);
        });
        registry.Register("dawAlignToBar", new FunctionSignature("dawAlignToBar", [project, DawClipType.Instance, IntType.Instance]), args =>
        {
            var meter = args[0].As<ProjectSnapshot>().Arrangement.Meter; int bar = args[2].As<int>();
            object aligned = args[1].Data switch
            {
                ScoreClip score => ClipOperations.AlignToBar(score, bar, meter),
                AudioClip audio => ClipOperations.AlignToBar(audio, bar, meter),
                _ => throw new ArgumentException("Unknown clip value")
            };
            return new Value(aligned, DawClipType.Instance);
        });
        registry.Register("dawTrimScore", new FunctionSignature("dawTrimScore", [DawClipType.Instance, number, number]), args =>
            new Value(ClipOperations.Trim(args[0].As<ScoreClip>(), args[1].As<double>(), args[2].As<double>()), DawClipType.Instance));
        registry.Register("dawTrimAudio", new FunctionSignature("dawTrimAudio", [project, DawClipType.Instance, str, str]), args =>
            new Value(ClipOperations.TrimFrames(args[1].As<AudioClip>(), long.Parse(args[2].As<string>(), CultureInfo.InvariantCulture),
                long.Parse(args[3].As<string>(), CultureInfo.InvariantCulture), args[0].As<ProjectSnapshot>().Arrangement.Tempo), DawClipType.Instance));
        registry.Register("dawRepeatScore", new FunctionSignature("dawRepeatScore", [DawClipType.Instance, new ArrayType(str)]), args =>
            Clips(ClipOperations.Repeat(args[0].As<ScoreClip>(), args[1].As<List<Value>>().Select(v => Guid.Parse(v.As<string>())))));
        registry.Register("dawRepeatAudio", new FunctionSignature("dawRepeatAudio", [project, DawClipType.Instance, new ArrayType(str)]), args =>
            Clips(ClipOperations.Repeat(args[1].As<AudioClip>(), args[2].As<List<Value>>().Select(v => Guid.Parse(v.As<string>())),
                args[0].As<ProjectSnapshot>().Arrangement.Tempo)));
        registry.Register("dawSplitScore", new FunctionSignature("dawSplitScore", [DawClipType.Instance, number, str]), args =>
        {
            var pair = ClipOperations.Split(args[0].As<ScoreClip>(), new(args[1].As<double>()), Guid.Parse(args[2].As<string>()));
            return Pair(pair.Left, pair.Right);
        });
        registry.Register("dawSplitAudio", new FunctionSignature("dawSplitAudio", [project, DawClipType.Instance, str, str]), args =>
        {
            var pair = ClipOperations.SplitFrames(args[1].As<AudioClip>(), long.Parse(args[2].As<string>(), CultureInfo.InvariantCulture),
                args[0].As<ProjectSnapshot>().Arrangement.Tempo, Guid.Parse(args[3].As<string>()));
            return Pair(pair.Left, pair.Right);
        });
        registry.Register("dawProjectCurves", new FunctionSignature("dawProjectCurves", [project, new ArrayType(DawAutomationType.Instance)]), args =>
        {
            var p = args[0].As<ProjectSnapshot>();
            return Wrap(new(p.Arrangement, p.Context, p.Sources.Values, p.Routing, p.Assets,
                args[1].As<List<Value>>().Select(v => v.As<ProjectAutomationLane>()), p.RenderSettings));
        });
        registry.Register("dawProjectGraph", new FunctionSignature("dawProjectGraph", [project, str, str, AudioGraphType.Instance]), args =>
            Wrap(ProjectGraphConstruction.Replace(args[0].As<ProjectSnapshot>(), Guid.Parse(args[1].As<string>()), args[2].As<string>(),
                args[3].As<Flow.Audio.Graph.AudioGraphDefinition>())));
        registry.Register("dawTrack", new FunctionSignature("dawTrack", [str, str, str]), args =>
        {
            string binding = args[2].As<string>();
            return new Value(new ProjectTrack(Guid.Parse(args[0].As<string>()), args[1].As<string>(), binding.Length == 0 ? null : Guid.Parse(binding)), DawTrackType.Instance);
        });
        registry.Register("dawRouting", new FunctionSignature("dawRouting", [project, new ArrayType(DawTrackType.Instance), str]), args =>
            Route(args[0].As<ProjectSnapshot>(), args[1].As<List<Value>>().Select(v => v.As<ProjectTrack>()), args[2].As<string>()));
        registry.Register("dawTrack", new FunctionSignature("dawTrack", [str, str, str, IntType.Instance]), args =>
        {
            string binding = args[2].As<string>();
            return new Value(new ProjectTrack(Guid.Parse(args[0].As<string>()), args[1].As<string>(),
                binding.Length == 0 ? null : Guid.Parse(binding), args[3].As<int>()), DawTrackType.Instance);
        });
        registry.Register("dawTrack", new FunctionSignature("dawTrack", [str, str, str, IntType.Instance, BoolType.Instance, BoolType.Instance]), args =>
        {
            string binding = args[2].As<string>();
            return new Value(new ProjectTrack(Guid.Parse(args[0].As<string>()), args[1].As<string>(),
                binding.Length == 0 ? null : Guid.Parse(binding), args[3].As<int>(), args[4].As<bool>(), args[5].As<bool>()), DawTrackType.Instance);
        });
        registry.Register("dawTrackColor", new FunctionSignature("dawTrackColor", [DawTrackType.Instance, IntType.Instance]), args =>
        {
            int color = args[1].As<int>();
            if (color is < -1 or > 0xFFFFFF) throw new ArgumentOutOfRangeException(nameof(color));
            return new Value(args[0].As<ProjectTrack>() with { ColorRgb = color == -1 ? null : color }, DawTrackType.Instance);
        });
        registry.Register("dawRouting", new FunctionSignature("dawRouting", [project, str]), args =>
            Route(args[0].As<ProjectSnapshot>(), [], args[1].As<string>()));
        registry.Register("dawInsertEffect", new FunctionSignature("dawInsertEffect", [project, str, str, BoolType.Instance]), args =>
        {
            var p = args[0].As<ProjectSnapshot>(); string track = args[1].As<string>();
            // Like dawRouting, construction preserves repairable missing bindings.
            // Interactive insertion validates availability; preparation validates sound.
            return Wrap(new(p.Arrangement, p.Context, p.Sources.Values,
                new(p.Routing.Tracks, p.Routing.GraphBinding, p.Routing.Effects.Append(new(Guid.Parse(args[2].As<string>()),
                    track.Length == 0 ? null : Guid.Parse(track), args[3].As<bool>()))), p.Assets, p.Automation, p.RenderSettings));
        });
        registry.Register("dawProjectInstrument", new FunctionSignature("dawProjectInstrument", [project, str, str, DawInstrumentType.Instance]), args =>
            Wrap(ProjectGraphConstruction.ReplaceInstrument(args[0].As<ProjectSnapshot>(), Guid.Parse(args[1].As<string>()), args[2].As<string>(),
                args[3].As<Flow.Audio.SineVoiceSettings>())));
        registry.Register("dawProjectGraphSample", new FunctionSignature("dawProjectGraphSample", [project, str, str, IntType.Instance]), args =>
        {
            var instrument = args[0].As<ProjectSnapshot>().Sources[Guid.Parse(args[1].As<string>())].Result.InstrumentLayers.Single(l => l.Id == args[2].As<string>()).Instrument;
            if (instrument.GraphSamples is null || !instrument.GraphSamples.Assets.TryGetValue(args[3].As<int>(), out var asset))
                throw new ArgumentException("Instrument sample slot is missing");
            return new Value(asset, DawAudioType.Instance);
        });
        registry.Register("dawProjectSample", new FunctionSignature("dawProjectSample", [project, str, str]), args =>
        {
            var p = args[0].As<ProjectSnapshot>();
            var sample = p.Sources[Guid.Parse(args[1].As<string>())].Result.InstrumentLayers.Single(l => l.Id == args[2].As<string>()).Instrument.Sample
                ?? throw new ArgumentException("Instrument has no sample asset");
            return new Value(sample, DawAudioType.Instance);
        });
        registry.Register("dawNote", new FunctionSignature("dawNote", [str, str, number, number, str, IntType.Instance,
            IntType.Instance, str, IntType.Instance, number, number, IntType.Instance, BoolType.Instance, number, number, str, str]), args =>
        {
            string letter = args[4].As<string>(), cents = args[7].As<string>(), rational = args[15].As<string>(), origin = args[16].As<string>();
            double frequency = args[9].As<double>();
            if (letter.Length > 1 || letter.Length == 1 && "ABCDEFG".IndexOf(letter[0]) < 0 || !double.IsFinite(frequency) || frequency < 0 || (letter.Length == 0) != (frequency == 0))
                throw new ArgumentException("Invalid note pitch/rest");
            NotePitch? pitch = letter.Length == 0 ? null : new(letter[0], args[5].As<int>(), args[6].As<int>(),
                cents.Length == 0 ? null : double.Parse(cents, CultureInfo.InvariantCulture), args[8].As<int>(), frequency);
            RationalDuration? exact = null;
            if (rational.Length > 0)
            {
                var pair = rational.Split('/'); if (pair.Length != 2) throw new ArgumentException("Invalid rational duration");
                exact = new(int.Parse(pair[0], CultureInfo.InvariantCulture), int.Parse(pair[1], CultureInfo.InvariantCulture));
            }
            var note = new NoteEvent(Guid.Parse(args[0].As<string>()), args[1].As<string>(), args[2].As<double>(), args[3].As<double>(), pitch,
                args[10].As<double>(), (NoteArticulation)args[11].As<int>(), args[12].As<bool>(), args[13].As<double>(), args[14].As<double>(),
                exact, origin.Length == 0 ? null : JsonSerializer.Deserialize<SourceOrigin>(origin));
            return new Value(note, DawNoteType.Instance);
        });
        registry.Register("dawProjectNotes", new FunctionSignature("dawProjectNotes", [project, str, str, str, str, new ArrayType(DawNoteType.Instance)]), args =>
            Wrap(ProjectNoteConstruction.Replace(args[0].As<ProjectSnapshot>(), Guid.Parse(args[1].As<string>()), args[2].As<string>(),
                Guid.Parse(args[3].As<string>()), Guid.Parse(args[4].As<string>()), args[5].As<List<Value>>().Select(v => v.As<NoteEvent>()))));
        registry.Register("dawProjectSnapshot", new FunctionSignature("dawProjectSnapshot", [str]), args =>
            Wrap(ProjectJson.Deserialize(args[0].As<string>())));
        registry.Register("dawPlaceScore", new FunctionSignature("dawPlaceScore", [project, str, str, str, str, number, number, number, number]), args =>
        {
            var p = args[0].As<ProjectSnapshot>();
            var clip = new ScoreClip(Guid.Parse(args[1].As<string>()), Guid.Parse(args[2].As<string>()), Guid.Parse(args[3].As<string>()),
                args[4].As<string>(), args[5].As<double>(), args[6].As<double>(), args[7].As<double>(), new(args[8].As<double>()));
            return Wrap(new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter, p.Arrangement.ScoreClips.Append(clip), p.Arrangement.AudioClips),
                p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation, p.RenderSettings));
        });
        // Frame counts are decimal strings to retain the full Int64 domain through Flow's Int32 type.
        registry.Register("dawPlaceAudio", new FunctionSignature("dawPlaceAudio", [project, str, str, str, number, str, str, IntType.Instance, number]), args =>
        {
            var p = args[0].As<ProjectSnapshot>();
            var clip = new AudioClip(Guid.Parse(args[1].As<string>()), Guid.Parse(args[2].As<string>()), Guid.Parse(args[3].As<string>()),
                args[4].As<double>(), long.Parse(args[5].As<string>(), CultureInfo.InvariantCulture), long.Parse(args[6].As<string>(), CultureInfo.InvariantCulture),
                args[7].As<int>(), new(args[8].As<double>()));
            return Wrap(new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter, p.Arrangement.ScoreClips, p.Arrangement.AudioClips.Append(clip)),
                p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation, p.RenderSettings));
        });
    }
    private static Value Route(ProjectSnapshot p, IEnumerable<ProjectTrack> tracks, string graph)
    {
        var copy = tracks.ToArray(); var ids = copy.Select(t => t.Id).ToHashSet();
        return Wrap(new(p.Arrangement, p.Context, p.Sources.Values, new(copy, graph.Length == 0 ? null : Guid.Parse(graph),
            p.Routing.Effects.Where(e => !e.TrackId.HasValue || ids.Contains(e.TrackId.Value))), p.Assets, p.Automation, p.RenderSettings));
    }
    private static Value Pair(object left, object right) => new(new List<Value>
        { new(left, DawClipType.Instance), new(right, DawClipType.Instance) }, new ArrayType(DawClipType.Instance));
    private static Value Clips(IEnumerable<object> clips) => new(clips.Select(c => new Value(c, DawClipType.Instance)).ToList(), new ArrayType(DawClipType.Instance));
    private static Value Wrap(ProjectSnapshot snapshot) => new(snapshot, DawProjectType.Instance);
}
