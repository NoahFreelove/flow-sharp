using FlowLang.StandardLibrary.Audio.Vocalization;
using Xunit;

namespace FlowLang.Tests.Unit.Phase10;

/// <summary>
/// VOC-01 unknown-vowel regression test: GetFormants rejects non-canonical
/// vowel phonemes with a helpful ArgumentException. The message lists the
/// valid 5-vowel set so users get actionable feedback.
///
/// API shape (per flow-lang/StandardLibrary/Audio/Vocalization/FormantData.cs:69-76):
///   public static FormantEntry[] GetFormants(string vowel)
///     — throws ArgumentException at :74-75 with message
///       "Unknown vowel phoneme: '{vowel}'. Valid: ah, ee, eh, oh, oo"
///     — single-argument ArgumentException ctor (no paramName suffix), so
///       Message equality holds without "(Parameter 'vowel')" appended.
/// </summary>
public class FormantDataTests
{
    [Fact]
    public void GetFormants_UnknownVowel_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => FormantData.GetFormants("xyz"));
        Assert.Equal("Unknown vowel phoneme: 'xyz'. Valid: ah, ee, eh, oh, oo", ex.Message);
    }
}
