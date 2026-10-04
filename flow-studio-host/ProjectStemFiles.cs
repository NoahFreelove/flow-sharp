using System.Text.Json;
using Flow.Audio;
using Flow.Studio.Engine;
using Flow.Studio.Model;

namespace Flow.Studio.Host;

public sealed record StemExportProgress(int TrackIndex, int TrackCount, Guid TrackId, long Frames, long TotalFrames);
public sealed record ExportedStem(Guid TrackId, string Name, string FileName);
public sealed record ProjectStemResult(string Directory, long Frames, int SampleRate,
    IReadOnlyList<ExportedStem> Stems, IReadOnlyList<string> Diagnostics);

/// <summary>Sequential captured-project stems, soloing each track's source and
/// inserts through the unchanged shared/master graph. Nonlinear processing and
/// autonomous shared generators mean their sum need not equal the full mix.</summary>
public static class ProjectStemFiles
{
    public const string ProcessingMode = "Solo track and inserts through shared mixer and master; saved mute retained, saved solo replaced by stem selection; stems are not guaranteed additive";
    private sealed record Manifest(int Version, Guid ProjectId, string Processing, int SampleRate,
        long Frames, ExportedStem[] Stems, string[] Diagnostics);

    /// <summary>Exports selected routed tracks (all when omitted) in project order
    /// to a new directory, atomically publishing
    /// the set after all WAVs and its manifest finish. Existing directories are never
    /// overwritten. The caller owns/awaits the task; progress runs on its worker.</summary>
    public static Task<ProjectStemResult> ExportAsync(ProjectSnapshot snapshot, string projectDirectory,
        string destinationDirectory, int? sampleRate = null, int? blockFrames = null,
        CancellationToken cancellation = default, Action<StemExportProgress>? progress = null,
        IReadOnlyCollection<Guid>? selectedTracks = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var ids = selectedTracks?.Take(65).ToArray() ?? snapshot.Routing.Tracks.Select(t => t.Id).ToArray();
        var selection = ids.ToHashSet();
        if (ids.Length is < 1 or > 64 || selection.Count != ids.Length ||
            selection.Any(id => !snapshot.Routing.Tracks.Any(t => t.Id == id)))
            throw new ArgumentException("Stem selection requires 1–64 distinct existing tracks", nameof(selectedTracks));
        var tracks = snapshot.Routing.Tracks.Where(t => selection.Contains(t.Id)).ToArray();
        string directory = Path.GetFullPath(projectDirectory), output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDirectory));
        return Task.Run(() => Export(snapshot, tracks, directory, output, sampleRate ?? snapshot.RenderSettings.SampleRate, blockFrames ?? snapshot.RenderSettings.BlockFrames, cancellation, progress), cancellation);
    }
    private static ProjectStemResult Export(ProjectSnapshot snapshot, ProjectTrack[] tracks, string directory, string output,
        int sampleRate, int blockFrames, CancellationToken cancellation, Action<StemExportProgress>? progress)
    {
        cancellation.ThrowIfCancellationRequested();
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Stem destination already exists");
        if (snapshot.Routing.Tracks.Count == 0) throw new ArgumentException("Stem export requires at least one track");
        string parent = Path.GetDirectoryName(output) ?? throw new ArgumentException("Invalid stem destination");
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Stem destination parent must exist");
        var assets = ProjectAudioAssets.Resolve(snapshot, directory, cancellation: cancellation);
        var full = ProjectCompiler.Prepare(snapshot, sampleRate, blockFrames, cancellation, assets.Assets);
        if (full.Diagnostics.Any(d => d.Code is "missing-source-layer" or "missing-audio-asset"))
            throw new InvalidDataException("Cannot export stems with missing clip media");
        long frames = full.Playback.TotalFrames;
        // All solo chains are subsets of the full chain. Keep its common timeline,
        // including the longest prepared release/effect tail, for every output.
        if (frames > (uint.MaxValue - 50L) / 8) throw new ArgumentException("Stems exceed RIFF/WAVE size limit");
        var diagnostics = assets.Diagnostics.Concat(full.Diagnostics.Select(d => $"{d.ClipId}: {d.Message}")).ToList();
        var stems = new List<ExportedStem>();
        string staging = Path.Combine(parent, $".flow-stems-{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(staging);
        try
        {
            for (int i = 0; i < tracks.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var track = tracks[i];
                var prepared = ProjectCompiler.Prepare(snapshot, sampleRate, blockFrames, cancellation, assets.Assets,
                    soloTrack: track.Id, minimumFrames: frames);
                if (prepared.Playback.TotalFrames != frames) throw new InvalidDataException("Solo render exceeds the common stem duration");
                string name = $"{i + 1:D2}-{track.Id:N}.wav";
                using (var stream = new FileStream(Path.Combine(staging, name), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    PlaybackWaveWriter.Write(stream, prepared.Playback, cancellation,
                        (done, total) => progress?.Invoke(new(i, tracks.Length, track.Id, done, total)));
                    stream.Flush(flushToDisk: true);
                }
                stems.Add(new(track.Id, track.Name, name));
            }
            var manifest = new Manifest(1, snapshot.Arrangement.Id, ProcessingMode, sampleRate, frames, stems.ToArray(), diagnostics.ToArray());
            using (var stream = new FileStream(Path.Combine(staging, "stems.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(flushToDisk: true);
            }
            cancellation.ThrowIfCancellationRequested();
            Directory.Move(staging, output);
            return new(output, frames, sampleRate, stems.AsReadOnly(), diagnostics.AsReadOnly());
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
