using Flow.Music.IO;
using Flow.Studio.Model;

namespace Flow.Studio.Host;

/// <summary>MIDI file adapter. Read on a worker, then apply detached notes on the
/// document owner. Each file track/channel becomes a separate editable clip on the
/// selected existing DAW track. File tempo/meter never changes the project implicitly.</summary>
public static class ProjectMidiImport
{
    public static Task<ImportedMidiNotes> ReadAsync(string path, CancellationToken cancellation = default)
    {
        string fullPath = Path.GetFullPath(path);
        return Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(fullPath);
            if (stream.Length > MidiNoteImporter.MaxBytes) throw new InvalidDataException("MIDI exceeds import file budget");
            var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
            return MidiNoteImporter.Read(bytes, cancellation);
        }, cancellation);
    }
    public static IReadOnlyList<Guid> Apply(ProjectDocument document, ImportedMidiNotes imported, Guid targetTrack, double anchorQuarters)
    {
        ArgumentNullException.ThrowIfNull(imported);
        if (imported.TicksPerQuarter <= 0) throw new ArgumentException("Invalid MIDI timing");
        if (imported.Parts.Count is < 1 or > 4096) throw new ArgumentException("MIDI import requires 1–4096 note-bearing track/channel parts");
        var clips = imported.Parts.Select(part => new ImportedNoteClip(Guid.NewGuid(), Guid.NewGuid(), targetTrack, anchorQuarters,
            string.IsNullOrWhiteSpace(part.Name) ? $"MIDI track {part.Track + 1}, channel {part.Channel + 1}" : part.Name,
            new(Math.Max(part.DurationQuarters, 1.0 / imported.TicksPerQuarter), part.Notes))).ToArray();
        ProjectNoteCommands.Import(document, clips);
        return Array.AsReadOnly(clips.Select(c => c.ClipId).ToArray());
    }
}
