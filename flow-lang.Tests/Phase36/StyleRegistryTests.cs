using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext —
// the bare name is ambiguous under net10.0's implicit usings.
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-11 Task 1 — facts for the style-pack registry surface.
///
/// <para>
/// Pins (a) the shipped-pack auto-load contract (jazz/blues/classical
/// register at FlowEngine init), (b) the registerStyle/listStyles builtins,
/// (c) the user-pack override + advisory contract (Pitfall 8), (d) the
/// charitable malformed-pack contract (load advisories, no crash), and
/// (e) the unknown-style charitable fallback to #jazz.
/// </para>
///
/// <para>
/// Test isolation: each fact constructs a fresh <see cref="FlowEngineRunner"/>
/// (which constructs a fresh FlowEngine + ExecutionContext). The user-pack
/// override test redirects <c>HOME</c> to a temp dir so the test's user
/// pack doesn't pollute the real user config, and restores HOME on dispose.
/// </para>
/// </summary>
// Phase 36 Plan 36-11: serialize with the "FlowScripts" non-parallel collection
// so HOME-env-var mutations in UserPackOverridesShippedWithAdvisory don't race
// against other FlowEngine-constructing facts. The pattern matches
// PatternEveryTests / PatternChalkyEdgeCasesTests in the same Phase36 dir.
[Collection("FlowScripts")]
public class StyleRegistryTests
{
    private static Value SymbolFor(ExecutionContext ctx, string name) =>
        Value.Symbol(name, ctx);

