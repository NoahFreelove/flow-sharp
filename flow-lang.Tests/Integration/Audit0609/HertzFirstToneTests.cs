using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// 2026-06-10 hello-world fix. The documented canonical call
/// <c>(createSineTone 440Hz 1.0 0.5)</c> (README, CLAUDE.md music-types table,
/// playground hello snippet) had NO frequency-first overload — only
/// (duration, frequency, amplitude) and (duration, Hertz, amplitude) — so the
/// leading 440Hz coerced into the DURATION slot (Hertz.IsCompatibleWith(Double))
/// and the hello-world silently produced 440 SECONDS of an inaudible 1 Hz wave
/// ("play hangs and makes no sound"). These tests pin the Hertz-first overloads
/// (audio.flow procs + the C# builtin) and that the legacy forms are unchanged.
/// </summary>
[Trait("Category", "Audit0609")]
[Collection("FlowScripts")]
public class HertzFirstToneTests
{
    private const string Use = "use \"@audio\"\n";

    private static AudioBuffer Render(string expr)
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(Use + "Buffer b = " + expr);
        Assert.True(ok && errorCount == 0, $"render failed: {stderr}");
        return runner.GetVariable("b").As<AudioBuffer>();
    }

    [Theory]
    [InlineData("(createSineTone 440Hz 1.0 0.5)")]
    [InlineData("(createSawTone 440Hz 1.0 0.5)")]
    [InlineData("(createSquareTone 440Hz 1.0 0.5)")]
    [InlineData("(createTriangleTone 440Hz 1.0 0.5)")]
    public void HertzFirst_DocumentedHelloWorldShape_YieldsOneSecondBuffer(string expr)
    {
        var b = Render(expr);
        // Pre-fix: 440Hz bound the DURATION slot → 440-second buffer.
        Assert.Equal(44100, b.Frames);
        Assert.Equal(44100, b.SampleRate);
    }

    [Fact]
    public void DurationFirst_BareDoubleForm_Unchanged()
    {
        var b = Render("(createSineTone 1.0 440.0 0.5)");
        Assert.Equal(44100, b.Frames);
    }

    [Fact]
    public void DurationFirst_HertzSecondForm_Unchanged()
    {
        var b = Render("(createSineTone 1.0 440Hz 0.5)");
        Assert.Equal(44100, b.Frames);
    }

    [Fact]
    public void HertzFirst_IsAudibleFrequency_NotOneHz()
    {
        // Pre-fix the rendered wave was 1 Hz (frequency slot got the literal 1.0).
        // A 440 Hz sine crosses zero ~880 times per second; 1 Hz crosses twice.
        var b = Render("(createSineTone 440Hz 1.0 0.5)");
        int crossings = 0;
        float prev = b.GetSample(0, 0);
        for (int i = 1; i < b.Frames; i++)
        {
            float cur = b.GetSample(i, 0);
            if ((prev < 0 && cur >= 0) || (prev > 0 && cur <= 0)) crossings++;
            prev = cur;
        }
        Assert.True(crossings > 800, $"expected ~880 zero crossings for 440 Hz, got {crossings}");
    }
}
