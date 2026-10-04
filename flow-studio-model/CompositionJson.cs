using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Music.Model;

namespace Flow.Studio.Model;

/// <summary>Versioned detached score interchange. Repeated placements refer to a
/// section table rather than duplicating notes. Validates before constructing a score.</summary>
public static class CompositionJson
{
    private sealed record SequenceData(Guid Id, string Name, double Duration, NoteEvent[] Notes, BarSpan[] Bars);
    private sealed record SectionData(Guid Id, string Name, SectionSettings Settings, SourceOrigin? Origin, SequenceData[] Sequences);
    private sealed record PlacementData(Guid Id, int Section, int Repeats);
    private sealed record Data(int Version, Guid Id, SectionData[] Sections, PlacementData[] Placements);
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32,
    };
    public const int MaxCharacters = 32 * 1024 * 1024;

    public static string Serialize(CompositionSnapshot score)
    {
        ArgumentNullException.ThrowIfNull(score);
        var sections = score.Placements.Select(p => p.Section).Distinct(ReferenceEqualityComparer.Instance).Cast<SectionSnapshot>().ToArray();
        var indices = sections.Select((s, i) => (s, i)).ToDictionary(p => p.s, p => p.i);
        var data = new Data(1, score.Id, sections.Select(s => new SectionData(s.Id, s.Name, s.Settings, s.Origin,
            s.Sequences.Select(q => new SequenceData(q.Id, q.Name, q.DurationQuarters, q.Notes.ToArray(), q.Bars.ToArray())).ToArray())).ToArray(),
            score.Placements.Select(p => new PlacementData(p.Id, indices[p.Section], p.RepeatCount)).ToArray());
        string json = JsonSerializer.Serialize(data, Options);
        if (json.Length > MaxCharacters) throw new InvalidDataException("Score exceeds interchange size limit");
        return json;
    }

    public static CompositionSnapshot Deserialize(string json)
    {
        if (json.Length > MaxCharacters) throw new InvalidDataException("Score exceeds interchange size limit");
        var data = JsonSerializer.Deserialize<Data>(json, Options) ?? throw new JsonException("Missing score");
        if (data.Version != 1 || data.Sections is null || data.Placements is null ||
            data.Sections.Length > 10_000 || data.Placements.Length > 10_000) throw new JsonException("Invalid score schema/budget");
        Validate.Id(data.Id, nameof(data.Id));
        long notes = 0, sequences = 0, bars = 0;
        var sections = new List<SectionSnapshot>();
        foreach (var s in data.Sections)
        {
            ArgumentNullException.ThrowIfNull(s);
            Validate.Id(s.Id, nameof(s.Id));
            ArgumentNullException.ThrowIfNull(s.Name);
            ArgumentNullException.ThrowIfNull(s.Settings);
            ArgumentNullException.ThrowIfNull(s.Sequences);
            Validate.Positive(s.Settings.Bpm, "Bpm");
            Validate.Nonnegative(s.Settings.Gain, "Gain");
            if (Math.Abs(Validate.Finite(s.Settings.Pan, "Pan")) > 1) throw new JsonException("Invalid pan");
            if (s.Settings.ReverbSeconds is double reverb) Validate.Nonnegative(reverb, "ReverbSeconds");
            if (s.Settings.Swing is double swing) Validate.Finite(swing, "Swing");
            if (s.Settings.VoicePoolSize is int voices && voices < 1) throw new JsonException("Invalid voice count");
            sequences += s.Sequences.Length;
            if (sequences > 100_000) throw new JsonException("Sequence budget exceeded");
            var detached = new List<SequenceSnapshot>();
            foreach (var q in s.Sequences)
            {
                ArgumentNullException.ThrowIfNull(q);
                Validate.Id(q.Id, nameof(q.Id));
                ArgumentNullException.ThrowIfNull(q.Name);
                ArgumentNullException.ThrowIfNull(q.Notes);
                ArgumentNullException.ThrowIfNull(q.Bars);
                notes += q.Notes.Length;
                bars += q.Bars.Length;
                if (notes > 1_000_000 || bars > 1_000_000) throw new JsonException("Score event budget exceeded");
                foreach (var n in q.Notes)
                {
                    ArgumentNullException.ThrowIfNull(n);
                    Validate.Id(n.Id, nameof(n.Id));
                    ArgumentNullException.ThrowIfNull(n.VoiceId);
                    Validate.Nonnegative(n.Velocity, "Velocity");
                    if (n.Velocity > 1 || !Enum.IsDefined(n.Articulation)) throw new JsonException("Invalid note controls");
                    Validate.Finite(n.DurationOverlap, "DurationOverlap");
                    Validate.Nonnegative(n.PortamentoMs, "PortamentoMs");
                    if (n.Pitch is { } pitch)
                    {
                        Validate.Positive(pitch.FrequencyHz, "FrequencyHz");
                        if (pitch.CentOffset is double cents) Validate.Finite(cents, "CentOffset");
                    }
                }
                foreach (var b in q.Bars)
                {
                    ArgumentNullException.ThrowIfNull(b);
                    Validate.Id(b.Id, nameof(b.Id));
                    Validate.Finite(b.OffsetQuarters, "OffsetQuarters");
                    Validate.Nonnegative(b.DurationQuarters, "DurationQuarters");
                    if (b.Numerator < 1 || b.Denominator < 1) throw new JsonException("Invalid bar meter");
                }
                detached.Add(new(q.Id, q.Name, q.Duration, q.Notes, q.Bars));
            }
            sections.Add(new(s.Id, s.Name, s.Settings, detached, s.Origin));
        }
        return new(data.Id, data.Placements.Select(p =>
        {
            ArgumentNullException.ThrowIfNull(p);
            Validate.Id(p.Id, nameof(p.Id));
            if (p.Section < 0 || p.Section >= sections.Count || p.Repeats < 0) throw new JsonException("Invalid placement");
            return new SectionPlacement(p.Id, sections[p.Section], p.Repeats);
        }));
    }
}
