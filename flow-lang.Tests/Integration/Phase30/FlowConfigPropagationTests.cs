using System.Collections.Generic;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using Xunit;
using ExecCtx = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Integration.Phase30;

/// <summary>
/// REQ-4 (Plan 30-03 Task 3): integration facts pinning all four optional-key
/// propagation paths from <c>~/.config/flow/config.toml</c> -> engine read site:
///
///   - <see cref="FlowConfigPoco.DefaultTempo"/> -> <see cref="ExecCtx.GetMusicalContext"/>
///     Tempo fallback chain
///   - <see cref="FlowConfigPoco.DefaultTimesig"/> -> <see cref="ExecCtx.GetMusicalContext"/>
///     TimeSignature fallback chain (parsed N/M; malformed -> charitable 4/4)
///   - <see cref="FlowConfigPoco.StdlibSearchPath"/> ->
///     <see cref="FlowConfig.ConfiguredStdlibSearchPaths"/> (seed source for
///     <c>ModuleLoader.AdditionalSearchPaths</c>)
///   - <see cref="FlowConfigPoco.DefaultAudioDevice"/> -> read-API pin (the
///     command-side <c>device ??= FlowConfig.Active.DefaultAudioDevice</c>
///     fallback is exercised by Task 4's grep acceptance criteria)
///
/// Plus baseline facts on the singleton's defaults / null-fallback / active-block
/// precedence. Every fact wraps its body in try/finally with
/// <see cref="FlowConfig.Reset"/> so test ordering cannot leak state.
/// </summary>
public class FlowConfigPropagationTests
{
    /// <summary>Helper: build a fresh <see cref="ExecCtx"/> matching FlowEngine's wiring.</summary>
    private static ExecCtx NewExecutionContext() =>
        new(new ErrorReporter(), new InternalFunctionRegistry());

    [Fact]
    public void Defaults_Reflect_Null_For_All_Five_Keys()
    {
        FlowConfig.Reset();
        try
        {
            var d = FlowConfigPoco.Defaults;
            Assert.Null(d.InstallPath);
            Assert.Null(d.DefaultAudioDevice);
            Assert.Null(d.DefaultTempo);
            Assert.Null(d.DefaultTimesig);
            Assert.Null(d.StdlibSearchPath);
        }
        finally { FlowConfig.Reset(); }
    }

    [Fact]
    public void Setting_Active_DefaultTempo_Propagates_To_New_MusicalContext()
    {
        FlowConfig.Reset();
        try
        {
            FlowConfig.Active = new FlowConfigPoco { DefaultTempo = 100 };
            var execCtx = NewExecutionContext();
            // No tempo block active -> ExecutionContext.GetMusicalContext walks the
            // empty (global-only) call stack, then hits the ??= layer that reads
            // FlowConfig.Active.DefaultTempo == 100 BEFORE the baked 120 default.
            var resolved = execCtx.GetMusicalContext();
            Assert.Equal(100.0, resolved.Tempo);
        }
        finally { FlowConfig.Reset(); }
    }

    [Fact]
    public void Active_DefaultTempo_Null_Falls_Back_To_Baked_120()
    {
        FlowConfig.Reset();
        try
        {
            // After Reset, Active == Defaults, all keys null
            var execCtx = NewExecutionContext();
            var resolved = execCtx.GetMusicalContext();
            Assert.Equal(120.0, resolved.Tempo);
        }
        finally { FlowConfig.Reset(); }
    }

    [Fact]
    public void Default_Timesig_From_Config_Applies_When_Script_Has_No_Timesig_Block()
    {
        FlowConfig.Reset();
        try
        {
            FlowConfig.Active = new FlowConfigPoco { DefaultTimesig = "3/4" };
            var execCtx = NewExecutionContext();
            var resolved = execCtx.GetMusicalContext();
            Assert.NotNull(resolved.TimeSignature);
            Assert.Equal(3, resolved.TimeSignature!.Numerator);
            Assert.Equal(4, resolved.TimeSignature.Denominator);
        }
        finally { FlowConfig.Reset(); }
    }

    [Fact]
    public void Malformed_Default_Timesig_Falls_Back_To_4_4_Silently()
    {
        FlowConfig.Reset();
        try
        {
            // Charitable per CLAUDE.md feedback_charitable_interpretation: malformed
            // string -> baked 4/4 + single stderr Warning at first encounter.
            // Functional contract: assert the fallback shape, not the warning text
            // (other tests may have already tripped the warning latch — that's fine).
            FlowConfig.Active = new FlowConfigPoco { DefaultTimesig = "not a timesig" };
            var execCtx = NewExecutionContext();
            var resolved = execCtx.GetMusicalContext();
            Assert.NotNull(resolved.TimeSignature);
            Assert.Equal(4, resolved.TimeSignature!.Numerator);
            Assert.Equal(4, resolved.TimeSignature.Denominator);
        }
        finally { FlowConfig.Reset(); }
    }

    [Fact]
    public void Stdlib_Search_Path_Propagates_To_ModuleLoader_AdditionalSearchPaths()
    {
        FlowConfig.Reset();
        try
        {
            FlowConfig.Active = new FlowConfigPoco
            {
                StdlibSearchPath = new List<string> { "/tmp/flow-custom-modules", "/opt/flow-shared" }
            };
            // Indirect propagation: FlowConfig.ConfiguredStdlibSearchPaths is what
            // FlowEngine reads to seed ModuleLoader.AdditionalSearchPaths. Pin the
            // singleton-side surface here; the loader-side wiring is covered by
            // the existing flow-lang.Tests module-loading suite at HEAD.
            var paths = FlowConfig.ConfiguredStdlibSearchPaths;
            Assert.Equal(2, paths.Count);
            Assert.Contains("/tmp/flow-custom-modules", paths);
            Assert.Contains("/opt/flow-shared", paths);
        }
        finally { FlowConfig.Reset(); }
    }

    [Fact]
    public void DefaultAudioDevice_Read_Through_Active_Singleton()
    {
        FlowConfig.Reset();
        try
        {
            FlowConfig.Active = new FlowConfigPoco
            {
                DefaultAudioDevice = "alsa_output.usb-FocusriteScarlett"
            };
            // Pure singleton-read API contract test — the command-side
            // `device ??= FlowConfig.Active.DefaultAudioDevice` fallback is exercised
            // by Plan 30-03 Task 4's grep acceptance criteria across the 3 audio-
            // producing commands (run, play, watch).
            Assert.Equal(
                "alsa_output.usb-FocusriteScarlett",
                FlowConfig.Active.DefaultAudioDevice);
        }
        finally { FlowConfig.Reset(); }
    }

    [Fact]
    public void Active_Block_Overrides_FlowConfig_DefaultTempo()
    {
        FlowConfig.Reset();
        try
        {
            FlowConfig.Active = new FlowConfigPoco { DefaultTempo = 100 };
            // Push a frame with explicit tempo=140 to mimic an active `tempo 140 { ... }`
            // block; verify call-stack value wins over the FlowConfig fallback layer.
            var execCtx = NewExecutionContext();
            execCtx.PushFrame();
            try
            {
                execCtx.CurrentFrame.MusicalContext = new MusicalContext { Tempo = 140.0 };
                var resolved = execCtx.GetMusicalContext();
                Assert.Equal(140.0, resolved.Tempo);
            }
            finally
            {
                execCtx.PopFrame();
            }
            // After pop, the FlowConfig fallback layer kicks back in.
            var resolvedAfter = execCtx.GetMusicalContext();
            Assert.Equal(100.0, resolvedAfter.Tempo);
        }
        finally { FlowConfig.Reset(); }
    }
}
