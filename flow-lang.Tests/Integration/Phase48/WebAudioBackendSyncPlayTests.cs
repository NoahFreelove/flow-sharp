using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-06 — cycle-7 regression gate (debug session
/// wasm-boot-no-app-bundle). Pins that <see cref="FlowLang.Audio.WebAudioBackend"/>'s
/// <c>Play</c> calls the <c>PlayStereoFloat32</c> [JSImport] marshal SYNCHRONOUSLY
/// on the calling thread, NOT wrapped in the <c>Task.Run(...)</c> +
/// <c>workerTask.Wait(PlayTimeout)</c> shape that DEADLOCKED the single browser
/// main thread (Task.Run queues the marshal to that same thread; Wait then
/// blocks it → the marshal never runs → 30s freeze → bogus "exceeded 30s cap"
/// advisory + no AudioBufferSourceNode).
///
/// <para>This is a SOURCE-LEVEL guard (reads <c>WebAudioBackend.cs</c> via
/// <c>File.ReadAllText</c>), mirroring the established
/// <see cref="CultureInvariantSweepTests"/> source-grep gate. The actual
/// [JSImport]-backed <c>Play</c> path is browser-only — it cannot be exercised
/// on the Desktop test runner (gated by <c>OperatingSystem.IsBrowser()</c>) nor
/// in the in-process Web test runner (no real <c>AudioContext</c>). The DECISIVE
/// confirmation is the human browser re-smoke (audible 440 Hz tone, no 30s
/// freeze, no cap error). This Fact catches a regression to the deadlocking
/// shape WITHOUT a browser.</para>
///
/// <para>Plain <c>[Fact]</c> (NOT FlowTargetFact-gated) — it reads source files
/// directly regardless of build target, so it runs from the Desktop runner.</para>
/// </summary>
public class WebAudioBackendSyncPlayTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "flow-lang", "flow-lang.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static string ReadWebAudioBackendSource()
    {
        var path = Path.Combine(
            FindRepoRoot(), "flow-lang", "Audio", "WebAudioBackend.cs");
        Assert.True(File.Exists(path), $"WebAudioBackend.cs not found at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Strips C# comments (line + block) so the assertions below only inspect
    /// EXECUTABLE source — the cycle-7 doc comments intentionally describe the
    /// removed <c>Task.Run + Wait</c> pattern, and must not trip the guard.
    /// </summary>
    private static string StripComments(string source)
    {
        // Remove /* ... */ block comments (incl. XML-doc-on-block) then //... lines.
        var noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var noLine = Regex.Replace(noBlock, @"//.*?$", string.Empty, RegexOptions.Multiline);
        return noLine;
    }

    [Fact]
    public void Play_DoesNotWrapMarshalInTaskRun_NoDeadlock()
    {
        var code = StripComments(ReadWebAudioBackendSource());

        Assert.DoesNotContain("Task.Run", code);
        Assert.DoesNotContain("workerTask", code);
        Assert.DoesNotContain(".Wait(", code);
    }

    [Fact]
    public void PlayTimeout_ConstAndCapThrow_AreRemoved()
    {
        var code = StripComments(ReadWebAudioBackendSource());

        // The 30s-cap field, the WarnOnce sentinel, and the OperationCanceled
        // throw were all removed when Play went synchronous (cycle 7).
        Assert.DoesNotContain("PlayTimeout", code);
        Assert.DoesNotContain("wasm-30s-cap", code);
        Assert.DoesNotContain("OperationCanceledException", code);
    }

    [Fact]
    public void Play_CallsPlayStereoFloat32Synchronously_AssignsActiveSource()
    {
        var code = StripComments(ReadWebAudioBackendSource());

        // The marshal is still invoked (synchronously now) and its handle is
        // still captured for Stop()/Dispose().
        Assert.Contains("FlowRuntimeInterop.PlayStereoFloat32", code);
        Assert.Contains("_activeSource = FlowRuntimeInterop.PlayStereoFloat32", code);

        // The D-48-07 stereo promotion + zero-copy reinterpret are preserved.
        Assert.Contains("PromoteToStereo", code);
        Assert.Contains("MemoryMarshal.AsBytes", code);

        // The browser gate + Desktop charitable no-op are preserved.
        Assert.Contains("OperatingSystem.IsBrowser()", code);
    }
}
