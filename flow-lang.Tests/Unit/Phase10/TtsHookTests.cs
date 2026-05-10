using FlowLang.StandardLibrary.Audio.Vocalization;
using Xunit;

namespace FlowLang.Tests.Unit.Phase10;

/// <summary>
/// VOC-02 regression tests: TtsHook exposes SetCommand / GetCommand for
/// configuring the external TTS engine. These Facts pin the round-trip API
/// and the empty-command validation. Subprocess invocation (RunTts) is
/// Manual-Only — this class does NOT invoke it (research Pitfall 9).
///
/// API shape (per flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs:17-28):
///   private static string _ttsCommand = "espeak-ng --stdout"  (default at :11)
///   public static void SetCommand(string command)
///     — throws ArgumentException on null/whitespace at :19-20 with message
///       "TTS command cannot be null or whitespace" + paramName "command"
///       (Message includes "(Parameter 'command')" suffix from 2-arg ctor;
///       use Assert.Contains for substring match)
///   public static string GetCommand() => _ttsCommand  (at :28)
///
/// Pitfall 9: _ttsCommand is a mutable global static. Each Fact MUST capture
/// the pre-test value in a local `original` and restore it in `finally` to
/// prevent cross-test pollution.
/// </summary>
public class TtsHookTests
{
    [Fact]
    public void SetCommand_RoundTrips_ViaGetCommand()
    {
        var original = TtsHook.GetCommand();
        try
        {
            TtsHook.SetCommand("echo");
            Assert.Equal("echo", TtsHook.GetCommand());
        }
        finally
        {
            TtsHook.SetCommand(original);  // restore global static (Pitfall 9)
        }
    }

    [Fact]
    public void SetCommand_Empty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => TtsHook.SetCommand(""));
        Assert.Contains("TTS command cannot be null or whitespace", ex.Message);
    }
}
