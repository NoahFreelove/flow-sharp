using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlowLang.Core;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Characterization;

/// <summary>
/// Pins the observable behavior of the whole music corpus (every tracked script
/// under tests/ and examples/): normalized stdout and diagnostics, the error count,
/// and a hash of every audio buffer the script leaves in a global variable. Playback
/// is captured, never sent to a device. When the snapshot is recorded each script
/// runs twice; any field that differs between the runs (timings, wall-clock output)
/// is recorded as unstable and not compared. Microphone capture is pinned to silence
/// at 44.1 kHz so the host's input device never changes the observation.
/// </summary>
[Collection("FlowScripts")]
[Trait("Category", "LongRunning")]
public class CorpusCharacterizationTests
{
    private const string SnapshotName = "corpus/behavior.txt";

    private sealed record Observation(string Stdout, string Diagnostics, int Errors, string Buffers);

    [Fact]
    public void MusicCorpusBehavesTheSame()
    {
        bool update = Environment.GetEnvironmentVariable("FLOW_UPDATE_SNAPSHOTS") == "1";
        var files = update ? TrackedCorpus() : SnapshotFiles();
        Assert.NotEmpty(files);

        var sb = new StringBuilder();
        foreach (var file in files)
        {
            var first = Observe(file);
            if (update)
            {
                var second = Observe(file);
                sb.Append(Line(file, first, second));
            }
            else
            {
                sb.Append(Line(file, first, first, Recorded(file)));
            }
        }
        Snapshot.Verify(SnapshotName, sb.ToString());
    }

    // One snapshot line per script. Fields recorded as unstable stay "unstable".
    private static string Line(string file, Observation a, Observation b, string? recorded = null)
    {
        string Field(string name, string x, string y, int index)
        {
            if (recorded is not null && recorded.Split(' ')[index] == $"{name}=unstable") return $"{name}=unstable";
            return x == y ? $"{name}={Hash(x)}" : $"{name}=unstable";
        }
        return string.Join(' ',
            file,
            Field("out", a.Stdout, b.Stdout, 1),
            Field("diag", a.Diagnostics, b.Diagnostics, 2),
            Field("errors", a.Errors.ToString(), b.Errors.ToString(), 3),
            Field("buffers", a.Buffers, b.Buffers, 4)) + "\n";
    }

    private static Observation Observe(string file)
    {
        var stdout = new StringWriter();
        var diagnostics = new StringWriter();
        var origCwd = Environment.CurrentDirectory;
        // Scripts use repository-relative paths, as when run with `flow run` from the root.
        Environment.CurrentDirectory = Snapshot.RepoRoot;
        var origCapture = InputFunctions.CaptureOverride;
        var origRate = InputFunctions.NativeRateForTesting;
        InputFunctions.CaptureOverride = (rate, channels, seconds) => new float[(int)(rate * seconds) * channels];
        InputFunctions.NativeRateForTesting = 44_100;
        try
        {
            using var engine = new FlowEngine(new EngineOptions { Output = stdout, Diagnostics = diagnostics });
            engine.AudioManager.CaptureMode = true;
            var path = Path.Combine(Snapshot.RepoRoot, file);
            engine.Evaluate(File.ReadAllText(path), path, new EvaluationOptions { TimeLimit = TimeSpan.FromMinutes(2) });

            var buffers = new StringBuilder();
            foreach (var (name, value) in engine.Context.GlobalFrame.GetLocalVariables().OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (value.Data is not AudioBuffer buffer) continue;
                var bytes = new byte[buffer.Data.Length * sizeof(float)];
                Buffer.BlockCopy(buffer.Data, 0, bytes, 0, bytes.Length);
                buffers.Append(name).Append(':').Append(buffer.Channels).Append('x').Append(buffer.Frames)
                       .Append(':').Append(Convert.ToHexString(SHA256.HashData(bytes))[..12]).Append(';');
            }
            return new Observation(Normalize(stdout.ToString()), Normalize(diagnostics.ToString()),
                engine.ErrorReporter.ErrorCount, buffers.ToString());
        }
        finally
        {
            Environment.CurrentDirectory = origCwd;
            InputFunctions.CaptureOverride = origCapture;
            InputFunctions.NativeRateForTesting = origRate;
        }
    }

    private static string Normalize(string text) =>
        Regex.Replace(text.Replace(Snapshot.RepoRoot + Path.DirectorySeparatorChar, "").Replace('\\', '/'),
            @"/tmp/[^\s""']+", "<tmp>");

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();

    private static string? Recorded(string file) =>
        RecordedLines().TryGetValue(file, out var line) ? line : null;

    private static Dictionary<string, string>? _recorded;

    private static Dictionary<string, string> RecordedLines()
    {
        if (_recorded is not null) return _recorded;
        var path = Path.Combine(Snapshot.RepoRoot, "flow-lang.Tests", "Characterization", "Snapshots", SnapshotName);
        _recorded = File.Exists(path)
            ? File.ReadAllLines(path).Where(l => l.Length > 0).ToDictionary(l => l.Split(' ')[0], l => l, StringComparer.Ordinal)
            : new Dictionary<string, string>();
        return _recorded;
    }

    private static List<string> SnapshotFiles() => RecordedLines().Keys.Order(StringComparer.Ordinal).ToList();

    // Tracked scripts only, so local untracked files never enter the snapshot.
    private static List<string> TrackedCorpus()
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, WorkingDirectory = Snapshot.RepoRoot };
        foreach (var arg in new[] { "ls-files", "tests/*.flow", "tests/**/*.flow", "examples/**/*.flow", "examples/*.flow" })
            psi.ArgumentList.Add(arg);
        using var git = Process.Start(psi)!;
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Distinct().Order(StringComparer.Ordinal).ToList();
    }
}
