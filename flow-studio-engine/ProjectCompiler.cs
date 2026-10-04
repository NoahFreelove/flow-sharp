using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Music.Model;
using Flow.Studio.Model;

namespace Flow.Studio.Engine;

/// <summary>Resolves accepted project outputs on the preparation worker. Score clips
/// reference source/layer; generated audio clips reference the stable output binding ID.
/// Missing audio/score outputs remain silent through ArrangementCompiler diagnostics.
/// Missing selected devices fail preparation so the host can retain last-good playback.</summary>
public static class ProjectCompiler
{
    public static PreparedArrangement Prepare(ProjectSnapshot project, int sampleRate = 48000,
        int blockFrames = 256, CancellationToken cancellation = default, IReadOnlyDictionary<Guid, PcmAsset>? externalAssets = null, Guid? monitoredTrack = null,
        Guid? soloTrack = null, long minimumFrames = 0, MidiPitchMap? midiPitchMap = null) =>
        Prepare(project, project.Routing.Tracks.Select(t => t.Id).ToArray(),
            project.Routing.GraphBinding ?? throw new InvalidOperationException("Project has no output graph"),
            project.Routing.Tracks.Where(t => t.InstrumentBinding.HasValue).ToDictionary(t => t.Id, t => t.InstrumentBinding!.Value),
            sampleRate, blockFrames, cancellation, externalAssets,
            project.Routing.Tracks.ToDictionary(t => t.Id, t => t.InputBus!.Value), monitoredTrack, soloTrack, minimumFrames, midiPitchMap);

