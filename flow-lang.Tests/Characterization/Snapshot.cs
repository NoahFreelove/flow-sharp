using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// Golden-text snapshots that pin current behavior in areas the language/music
/// extraction touches. A snapshot lives in <c>Characterization/Snapshots/</c>. On a
/// mismatch the actual text is written to the test-report directory and the first
/// differing lines are reported. Regenerate deliberately with
/// <c>FLOW_UPDATE_SNAPSHOTS=1</c> and review the diff: a changed snapshot is a
/// behavior change.
/// </summary>
internal static class Snapshot
{
    public static readonly string RepoRoot = Path.GetDirectoryName(FlowScriptData.FindTestsRoot())!;

    private static string Directory => Path.Combine(RepoRoot, "flow-lang.Tests", "Characterization", "Snapshots");

    public static void Verify(string name, string actual)
    {
        actual = actual.Replace("\r\n", "\n").TrimEnd('\n') + "\n";
        var path = Path.Combine(Directory, name);
        if (Environment.GetEnvironmentVariable("FLOW_UPDATE_SNAPSHOTS") == "1")
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"Snapshot {name} is missing; create it with FLOW_UPDATE_SNAPSHOTS=1.");
        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected == actual) return;

        var reportDir = TestReportDirectory.Create(RepoRoot, Path.Combine("flow-lang.Tests", "Characterization", "actual"));
        var actualPath = Path.Combine(reportDir, name);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
        File.WriteAllText(actualPath, actual);

        var e = expected.Split('\n');
        var a = actual.Split('\n');
        var diffs = new List<string>();
        for (int i = 0; i < Math.Max(e.Length, a.Length) && diffs.Count < 12; i++)
        {
            var el = i < e.Length ? e[i] : "<end>";
            var al = i < a.Length ? a[i] : "<end>";
            if (el != al) diffs.Add($"line {i + 1}:\n  - {el}\n  + {al}");
        }
        Assert.Fail($"Snapshot {name} changed ({e.Length} → {a.Length} lines). Actual: {actualPath}\n" + string.Join("\n", diffs));
    }
}
