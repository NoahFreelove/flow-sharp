using System.Security.Cryptography;
using Flow.Audio;
using Flow.Studio.Model;
namespace Flow.Studio.Engine;

public sealed record ResolvedAudioAsset(AudioAssetReference Reference, PcmAsset Audio);
/// <summary>Worker-only file hashing/decoding. No file handles reach playback.</summary>
public static class AudioAssetFiles
{
    public static ResolvedAudioAsset Inspect(string projectDirectory, string relativePath, Guid id,
        long maxDecodedBytes = 256 * 1024 * 1024, long maxFileBytes = 512 * 1024 * 1024,
        CancellationToken cancellation = default)
    {
        // Validate the path before opening, using placeholder metadata.
        _ = new AudioAssetReference(id, relativePath, new string('0', 64), 1, 0);
        string path = Path.Combine(Path.GetFullPath(projectDirectory), relativePath);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (maxFileBytes <= 0 || stream.Length > maxFileBytes) throw new InvalidDataException("Audio file exceeds import budget");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var block = new byte[65536]; long bytes = 0;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            int count = stream.Read(block); if (count == 0) break;
            bytes += count; if (bytes > maxFileBytes) throw new InvalidDataException("Audio file exceeds import budget");
            hash.AppendData(block.AsSpan(0, count));
        }
        string digest = Convert.ToHexStringLower(hash.GetHashAndReset());
        stream.Position = 0;
        var audio = WaveAssetReader.Read(stream, maxDecodedBytes, cancellation);
        return new(new(id, relativePath, digest, audio.SampleRate, audio.Frames), audio);
    }
    public static PcmAsset Resolve(string projectDirectory, AudioAssetReference expected,
        long maxDecodedBytes = 256 * 1024 * 1024, CancellationToken cancellation = default)
    {
        var result = Inspect(projectDirectory, expected.RelativePath, expected.Id, maxDecodedBytes, cancellation: cancellation);
        if (result.Reference != expected) throw new InvalidDataException("Audio asset content or format changed; explicit relink required");
        return result.Audio;
    }
}

public sealed record AudioAssetResolution(IReadOnlyDictionary<Guid, PcmAsset> Assets, IReadOnlyList<string> Diagnostics);
public static class ProjectAudioAssets
{
    /// <summary>Resolve before playback preparation. Missing/changed files remain absent,
    /// preserving clip windows as silence. Resource failures abort preparation.</summary>
    public static AudioAssetResolution Resolve(ProjectSnapshot project, string directory,
        long maxDecodedBytes = 512 * 1024 * 1024, CancellationToken cancellation = default)
    {
        var assets = new Dictionary<Guid, PcmAsset>(); var diagnostics = new List<string>(); long used = 0;
        foreach (var reference in project.Assets)
        {
            cancellation.ThrowIfCancellationRequested();
            long required = checked(reference.Frames * 8);
            if (required > maxDecodedBytes - used) throw new InvalidDataException("Project asset decode budget exceeded");
            try
            {
                var asset = AudioAssetFiles.Resolve(directory, reference, Math.Min(256 * 1024 * 1024, maxDecodedBytes - used), cancellation);
                assets.Add(reference.Id, asset); used += asset.Bytes;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { diagnostics.Add($"Asset {reference.Id} ({reference.RelativePath}): {ex.Message}"); }
        }
        return new(new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, PcmAsset>(assets), diagnostics.AsReadOnly());
    }
}