    public static PreparedArrangement Prepare(ProjectSnapshot project, IReadOnlyList<Guid> tracks,
        Guid graphBinding, IReadOnlyDictionary<Guid, Guid>? trackInstruments = null,
        int sampleRate = 48000, int blockFrames = 256, CancellationToken cancellation = default,
        IReadOnlyDictionary<Guid, PcmAsset>? externalAssets = null, IReadOnlyDictionary<Guid, int>? trackInputBuses = null, Guid? monitoredTrack = null,
        Guid? soloTrack = null, long minimumFrames = 0, MidiPitchMap? midiPitchMap = null)
    {
        if (monitoredTrack.HasValue && midiPitchMap is null &&
            (project.Context.Tuning.Scala is not null || project.Context.Tuning.System != "EqualTemperament"))
            throw new InvalidOperationException("Project monitoring requires a resolved MIDI pitch map for this tuning");
        var scores = new Dictionary<(Guid, string), CompositionSnapshot>();
        var audio = new Dictionary<Guid, PcmAsset>();
        foreach (var reference in project.Assets)
            if (externalAssets is not null && externalAssets.TryGetValue(reference.Id, out var asset))
            {
                if (asset.SampleRate != reference.SampleRate || asset.Frames != reference.Frames)
                    throw new ArgumentException("Resolved asset format differs from project metadata");
                audio.Add(reference.Id, asset);
            }
        var graphs = new Dictionary<Guid, AudioGraphDefinition>();
        var instruments = new Dictionary<Guid, SineVoiceSettings>();
        foreach (var source in project.Sources.Values)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var layer in source.Result.ScoreLayers) scores.Add((source.Descriptor.SourceId, layer.Id), layer.Composition);
            foreach (var binding in source.Bindings.Where(b => b.Available))
            {
                switch (binding.Output.Role)
                {
                    case GeneratedRole.Audio:
                        audio.Add(binding.Id, source.Result.AudioLayers.Single(l => l.Id == binding.Output.LayerId).Asset); break;
                    case GeneratedRole.Graph:
                        var definition = source.Result.GraphLayers.Single(l => l.Id == binding.Output.LayerId).Graph;
                        if (binding.Id == graphBinding && source.Plugin is { } plugin)
                            definition = plugin.ValidateEffect(source.Result, sampleRate, blockFrames).ApplyValues(source.PluginValues);
                        graphs.Add(binding.Id, definition); break;
                    case GeneratedRole.Instrument:
                        var instrument = source.Result.InstrumentLayers.Single(l => l.Id == binding.Output.LayerId).Instrument;
                        if (source.Plugin is { } instrumentPlugin && trackInstruments is not null && trackInstruments.Values.Contains(binding.Id))
                            instrument = instrumentPlugin.ValidateInstrument(source.Result, sampleRate, blockFrames).ApplyValues(source.PluginValues);
                        instruments.Add(binding.Id, instrument); break;
                }
            }
        }
        if (!graphs.TryGetValue(graphBinding, out var graph)) throw new InvalidOperationException("Selected graph output is unavailable");
        var voices = new Dictionary<Guid, SineVoiceSettings>();
        foreach (var pair in trackInstruments ?? new Dictionary<Guid, Guid>())
        {
            if (!tracks.Contains(pair.Key)) throw new ArgumentException("Instrument binding refers to an unknown track");
            if (!instruments.TryGetValue(pair.Value, out var instrument)) throw new InvalidOperationException("Selected instrument output is unavailable");
            voices.Add(pair.Key, instrument);
        }
        var resolved = ResolveAutomation(project).ToArray();
        var effects = project.Routing.Effects.Where(e => !soloTrack.HasValue || !e.TrackId.HasValue || e.TrackId == soloTrack).Select(e =>
        {
            var source = project.Sources.Values.SingleOrDefault(s => s.Bindings.Any(b => b.Id == e.Binding && b.Available))
                ?? throw new InvalidOperationException("Selected effect output is unavailable");
            var bound = BoundEffect.FromSource(source, sampleRate, blockFrames, e.Bypassed);
            if (bound.Binding != e.Binding) throw new ArgumentException("Selected effect does not match the packaged output");
            return (Insert: e, Effect: bound);
        }).ToArray();
        var buses = trackInputBuses ?? tracks.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        var trackEffects = effects.Where(e => e.Insert.TrackId.HasValue).GroupBy(e => e.Insert.TrackId!.Value)
            .ToDictionary(g => buses.TryGetValue(g.Key, out int bus) ? bus : throw new ArgumentException("Effect track is unavailable"),
                g => (IReadOnlyList<BoundEffect>)g.Select(e => e.Effect).ToArray());
        bool anySolo = project.Routing.Tracks.Any(t => t.Solo);
        // An explicit stem selection replaces saved solo selection, while saved mute wins.
        // Gates are ordinary Flow gain nodes after track inserts, so autonomous inserts
        // and live monitoring cannot bypass mute. Shared/master graph behavior is retained.
        var silentBuses = project.Routing.Tracks.Where(t => t.Muted ||
            (soloTrack.HasValue ? t.Id != soloTrack.Value : anySolo && !t.Solo))
            .Where(t => buses.ContainsKey(t.Id)).Select(t => buses[t.Id]).ToHashSet();
        var composition = EffectChainCompiler.Compose(graph, trackEffects,
            effects.Where(e => !e.Insert.TrackId.HasValue).Select(e => e.Effect).ToArray(), cancellation, silentBuses);
        var effectBindings = effects.Where(e => !e.Insert.Bypassed).Select(e => e.Insert.Binding).ToHashSet();
        var graphAutomation = resolved.Where(l => l.GraphBinding == graphBinding)
            .Select(l => AutomationCompiler.Lower(l, project.Arrangement.Tempo, sampleRate))
            .Concat(resolved.Where(l => effectBindings.Contains(l.GraphBinding)).Select(l => composition.MapAutomation(l.GraphBinding,
                AutomationCompiler.Lower(l, project.Arrangement.Tempo, sampleRate)))).ToArray();
        var voiceAutomation = (trackInstruments ?? new Dictionary<Guid, Guid>()).ToDictionary(p => p.Key,
            p => resolved.Where(l => l.GraphBinding == p.Value).Select(l => AutomationCompiler.Lower(l, project.Arrangement.Tempo, sampleRate)).ToArray());
        var prepared = ArrangementCompiler.Prepare(project.Arrangement, scores, tracks, composition.Graph, sampleRate, blockFrames,
            voices, cancellation: cancellation, audioAssets: audio, automation:
                graphAutomation,
            trackInputBuses: trackInputBuses, trackInstrumentBindings: trackInstruments, instrumentAutomation: voiceAutomation, monitoredTrack: monitoredTrack,
            soloTrack: soloTrack, minimumFrames: minimumFrames, midiPitchMap: midiPitchMap);
        return prepared with { EffectNodes = composition.Nodes };
    }
    private static IEnumerable<ProjectAutomationLane> ResolveAutomation(ProjectSnapshot project)
    {
        int count = 0; long points = 0;
        foreach (var lane in project.Automation)
        {
            if (lane.TargetKind == AutomationTargetKind.GraphNode) { yield return lane; continue; }
            var source = project.Sources.Values.SingleOrDefault(s => s.Bindings.Any(b => b.Available && b.Id == lane.GraphBinding))
                ?? throw new ArgumentException("Public automation binding is unavailable");
            var plugin = source.Plugin ?? throw new ArgumentException("Public automation requires a declared plugin");
            var parameter = plugin.Manifest.Parameters.SingleOrDefault(p => p.Id == lane.ParameterId)
                ?? throw new ArgumentException("Unknown public automation parameter");
            if (parameter.RequiresRebuild) throw new ArgumentException("Structural plugin parameters cannot be automated");
            if (parameter.Scale == PluginParameterScale.Enumeration && lane.Shape != AutomationShape.Step)
                throw new ArgumentException("Discrete plugin parameters require step automation");
            foreach (var point in lane.Points) _ = parameter.ToNormalized(point.Value);
            foreach (var target in plugin.Targets.Where(t => t.ParameterId == parameter.Id))
            {
                if (++count > 256 || (points += lane.Points.Count) > 100000) throw new ArgumentException("Expanded public automation exceeds preparation budget");
                yield return new(lane.Id, lane.GraphBinding, target.NodeId, target.DeviceParameterId, lane.Points, lane.Shape);
            }
        }
    }
}
