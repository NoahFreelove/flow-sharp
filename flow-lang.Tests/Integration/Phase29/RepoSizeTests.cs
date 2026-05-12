using System.IO;
using System.Linq;
using FlowLang.Tests;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// Phase 29 Gate B — repo size cap. flow-lang/Samples/ must be ≤ 5 MB total
/// (SPEC constraint: "Repo size cap: 5 MB"). Cross-platform: uses .NET file
/// enumeration so it runs identically on Linux/macOS/Windows.
/// </summary>
public class RepoSizeTests
{
    private const long FIVE_MB = 5L * 1024 * 1024;

    [Fact]
    public void SamplesDirectory_DoesNotExceed5MB()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string samplesRoot = Path.Combine(repoRoot, "flow-lang", "Samples");

        Assert.True(Directory.Exists(samplesRoot),
            $"flow-lang/Samples/ must exist at {samplesRoot}");

        long totalBytes = Directory.EnumerateFiles(samplesRoot, "*.*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);

        Assert.True(totalBytes <= FIVE_MB,
            $"flow-lang/Samples/ is {totalBytes / 1024.0 / 1024.0:F2} MB; " +
            $"must be ≤ 5 MB (got {totalBytes} bytes vs cap {FIVE_MB} bytes)");
    }
}
