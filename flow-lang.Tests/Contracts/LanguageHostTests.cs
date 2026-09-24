using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FlowLang.Tests.Characterization;
using LanguageHost;
using Xunit;

namespace FlowLang.Tests.Contracts;

public class LanguageHostTests
{
    [Theory]
    [MemberData(nameof(LanguageContractTests.Examples), MemberType = typeof(LanguageContractTests))]
    public async Task ExampleRunsInLanguageOnlyProcess(string relativePath)
    {
        var source = Path.Combine(Snapshot.RepoRoot, relativePath);
        var (code, stdout, stderr, report) = await RunHostAsync([source]);
        Assert.True(code == 0, stderr);
        Assert.Empty(stderr);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(source, ".out")).Replace("\r\n", "\n"), stdout);
        AssertLanguageClosure(report);
    }

    [Fact]
    public void EmbeddingUsesInjectedSinksAndExplicitImports()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var session = new LanguageSession(stdout, stderr);
        Assert.False(session.Execute("(print 1)"));
        Assert.Contains("not found", stderr.ToString());
        stderr.GetStringBuilder().Clear();
        Assert.True(session.Execute("use \"@core\"\n(print (reduce (range 1 5) 0 (fn Int acc, Int x => (add acc x))))"));
        Assert.Equal("10\n", stdout.ToString().Replace("\r\n", "\n"));
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public async Task InteractiveCoreImportAndStatePersistAcrossLines()
    {
        var (code, stdout, stderr, report) = await RunHostAsync(["--repl"], "Int total = 7\n(print (add total 5))\n:quit\n");
        Assert.True(code == 0, stderr);
        Assert.Equal("12\n", stdout);
        Assert.Empty(stderr);
        AssertLanguageClosure(report);
    }

    [Theory]
    [InlineData("(print 1)", "not found")]
    [InlineData("Sequence notes = | C4 D4 |", "not available")]
    [InlineData("use \"@std\"", "not found")]
    public async Task ScriptsDoNotAcquirePreludeOrMusic(string source, string diagnostic)
    {
        var (code, _, stderr, report) = await RunHostAsync([], source);
        Assert.Equal(1, code);
        Assert.Contains(diagnostic, stderr, StringComparison.OrdinalIgnoreCase);
        AssertLanguageClosure(report);
    }

    private static void AssertLanguageClosure(JsonElement report)
    {
        var loaded = report.GetProperty("loadedAssemblies").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Contains("flow-language", loaded);
        Assert.All(loaded, name => Assert.True(name is "LanguageHost" or "flow-language"
            || name.StartsWith("System.", StringComparison.Ordinal) || name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal), name));
        Assert.All(report.GetProperty("languageReferences").EnumerateArray(), name => Assert.StartsWith("System.", name.GetString()));
        var native = string.Join("\n", report.GetProperty("nativeLibraries").EnumerateArray().Select(x => x.GetString()));
        foreach (var forbidden in new[] { "pulse", "asound", "jack", "rtmidi", "portaudio", "sndfile", "AudioToolbox" })
            Assert.DoesNotContain(forbidden, native, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int Code, string Stdout, string Stderr, JsonElement Report)> RunHostAsync(string[] args, string? stdin = null)
    {
        var configuration = typeof(LanguageHostTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var hostDir = Path.Combine(Snapshot.RepoRoot, "scripts", "LanguageHost", "bin", configuration, "net10.0");
        Assert.Equal(new[] { "flow-language.dll", "LanguageHost.dll" },
            Directory.GetFiles(hostDir, "*.dll").Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(new[] { "collections.flow", "core.flow" },
            Directory.GetFiles(hostDir, "*.flow", SearchOption.AllDirectories).Select(Path.GetFileName).Order());
        var temp = Path.Combine(Path.GetTempPath(), "flow-language-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var json = Path.Combine(temp, "closure.json");
            var psi = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = temp,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(Path.Combine(hostDir, "LanguageHost.dll"));
            psi.ArgumentList.Add("--json");
            psi.ArgumentList.Add(json);
            foreach (var arg in args) psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (stdin is not null) await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(json));
            return (process.ExitCode, (await stdout).Replace("\r\n", "\n"), await stderr, doc.RootElement.Clone());
        }
        finally { Directory.Delete(temp, recursive: true); }
    }
}
