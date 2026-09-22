using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio.Vocalization;
using Xunit;

namespace FlowLang.Tests.Unit.Phase10;

// Contract: docs/decisions/2026-09-20-baseline-compatibility.md.
[Collection("FlowScripts")]
public class FormantDataTests : IDisposable
{
    public FormantDataTests() => RenderingDiagnostics.ResetForTesting();
    public void Dispose() => RenderingDiagnostics.ResetForTesting();

    [Fact]
    public void UnknownVowels_ReturnAh_AndWarnOncePerPhoneme()
    {
        using var stderr = new StringWriter();
        var previous = Console.Error;
        Console.SetError(stderr);
        try
        {
            var neutral = FormantData.GetFormants("ah");
            Assert.Same(neutral, FormantData.GetFormants("xyz"));
            Assert.Same(neutral, FormantData.GetFormants("xyz"));
            Assert.Same(neutral, FormantData.GetFormants("abc"));
        }
        finally { Console.SetError(previous); }

        var lines = stderr.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("unknown phoneme 'xyz' — using 'ah'", lines[0]);
        Assert.Contains("unknown phoneme 'abc' — using 'ah'", lines[1]);
        Assert.All(lines, line => Assert.Contains("valid: ah, ee, eh, oh, oo", line));
    }
}
