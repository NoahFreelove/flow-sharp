using System.Collections.ObjectModel;
using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Music.Model;
using Flow.Studio.Model;

namespace Flow.Studio.Engine;

public sealed record ArrangementDiagnostic(Guid ClipId, string Code, string Message);
public sealed record PreparedArrangement(PreparedGraphPlayback Playback,
    IReadOnlyDictionary<Guid, IReadOnlyList<ScheduledNote>> TrackNotes,
    IReadOnlyList<ArrangementDiagnostic> Diagnostics)
{
    public IReadOnlyDictionary<Guid, PreparedLiveInstrument> Monitors { get; init; }
        = new ReadOnlyDictionary<Guid, PreparedLiveInstrument>(new Dictionary<Guid, PreparedLiveInstrument>());
    public IReadOnlyDictionary<Guid, PreparedInstrumentControlGroup> InstrumentControls { get; init; }
        = new ReadOnlyDictionary<Guid, PreparedInstrumentControlGroup>(new Dictionary<Guid, PreparedInstrumentControlGroup>());
    public IReadOnlyDictionary<(Guid Binding, string Node), string> EffectNodes { get; init; }
        = new ReadOnlyDictionary<(Guid, string), string>(new Dictionary<(Guid, string), string>());
}

/// <summary>Worker-side lowering from linked score windows to frame events. Master
/// project tempo owns time. No interpreter, UI or hardware access. Failure publishes
/// nothing; source data is never changed. The caller publishes the completed playback.</summary>
public static class ArrangementCompiler
{
    public static PreparedArrangement Prepare(ArrangementSnapshot arrangement,
        IReadOnlyDictionary<(Guid SourceId, string LayerId), CompositionSnapshot> sources,
        IReadOnlyList<Guid> trackBuses, AudioGraphDefinition graph, int sampleRate = 48000, int blockFrames = 256,
        IReadOnlyDictionary<Guid, SineVoiceSettings>? instruments = null, int maxEvents = 100_000,
        int maxOccurrences = 100_000, CancellationToken cancellation = default,
        IReadOnlyDictionary<Guid, PcmAsset>? audioAssets = null, long maxAssetBytes = 512 * 1024 * 1024, IEnumerable<GraphAutomationLane>? automation = null,
        IReadOnlyDictionary<Guid, int>? trackInputBuses = null, IReadOnlyDictionary<Guid, Guid>? trackInstrumentBindings = null, IReadOnlyDictionary<Guid, GraphAutomationLane[]>? instrumentAutomation = null, Guid? monitoredTrack = null,
        Guid? soloTrack = null, long minimumFrames = 0, MidiPitchMap? midiPitchMap = null)
    {
        if (sampleRate is < 1 or > 384000 || maxEvents < 1 || maxOccurrences < 1 || trackBuses.Count > 64 ||
            (trackInputBuses is null && trackBuses.Count == 0) ||
            trackBuses.Any(t => t == Guid.Empty) || trackBuses.Distinct().Count() != trackBuses.Count)
            throw new ArgumentException("Invalid arrangement preparation configuration");
        if (monitoredTrack.HasValue && !trackBuses.Contains(monitoredTrack.Value)) throw new ArgumentException("Unknown monitor track");
        if (soloTrack.HasValue && (!trackBuses.Contains(soloTrack.Value) || monitoredTrack.HasValue)) throw new ArgumentException("Unknown solo track or incompatible monitoring");
        ArgumentOutOfRangeException.ThrowIfNegative(minimumFrames);
        cancellation.ThrowIfCancellationRequested();
        var processor = new PreparedAudioGraph(graph, sampleRate, blockFrames, automation: automation);
        if (trackInputBuses is null && processor.InputBusCount != trackBuses.Count)
            throw new ArgumentException("Graph buses must match the track binding list");
        var busMap = trackInputBuses ?? trackBuses.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        if (busMap.Count != trackBuses.Count || trackBuses.Any(t => !busMap.ContainsKey(t)) ||
            busMap.Values.Any(b => b < 0 || b >= processor.InputBusCount) || busMap.Values.Distinct().Count() != busMap.Count)
            throw new ArgumentException("Tracks require distinct input buses available in the selected graph");
        var events = trackBuses.ToDictionary(t => t, _ => new List<ScheduledNote>());
        var diagnostics = new List<ArrangementDiagnostic>();
        var sections = new Dictionary<SectionSnapshot, (NoteEvent Note, double Quarters, double FixedSeconds)[]>(ReferenceEqualityComparer.Instance);
        int eventCount = 0, occurrences = 0;
        long preparedNoteCount = 0, candidates = 0;
        long visibleEnd = 0;
        foreach (var clip in arrangement.ScoreClips)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!events.TryGetValue(clip.TrackId, out var notes)) throw new ArgumentException($"Clip {clip.Id} has no track bus");
            double windowEnd = clip.SourceOffsetQuarters + clip.LengthQuarters;
            double clipEnd = clip.SecondsAtSourceQuarter(windowEnd, arrangement.Tempo);
            visibleEnd = Math.Max(visibleEnd, Frame(Math.Max(0, clipEnd), sampleRate));
            if (soloTrack.HasValue && clip.TrackId != soloTrack.Value) continue;
            if (!sources.TryGetValue((clip.SourceId, clip.LayerId), out var composition))
            {
                diagnostics.Add(new(clip.Id, "missing-source-layer", "Source/layer is unavailable; clip plays silence"));
                continue;
            }
            double sourceLength = composition.Placements.Sum(p => p.Section.DurationQuarters * p.RepeatCount);
            if (!double.IsFinite(sourceLength)) throw new ArgumentException("Source length is not finite");
            if (windowEnd > sourceLength)
                diagnostics.Add(new(clip.Id, "source-window-outside", "Source is shorter than this window; missing material plays silence"));
            double offset = 0;
            foreach (var placement in composition.Placements)
            {
                cancellation.ThrowIfCancellationRequested();
                double length = placement.Section.DurationQuarters;
                double end = offset + length * placement.RepeatCount;
                if (length > 0 && end > clip.SourceOffsetQuarters && offset < windowEnd)
                {
                    int first = (int)Math.Clamp(Math.Floor((clip.SourceOffsetQuarters - offset) / length), 0, placement.RepeatCount);
                    int last = (int)Math.Clamp(Math.Ceiling((windowEnd - offset) / length), 0, placement.RepeatCount);
                    if (!sections.TryGetValue(placement.Section, out var prepared))
                    {
                        preparedNoteCount += placement.Section.Sequences.Sum(q => (long)q.Notes.Count);
                        if (preparedNoteCount > maxEvents) throw new ArgumentException("Source note preparation budget exceeded");
                        prepared = PrepareSection(placement.Section, maxEvents, cancellation);
                        sections.Add(placement.Section, prepared);
                    }
                    for (int repeat = first; repeat < last; repeat++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (++occurrences > maxOccurrences) throw new ArgumentException("Source occurrence budget exceeded");
                        double start = offset + repeat * length;
                        double sectionEnd = clip.SecondsAtSourceQuarter(start + length, arrangement.Tempo);
                        foreach (var item in prepared)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (++candidates > (long)maxEvents * 16)
                                throw new ArgumentException("Note-window intersection work budget exceeded");
                            var note = item.Note;
                            double sourceStart = start + note.OffsetQuarters;
                            double clippedStart = Math.Max(Math.Max(sourceStart, start), clip.SourceOffsetQuarters);
                            if (clippedStart >= windowEnd || clippedStart >= start + length) continue;
                            double on = clip.SecondsAtSourceQuarter(clippedStart, arrangement.Tempo);
                            double off = Math.Min(Math.Min(clip.SecondsAtSourceQuarter(sourceStart + item.Quarters, arrangement.Tempo) + item.FixedSeconds, clipEnd), sectionEnd);
                            if (off <= Math.Max(0, on)) continue;
                            long onFrame = Frame(Math.Max(0, on), sampleRate), offFrame = Frame(off, sampleRate);
                            if (offFrame <= onFrame) continue;
                            if (++eventCount > maxEvents) throw new ArgumentException("Scheduled event budget exceeded");
                            notes.Add(new(clip.Id, note.Id, onFrame, offFrame, note.Pitch!.FrequencyHz,
                                note.Velocity, placement.Section.Settings.Gain, placement.Section.Settings.Pan, placement.Id, repeat));
                        }
                    }
                }
                offset = end;
            }
        }
        if (maxAssetBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxAssetBytes));
        var usedAssets = new HashSet<PcmAsset>(ReferenceEqualityComparer.Instance);
        long assetBytes = 0;
        var audio = trackBuses.ToDictionary(t => t, _ => new List<ScheduledAudioClip>());
        foreach (var clip in arrangement.AudioClips)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!audio.TryGetValue(clip.TrackId, out var clips)) throw new ArgumentException($"Clip {clip.Id} has no track bus");
            double start = clip.StartSeconds(arrangement.Tempo);
            double startFrames = start * sampleRate;
            if (!double.IsFinite(startFrames) || startFrames <= long.MinValue || startFrames >= long.MaxValue)
                throw new ArgumentException("Audio placement is outside the supported frame range");
            long startFrame = checked((long)Math.Round(startFrames, MidpointRounding.AwayFromZero));
            // Quantize placement once, then preserve duration independently. At a
            // matching sample rate every source frame retains exactly one output frame.
            long durationFrames = clip.SampleRate == sampleRate ? clip.LengthFrames :
                Frame((double)clip.LengthFrames / clip.SampleRate, sampleRate);
            long endFrame = checked(startFrame + durationFrames);
            visibleEnd = Math.Max(visibleEnd, endFrame);
            if (soloTrack.HasValue && clip.TrackId != soloTrack.Value) continue;
            if (audioAssets is null || !audioAssets.TryGetValue(clip.SourceId, out var asset))
            {
                diagnostics.Add(new(clip.Id, "missing-audio-asset", "Audio asset is unavailable; clip plays silence"));
                continue;
            }
            if (usedAssets.Add(asset) && (assetBytes = checked(assetBytes + asset.Bytes)) > maxAssetBytes)
                throw new ArgumentException("Arrangement audio assets exceed the decoded memory budget");
            if (asset.SampleRate != clip.SampleRate) throw new ArgumentException("Audio asset sample rate differs from the saved clip metadata");
            if (clip.SourceOffsetFrames + clip.LengthFrames > asset.Frames)
                diagnostics.Add(new(clip.Id, "audio-window-outside", "Audio asset is shorter than the window; missing material plays silence"));
            if (asset.SampleRate != sampleRate)
                diagnostics.Add(new(clip.Id, "linear-rate-conversion", "Audio playback uses linear sample-rate conversion"));
            if (endFrame > Math.Max(0, startFrame))
                clips.Add(new(clip.Id, asset, startFrame, endFrame, clip.SourceOffsetFrames, clip.LengthFrames, clip.Envelope));
        }
        if (instruments is not null)
            foreach (var track in trackBuses)
                if (instruments.TryGetValue(track, out var instrument) && instrument.Sample is { } sample)
                {
                    if (usedAssets.Add(sample) && (assetBytes = checked(assetBytes + sample.Bytes)) > maxAssetBytes)
                        throw new ArgumentException("Arrangement sampler assets exceed the decoded memory budget");
                    diagnostics.Add(new(track, "sampler-linear-interpolation", "Sampler uses linear pitch/rate interpolation"));
                }
        if (instruments is not null)
            foreach (var instrument in instruments.Values)
                foreach (var sample in instrument.GraphSamples?.Assets.Values ?? [])
                    if (usedAssets.Add(sample) && (assetBytes = checked(assetBytes + sample.Bytes)) > maxAssetBytes)
                        throw new ArgumentException("Arrangement graph sample assets exceed the decoded memory budget");
        var byBus = busMap.ToDictionary(p => p.Value, p => p.Key);
        var pools = new Dictionary<Guid, PreparedNotePlayback>();
        var monitors = new Dictionary<Guid, PreparedLiveInstrument>();
        var playbackSources = Enumerable.Range(0, processor.InputBusCount).Select(bus =>
        {
            if (!byBus.TryGetValue(bus, out var track))
                return (IPreparedAudioPlayback)new PreparedPcmPlayback([], sampleRate, blockFrames, visibleEnd);
            var settings = instruments is not null && instruments.TryGetValue(track, out var configured) ? configured : null;
            var pool = new PreparedNotePlayback(events[track], sampleRate, blockFrames, visibleEnd, settings, maxEvents, graphAutomation: instrumentAutomation is not null && instrumentAutomation.TryGetValue(track, out var lanes) ? lanes : null);
            if (settings?.VoiceGraph is not null) pools.Add(track, pool);
            if (track == monitoredTrack)
                monitors.Add(track, new(settings ?? new(), midiPitchMap ?? MidiPitchMap.EqualTemperament, sampleRate, blockFrames,
                    automation: instrumentAutomation is not null && instrumentAutomation.TryGetValue(track, out var monitorLanes) ? monitorLanes : null));
            return new PreparedMixedPlayback(pool, new PreparedPcmPlayback(audio[track], sampleRate, blockFrames, visibleEnd));
        }).ToArray();
        cancellation.ThrowIfCancellationRequested();
        var controls = (trackInstrumentBindings ?? new Dictionary<Guid, Guid>()).Where(p => pools.ContainsKey(p.Key))
            .GroupBy(p => p.Value).ToDictionary(g => g.Key, g => new PreparedInstrumentControlGroup(g.Select(p => pools[p.Key]), g.Where(p => monitors.ContainsKey(p.Key)).Select(p => monitors[p.Key])));
        return new(new(processor, playbackSources, instrumentControls: controls.Values, monitors: monitors.ToDictionary(p => busMap[p.Key], p => p.Value), minimumFrames: minimumFrames), new ReadOnlyDictionary<Guid, IReadOnlyList<ScheduledNote>>(
            events.ToDictionary(p => p.Key, p => (IReadOnlyList<ScheduledNote>)Array.AsReadOnly(p.Value.ToArray()))), diagnostics.AsReadOnly()) { InstrumentControls = new ReadOnlyDictionary<Guid, PreparedInstrumentControlGroup>(controls), Monitors = new ReadOnlyDictionary<Guid, PreparedLiveInstrument>(monitors) };
    }

    private static (NoteEvent, double, double)[] PrepareSection(SectionSnapshot section, int budget, CancellationToken cancellation)
    {
        if (section.Settings.ReverbSeconds is > 0 || section.Settings.VoicePoolSize.HasValue)
            throw new NotSupportedException("Source-local reverb/voice-pool settings need explicit device bindings before arrangement playback");
        var result = new List<(NoteEvent, double, double)>();
        foreach (var sequence in section.Sequences)
        {
            foreach (var voice in sequence.Notes.GroupBy(n => n.VoiceId))
            {
                var authored = voice.ToArray();
                for (int i = 0; i < authored.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var note = authored[i];
                    if (!Enum.IsDefined(note.Articulation) || !double.IsFinite(note.DurationOverlap))
                        throw new ArgumentException("Invalid note articulation/overlap");
                    if (note.Pitch is null) continue;
                    if (note.PortamentoMs != 0) throw new NotSupportedException("Portamento needs an instrument supporting note glide");
                    double rests = 0;
                    if (note.IsTied)
                        for (int r = i + 1; r < authored.Length && authored[r].Pitch is null; r++) rests += authored[r].DurationQuarters;
                    var duration = NoteDuration.ProjectDuration(note.DurationQuarters, note.Articulation, note.IsTied, rests,
                        note.DurationOverlap, section.Settings.SustainPedal);
                    if (result.Count == budget) throw new ArgumentException("Source note preparation budget exceeded");
                    result.Add((note, duration.Quarters, duration.FixedSeconds));
                }
            }
        }
        return result.ToArray();
    }
    private static long Frame(double seconds, int rate)
    {
        double frames = seconds * rate;
        if (!double.IsFinite(frames) || frames < 0 || frames >= long.MaxValue) throw new ArgumentOutOfRangeException(nameof(seconds));
        return checked((long)Math.Round(frames, MidpointRounding.AwayFromZero));
    }
}
