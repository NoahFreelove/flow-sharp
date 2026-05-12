using System.Reflection;

namespace FlowCli.Scaffold;

// Writes a scaffold .flow file from the embedded `default.flow` resource.
// The resource name follows the .NET embedded-resource convention:
//   {RootNamespace}.{PathWithDots}.{filename}
// → FlowCli.Scaffold.Templates.default.flow  (RootNamespace=FlowCli, csproj override)
public static class ScaffoldEmitter
{
    // Writes a scaffold .flow file to <targetDir>/<pieceName>.flow.
    // - targetDir is created if it does not exist
    // - returns false (no overwrite) if <pieceName>.flow already exists
    // - pieceName must contain only letters, digits, underscore, hyphen
    public static bool WriteScaffold(string pieceName, string targetDir, out string writtenPath, out string? error)
    {
        writtenPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(pieceName) || pieceName.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
        {
            error = $"Invalid piece name '{pieceName}': must contain only letters, digits, underscore, hyphen.";
            return false;
        }

        Directory.CreateDirectory(targetDir);
        writtenPath = Path.Combine(targetDir, $"{pieceName}.flow");

        if (File.Exists(writtenPath))
        {
            error = $"File already exists: {writtenPath} (not overwriting).";
            return false;
        }

        var asm = Assembly.GetExecutingAssembly();
        var resourceName = "FlowCli.Scaffold.Templates.default.flow";
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            var avail = string.Join(", ", asm.GetManifestResourceNames());
            error = $"Embedded resource not found: {resourceName}. Available: {avail}";
            return false;
        }

        using var reader = new StreamReader(stream);
        var template = reader.ReadToEnd();
        var content = template.Replace("{{PIECE_NAME}}", pieceName);
        File.WriteAllText(writtenPath, content);
        return true;
    }
}
