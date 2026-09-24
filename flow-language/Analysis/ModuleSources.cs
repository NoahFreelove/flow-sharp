namespace FlowLang.Analysis;

/// <summary>Hosts supply source identities and text; analysis never invokes module bodies.</summary>
public interface IModuleSourceProvider
{
    SourceDocument? Resolve(string requestedPath, string importingSourceId);
}

/// <summary>Filesystem adapter with explicit roots. Never changes process working directory.</summary>
public sealed class FileModuleSourceProvider : IModuleSourceProvider
{
    private readonly string _baseDirectory;
    private readonly string _stdlibDirectory;

    public FileModuleSourceProvider(string baseDirectory, string? stdlibDirectory = null)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
        _stdlibDirectory = Path.GetFullPath(stdlibDirectory ?? AppContext.BaseDirectory);
    }

    public SourceDocument? Resolve(string requestedPath, string importingSourceId)
    {
        string path;
        if (requestedPath.StartsWith('@'))
        {
            var name = requestedPath[1..];
            if (!name.EndsWith(".flow", StringComparison.Ordinal)) name += ".flow";
            path = Path.GetFullPath(Path.Combine(_stdlibDirectory, name));
        }
        else
        {
            var importer = Path.GetFullPath(importingSourceId, _baseDirectory);
            path = Path.GetFullPath(requestedPath, Path.GetDirectoryName(importer)!);
        }
        return File.Exists(path) ? new SourceDocument(path, File.ReadAllText(path)) : null;
    }
}
