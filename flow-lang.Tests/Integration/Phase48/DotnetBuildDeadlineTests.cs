using FlowLang.Tests.Helpers;
using Xunit;
namespace FlowLang.Tests.Integration.Phase48;

[Trait("Category", "Platform")]
public class DotnetBuildDeadlineTests
{
    [Fact]
    public void ExpiredDeadlineStopsBuildAndDoesNotWaitForPipeEof()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "flow-sharp.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var result = DotnetBuildProcess.Run("build", "--no-restore", root.FullName, TimeSpan.Zero);
        Assert.Equal(-1, result.exitCode);
        Assert.Contains("deadline exceeded", result.stderr);
    }
}
