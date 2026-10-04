using System.Diagnostics;
namespace FlowLang.Tests.Helpers;

/// <summary>Bounded build/publish runner. Output draining and process exit share
/// one deadline; build servers must not retain redirected pipe handles.</summary>
internal static class DotnetBuildProcess
{
    public static (int exitCode, string stdout, string stderr) Run(string operation, string arguments,
        string repository, TimeSpan timeout) => RunAsync(operation, arguments, repository, timeout).GetAwaiter().GetResult();

    private static async Task<(int, string, string)> RunAsync(string operation, string arguments,
        string repository, TimeSpan timeout)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            Arguments = operation + " flow-lang/flow-lang.csproj " + arguments + " -v quiet --nologo -m:1 /nodeReuse:false",
            WorkingDirectory = repository, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        info.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Build process did not start");
        using var deadline = new CancellationTokenSource(timeout);
        var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(deadline.Token)).WaitAsync(deadline.Token).ConfigureAwait(false);
            return (process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); } catch (OperationCanceledException) { }
            return (-1, stdout.IsCompletedSuccessfully ? stdout.Result : "",
                (stderr.IsCompletedSuccessfully ? stderr.Result : "") + "\n[test] Build/publish deadline exceeded");
        }
    }
}
