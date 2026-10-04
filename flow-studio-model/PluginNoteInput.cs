using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Music.Model;

namespace Flow.Studio.Model;

/// <summary>A detached source-relative note window. Notes retain identity and
/// resolved tuning; the window has explicit duration independent of its last note.</summary>
public sealed class PluginNoteInput
{
    public double DurationQuarters { get; }
    public IReadOnlyList<NoteEvent> Notes { get; }
    public PluginNoteInput(double durationQuarters, IEnumerable<NoteEvent> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        Validate.Nonnegative(durationQuarters, nameof(durationQuarters));
        var copy = notes.Take(100001).ToArray();
        if (copy.Length > 100000 || copy.Any(n => n is null || n.Id == Guid.Empty || string.IsNullOrWhiteSpace(n.VoiceId) ||
            !double.IsFinite(n.OffsetQuarters) || n.OffsetQuarters < 0 || !double.IsFinite(n.DurationQuarters) || n.DurationQuarters < 0 ||
            !double.IsFinite(n.OffsetQuarters + n.DurationQuarters) || n.OffsetQuarters + n.DurationQuarters > durationQuarters + 1e-9 ||
            !double.IsFinite(n.Velocity) || n.Velocity is < 0 or > 1 || !Enum.IsDefined(n.Articulation) ||
            !double.IsFinite(n.DurationOverlap) || !double.IsFinite(n.PortamentoMs) || n.PortamentoMs < 0 ||
            n.ExactDuration is { } exact && (exact.Numerator < 0 || exact.Denominator <= 0) ||
            n.Pitch is { } pitch && (!double.IsFinite(pitch.FrequencyHz) || pitch.FrequencyHz <= 0 || "ABCDEFG".IndexOf(pitch.Letter) < 0 ||
                pitch.CentOffset is { } cents && !double.IsFinite(cents))) || copy.Select(n => n.Id).Distinct().Count() != copy.Length)
            throw new ArgumentException("Invalid processor note window, duplicate identity or 100,000-note budget exceeded");
        DurationQuarters = durationQuarters; Notes = Array.AsReadOnly(copy);
    }
    public CompositionSnapshot ToComposition(Guid sourceId, string layer, double bpm = 120)
    {
        Validate.Id(sourceId, nameof(sourceId)); ArgumentException.ThrowIfNullOrWhiteSpace(layer);
        Guid Id(string role) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceId:N}/{layer}/{role}")).AsSpan(0, 16));
        var sequence = new SequenceSnapshot(Id("sequence"), layer, DurationQuarters, Notes);
        var section = new SectionSnapshot(Id("section"), layer, new(Bpm: bpm), [sequence]);
        return new(Id("score"), [new(Id("placement"), section)]);
    }
    private sealed record Wire(int Version, double DurationQuarters, NoteEvent[] Notes);
    private static readonly JsonSerializerOptions Options = new()
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 16 };
    public string Serialize()
    {
        string json = JsonSerializer.Serialize(new Wire(1, DurationQuarters, Notes.ToArray()), Options);
        if (json.Length > 16 * 1024 * 1024) throw new ArgumentException("Note input exceeds interchange budget");
        return json;
    }
    public static PluginNoteInput Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 16 * 1024 * 1024) throw new ArgumentException("Note input exceeds interchange budget");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 16 }); PluginManifest.RejectDuplicates(document.RootElement);
        var wire = JsonSerializer.Deserialize<Wire>(json, Options) ?? throw new ArgumentException("Missing note input");
        if (wire.Version != 1) throw new ArgumentException("Unsupported note input version");
        return new(wire.DurationQuarters, wire.Notes);
    }
}
