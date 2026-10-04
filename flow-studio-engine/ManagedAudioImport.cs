using Flow.Studio.Model;
namespace Flow.Studio.Engine;

/// <summary>Worker-side import preparation. Copies into a content-addressed project
/// asset directory, validates the copy, and returns detached data for control-thread commit.</summary>
public static class ManagedAudioImport
{
    public static ResolvedAudioAsset Prepare(string sourcePath, string projectDirectory, Guid assetId,
        long maxFileBytes = 512 * 1024 * 1024, long maxDecodedBytes = 256 * 1024 * 1024,
        CancellationToken cancellation = default)
    {
        if (assetId == Guid.Empty || maxFileBytes <= 0) throw new ArgumentException("Invalid import identity/budget");
        cancellation.ThrowIfCancellationRequested();
        string directory = Path.Combine(Path.GetFullPath(projectDirectory), "audio-assets");
        Directory.CreateDirectory(directory);
        string temporaryName = $"import-{Guid.NewGuid():N}.tmp";
        string temporary = Path.Combine(directory, temporaryName);
        try
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (source.Length > maxFileBytes) throw new InvalidDataException("Import exceeds file budget");
                var block = new byte[65536]; long bytes = 0;
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int count = source.Read(block); if (count == 0) break;
                    bytes += count; if (bytes > maxFileBytes) throw new InvalidDataException("Import exceeds file budget");
                    destination.Write(block.AsSpan(0, count));
                }
                destination.Flush(true);
            }
            var prepared = AudioAssetFiles.Inspect(projectDirectory, "audio-assets/" + temporaryName, assetId,
                maxDecodedBytes, maxFileBytes, cancellation);
            string relative = "audio-assets/" + prepared.Reference.Sha256 + ".wav";
            string target = Path.Combine(Path.GetFullPath(projectDirectory), relative);
            cancellation.ThrowIfCancellationRequested();
            try { File.Move(temporary, target, overwrite: false); }
            catch (IOException) when (File.Exists(target))
            {
                var existing = AudioAssetFiles.Inspect(projectDirectory, relative, assetId, maxDecodedBytes, maxFileBytes, cancellation);
                if (existing.Reference.Sha256 != prepared.Reference.Sha256) throw new InvalidDataException("Managed asset path contains different content");
            }
            return new(new(assetId, relative, prepared.Reference.Sha256, prepared.Audio.SampleRate, prepared.Audio.Frames), prepared.Audio);
        }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
