using Flow.Audio;
using Flow.Studio.Engine;
using Flow.Studio.Model;

namespace Flow.Studio.Host;

public sealed record ProjectBounceResult(string Path, long Frames, int SampleRate, IReadOnlyList<string> Diagnostics);

/// <summary>Captured-project offline bounce. Uses fresh playback state and no
/// device, interpreter evaluation, document mutation or live transport changes.</summary>
public static class ProjectBounceFile
{
    /// <summary>Exports an exact selection at the chosen output sample rate.
    /// Includes continuous effect history from before the start. The end is a hard
    /// cut; select through the prepared tail to include it. Progress includes preroll.</summary>
    public static Task<ProjectBounceResult> ExportRangeAsync(ProjectSnapshot snapshot, string projectDirectory,
        string destination, ProjectExportRange range, int? sampleRate = null, int? blockFrames = null,
        bool overwrite = false, CancellationToken cancellation = default, Action<long, long>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(range);
        string output = Path.GetFullPath(destination), directory = Path.GetFullPath(projectDirectory);
        return Task.Run(() => Export(snapshot, directory, output, sampleRate ?? snapshot.RenderSettings.SampleRate,
            blockFrames ?? snapshot.RenderSettings.BlockFrames, overwrite, cancellation, progress, range), cancellation);
    }

    /// <summary>The caller owns/awaits the returned task and supplies cancellation.
    /// Progress runs on the export worker. Output is stereo float32 without clipping
    /// or normalization and includes the prepared instrument/effect tails.</summary>
    public static Task<ProjectBounceResult> ExportAsync(ProjectSnapshot snapshot, string projectDirectory,
        string destination, int? sampleRate = null, int? blockFrames = null, bool overwrite = false,
        CancellationToken cancellation = default, Action<long, long>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string output = Path.GetFullPath(destination), directory = Path.GetFullPath(projectDirectory);
        return Task.Run(() => Export(snapshot, directory, output, sampleRate ?? snapshot.RenderSettings.SampleRate, blockFrames ?? snapshot.RenderSettings.BlockFrames, overwrite, cancellation, progress), cancellation);
    }

    private static ProjectBounceResult Export(ProjectSnapshot snapshot, string directory, string output,
        int sampleRate, int blockFrames, bool overwrite, CancellationToken cancellation, Action<long, long>? progress,
        ProjectExportRange? range = null)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!overwrite && File.Exists(output)) throw new IOException("Export destination already exists");
        foreach (var asset in snapshot.Assets)
            if (Path.GetFullPath(Path.Combine(directory, asset.RelativePath)) == output)
                throw new ArgumentException("Export cannot overwrite a project audio asset");
        var assets = ProjectAudioAssets.Resolve(snapshot, directory, cancellation: cancellation);
        var prepared = ProjectCompiler.Prepare(snapshot, sampleRate, blockFrames, cancellation, assets.Assets);
        long start = range?.StartFrame ?? 0, end = range?.EndFrame ?? prepared.Playback.TotalFrames;
        if (end > prepared.Playback.TotalFrames) throw new ArgumentException("Export range extends beyond prepared playback");
        var missing = prepared.Diagnostics.Where(d => d.Code is "missing-source-layer" or "missing-audio-asset").ToArray();
        if (missing.Length != 0) throw new InvalidDataException("Cannot export missing clip media: " + string.Join("; ", missing.Select(d => $"{d.ClipId}: {d.Message}")));
        var diagnostics = Array.AsReadOnly(assets.Diagnostics.Concat(prepared.Diagnostics.Select(d => $"{d.ClipId}: {d.Message}")).ToArray());
        string temporary = Path.Combine(Path.GetDirectoryName(output)!, $".flow-bounce-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                PlaybackWaveWriter.WriteRange(stream, prepared.Playback, start, end, cancellation, progress);
                stream.Flush(flushToDisk: true);
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, output, overwrite);
            return new(output, end - start, sampleRate, diagnostics);
        }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
