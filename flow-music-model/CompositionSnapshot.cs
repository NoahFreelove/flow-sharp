namespace Flow.Music.Model;

/// <summary>Optional authoring provenance, independent of any parser or document service.</summary>
public sealed record SourceOrigin(string? SourceId, int Line, int Column, int Length = 0);

public enum NoteArticulation { Normal, Staccato, Tenuto, Marcato, Accent, Sforzando, Legato }

/// <summary>Spelling and resolved tuning at compilation time. No live tuning/session reference.</summary>
public sealed record NotePitch(char Letter, int Octave, int Alteration, double? CentOffset,
    int MidiKey, double FrequencyHz);

/// <summary>Exact authored tuplet duration, in quarter-note units.</summary>
public sealed record RationalDuration(int Numerator, int Denominator);

/// <summary>
/// An authored event, including rests (Pitch=null). Onsets already include timing
/// offsets; duration excludes articulation, tie, overlap and pedal render tails.
/// VoiceId preserves independent voice/bar order for tie and rest interpretation.
/// </summary>
public sealed record NoteEvent(Guid Id, string VoiceId, double OffsetQuarters, double DurationQuarters,
    NotePitch? Pitch, double Velocity = 0.63, NoteArticulation Articulation = NoteArticulation.Normal,
    bool IsTied = false, double DurationOverlap = 0, double PortamentoMs = 0,
    RationalDuration? ExactDuration = null, SourceOrigin? Origin = null);

public sealed record BarSpan(Guid Id, double OffsetQuarters, double DurationQuarters,
    int Numerator, int Denominator, bool IsPickup);

/// <summary>A detached sequence. Construction copies the input collections.</summary>
public sealed class SequenceSnapshot
{
    public Guid Id { get; }
    public string Name { get; }
    public double DurationQuarters { get; }
    public IReadOnlyList<NoteEvent> Notes { get; }
    public IReadOnlyList<BarSpan> Bars { get; }

    public SequenceSnapshot(Guid id, string name, double durationQuarters,
        IEnumerable<NoteEvent> notes, IEnumerable<BarSpan>? bars = null)
    {
        Timing.RequireNonnegative(durationQuarters, nameof(durationQuarters));
        Id = id;
        Name = name;
        DurationQuarters = durationQuarters;
        Notes = Array.AsReadOnly(notes.ToArray());
        Bars = Array.AsReadOnly((bars ?? []).ToArray());
        foreach (var note in Notes)
        {
            if (!double.IsFinite(note.OffsetQuarters)) throw new ArgumentException("Note offset must be finite", nameof(notes));
            Timing.RequireNonnegative(note.DurationQuarters, nameof(notes));
            if (note.ExactDuration is { } rational && (rational.Numerator < 0 || rational.Denominator <= 0))
                throw new ArgumentException("Exact duration must have nonnegative numerator and positive denominator", nameof(notes));
        }
    }
}

/// <summary>Evaluated rendering controls; no executable section body, scope or closures.</summary>
public sealed record SectionSettings(double Bpm = 120, double Pan = 0, double Gain = 1,
    double? ReverbSeconds = null, int? VoicePoolSize = null, bool SustainPedal = false,
    string? Key = null, double? Swing = null);

public sealed class SectionSnapshot
{
    public Guid Id { get; }
    public string Name { get; }
    public SectionSettings Settings { get; }
    public SourceOrigin? Origin { get; }
    public IReadOnlyList<SequenceSnapshot> Sequences { get; }
    public double DurationQuarters { get; }
    public double DurationSeconds => Timing.QuartersToSeconds(DurationQuarters, Settings.Bpm);

    public SectionSnapshot(Guid id, string name, SectionSettings settings,
        IEnumerable<SequenceSnapshot> sequences, SourceOrigin? origin = null)
    {
        Timing.RequirePositive(settings.Bpm, nameof(settings));
        Id = id;
        Name = name;
        Settings = settings;
        Origin = origin;
        Sequences = Array.AsReadOnly(sequences.ToArray());
        DurationQuarters = Sequences.Select(s => s.DurationQuarters).DefaultIfEmpty(0).Max();
    }
}

/// <summary>Repeated placements share immutable evaluated section data.</summary>
public sealed record SectionPlacement(Guid Id, SectionSnapshot Section, int RepeatCount = 1);
public sealed record SectionOccurrence(Guid PlacementId, int RepeatIndex, SectionSnapshot Section,
    double OffsetQuarters, double OffsetSeconds);

public sealed class CompositionSnapshot
{
    public Guid Id { get; }
    public IReadOnlyList<SectionPlacement> Placements { get; }
    public double DurationSeconds { get; }

    public CompositionSnapshot(Guid id, IEnumerable<SectionPlacement> placements)
    {
        Id = id;
        Placements = Array.AsReadOnly(placements.ToArray());
        foreach (var placement in Placements)
            ArgumentOutOfRangeException.ThrowIfNegative(placement.RepeatCount);
        DurationSeconds = Placements.Sum(p => p.Section.DurationSeconds * p.RepeatCount);
        Timing.RequireNonnegative(DurationSeconds, nameof(placements));
    }

    /// <summary>Score-time positions; excludes renderer-specific tails and frame rounding.</summary>
    public IEnumerable<SectionOccurrence> Timeline()
    {
        double quarters = 0, seconds = 0;
        foreach (var placement in Placements)
            for (int repeat = 0; repeat < placement.RepeatCount; repeat++)
            {
                yield return new(placement.Id, repeat, placement.Section, quarters, seconds);
                quarters += placement.Section.DurationQuarters;
                seconds += placement.Section.DurationSeconds;
            }
    }
}
