using System.Text;

namespace Flow.Studio.Model;

/// <summary>Atomic project save/reopen. Source is never executed on load.</summary>
public static class ProjectFile
{
    public static void Save(string path, ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        WriteSnapshot(path, document.Snapshot);
        document.History.MarkSaved();
    }

    /// <summary>Write an autosave/recovery snapshot without marking the working document saved.
    /// The host supplies a separate recovery path and controls scheduling.</summary>
    public static void SaveRecovery(string path, ProjectSnapshot snapshot) => WriteSnapshot(path, snapshot);

    private static void WriteSnapshot(string path, ProjectSnapshot snapshot)
    {
        string destination = Path.GetFullPath(path);
        string json = ProjectJson.Serialize(snapshot);
        if (json.Length > ProjectJson.MaxCharacters) throw new InvalidOperationException("Project exceeds file limit");
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".flow-project-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true), leaveOpen: true))
                {
                    writer.Write(json);
                    writer.Flush();
                }
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            // Do not hide the original I/O failure if cleanup is unavailable.
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public static ProjectDocument Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 3L * ProjectJson.MaxCharacters) throw new InvalidDataException("Project exceeds file limit");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        return new(ProjectJson.Deserialize(reader.ReadToEnd()));
    }
}
