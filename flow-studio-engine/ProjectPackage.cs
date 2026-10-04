using Flow.Studio.Model;
namespace Flow.Studio.Engine;

/// <summary>Worker-side self-contained project-directory export. Publishes a new
/// directory only after every copied asset and the project file are verified.
/// Does not mutate the working document or overwrite an existing package.</summary>
public static class ProjectPackage
{
    public static string Create(ProjectSnapshot snapshot, string sourceDirectory, string destinationDirectory,
        long maxPackageBytes = 4L * 1024 * 1024 * 1024, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPackageBytes);
        cancellation.ThrowIfCancellationRequested();
        string destination = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Package destination already exists");
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Package requires a parent directory");
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
        string staging = Path.Combine(parent, ".flow-package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        bool published = false;
        try
        {
            long used = 0;
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            var assets = new List<AudioAssetReference>();
            foreach (var asset in snapshot.Assets)
            {
                cancellation.ThrowIfCancellationRequested();
                bool duplicate = hashes.Contains(asset.Sha256);
                long available = duplicate ? 512L * 1024 * 1024 : Math.Min(512L * 1024 * 1024, maxPackageBytes - used);
                if (available <= 0) throw new InvalidDataException("Package exceeds byte budget");
                var imported = ManagedAudioImport.Prepare(Path.Combine(Path.GetFullPath(sourceDirectory), asset.RelativePath),
                    staging, asset.Id, maxFileBytes: available, cancellation: cancellation);
                if (imported.Reference.Sha256 != asset.Sha256 || imported.Reference.SampleRate != asset.SampleRate || imported.Reference.Frames != asset.Frames)
                    throw new InvalidDataException($"Asset {asset.Id} changed; relink before packaging");
                assets.Add(imported.Reference);
                if (hashes.Add(asset.Sha256)) used = checked(used + new FileInfo(Path.Combine(staging, imported.Reference.RelativePath)).Length);
            }
            var packaged = new ProjectSnapshot(snapshot.Arrangement, snapshot.Context, snapshot.Sources.Values,
                snapshot.Routing, assets, snapshot.Automation, snapshot.RenderSettings);
            string projectFile = Path.Combine(staging, "project.flowproject");
            ProjectFile.SaveRecovery(projectFile, packaged);
            if (new FileInfo(projectFile).Length > maxPackageBytes - used) throw new InvalidDataException("Package exceeds byte budget");
            cancellation.ThrowIfCancellationRequested();
            // Same-parent rename publishes the complete tree. Any race with a newly
            // created destination fails instead of replacing its contents.
            Directory.Move(staging, destination); published = true;
            return Path.Combine(destination, "project.flowproject");
        }
        finally
        {
            if (!published)
                try { Directory.Delete(staging, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
