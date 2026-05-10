using FlowLang.StandardLibrary.Audio.Vocalization;
using Xunit;

namespace FlowLang.Tests.Unit.Phase10;

/// <summary>
/// VOC-01 regression tests: sing() produces a recognizable formant-synthesized
/// vowel AudioBuffer. This Fact class pins the D-18 canonical observable:
/// SynthesizeVowel("ah", C4=261.63Hz, 2.0s) returns exactly 88200 samples
/// (2.0 * 44100 under IEEE-754). Pins survive any formant-algorithm refactor
/// that preserves sample-rate and duration semantics.
///
/// API shape (per flow-lang/StandardLibrary/Audio/Vocalization/FormantSynthesizer.cs:22):
///   public static AudioBuffer SynthesizeVowel(string vowel, double frequencyHz,
///     double durationSeconds, int sampleRate = 44100)
///     — numSamples = (int)(durationSeconds * sampleRate) at :24
/// AudioBuffer exposes Frames/Channels/SampleRate/Data
/// (flow-lang/StandardLibrary/Audio/AudioCore.cs:10-32).
/// </summary>
public class FormantSynthesizerTests
{
    [Fact]
    public void SynthesizeVowel_Ah_C4_2s_Returns_88200_Frames()
    {
        // D-18 canonical pin: 2.0s x 44100Hz = 88200 samples.
        // 261.63Hz is C4 per 12-tone equal temperament.
        var buffer = FormantSynthesizer.SynthesizeVowel("ah", 261.63, 2.0);
        Assert.Equal(88200, buffer.Frames);
        Assert.Equal(1, buffer.Channels);
        Assert.Equal(44100, buffer.SampleRate);
    }
}
