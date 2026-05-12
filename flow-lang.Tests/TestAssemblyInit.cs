using System.Runtime.CompilerServices;

namespace FlowLang.Tests;

/// <summary>
/// Test-assembly initialization. Runs once before any test code in this assembly.
/// Sets FLOW_SUPPRESS_PLAYBACK=1 so AudioPlaybackManager auto-enables CaptureMode
/// and tests that exercise the (play ...) / (loop ...) builtins never push audio
/// through PulseAudio. Without this, `dotnet test` plays every renderSong result
/// to the user's speakers, which is both annoying and slows the suite by adding
/// real-time audio playback delays.
/// </summary>
internal static class TestAssemblyInit
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable("FLOW_SUPPRESS_PLAYBACK", "1");
    }
}
