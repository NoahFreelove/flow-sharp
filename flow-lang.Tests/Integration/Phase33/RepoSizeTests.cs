using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FlowLang.Tests.Integration.Phase33;

/// <summary>
/// Phase 33 Plan 01 Task 3 — repo size cap for the SFZ smoke fixture
/// directory. <c>flow-lang.Tests/fixtures/sfz-smoke/</c> must be ≤ 100 KB
/// total (SPEC-7 + 33-SPEC.md "Constraints" — "fixtures &lt; 100 KB" + the
/// "Phase 33 in-repo artifacts &lt; 100 KB" acceptance criterion).
///
/// Cross-platform — uses .NET file enumeration + recursive search so it
/// runs identically on Linux / macOS / Windows. Mirrors the Phase 29
/// <c>RepoSizeTests</c> shape (<c>flow-lang.Tests/Integration/Phase29/RepoSizeTests.cs</c>)
/// down to the const-named cap, error-message format, and recursive
/// enumeration helper.
///
/// FindRepoRoot uses the same walk-up-from-AppContext.BaseDirectory pattern
/// as <c>flow-lang.Tests/Unit/Phase32/ScalaParserFacts.cs</c>, lifted here
/// so this fact has no cross-namespace dependency on the Tools helper.
/// </summary>
public class RepoSizeTests
{
    private const long ONE_HUNDRED_KB = 100L * 1024;

    [Fact]
    public void SfzSmokeFixturesDirectory_TotalBytes_Under100Kb()
    {
        string repoRoot = FindRepoRoot();
        string fixtureDir = Path.Combine(repoRoot, "flow-lang.Tests", "fixtures", "sfz-smoke");

        Assert.True(Directory.Exists(fixtureDir),
            $"flow-lang.Tests/fixtures/sfz-smoke/ must exist at {fixtureDir} " +
            "(regenerate via Phase33FixtureGenerator)");

        long totalBytes = Directory
            .EnumerateFiles(fixtureDir, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);

        Assert.True(totalBytes < ONE_HUNDRED_KB,
            $"sfz-smoke fixtures total {totalBytes} bytes — exceeds 100 KB cap per SPEC-7. " +
            $"Trim or compress (cap: {ONE_HUNDRED_KB} bytes).");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "flow-lang.Tests", "fixtures")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }
}
