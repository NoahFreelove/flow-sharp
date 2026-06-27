using System;
using System.IO;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0614;

/// <summary>
/// sweep-0614 (cli-repl-watch, HIGH): the bare interpreter
/// (flow-interpreter / `dotnet run --project flow-interpreter`) never loaded
/// <c>~/.config/flow/config.toml</c>, so <see cref="FlowConfig.Active"/> stayed
/// at <see cref="FlowConfigPoco.Defaults"/> (all keys null). Most visible symptom:
/// <c>sfz_root</c> was always null → <c>loadSfz #violin</c> hard-failed under the
/// interpreter even with a valid config file. Root cause: <c>FlowConfigLoader</c>
/// lived in <c>flow-cli/Config/</c> and only flow-cli called it.
///
/// Fix: <c>FlowConfigLoader</c> moved to <c>FlowLang.Runtime</c> (this assembly's
/// own namespace) + Tomlyn moved to flow-lang.csproj (Desktop-only), and
/// flow-interpreter/Program.cs now calls <c>FlowConfigLoader.LoadFromXdg()</c> at
/// the top of Main(), mirroring flow-cli.
///
/// These tests pin the loader's parse/populate behavior via the internal
/// <c>LoadFromFile(path)</c> seam (so we never touch the real ~/.config), and
/// the charitable fallbacks (missing file / malformed TOML → defaults, no throw).
/// </summary>
[Collection("FlowScripts")]
public class InterpreterConfigLoadSweepTests : IDisposable
{
    public InterpreterConfigLoadSweepTests() => FlowConfig.Reset();
    public void Dispose() => FlowConfig.Reset();

    [Fact]
    public void LoadFromFile_PopulatesSfzRoot_And_AllFiveKeys()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp,
                "sfz_root = \"/home/test/.flow/samples/VSCO\"\n" +
                "default_tempo = 90\n" +
                "default_timesig = \"3/4\"\n" +
                "default_audio_device = \"alsa_output.usb-Test\"\n" +
                "stdlib_search_path = [\"/opt/flow-mods\"]\n");

            FlowConfigLoader.LoadFromFile(tmp);

            // The HIGH-severity symptom: sfz_root must now reach FlowConfig.Active
            // on the interpreter path (was always null before the move).
            Assert.Equal("/home/test/.flow/samples/VSCO", FlowConfig.Active.SfzRoot);
            // The silently-dropped overloads must propagate too.
            Assert.Equal(90, FlowConfig.Active.DefaultTempo);
            Assert.Equal("3/4", FlowConfig.Active.DefaultTimesig);
            Assert.Equal("alsa_output.usb-Test", FlowConfig.Active.DefaultAudioDevice);
            Assert.Contains("/opt/flow-mods", FlowConfig.ConfiguredStdlibSearchPaths);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void LoadFromFile_MissingFile_SilentFallbackToDefaults()
    {
        var ghost = Path.Combine(Path.GetTempPath(), "flow-config-does-not-exist-" + Guid.NewGuid() + ".toml");
        Assert.False(File.Exists(ghost));

        // Must not throw; Active stays at the all-null Defaults singleton.
        FlowConfigLoader.LoadFromFile(ghost);
        Assert.Null(FlowConfig.Active.SfzRoot);
        Assert.Null(FlowConfig.Active.DefaultTempo);
    }

    [Fact]
    public void LoadFromFile_MalformedToml_CharitableFallback_NoThrow()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            // Not valid TOML (dangling bracket / no key).
            File.WriteAllText(tmp, "this is = = not [ valid toml\n");

            // Charitable: warn to stderr + fall back to defaults; never throw.
            var ex = Record.Exception(() => FlowConfigLoader.LoadFromFile(tmp));
            Assert.Null(ex);
            Assert.Same(FlowConfigPoco.Defaults, FlowConfig.Active);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
