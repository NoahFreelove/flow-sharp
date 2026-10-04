using System.Text;

namespace Flow.Studio.Model;

/// <summary>Control-thread arrangement persistence. Writes a flushed sibling temp
/// file then atomically replaces the destination. Dirty state changes only after
/// successful replacement. Full project asset/plugin persistence is separate.</summary>
public static class ArrangementFile
{
    public static void Save(string path, ArrangementDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        string destination = Path.GetFullPath(path);
        string json = ArrangementJson.Serialize(document.Snapshot);
        if (json.Length > 32 * 1024 * 1024) throw new InvalidOperationException("Arrangement exceeds file limit");
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".flow-arrangement-{Guid.NewGuid():N}.tmp");
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
            document.History.MarkSaved();
        }
        finally
        {
            // Do not hide the original I/O failure if cleanup is unavailable.
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public static ArrangementDocument Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 96L * 1024 * 1024) throw new InvalidDataException("Arrangement exceeds file limit");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        return new(ArrangementJson.Deserialize(reader.ReadToEnd()));
    }
}
