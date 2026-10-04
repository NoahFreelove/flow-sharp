using System.Globalization;
using System.Text;
using System.Text.Json;
using Flow.Music.Model;
using Flow.Studio.Model;
using Flow.Audio.Graph;
namespace FlowLang.Hosting;

/// <summary>Executable restoration export: resource definitions use versioned project
/// snapshot data; graphs, curves and clips use explicit public construction calls. Saved code is data,
/// never executed. This is not a source-code pretty-printer or portable asset bundle.</summary>
public static class FlowProjectExporter
{
    public static string Export(ProjectSnapshot project)
    {
        var resources = new ProjectSnapshot(new(project.Arrangement.Id, project.Arrangement.Tempo, project.Arrangement.Meter),
            project.Context, project.Sources.Values, new ProjectRouting([], null), project.Assets, renderSettings: project.RenderSettings);
        // Keep stable graph output/binding slots in the resource seed, then reconstruct
        // their actual processors explicitly below. Placeholders are never published.
        var graphs = project.Sources.Values.SelectMany(s => s.Result.GraphLayers.Select(l => (Source: s.Descriptor.SourceId, Layer: l))).ToArray();
        foreach (var item in graphs) resources = ProjectGraphConstruction.Replace(resources, item.Source, item.Layer.Id, AudioGraphDefinition.Input("exportPlaceholder"));
        var instruments = project.Sources.Values.SelectMany(s => s.Result.InstrumentLayers.Select(l => (Source: s.Descriptor.SourceId, Layer: l))).ToArray();
        foreach (var item in instruments.Where(i => i.Layer.Instrument.GraphSamples is null)) resources = ProjectGraphConstruction.ReplaceInstrument(resources, item.Source, item.Layer.Id,
            new Flow.Audio.SineVoiceSettings(Sample: item.Layer.Instrument.Sample));
        var sequences = project.Sources.Values.SelectMany(s => s.Result.ScoreLayers.SelectMany(l =>
            l.Composition.Placements.Select(p => p.Section).Distinct<SectionSnapshot>(ReferenceEqualityComparer.Instance).SelectMany(section =>
                section.Sequences.Where(sequence => sequence.Notes.Count > 0).Select(sequence =>
                    (Source: s.Descriptor.SourceId, Layer: l.Id, Section: section.Id, Sequence: sequence))))).ToArray();
        foreach (var item in sequences) resources = ProjectNoteConstruction.Replace(resources, item.Source, item.Layer, item.Section, item.Sequence.Id, []);
        var text = new StringBuilder("use \"@flowDaw\"\n");
        text.AppendLine($"DawProject project0 = (dawProjectSnapshot {Quote(ProjectJson.Serialize(resources))})");
        string current = "project0";
        for (int sequenceIndex = 0; sequenceIndex < sequences.Length; sequenceIndex++)
        {
            var item = sequences[sequenceIndex];
            for (int noteIndex = 0; noteIndex < item.Sequence.Notes.Count; noteIndex++)
                text.AppendLine($"DawNote sequence{sequenceIndex}Note{noteIndex} = {NoteCode(item.Sequence.Notes[noteIndex])}");
            string notes = "(list " + string.Join(' ', Enumerable.Range(0, item.Sequence.Notes.Count).Select(index => $"sequence{sequenceIndex}Note{index}")) + ")";
            string next = "projectScoreState" + sequenceIndex;
            text.AppendLine($"DawProject {next} = (dawProjectNotes {current} \"{item.Source}\" {Quote(item.Layer)} \"{item.Section}\" \"{item.Sequence.Id}\" {notes})"); current = next;
        }
        for (int graphIndex = 0; graphIndex < graphs.Length; graphIndex++)
        {
            var item = graphs[graphIndex];
            var lines = FlowGraphExporter.Export(item.Layer.Graph, "projectGraph" + graphIndex + "Node").TrimEnd().Split('\n');
            foreach (var line in lines.Skip(1).SkipLast(1)) text.AppendLine(line.TrimEnd('\r'));
            string next = "projectGraphState" + graphIndex;
            text.AppendLine($"DawProject {next} = (dawProjectGraph {current} \"{item.Source}\" {Quote(item.Layer.Id)} {lines[^1].TrimEnd('\r')})");
            current = next;
        }
        for (int index = 0; index < instruments.Length; index++)
        {
            var item = instruments[index]; var settings = item.Layer.Instrument;
            string definition = settings.Sample is null ? $"(dawSine {settings.VoiceLimit} {Number(settings.AttackMilliseconds)}ms {Number(settings.ReleaseMilliseconds)}ms)" :
                $"(dawSampler (dawProjectSample project0 \"{item.Source}\" {Quote(item.Layer.Id)}) {Number(settings.RootFrequencyHz)} {settings.VoiceLimit} {Number(settings.AttackMilliseconds)}ms {Number(settings.ReleaseMilliseconds)}ms)";
            if (settings.VoiceGraph is { } voiceGraph)
            {
                var lines = FlowGraphExporter.Export(voiceGraph, "projectVoice" + index + "Node").TrimEnd().Split('\n');
                foreach (var line in lines.Skip(1).SkipLast(1)) text.AppendLine(line.TrimEnd('\r'));
                definition = $"(dawGraphInstrument {lines[^1].TrimEnd('\r')} {settings.VoiceLimit})";
                if (settings.GraphSamples is { } samples)
                {
                    string values = string.Join(' ', samples.Assets.Keys.Order().Select(slot => $"{slot} (dawProjectGraphSample project0 \"{item.Source}\" {Quote(item.Layer.Id)} {slot})"));
                    text.AppendLine($"Dict<Int, DawAudio> instrument{index}Samples = (dict {values})");
                    definition = $"(dawSampleInstrument {lines[^1].TrimEnd('\r')} {settings.VoiceLimit} instrument{index}Samples)";
                }
            }
            string next = "projectInstrumentState" + index;
            text.AppendLine($"DawProject {next} = (dawProjectInstrument {current} \"{item.Source}\" {Quote(item.Layer.Id)} {definition})"); current = next;
        }
        for (int index = 0; index < project.Routing.Tracks.Count; index++)
        {
            var track = project.Routing.Tracks[index];
            string definition = $"(dawTrack \"{track.Id}\" {Quote(track.Name)} {Quote(track.InstrumentBinding?.ToString() ?? "")} {track.InputBus!.Value} {track.Muted.ToString().ToLowerInvariant()} {track.Solo.ToString().ToLowerInvariant()})";
            if (track.ColorRgb is int color) definition = $"(dawTrackColor {definition} {color})";
            text.AppendLine($"DawTrack projectTrack{index} = {definition}");
        }
        string tracks = project.Routing.Tracks.Count == 0 ? "" : "(list " + string.Join(' ', Enumerable.Range(0, project.Routing.Tracks.Count).Select(index => "projectTrack" + index)) + ") ";
        text.AppendLine($"DawProject projectRouting = (dawRouting {current} {tracks}{Quote(project.Routing.GraphBinding?.ToString() ?? "")})"); current = "projectRouting";
        for (int effectIndex = 0; effectIndex < project.Routing.Effects.Count; effectIndex++)
        {
            var effect = project.Routing.Effects[effectIndex]; string next = "projectEffects" + effectIndex;
            text.AppendLine($"DawProject {next} = (dawInsertEffect {current} {Quote(effect.TrackId?.ToString() ?? "")} {Quote(effect.Binding.ToString())} {effect.Bypassed.ToString().ToLowerInvariant()})");
            current = next;
        }
        if (project.Automation.Count > 0)
        {
            var lines = FlowAutomationExporter.Export(project.Automation).TrimEnd().Split('\n');
            foreach (var line in lines.Skip(1).SkipLast(1)) text.AppendLine(line.TrimEnd('\r'));
            text.AppendLine($"DawProject projectCurves = (dawProjectCurves {current} {lines[^1].TrimEnd('\r')})");
            current = "projectCurves";
        }
        int i = 0;
        foreach (var clip in project.Arrangement.ScoreClips)
        {
            text.AppendLine($"DawClip clip{i} = (dawScoreClip \"{clip.Id}\" \"{clip.TrackId}\" \"{clip.SourceId}\" {Quote(clip.LayerId)} {Number(clip.AnchorQuarters)} {Number(clip.SourceOffsetQuarters)} {Number(clip.LengthQuarters)} {Number(clip.Nudge.Milliseconds)})"); i++;
        }
        foreach (var clip in project.Arrangement.AudioClips)
        {
            string expression = $"(dawAudioClip \"{clip.Id}\" \"{clip.TrackId}\" \"{clip.SourceId}\" {Number(clip.AnchorQuarters)} \"{clip.SourceOffsetFrames.ToString(CultureInfo.InvariantCulture)}\" \"{clip.LengthFrames.ToString(CultureInfo.InvariantCulture)}\" {clip.SampleRate} {Number(clip.Nudge.Milliseconds)})";
            if (clip.Envelope is { } envelope)
                expression = $"(dawClipEnvelope {expression} {Number(envelope.Gain)} {Quote(envelope.StartFrame.ToString(CultureInfo.InvariantCulture))} {Quote(envelope.LengthFrames.ToString(CultureInfo.InvariantCulture))} {Quote(envelope.FadeInFrames.ToString(CultureInfo.InvariantCulture))} {Quote(envelope.FadeOutFrames.ToString(CultureInfo.InvariantCulture))})";
            text.AppendLine($"DawClip clip{i} = {expression}"); i++;
        }
        if (i == 0) text.AppendLine(current);
        else text.AppendLine("(dawArrange " + current + " (list " + string.Join(' ', Enumerable.Range(0, i).Select(index => "clip" + index)) + "))");
        if (text.Length > 64 * 1024 * 1024) throw new InvalidOperationException("Project Flow export exceeds source budget");
        return text.ToString();
    }
    private static string NoteCode(NoteEvent note)
    {
        var pitch = note.Pitch;
        string cents = pitch?.CentOffset?.ToString("R", CultureInfo.InvariantCulture) ?? "";
        string rational = note.ExactDuration is { } exact ? $"{exact.Numerator}/{exact.Denominator}" : "";
        string origin = note.Origin is null ? "" : JsonSerializer.Serialize(note.Origin);
        return $"(dawNote \"{note.Id}\" {Quote(note.VoiceId)} {Number(note.OffsetQuarters)} {Number(note.DurationQuarters)} {Quote(pitch?.Letter.ToString() ?? "")} {Integer(pitch?.Octave ?? 0)} {Integer(pitch?.Alteration ?? 0)} {Quote(cents)} {Integer(pitch?.MidiKey ?? 0)} {Number(pitch?.FrequencyHz ?? 0)} {Number(note.Velocity)} {(int)note.Articulation} {note.IsTied.ToString().ToLowerInvariant()} {Number(note.DurationOverlap)} {Number(note.PortamentoMs)} {Quote(rational)} {Quote(origin)})";
    }
    private static string Integer(int value) => value < 0 ? "(neg " + (-(long)value).ToString(CultureInfo.InvariantCulture) + ")" : value.ToString(CultureInfo.InvariantCulture);
    private static string Number(double value) => FlowGraphExporter.Number(value);
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
}
