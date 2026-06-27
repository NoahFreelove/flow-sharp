#if !FLOW_WEB
using FlowLang.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 WR-07 regression — port-name matching must NOT bind an arbitrary
/// first device on an empty/whitespace port name (the original code used
/// <c>Name.Contains(port)</c>, and <c>string.Contains("")</c> is true for every
/// device). Exercises the pure <see cref="RtMidiMidiBackend.MatchPortIndex"/>
/// matcher so no real <c>librtmidi.so</c> is needed.
/// </summary>
public class MidiPortMatchTests
{
    private static readonly string[] Ports =
        { "Midi Through Port-0", "Roland JV-1080", "ROLAND JV-1080 MIDI 2" };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void EmptyOrWhitespacePort_MatchesNothing(string port)
    {
        // WR-07: an empty/whitespace port matches NO device — never the first one.
        Assert.Equal(-1, RtMidiMidiBackend.MatchPortIndex(Ports, port));
    }

    [Fact]
    public void ExactCaseInsensitiveMatch_PreferredOverSubstring()
    {
        // "Roland JV-1080" is an exact (case-insensitive) match for index 1, even
        // though it is ALSO a substring of index 2 ("ROLAND JV-1080 MIDI 2"). The
        // exact match must win so a broad substring can't shadow the real port.
        Assert.Equal(1, RtMidiMidiBackend.MatchPortIndex(Ports, "roland jv-1080"));
    }

    [Fact]
    public void SubstringMatch_FallsBackWhenNoExact()
    {
        // No exact match for "JV-1080" → first substring match (index 1).
        Assert.Equal(1, RtMidiMidiBackend.MatchPortIndex(Ports, "JV-1080"));
    }

    [Fact]
    public void NoMatch_ReturnsMinusOne()
    {
        Assert.Equal(-1, RtMidiMidiBackend.MatchPortIndex(Ports, "Korg"));
    }
}
#endif
