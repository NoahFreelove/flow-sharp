using Flow.Music.Model;

namespace Flow.Studio.Model;

/// <summary>Captured note transformation. The worker receives a flat authored
/// window; acceptance reinstates section settings and unchanged boundary context.</summary>
public sealed class NoteClipProcessingOperation : IClipProcessingOperation
{
    private readonly ProjectDocument _document;
    private readonly long _version;
    private readonly ScoreClip _clip;
    private readonly CompositionSnapshot _score;
    private readonly Dictionary<Guid, ProcessorClipCapture.NoteOrigin> _origins = [];
    private bool _finished;
    public GeneratorDescriptor Descriptor { get; }
    public PluginPackage Package { get; }
    public GenerationContext Context { get; }
    public PluginNoteInput Input { get; }
    public bool IsCurrent => !_finished && _document.ChangeVersion == _version;

    private NoteClipProcessingOperation(ProjectDocument document, ScoreClip clip, CompositionSnapshot score,
        PluginPackage package, IReadOnlyDictionary<string, double>? values, CancellationToken cancellation)
    {
        _document = document; _version = document.ChangeVersion; _clip = clip; _score = score; Package = package;
        Descriptor = new(1, Guid.NewGuid(), package.Manifest.Builder);
        Input = ProcessorClipCapture.Notes(clip, score, _origins, cancellation);
        var parameters = package.Manifest.Parameters.ToDictionary(p => p.Id, p => p.Default, StringComparer.Ordinal);
        foreach (var pair in values ?? new Dictionary<string, double>())
        {
            var parameter = package.Manifest.Parameters.SingleOrDefault(p => p.Id == pair.Key)
                ?? throw new ArgumentException("Unknown processor parameter");
            _ = parameter.ToNormalized(pair.Value); parameters[pair.Key] = pair.Value;
        }
        var context = document.Snapshot.Context;
        Context = new(context.Revision, context.Seed, context.Tempo, context.Meter, parameters, context.Tuning);
    }
    public static NoteClipProcessingOperation Capture(ProjectDocument document, Guid clipId,
        PluginPackage package, IReadOnlyDictionary<string, double>? values = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(package);
        if (package.Manifest.Kind != FlowPluginKind.NoteTransform) throw new ArgumentException("Expected a note processor");
        var clip = document.Snapshot.Arrangement.ScoreClips.SingleOrDefault(c => c.Id == clipId)
            ?? throw new ArgumentException("Unknown score clip");
        var score = document.Snapshot.Sources.GetValueOrDefault(clip.SourceId)?.Result.ScoreLayers.SingleOrDefault(l => l.Id == clip.LayerId)?.Composition
            ?? throw new ArgumentException("Score source is unavailable");
        return new(document, clip, score, package, values, cancellation);
    }
    public void Cancel() => _finished = true;
    public bool Accept(GeneratedSourceOutput result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!IsCurrent) return false;
        if (result.SourceId != Descriptor.SourceId || result.SourceRevision != 0 || !ReferenceEquals(result.Context, Context))
            throw new ArgumentException("Processor result identity or context does not match capture");
        Package.ValidateOutput(result, 1, 1);
        var sequence = result.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0];
        if (sequence.DurationQuarters <= 0) throw new ArgumentException("A replacement note clip must have positive duration");
        var restored = RestoreSections(sequence);
        // Persist the host-adapted score, not the worker's flat transport envelope.
        var output = new GeneratedSourceOutput(result.SourceId, 0, Context, [new(Package.OutputLayer, restored)]);
        var source = new ProjectSource(Descriptor, Package.Source, output, null, plugin: Package);
        var replacement = new ScoreClip(_clip.Id, _clip.TrackId, Descriptor.SourceId, Package.OutputLayer,
            _clip.AnchorQuarters, _clip.SourceOffsetQuarters, sequence.DurationQuarters, _clip.Nudge);
        _document.Edit("Process note clip", p => new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
            p.Arrangement.ScoreClips.Select(c => c.Id == _clip.Id ? replacement : c), p.Arrangement.AudioClips),
            p.Context, p.Sources.Values.Append(source), p.Routing, p.Assets, p.Automation, p.RenderSettings));
        _finished = true;
        return true;
    }

    private sealed record Segment(int Placement, int Repeat, double Start, SectionSnapshot Section);

    private CompositionSnapshot RestoreSections(SequenceSnapshot processed)
    {
        double windowEnd = Validate.Finite(_clip.SourceOffsetQuarters + Math.Max(_clip.LengthQuarters, processed.DurationQuarters), "processed duration");
        var placements = new List<SectionPlacement>();
        var segments = new List<Segment>();
        var slots = new List<int>();
        double offset = 0;
        for (int p = 0; p < _score.Placements.Count; p++)
        {
            var placement = _score.Placements[p]; double length = placement.Section.DurationQuarters;
            double end = offset + length * placement.RepeatCount;
            if (length > 0 && end > _clip.SourceOffsetQuarters && offset < windowEnd)
            {
                int first = (int)Math.Clamp(Math.Floor((_clip.SourceOffsetQuarters - offset) / length), 0, placement.RepeatCount);
                int last = (int)Math.Clamp(Math.Ceiling((windowEnd - offset) / length), 0, placement.RepeatCount);
                if (last - first > 10000 - segments.Count) throw new ArgumentException("Processed section budget exceeded");
                if (first > 0) placements.Add(placement with { RepeatCount = first });
                for (int repeat = first; repeat < last; repeat++)
                {
                    slots.Add(placements.Count); placements.Add(new(Guid.NewGuid(), placement.Section));
                    segments.Add(new(p, repeat, offset + repeat * length, placement.Section));
                }
                if (last < placement.RepeatCount) placements.Add(placement with { Id = Guid.NewGuid(), RepeatCount = placement.RepeatCount - last });
            }
            else placements.Add(placement);
            offset = end;
        }
        if (windowEnd > offset)
        {
            var section = new SectionSnapshot(Guid.NewGuid(), "Processor extension", new(Bpm: Context.Tempo.Changes[0].Bpm),
                [new(Guid.NewGuid(), "Notes", windowEnd - offset, [])]);
            slots.Add(placements.Count); placements.Add(new(Guid.NewGuid(), section)); segments.Add(new(-1, 0, offset, section));
        }
        var removed = _origins.Values.GroupBy(o => (o.Placement, o.Repeat)).ToDictionary(g => g.Key, g => g.ToDictionary(o => (o.Sequence, o.Note)));
        var additions = segments.Select(_ => new List<(int Sequence, NoteEvent Note)>()).ToArray();
        foreach (var note in processed.Notes)
        {
            _origins.TryGetValue(note.Id, out var origin);
            double at = _clip.SourceOffsetQuarters + note.OffsetQuarters;
            // An unchanged onset at a trimmed left edge belongs to its original
            // occurrence, and retains the original pre-window onset/duration.
            bool sameTiming = origin is not null && note.OffsetQuarters == origin.Captured.OffsetQuarters && note.DurationQuarters == origin.Captured.DurationQuarters;
            int low = 0, high = segments.Count;
            while (low < high) { int mid = low + (high - low) / 2; if (segments[mid].Start <= at) low = mid + 1; else high = mid; }
            int index = low - 1;
            // Zero-duration events at the right endpoint belong to the last section.
            if (index < 0) throw new ArgumentException("Processed note is outside source sections");
            var segment = segments[index];
            bool originalSection = origin is not null && origin.Placement == segment.Placement && origin.Repeat == segment.Repeat;
            int sequenceIndex = originalSection ? origin!.Sequence : 0;
            var restored = note with { OffsetQuarters = at - segment.Start };
            if (originalSection && note.VoiceId == origin!.Captured.VoiceId) restored = restored with { VoiceId = origin.Original.VoiceId };
            if (originalSection && sameTiming)
                restored = restored with { OffsetQuarters = origin!.Original.OffsetQuarters, DurationQuarters = origin.Original.DurationQuarters,
                    ExactDuration = note.ExactDuration == origin.Captured.ExactDuration ? origin.Original.ExactDuration : note.ExactDuration };
            additions[index].Add((sequenceIndex, restored));
        }
        long retainedNotes = 0, retainedSequences = 0, retainedBars = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i]; removed.TryGetValue((segment.Placement, segment.Repeat), out var omit);
            retainedNotes += segment.Section.Sequences.Sum(s => (long)s.Notes.Count) + additions[i].Count;
            retainedSequences += segment.Section.Sequences.Count;
            retainedBars += segment.Section.Sequences.Sum(s => (long)s.Bars.Count);
            if (retainedNotes > 1000000 || retainedSequences > 100000 || retainedBars > 1000000) throw new ArgumentException("Processed score budget exceeded");
            var added = additions[i].ToLookup(a => a.Sequence, a => a.Note);
            var sequences = new List<SequenceSnapshot>();
            for (int s = 0; s < segment.Section.Sequences.Count; s++)
            {
                var original = segment.Section.Sequences[s];
                var notes = original.Notes.Where((note, n) => (omit is null || !omit.ContainsKey((s, n))) &&
                        segment.Start + note.OffsetQuarters < _clip.SourceOffsetQuarters + _clip.LengthQuarters)
                    .Concat(added[s]).OrderBy(n => n.OffsetQuarters);
                sequences.Add(new(Guid.NewGuid(), original.Name, original.DurationQuarters, notes, original.Bars));
            }
            var section = new SectionSnapshot(Guid.NewGuid(), segment.Section.Name, segment.Section.Settings, sequences, segment.Section.Origin);
            placements[slots[i]] = new(Guid.NewGuid(), section);
        }
        if (placements.Count > 10000) throw new ArgumentException("Processed placement budget exceeded");
        var score = new CompositionSnapshot(Guid.NewGuid(), placements);
        // Enforce the same structural and retained-content budgets as save/reopen.
        _ = CompositionJson.Deserialize(CompositionJson.Serialize(score));
        return score;
    }
}
