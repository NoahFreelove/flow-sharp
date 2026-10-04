namespace Flow.Studio.Model;

/// <summary>Portable relative location and immutable identity of an imported WAVE.
/// A changed file must be explicitly imported/relinked, never silently substituted.</summary>
public sealed record AudioAssetReference
{
    public Guid Id { get; }
    public string RelativePath { get; }
    public string Sha256 { get; }
    public int SampleRate { get; }
    public long Frames { get; }
    public AudioAssetReference(Guid id, string relativePath, string sha256, int sampleRate, long frames)
    {
        Id = Validate.Id(id, nameof(id));
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Length > 4096 || relativePath.Contains('\\') ||
            Path.IsPathRooted(relativePath) || relativePath.Split('/').Any(p => p is "" or "." or ".."))
            throw new ArgumentException("Asset path must be a normalized project-relative path");
        if (sha256 is null || sha256.Length != 64 || sha256.Any(c => !char.IsAsciiHexDigit(c))) throw new ArgumentException("Invalid asset hash");
        if (sampleRate is < 1 or > 384000 || frames < 0) throw new ArgumentException("Invalid asset metadata");
        RelativePath = relativePath; Sha256 = sha256.ToLowerInvariant(); SampleRate = sampleRate; Frames = frames;
    }
}
