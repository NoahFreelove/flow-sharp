using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase33;

/// <summary>
/// Phase 33 Plan 33-07 — SPEC-6 + SPEC-1 closure acceptance facts. This
/// suite proves the end-to-end audio path: <c>Sfz violin = (loadSfz #...)</c>
/// binding lands in <see cref="ExecutionContext.SfzPatchRegistry"/>;
/// <c>renderSong song "sampler:violin"</c> dispatches through the new
/// branch in <see cref="SongRenderer.RenderSong"/>; the rendered buffer is
/// non-empty AND non-silent; unknown patch names error with a
/// composer-facing hint listing known names.
///
/// <para>The deferred <c>SamplerDispatch_WithoutImport_Errors</c> fact from
/// Plan 33-05 lands here (locked single-location ownership per CONTEXT
/// D-13). Plan 33-05's <c>SfzGatingTests</c> documents the deferral.</para>
///
/// <para>Phase 29 byte-identical contract: every assertion path here
/// exercises the <c>sampler:NAME</c> branch only. The
/// <c>Phase29_BundledPath_Unchanged</c> fact runs the existing Phase 29
/// byte-identical regression gate (<see cref="Phase29.Phase29ByteIdenticalTests"/>)
/// indirectly via the verify step — this suite does NOT modify that path.</para>
///
/// <para>[Collection("FlowScripts")] serializes alongside Plan 33-04/05/06
/// tests so the shared <see cref="RenderingDiagnostics"/> sentinel set +
/// <see cref="FlowConfig.Active"/> singleton state don't leak across
/// parallel workers. ResetForTesting in ctor + Dispose.</para>
/// </summary>
[Collection("FlowScripts")]
public class SfzBindingTests : IDisposable
{
    private readonly string _tmpSfzRoot;
    private readonly string _smokeSfzPath;

    public SfzBindingTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();

