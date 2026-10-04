namespace Flow.Studio.Model;

/// <summary>Detached arrangement state. Source and track IDs are external bindings;
/// missing bindings remain representable for project repair. No runtime or UI objects.</summary>
public sealed class ArrangementSnapshot
{
    public Guid Id { get; }
    public ProjectTempoMap Tempo { get; }
    public ProjectMeterMap Meter { get; }
    public IReadOnlyList<ScoreClip> ScoreClips { get; }
    public IReadOnlyList<AudioClip> AudioClips { get; }

    public ArrangementSnapshot(Guid id, ProjectTempoMap tempo, ProjectMeterMap meter,
        IEnumerable<ScoreClip>? scoreClips = null, IEnumerable<AudioClip>? audioClips = null)
    {
        Id = Validate.Id(id, nameof(id));
        ArgumentNullException.ThrowIfNull(tempo);
        ArgumentNullException.ThrowIfNull(meter);
        Tempo = tempo;
        Meter = meter;
        var scores = (scoreClips ?? []).ToArray();
        var audio = (audioClips ?? []).ToArray();
        if (scores.Length + (long)audio.Length > 100_000)
            throw new ArgumentException("Arrangement exceeds the 100,000 clip limit");
        var ids = new HashSet<Guid>();
        foreach (var clip in scores)
            if (clip is null || !ids.Add(clip.Id)) throw new ArgumentException("Clip IDs must be unique and clips non-null");
        foreach (var clip in audio)
            if (clip is null || !ids.Add(clip.Id)) throw new ArgumentException("Clip IDs must be unique and clips non-null");
        ScoreClips = Array.AsReadOnly(scores);
        AudioClips = Array.AsReadOnly(audio);
    }

    public ArrangementSnapshot SplitScore(Guid clipId, MusicalDuration point, Guid rightId)
    {
        var clip = ScoreClips.FirstOrDefault(c => c.Id == clipId) ?? throw new ArgumentException("Unknown score clip", nameof(clipId));
        var pair = ClipOperations.Split(clip, point, rightId);
        return new(Id, Tempo, Meter,
            ScoreClips.SelectMany(c => c.Id == clipId ? new[] { pair.Left, pair.Right } : new[] { c }), AudioClips);
    }
}

/// <summary>Control-thread document. All mutations go through history actions.</summary>
public sealed class ArrangementDocument
{
    public ArrangementSnapshot Snapshot { get; private set; }
    public ActionHistory History { get; }
    public ArrangementDocument(ArrangementSnapshot snapshot, int historyCapacity = 128,
        long historyBudgetBytes = 64 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
        History = new(historyCapacity, historyBudgetBytes);
    }

    /// <summary>Build the entire detached result before installation. Use one edit
    /// per gesture/transaction; redo stores the result rather than rerunning this function.</summary>
    public void Edit(string description, Func<ArrangementSnapshot, ArrangementSnapshot> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var before = Snapshot;
        var after = transform(before);
        ArgumentNullException.ThrowIfNull(after);
        if (after.Id != before.Id) throw new ArgumentException("An edit cannot change document identity");
        if (ReferenceEquals(before, after)) return;
        var action = new EditAction(this, description, before, after);
        History.Execute(action, EstimateBytes(before) + EstimateBytes(after));
    }

    // Conservative accounting of snapshot collections/clip strings, not a CLR heap measurement.
    private static long EstimateBytes(ArrangementSnapshot snapshot) => checked(512L +
        snapshot.ScoreClips.Sum(c => 256L + c.LayerId.Length * 2L) + snapshot.AudioClips.Count * 256L +
        snapshot.Tempo.Changes.Count * 64L + snapshot.Meter.Changes.Count * 64L);

    private sealed class EditAction(ArrangementDocument document, string description,
        ArrangementSnapshot before, ArrangementSnapshot after) : IUndoableAction
    {
        public string Description { get; } = description;
        public void Undo() => Install(after, before);
        public void Redo() => Install(before, after);
        private void Install(ArrangementSnapshot expected, ArrangementSnapshot next)
        {
            if (!ReferenceEquals(document.Snapshot, expected))
                throw new InvalidOperationException("Action was prepared for a different document revision");
            document.Snapshot = next;
        }
    }
}
