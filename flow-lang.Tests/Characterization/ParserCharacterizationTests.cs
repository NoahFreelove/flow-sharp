using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// Pins what the frontend parses for every tracked .flow file (tests, examples,
/// contracts, stdlib modules, style packs, scaffold templates). The manifest lists
/// each file with a hash of its canonical AST dump; full dumps of the language
/// contract and examples are kept alongside for reviewable diffs.
/// </summary>
[Collection("FlowScripts")]
public class ParserCharacterizationTests
{
    private const string Manifest = "ast/manifest.txt";

    [Fact]
    public void EveryTrackedProgramParsesToTheSameSyntaxTree()
    {
        var files = Environment.GetEnvironmentVariable("FLOW_UPDATE_SNAPSHOTS") == "1"
            ? TrackedFlowFiles()
            : ManifestFiles();
        Assert.NotEmpty(files);

        var manifest = new StringBuilder();
        foreach (var file in files)
        {
            var source = File.ReadAllText(Path.Combine(Snapshot.RepoRoot, file));
            var dump = AstDump.Parse(source, file);
            manifest.Append(Hash(dump)).Append("  ").Append(file).Append('\n');
            if (file.StartsWith("contracts/", StringComparison.Ordinal) || file.StartsWith("examples/language/", StringComparison.Ordinal))
                Snapshot.Verify("ast/" + file.Replace('/', '_') + ".txt", dump);
        }
        Snapshot.Verify(Manifest, manifest.ToString());
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();

    private static List<string> ManifestFiles()
    {
        var path = Path.Combine(Snapshot.RepoRoot, "flow-lang.Tests", "Characterization", "Snapshots", Manifest);
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path).Where(l => l.Length > 18).Select(l => l[18..]).ToList();
    }

    // Tracked files only, so local untracked scripts never enter the snapshot.
    private static List<string> TrackedFlowFiles()
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, WorkingDirectory = Snapshot.RepoRoot };
        psi.ArgumentList.Add("ls-files");
        psi.ArgumentList.Add("*.flow");
        using var git = Process.Start(psi)!;
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(f => !f.StartsWith("flow-site/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToList();
    }
}
