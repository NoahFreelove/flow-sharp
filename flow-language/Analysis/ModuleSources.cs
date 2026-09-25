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
    private readonly string[] _searchPaths;
    private readonly Func<string, string?>? _sourceOverlay;

    public FileModuleSourceProvider(string baseDirectory, string? stdlibDirectory = null,
        IEnumerable<string>? searchPaths = null, Func<string, string?>? sourceOverlay = null)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory);
        _sourceOverlay = sourceOverlay;
        _stdlibDirectory = Path.GetFullPath(stdlibDirectory ?? AppContext.BaseDirectory);
        _searchPaths = (searchPaths ?? Array.Empty<string>())
            .Select(path => Path.GetFullPath(path, _baseDirectory)).ToArray();
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
            foreach (var directory in _searchPaths)
            {
                var candidate = Path.GetFullPath(Path.Combine(directory,
                    requestedPath.EndsWith(".flow", StringComparison.Ordinal) ? requestedPath : requestedPath + ".flow"));
                if (Read(candidate) is { } source) return source;
            }
            var importer = Path.GetFullPath(importingSourceId, _baseDirectory);
            path = Path.GetFullPath(requestedPath, Path.GetDirectoryName(importer)!);
        }
        return Read(path);
    }

    private SourceDocument? Read(string path)
    {
        var text = _sourceOverlay?.Invoke(path);
        if (text is not null) return new SourceDocument(path, text);
        return File.Exists(path) ? new SourceDocument(path, File.ReadAllText(path)) : null;
    }
}