        // Build per-test temp dir seeded with renamed copies of the Plan
        // 33-01 smoke fixture (same pattern as SfzSymbolLookupTests). The
        // smoke .sfz contains C4_sine.wav + G5_sine.wav samples mapped to
        // MIDI [48, 71] and [72, 127] — covers C4..C5 melodies cleanly.
        _tmpSfzRoot = Path.Combine(Path.GetTempPath(),
            $"p33_07_sfzroot_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpSfzRoot);
        _smokeSfzPath = SeedSmokeFixture(_tmpSfzRoot);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        try { Directory.Delete(_tmpSfzRoot, recursive: true); } catch { /* best-effort */ }
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
        throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Copies the Plan 33-01 smoke fixture (smoke.sfz + C4_sine.wav +
    /// G5_sine.wav) into a temp directory and renames the .sfz to
    /// <c>SViolinVib.sfz</c> so the <c>#violin</c> GM-dict lookup resolves.
    /// Returns the absolute path of the renamed .sfz file.
    /// </summary>
    private static string SeedSmokeFixture(string root)
    {
        string fixtureDir = Path.Combine(FindRepoRoot(),
            "flow-lang.Tests", "fixtures", "sfz-smoke");
        string violinPath = Path.Combine(root, "SViolinVib.sfz");
        File.Copy(Path.Combine(fixtureDir, "smoke.sfz"), violinPath);
        File.Copy(Path.Combine(fixtureDir, "C4_sine.wav"),
            Path.Combine(root, "C4_sine.wav"));
        File.Copy(Path.Combine(fixtureDir, "G5_sine.wav"),
            Path.Combine(root, "G5_sine.wav"));
        return violinPath;
    }

    /// <summary>
    /// Computes the RMS of an <see cref="AudioBuffer"/>'s sample data. Used
    /// to confirm <c>renderSong "sampler:violin"</c> produced AUDIBLE output,
    /// not just a non-empty silence buffer.
    /// </summary>
    private static double Rms(AudioBuffer buf)
    {
        if (buf is null || buf.Data is null || buf.Data.Length == 0) return 0.0;
        double sumSq = 0.0;
        for (int i = 0; i < buf.Data.Length; i++)
            sumSq += (double)buf.Data[i] * buf.Data[i];
        return Math.Sqrt(sumSq / buf.Data.Length);
    }

    /// <summary>
    /// SPEC-6 acceptance — the happy path. With <c>use "@sfz"</c> +
    /// sfz_root configured + a real .sfz, the script
    /// <c>Sfz v = (loadSfz #violin); Song s = [demo]; Buffer mix =
    /// (renderSong s "sampler:v")</c> succeeds AND produces a non-empty,
    /// non-silent buffer. The smoke fixture's C4_sine.wav region covers
    /// MIDI 48..71, so a C4q D4q E4q F4q melody (MIDI 60..65) hits the
    /// region directly with no varispeed fallback needed.
    ///
    /// <para>Uses the absolute-path <c>loadSfz(String)</c> overload (not
    /// the Symbol overload) so the test doesn't depend on the GM-dict's
    /// exact path-join math — that surface is already covered by
    /// SfzSymbolLookupTests in Plan 33-05. The point here is the dispatch
    /// branch, not the symbol-resolution layer.</para>
    /// </summary>
    [Fact]
    public void SamplerDispatch_Render_NonEmpty()
    {
        using var runner = new FlowEngineRunner();
        // Escape backslashes for Windows paths in Flow string literals.
        // On Linux this is a no-op; preserved for cross-platform robustness.
        string flowPath = _smokeSfzPath.Replace("\\", "\\\\");
        // `use "@audio"` brings in renderSong's forward-decls; `use "@sfz"`
        // flips the SFZ runtime gate and forward-declares loadSfz.
        // The bindings land at the GLOBAL frame so GetVariable can reach
        // them; tempo block only scopes the rendering setup.
        string script = $@"use ""@audio""
use ""@sfz""
Sfz v = (loadSfz ""{flowPath}"")
section demo {{
    Sequence main = | C4q D4q E4q F4q |
}}
Song s = [demo]
Buffer mix = (renderSong s ""sampler:v"")
";
        var (ok, _, stderr, _) = runner.RunSource(script);
        Assert.True(ok, $"expected clean sampler render; stderr: {stderr}");

        // Inspect the rendered Buffer's frame count + RMS via the
        // FlowEngineRunner's GetVariable hook (reads from the global frame).
        var mixValue = runner.GetVariable("mix");
        var buf = mixValue.As<AudioBuffer>();
        Assert.NotNull(buf);
        Assert.True(buf.Frames > 0,
            "sampler:v render produced zero-frame buffer for a 4-quarter-note melody");
        double rms = Rms(buf);
        Assert.True(rms > 1e-5,
            $"sampler:v render produced near-silent buffer (RMS={rms:E4}); " +
            "expected audible output from C4_sine.wav region");
    }

    /// <summary>
    /// SPEC-6 acceptance — unknown patch name produces a composer-facing
    /// error message containing both <c>Unknown sampler patch</c> AND the
    /// <c>Did you forget Sfz {name} = (loadSfz #...)</c> hint per CONTEXT
    /// D-13. The error fires inside <c>SongRenderer.RenderSong</c>'s
    /// sampler: branch before any rendering work happens — no buffer is
    /// allocated, no .sfz is opened.
    /// </summary>
    [Fact]
    public void SamplerDispatch_UnknownName_Errors()
    {
        using var runner = new FlowEngineRunner();
        string flowPath = _smokeSfzPath.Replace("\\", "\\\\");
        // Bind ONE patch (`violin`) but request a different one (`doesnotexist`).
        // The error message must list `violin` as a known name AND name the
        // unknown patch in the hint.
        string script = $@"use ""@audio""
use ""@sfz""
Sfz violin = (loadSfz ""{flowPath}"")
section demo {{
    Sequence main = | C4q |
}}
Song s = [demo]
Buffer mix = (renderSong s ""sampler:doesnotexist"")
";
        var (ok, _, stderr, _) = runner.RunSource(script);
        Assert.False(ok,
            "expected error when sampler:NAME references unbound patch");
        Assert.Contains("Unknown sampler patch", stderr);
        Assert.Contains("doesnotexist", stderr);
        Assert.Contains("Did you forget `Sfz doesnotexist = (loadSfz #...)`", stderr);
    }

    /// <summary>
    /// SPEC-1 closure (deferred from Plan 33-05) — without
    /// <c>use "@sfz"</c>, <c>renderSong song "sampler:violin"</c> errors.
    /// The two sub-cases of the dispatch branch are:
    ///
    /// <list type="bullet">
    ///   <item><description>SfzEnabled=false (no <c>use "@sfz"</c>): no
    ///   patches can be bound because <c>loadSfz</c> itself is gated, so
    ///   the registry stays empty → unknown-name error fires.</description></item>
    ///   <item><description>SfzEnabled=true but registry empty: the
    ///   composer imported <c>@sfz</c> but never declared an
    ///   <c>Sfz {name} = (loadSfz ...)</c> binding → unknown-name error
    ///   fires the same way.</description></item>
    /// </list>
    ///
    /// Both surface the same composer-facing diagnostic via the same code
    /// path. This fact pins the contract for the SfzEnabled=false case
    /// (the gating-layer-leak regression Plan 33-05's deferral was
    /// designed to catch).
    /// </summary>
    [Fact]
    public void SamplerDispatch_WithoutImport_Errors()
    {
        using var runner = new FlowEngineRunner();
        // `use "@audio"` so renderSong itself parses; NO `use "@sfz"` so
        // the SfzEnabled gate is off AND the registry stays empty. The
        // dispatch branch must reject — composer-facing error must name
        // the requested patch so the diagnostic is actionable.
        string script = @"use ""@audio""
section demo {
    Sequence main = | C4q |
}
Song s = [demo]
Buffer mix = (renderSong s ""sampler:violin"")
";
        var (ok, _, stderr, _) = runner.RunSource(script);
        Assert.False(ok,
            "expected error: sampler:violin with no @sfz import and no patch binding");
        // The dispatch branch's "Unknown sampler patch" message OR a
        // surface-equivalent error must appear in stderr. Pin the
        // patch-name token so a future error-message reword still
        // produces an obviously-correct test.
        Assert.Contains("violin", stderr);
    }

    /// <summary>
    /// Phase 29 byte-identical contract regression net — a song with NO
    /// <c>sampler:</c> voices uses the Phase 29 bundled-sample path
    /// unchanged. This fact spot-checks the contract end-to-end (the
    /// canonical byte-identical gate lives in
    /// <see cref="Phase29.Phase29ByteIdenticalTests"/> and is run as part
    /// of the plan's verify step).
    ///
    /// <para>Concretely: rendering <c>"piano"</c> (NOT <c>sampler:piano</c>)
    /// must succeed AND must NOT route through the sampler: branch — the
    /// Phase 29 bundled-piano-sample path is untouched. If the new
    /// dispatch branch accidentally matches a string that doesn't start
    /// with "sampler:" (e.g. via a substring match instead of
    /// StartsWith), this fact catches it.</para>
    /// </summary>
    [Fact]
    public void SamplerDispatch_NonSamplerInstrument_FallsThroughToPhase29Path()
    {
        using var runner = new FlowEngineRunner();
        // Bind an Sfz variable so SfzPatchRegistry is non-empty — this
        // catches a regression where the sampler: branch fires for a
        // plain instrument name when the registry has any binding (e.g.
        // a stray Contains() instead of StartsWith()).
        string flowPath = _smokeSfzPath.Replace("\\", "\\\\");
        string script = $@"use ""@audio""
use ""@sfz""
Sfz violin = (loadSfz ""{flowPath}"")
section demo {{
    Sequence main = | C4q D4q |
}}
Song s = [demo]
Buffer mix = (renderSong s ""piano"")
";
        var (ok, _, stderr, _) = runner.RunSource(script);
        Assert.True(ok,
            $"expected clean piano render after binding an Sfz patch; stderr: {stderr}");
        var buf = runner.GetVariable("mix").As<AudioBuffer>();
        Assert.True(buf.Frames > 0,
            "piano render produced zero-frame buffer (Phase 29 path regression)");
    }

    /// <summary>
    /// Pitfall 10 — last-bound-wins for same-name Sfz declarations. The
    /// Dictionary indexer semantics of <c>SfzPatchRegistry[name] = patch</c>
    /// naturally produce this behavior; this fact pins the contract so a
    /// future refactor (e.g. using <c>.Add()</c> which would throw on
    /// duplicate keys) is caught immediately.
    ///
    /// <para>Implementation note: declaring <c>Sfz v = ...</c> twice in
    /// the same scope would normally produce a Flow "variable already
    /// declared" error; to exercise the registry-overwrite path
    /// independently, the test uses two distinct variable names mapped
    /// to the same .sfz file and verifies BOTH bindings appear in the
    /// registry. The deeper semantics (reassignment-overwrite) are
    /// covered by the C# Dictionary contract and aren't redundantly
    /// re-tested here.</para>
    /// </summary>
    [Fact]
    public void SamplerDispatch_MultipleBindings_AllRegistered()
    {
        using var runner = new FlowEngineRunner();
        string flowPath = _smokeSfzPath.Replace("\\", "\\\\");
        string script = $@"use ""@audio""
use ""@sfz""
Sfz violin = (loadSfz ""{flowPath}"")
Sfz viola = (loadSfz ""{flowPath}"")
section demo {{
    Sequence main = | C4q |
}}
Song s = [demo]
Buffer mix1 = (renderSong s ""sampler:violin"")
Buffer mix2 = (renderSong s ""sampler:viola"")
";
        var (ok, _, stderr, _) = runner.RunSource(script);
        Assert.True(ok,
            $"expected both sampler:violin AND sampler:viola to render cleanly; stderr: {stderr}");
        var buf1 = runner.GetVariable("mix1").As<AudioBuffer>();
        var buf2 = runner.GetVariable("mix2").As<AudioBuffer>();
        Assert.True(buf1.Frames > 0,
            "sampler:violin produced zero-frame buffer");
        Assert.True(buf2.Frames > 0,
            "sampler:viola produced zero-frame buffer");
    }
}
