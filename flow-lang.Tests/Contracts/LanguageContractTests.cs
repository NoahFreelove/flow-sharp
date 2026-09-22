using System.Text.RegularExpressions;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Contracts;

/// <summary>
/// Executable language contract (restructuring roadmap section 2).
///
/// Every <c>contracts/language/**/*.flow</c> file whose header contains
/// <c>// contract: ID</c> is run in a fresh engine. Its stdout must equal the
/// sibling <c>.out</c> file, its error count must equal <c>// errors: N</c>
/// (default 0) and every <c>// stderr: TEXT</c> line must appear in the
/// normalized stderr. Files without a <c>contract:</c> header are helper
/// modules imported by contracts. The same runner checks the non-musical
/// tutorial programs under <c>examples/language/</c>, which carry no header and
/// must run without errors.
/// </summary>
[Collection("FlowScripts")]
public class LanguageContractTests
{
    public static readonly string[] Statuses = ["preserve", "generous", "disputed", "defect", "gap"];

    public static IEnumerable<object[]> Contracts() =>
        ContractFiles().Select(p => new object[] { Relative(p) });

    public static IEnumerable<object[]> Examples() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot, "examples", "language"), "*.flow", SearchOption.AllDirectories)
            .Where(p => File.Exists(Path.ChangeExtension(p, ".out")))
            .Order(StringComparer.Ordinal)
            .Select(p => new object[] { Relative(p) });

    [Theory]
    [MemberData(nameof(Contracts))]
    public void ContractHolds(string relativePath)
    {
        var header = ContractHeader.Parse(Path.Combine(RepoRoot, relativePath));
        var (stdout, stderr, errors) = Run(relativePath);

        Assert.True(errors == header.Errors,
            $"{header.Id}: expected {header.Errors} error(s), got {errors}.\nstderr:\n{stderr}");
        Assert.Equal(Expected(relativePath), Normalize(stdout));
        foreach (var fragment in header.StderrFragments)
            Assert.True(stderr.Contains(fragment, StringComparison.Ordinal),
                $"{header.Id}: stderr is missing \"{fragment}\".\nstderr:\n{stderr}");
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void ExampleRuns(string relativePath)
    {
        var (stdout, stderr, errors) = Run(relativePath);
        Assert.True(errors == 0, $"{relativePath} reported {errors} error(s):\n{stderr}");
        Assert.Equal(Expected(relativePath), Normalize(stdout));
    }

    /// <summary>Every contract has a known status, a rationale, a unique id and an index entry.</summary>
    [Fact]
    public void ContractsAreDocumentedAndIndexed()
    {
        var index = File.ReadAllText(Path.Combine(RepoRoot, "contracts", "language", "README.md"));
        var headers = ContractFiles().Select(ContractHeader.Parse).ToList();

        Assert.NotEmpty(headers);
        foreach (var h in headers)
        {
            Assert.Contains(h.Status, Statuses);
            Assert.False(string.IsNullOrWhiteSpace(h.Rationale), $"{h.Id} has no rationale");
            Assert.True(File.Exists(Path.ChangeExtension(h.Path, ".out")), $"{h.Id} has no .out file");
            Assert.True(index.Contains($"`{h.Id}`", StringComparison.Ordinal), $"{h.Id} is missing from contracts/language/README.md");
            Assert.StartsWith(Path.GetFileName(Path.GetDirectoryName(h.Path)!) + ".", h.Id);
        }
        var duplicates = headers.GroupBy(h => h.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    private static (string Stdout, string Stderr, int Errors) Run(string relativePath)
    {
        RenderingDiagnostics.ResetForTesting();
        var absolute = Path.Combine(RepoRoot, relativePath);
        using var runner = new FlowEngineRunner();
        var (_, stdout, stderr, _) = runner.RunFile(absolute);

        // Legacy FlowErrors and rich FlowDiagnostics (unknown identifiers, match
        // exhaustiveness) accumulate separately; the CLI prints both, so count both.
        var engine = runner.GetEngine();
        var reporter = engine.ErrorReporter;
        var errors = reporter.Errors.Count(e => e.Level == DiagnosticLevel.Error)
            + reporter.Diagnostics.Count(d => d.Level == DiagnosticLevel.Error);
        if (reporter.HasDiagnostics)
            stderr += reporter.FormatDiagnostics(engine.SourceMap, useColor: false);
        return (stdout, NormalizeStderr(stderr), errors);
    }

    private static string Expected(string relativePath) =>
        Normalize(File.ReadAllText(Path.Combine(RepoRoot, Path.ChangeExtension(relativePath, ".out"))));

    private static IEnumerable<string> ContractFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot, "contracts", "language"), "*.flow", SearchOption.AllDirectories)
            .Where(ContractHeader.IsContract)
            .Order(StringComparer.Ordinal);

    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\n');

    // Diagnostics name files by absolute path and may carry ANSI styling; contracts
    // compare repository-relative, unstyled text.
    private static string NormalizeStderr(string text) =>
        Regex.Replace(text, @"\x1b\[[0-9;]*m", "")
            .Replace(RepoRoot + Path.DirectorySeparatorChar, "")
            .Replace('\\', '/')
            .Replace("\r\n", "\n");

    private static string Relative(string path) => Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');

    private static readonly string RepoRoot = Path.GetDirectoryName(FlowScriptData.FindTestsRoot())!;

    private sealed record ContractHeader(
        string Path, string Id, string Status, string Rationale, int Errors, IReadOnlyList<string> StderrFragments)
    {
        public static bool IsContract(string path) =>
            File.ReadLines(path).TakeWhile(l => l.StartsWith("//")).Any(l => l.StartsWith("// contract:"));

        public static ContractHeader Parse(string path)
        {
            string id = "", status = "", rationale = "";
            int errors = 0;
            var stderr = new List<string>();
            string? last = null;
            foreach (var line in File.ReadLines(path).TakeWhile(l => l.StartsWith("//")))
            {
                var body = line[2..];
                var m = Regex.Match(body, @"^ ([a-z]+): ?(.*)$");
                if (m.Success)
                {
                    last = m.Groups[1].Value;
                    var value = m.Groups[2].Value.Trim();
                    switch (last)
                    {
                        case "contract": id = value; break;
                        case "status": status = value; break;
                        case "rationale": rationale = value; break;
                        case "errors": errors = int.Parse(value); break;
                        case "stderr": stderr.Add(value); break;
                    }
                }
                else if (last == "rationale" && body.StartsWith("   "))
                {
                    rationale += " " + body.Trim();
                }
            }
            if (id.Length == 0) throw new InvalidOperationException($"{path} has no contract id");
            return new ContractHeader(path, id, status, rationale, errors, stderr);
        }
    }
}
