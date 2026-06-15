using System;
using System.IO;
using FlowInterpreter;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0614;

/// <summary>
/// sweep-0614 (cli-repl-watch, MEDIUM): in plain-line mode (NO_COLOR / TERM=dumb
/// / --no-color / redirected stdout — common in tmux/CI/logged sessions) every
/// watch advisory routed through <see cref="RenderingDiagnostics.WarnOnce"/>,
/// which dedups by sentinel for the WHOLE process lifetime. The parse/render-
/// failure path used a PATH-ONLY dedup key (<c>live-parse:{filePath}</c>), so
/// after the FIRST parse error on a file every SUBSEQUENT distinct error on that
/// same file was silently dropped — the composer fixed error A, introduced error
/// B, and saw nothing.
///
/// Fix: salt the per-render dedup key with a per-save sequence counter
/// (<c>live-parse:{filePath}:{renderSeq}</c>) so distinct saves always reach
/// stderr. These tests drive the <see cref="LiveReloadManager"/> seam directly
/// (no 30s timeout / no real file watcher) to pin the dedup-key behavior.
/// </summary>
[Collection("FlowScripts")]
public class WatchAdvisoryDedupSweepTests : IDisposable
{
    public WatchAdvisoryDedupSweepTests() => RenderingDiagnostics.ResetForTesting();
    public void Dispose() => RenderingDiagnostics.ResetForTesting();

    /// <summary>
    /// Two DISTINCT parse errors emitted on successive saves (renderSeq 1 then 2)
    /// MUST both register a sentinel — i.e. both reach stderr. Before the fix the
    /// path-only key collapsed them to one and the second was swallowed.
    /// </summary>
    [Fact]
    public void DistinctParseErrorsOnSuccessiveSaves_BothEmit()
    {
        using var harness = new SeamLiveReloadManager();

        harness.EmitParseFailure("[live] error A — keeping previous version", renderSeq: 1);
        harness.EmitParseFailure("[live] error B — keeping previous version", renderSeq: 2);

        // The salted keys are distinct → both sentinels recorded → both surfaced.
        Assert.True(
            RenderingDiagnostics.WasWarnedForTesting($"live-parse:{harness.FilePath}:1"),
            "First parse-error save must register its dedup sentinel");
        Assert.True(
            RenderingDiagnostics.WasWarnedForTesting($"live-parse:{harness.FilePath}:2"),
            "Second DISTINCT parse-error save must ALSO register (was swallowed before fix)");
    }

    /// <summary>
    /// Regression guard: the OLD path-only key would have been
    /// <c>live-parse:{filePath}</c> with NO save suffix — confirm the fix no
    /// longer registers that bare key (so a revert to the path-only form is
    /// caught).
    /// </summary>
    [Fact]
    public void PathOnlyDedupKey_IsNoLongerUsed()
    {
        using var harness = new SeamLiveReloadManager();

        harness.EmitParseFailure("[live] error A — keeping previous version", renderSeq: 1);

        Assert.False(
            RenderingDiagnostics.WasWarnedForTesting($"live-parse:{harness.FilePath}"),
            "Bare path-only dedup key must not be used (it caused later errors to be swallowed)");
    }

    /// <summary>
    /// Test seam: subclasses <see cref="LiveReloadManager"/> and installs a
    /// plain-line panel so <see cref="PublishParseFailureAdvisory"/> routes
    /// through WarnOnce exactly as a real watch session would in
    /// NO_COLOR/redirected mode (the CI test process has redirected stdout, so
    /// the panel is in plain-line mode by construction).
    /// </summary>
    private sealed class SeamLiveReloadManager : LiveReloadManager
    {
        // The base constructor stores Path.GetFullPath(filePath) in _filePath,
        // which the dedup key reads. GetTempFileName() already returns an
        // absolute path so GetFullPath is idempotent — we mirror it here so the
        // test's expected key matches the emitted one.
        public string FilePath { get; }

        private SeamLiveReloadManager(string fullPath)
            : base(filePath: fullPath, deviceName: null)
        {
            FilePath = fullPath;
            InitPanelForTesting();
        }

        public SeamLiveReloadManager()
            : this(Path.GetFullPath(Path.GetTempFileName())) { }

        public void EmitParseFailure(string message, long renderSeq)
            => PublishParseFailureAdvisory(message, renderSeq);
    }
}
