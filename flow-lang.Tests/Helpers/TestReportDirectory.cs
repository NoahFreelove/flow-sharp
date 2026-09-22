namespace FlowLang.Tests.Helpers;

/// <summary>Separates generated test reports from explicitly updated historical records.</summary>
internal static class TestReportDirectory
{
    public static bool UpdateHistoricalReports =>
        Environment.GetEnvironmentVariable("FLOW_UPDATE_REPORTS") == "1";

    public static string Create(string repoRoot, string historicalRelativePath)
    {
        string directory;
        if (UpdateHistoricalReports)
        {
            directory = Path.Combine(repoRoot, historicalRelativePath);
        }
        else
        {
            var artifactRoot = Environment.GetEnvironmentVariable("FLOW_TEST_ARTIFACTS_DIR");
            if (string.IsNullOrWhiteSpace(artifactRoot))
                artifactRoot = Path.Combine(Path.GetTempPath(), "flow-test-reports");

            // Each invocation has its own directory, including concurrent test processes.
            directory = Path.Combine(Path.GetFullPath(artifactRoot),
                Guid.NewGuid().ToString("N"), Path.GetFileName(historicalRelativePath));
        }

        Directory.CreateDirectory(directory);
        Console.WriteLine($"[TestReports] output directory: {directory}");
        return directory;
    }
}