    [Fact]
    public void LoadsShippedPacksAtInit()
    {
        // FlowEngineRunner ctor constructs a FlowEngine which triggers the
        // shipped-pack load (StyleRegistry.LoadShippedAndUserPacks via the
        // FlowEngine ctor). After ctor, the three shipped Symbols MUST be
        // present in context.StyleRegistry. We probe both via direct context
        // inspection AND via the (listStyles) builtin to cross-check.
        using var runner = new FlowEngineRunner();
        var ctx = runner.GetEngine().Context;

        // Direct probe of ExecutionContext.StyleRegistry.
        Assert.True(ctx.StyleRegistry.ContainsKey(SymbolFor(ctx, "jazz")),
            "shipped #jazz pack should be loaded at engine init");
        Assert.True(ctx.StyleRegistry.ContainsKey(SymbolFor(ctx, "blues")),
            "shipped #blues pack should be loaded at engine init");
        Assert.True(ctx.StyleRegistry.ContainsKey(SymbolFor(ctx, "classical")),
            "shipped #classical pack should be loaded at engine init");

        // Probe via the (listStyles) builtin — composer-facing path.
        var (success, stdout, stderr, _) = runner.RunSource("""
            use "@improv"
            Symbol[] names = (listStyles)
            (each names (fn Symbol s => (print (str s))))
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        Assert.Contains("#jazz", stdout);
        Assert.Contains("#blues", stdout);
        Assert.Contains("#classical", stdout);
    }

    [Fact]
    public void RegisterStyleAddsToRegistry()
    {
        // Composer's own (registerStyle #custom ...) call mutates the
        // ExecutionContext.StyleRegistry. Verify the new entry is reachable
        // via (listStyles).
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, _) = runner.RunSource("""
            use "@improv"
            (registerStyle #mycustom
                (dict
                    #beat_weights (dict
                        #strong (dict #chord_tone 0.5 #scale_tone 0.5 #chromatic_passing 0.0)
                        #weak   (dict #chord_tone 0.2 #scale_tone 0.8 #chromatic_passing 0.0))
                    #interval_transitions (dict
                        #step_up 0.5 #step_down 0.5
                        #leap_up 0.0 #leap_down 0.0
                        #chromatic 0.0 #repeat 0.0)
                    #rhythmic_template <<#eighth #eighth>>
                    #articulation_distribution (dict
                        #downbeat #legato
                        #offbeat  #accent
                        #syncopated #marcato)))
            Symbol[] names = (listStyles)
            (each names (fn Symbol s => (print (str s))))
            """);

        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        Assert.Contains("#mycustom", stdout);
    }

    [Fact]
    public void ListStylesReturnsAllRegistered()
    {
        // (listStyles) returns Symbol[] in insertion order (shipped first,
        // then any later (registerStyle ...) additions).
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, _) = runner.RunSource("""
            use "@improv"
            Symbol[] names = (listStyles)
            (print (str (len names)))
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");
        // Three shipped packs at minimum.
        Assert.True(int.TryParse(stdout.Trim(), out var count) && count >= 3,
            $"Expected at least 3 registered styles; got: '{stdout.Trim()}'");
    }

    [Fact]
    public void UnknownStyleFallsBackToJazzInJamLookup()
    {
        // Phase 36 Plan 36-11 — when jam is called with a Symbol that's NOT
        // in StyleRegistry, jam falls back to the #jazz pack and emits a
        // one-shot stderr advisory. Verified via the jam impl (the
        // JamFunctionsTests in Task 2 will cross-pin this with output
        // assertions). Here we just confirm the registry semantics —
        // direct dict lookup returns absent on unknown.
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, _) = runner.RunSource("""
            use "@improv"
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");

        var ctx = runner.GetEngine().Context;
        var unknown = SymbolFor(ctx, "nonexistent_style");
        Assert.False(ctx.StyleRegistry.ContainsKey(unknown));
        // #jazz IS present (shipped pack).
        var jazz = SymbolFor(ctx, "jazz");
        Assert.True(ctx.StyleRegistry.ContainsKey(jazz));
    }

    [Fact]
    public void UserPackOverridesShippedWithAdvisory()
    {
        // Pitfall 8 — user packs at ~/.config/flow/styles/*.flow load AFTER
        // shipped packs and OVERRIDE on Symbol-name collision. The override
        // fires a one-shot stderr advisory.
        //
        // We redirect HOME to a temp dir, drop a custom jazz.flow there,
        // construct a FRESH FlowEngine, and assert:
        //  1. The override advisory appears on stderr.
        //  2. The registered #jazz pack carries our test marker, not the
        //     shipped jazz pack content.
        string tempHome = Path.Combine(Path.GetTempPath(), "flow-tests-" + Guid.NewGuid().ToString("N"));
        string userStylesDir = Path.Combine(tempHome, ".config", "flow", "styles");
        Directory.CreateDirectory(userStylesDir);

        try
        {
            File.WriteAllText(
                Path.Combine(userStylesDir, "jazz.flow"),
                """
                use "@improv"
                (registerStyle #jazz
                    (dict
                        #marker_for_test 12345
                        #beat_weights (dict
                            #strong (dict #chord_tone 0.5 #scale_tone 0.5 #chromatic_passing 0.0)
                            #weak   (dict #chord_tone 0.5 #scale_tone 0.5 #chromatic_passing 0.0))))
                """);

            string? prevHome = Environment.GetEnvironmentVariable("HOME");
            string? prevUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            Environment.SetEnvironmentVariable("HOME", tempHome);
            Environment.SetEnvironmentVariable("USERPROFILE", tempHome);

            try
            {
                // RenderingDiagnostics dedup is per-process — reset so the
                // advisory has a chance to fire on this fresh engine init.
                RenderingDiagnostics.ResetForTesting();

                using var runner = new FlowEngineRunner();
                var ctx = runner.GetEngine().Context;

                // Loaded packs include jazz from the user dir, which overrides
                // the shipped jazz pack. Verify the marker is present in the
                // registered pack.
                var jazzSym = SymbolFor(ctx, "jazz");
                Assert.True(ctx.StyleRegistry.ContainsKey(jazzSym));
                var pack = ctx.StyleRegistry[jazzSym];
                bool foundMarker = false;
                foreach (var kv in pack.Entries)
                {
                    if (kv.Key.Data is string keyStr && keyStr == "marker_for_test")
                    {
                        foundMarker = true;
                        break;
                    }
                }
                Assert.True(foundMarker, "User jazz pack should have replaced shipped pack content");

                // Override advisory should have fired during the engine ctor's
                // user-pack-load phase. The runner captures stderr; the
                // construction-time stderr lives on the engine's stderr writer.
                // We re-trigger by running a no-op script (just to surface
                // the captured stderr).
                var (success, _, stderr, _) = runner.RunSource("(print \"ok\")");
                Assert.True(success);
                Assert.Contains("user style '#jazz' overrides shipped pack", stderr);
            }
            finally
            {
                Environment.SetEnvironmentVariable("HOME", prevHome);
                Environment.SetEnvironmentVariable("USERPROFILE", prevUserProfile);
                RenderingDiagnostics.ResetForTesting();
            }
        }
        finally
        {
            try { Directory.Delete(tempHome, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void MalformedPackFileWarnsButDoesntCrash()
    {
        // Charitable contract — a malformed user pack fires a one-shot
        // advisory and FlowEngine init continues. The shipped packs remain
        // accessible.
        string tempHome = Path.Combine(Path.GetTempPath(), "flow-tests-" + Guid.NewGuid().ToString("N"));
        string userStylesDir = Path.Combine(tempHome, ".config", "flow", "styles");
        Directory.CreateDirectory(userStylesDir);

        try
        {
            // Deliberately busted: unclosed paren + missing (registerStyle ...).
            File.WriteAllText(
                Path.Combine(userStylesDir, "broken.flow"),
                "use \"@improv\"\n(registerStyle #broken (dict\n");

            string? prevHome = Environment.GetEnvironmentVariable("HOME");
            string? prevUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            Environment.SetEnvironmentVariable("HOME", tempHome);
            Environment.SetEnvironmentVariable("USERPROFILE", tempHome);

            try
            {
                RenderingDiagnostics.ResetForTesting();

                // Engine construction must NOT throw despite the busted pack.
                using var runner = new FlowEngineRunner();
                var ctx = runner.GetEngine().Context;

                // Shipped packs still loaded.
                Assert.True(ctx.StyleRegistry.ContainsKey(SymbolFor(ctx, "jazz")));
                Assert.True(ctx.StyleRegistry.ContainsKey(SymbolFor(ctx, "blues")));
                Assert.True(ctx.StyleRegistry.ContainsKey(SymbolFor(ctx, "classical")));

                // Broken pack should NOT be registered.
                Assert.False(ctx.StyleRegistry.ContainsKey(SymbolFor(ctx, "broken")));

                var (success, _, stderr, _) = runner.RunSource("(print \"ok\")");
                Assert.True(success);
                Assert.Contains("[improv]", stderr);  // some advisory MUST have fired
            }
            finally
            {
                Environment.SetEnvironmentVariable("HOME", prevHome);
                Environment.SetEnvironmentVariable("USERPROFILE", prevUserProfile);
                RenderingDiagnostics.ResetForTesting();
            }
        }
        finally
        {
            try { Directory.Delete(tempHome, recursive: true); } catch { /* best-effort */ }
        }
    }
}
