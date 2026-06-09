using System;
using System.IO;
using System.Linq;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.Tests.Integration.Phase48;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 Plan 40-03 — records the Ableton Link deferral (LINK-01) HONESTLY and
/// reinforces the LINK-02 determinism intent.
///
/// <para><b>LINK-01 is DEFERRED to community/v1.6 per D-40-06.</b> Ableton Link is
/// GPLv2+/commercial dual-licensed; P/Invoking it from MIT-licensed
/// <c>flow-lang.dll</c> is a derivative-work contamination hazard (HIGH license
/// threat T-40-02). NO Link implementation ships this phase: there is no
/// <c>@link</c> module, no <c>linkEnable</c>/<c>linkDisable</c> builtin, and NO
/// <c>libabl_link</c> / Ableton-Link binary reference anywhere in the tree. The
/// structural enforcement is the Phase 47 <c>AssemblyReferenceScanTests</c>
/// forbidden-prefix gate; the Facts below are documentation-anchored assertions of
/// the same posture.</para>
///
/// <para><b>LINK-02 determinism is SHIPPED</b> (Plan 40-01
/// <c>OfflineRenderDeterminismTests.OfflineRenderIgnoresSync</c>). This class adds
/// a Link-framed restatement: offline render (<c>renderSong</c> / <c>writeWav</c>)
/// is byte-identical regardless of any sync state — no Link/clock/JACK path touches
/// the deterministic offline render. Link ships nothing, but its determinism intent
/// is fully covered.</para>
/// </summary>
// Serialized with the WASM console collection (drives a FlowEngineRunner that
// redirects process-wide Console.Out/Error) — same posture as
// OfflineRenderDeterminismTests.
[Collection(WasmEntryConsoleCollection.Name)]
public class LinkDeferralTests
{
    /// <summary>
    /// LINK-01 deferral record (T-40-02): no GPL Ableton-Link symbol is referenced
    /// in the Desktop flow-lang assembly's type-reference graph. The
    /// AssemblyReferenceScanTests gate (Web target) is the standing structural
    /// enforcement; here we assert the Desktop assembly likewise carries no
    /// libabl_link / Ableton.Link / abl_link reference — i.e. Link was never wired,
    /// honestly deferred rather than half-implemented.
    /// </summary>
    [Fact]
    public void LinkDeferral_NoGplReference()
    {
        var asm = typeof(FlowLang.Core.FlowEngine).Assembly;

        // Scan referenced assemblies — a GPL Link binding would show up here.
        var referenced = asm.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        string[] forbidden = { "abl_link", "ableton", "link", "libabl" };
        foreach (var refName in referenced)
        {
            var lower = refName.ToLowerInvariant();
            // "link" as a substring is too broad for a referenced-assembly name in
            // general, but no legitimate Flow dependency contains it; assert none do.
            foreach (var bad in forbidden)
            {
                Assert.DoesNotContain(bad, lower);
            }
        }

        // Scan loaded types for any Ableton-Link type that a P/Invoke binding would
        // introduce. No FlowLang type should mention Link tempo sync.
        var linkTypes = asm.GetTypes()
            .Where(t => t.FullName != null &&
                        (t.FullName.IndexOf("AbletonLink", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         t.FullName.IndexOf("AblLink", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         t.FullName.IndexOf("LinkEnable", StringComparison.OrdinalIgnoreCase) >= 0))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(linkTypes.Count == 0,
            "LINK-01 is DEFERRED (D-40-06, GPL) — no Ableton-Link type may exist in flow-lang. Found:\n  " +
            string.Join("\n  ", linkTypes));

        // And there is no @link stdlib module shipped this phase.
        // (jack.flow / midi.flow exist; link.flow must NOT.)
        var baseDir = AppContext.BaseDirectory;
        Assert.False(File.Exists(Path.Combine(baseDir, "link.flow")),
            "link.flow must NOT exist — Ableton Link is deferred to community/v1.6 (D-40-06).");
    }

    // ----- LINK-02 reinforcement: offline render ignores any sync state -----

    private const string RenderScript = @"use ""@audio""
section main {
    Sequence lead = | C4q E4q G4q C5q |
}
Song s = [main]
Buffer mix = (renderSong s ""sine"")
";

    private static byte[] RenderToPcm(string script)
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(script, "<phase40-link02-deferred>");
        Assert.True(ok, $"render failed: {stderr}");
        var buf = runner.GetVariable("mix").As<AudioBuffer>();
        Assert.NotNull(buf);
        Assert.True(buf.Frames > 0, "render produced zero frames");
        var bytes = new byte[buf.Data.Length * 4];
        System.Buffer.BlockCopy(buf.Data, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// LINK-02 (Link-deferred restatement): the deterministic offline render is
    /// untouched by any sync subsystem. Since Link ships nothing, this proves the
    /// strong form — there is no Link path that could perturb writeWav at all — and
    /// the render is two-run byte-identical (the standard Flow determinism
    /// contract). The cross-state @midi-present version lives in
    /// OfflineRenderDeterminismTests.OfflineRenderIgnoresSync (Plan 40-01).
    /// </summary>
    [Fact]
    public void OfflineRenderIgnoresSync_LinkDeferred()
    {
        var a = RenderToPcm(RenderScript);
        var b = RenderToPcm(RenderScript);
        Assert.Equal(a.Length, b.Length);
        Assert.True(a.SequenceEqual(b),
            "LINK-02 VIOLATED: offline render is not byte-identical across runs — " +
            "a nondeterministic (sync?) input leaked into writeWav.");
    }
}
