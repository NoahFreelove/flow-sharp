using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Studio.Model;

/// <summary>Versioned arrangement interchange, not yet a complete DAW project file.
/// Unknown schema/fields fail rather than silently discarding future project data.</summary>
public static class ArrangementJson
{
    private sealed record Data(int Version, Guid Id, TempoChange[] Tempo, MeterChange[] Meter,
        ScoreClip[] ScoreClips, AudioClip[] AudioClips);
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
    };
    public static string Serialize(ArrangementSnapshot snapshot) => JsonSerializer.Serialize(
        new Data(2, snapshot.Id, snapshot.Tempo.Changes.ToArray(), snapshot.Meter.Changes.ToArray(),
            snapshot.ScoreClips.ToArray(), snapshot.AudioClips.ToArray()), Options);

    public static ArrangementSnapshot Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 32 * 1024 * 1024) throw new ArgumentException("Arrangement exceeds input size limit", nameof(json));
        var data = JsonSerializer.Deserialize<Data>(json, Options) ?? throw new JsonException("Missing arrangement");
        if (data.Version is not (1 or 2)) throw new JsonException($"Unsupported arrangement version {data.Version}");
        if (data.Tempo is null || data.Meter is null || data.ScoreClips is null || data.AudioClips is null)
            throw new JsonException("Missing arrangement collections");
        if (data.Version < 2 && data.AudioClips.Any(c => c.Envelope is not null))
            throw new JsonException("Clip envelopes require arrangement schema 2");
        return new(data.Id, new(data.Tempo), new(data.Meter), data.ScoreClips, data.AudioClips);
    }
}
