using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using Xunit;

namespace FlowLang.Tests.Unit.Phase08;

/// <summary>
/// AUDIO-07 regression test: SynthesizerFactory.Create dispatches "strings",
/// "organ", "bell" preset names to the three new synthesizer classes shipped
/// in Phase 8-02. Structural type check — no audio rendering required.
///
/// API shape (per flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:201):
///   public static class SynthesizerFactory
///     — namespace: FlowLang.StandardLibrary.Audio (NOT .Synthesizers)
///     — method: public static INoteSynthesizer Create(string synthType)
///     — switch at :231-233 returns StringsSynthesizer / OrganSynthesizer / BellSynthesizer
/// Synthesizer class namespace: FlowLang.StandardLibrary.Audio.Synthesizers.
/// </summary>
public class SynthesizerFactoryTests
{
    [Theory]
    [InlineData("strings", typeof(StringsSynthesizer))]
    [InlineData("organ", typeof(OrganSynthesizer))]
    [InlineData("bell", typeof(BellSynthesizer))]
    public void Create_ReturnsExpectedSynthesizerType(string presetName, Type expectedType)
    {
        var synth = SynthesizerFactory.Create(presetName);
        Assert.IsType(expectedType, synth);
    }
}
