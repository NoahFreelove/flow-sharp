using Flow.Studio.Model;

namespace Flow.Studio.Host;

public sealed record ProjectRecoveryInspection(ProjectSnapshot? Saved, ProjectSnapshot? Recovery,
    string? SavedError, string? RecoveryError, bool DiffersFromSaved);

/// <summary>Read-only recovery discovery. Candidates are not applied/deleted or
/// assumed newer from filesystem timestamps; the host explicitly chooses restore.</summary>
public static class ProjectRecovery
{
    /// <summary>Explicitly discard a resolved recovery candidate. The same lease
    /// as autosave prevents deletion while another host owns this recovery path.</summary>
    public static void Discard(string recoveryPath)
    {
        string path = Path.GetFullPath(recoveryPath);
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        File.Delete(path);
    }
    public static ProjectRecoveryInspection Inspect(string? projectPath, string recoveryPath)
    {
        static (ProjectSnapshot? Snapshot, string? Error) Read(string? path)
        {
            if (path is null) return (null, null);
            try { return (ProjectFile.Load(path).Snapshot, null); }
            catch (FileNotFoundException) { return (null, null); }
            catch (DirectoryNotFoundException) { return (null, null); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException or OverflowException or FormatException)
            { return (null, error.Message); }
        }
        var saved = Read(projectPath); var recovery = Read(recoveryPath);
        if (saved.Snapshot is not null && recovery.Snapshot is not null && saved.Snapshot.Arrangement.Id != recovery.Snapshot.Arrangement.Id)
            recovery = (null, "Recovery belongs to a different project");
        bool differs = recovery.Snapshot is not null && (saved.Snapshot is null ||
            ProjectJson.Serialize(saved.Snapshot) != ProjectJson.Serialize(recovery.Snapshot));
        return new(saved.Snapshot, recovery.Snapshot, saved.Error, recovery.Error, differs);
    }
    public static ProjectDocument Restore(ProjectRecoveryInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        var snapshot = inspection.Recovery ?? throw new InvalidOperationException("No valid recovery snapshot is available");
        var document = new ProjectDocument(snapshot); document.History.MarkUnsaved(); return document;
    }
}
