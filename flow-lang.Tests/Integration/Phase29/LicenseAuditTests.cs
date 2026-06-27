using System.IO;
using FlowLang.Tests;
using Xunit;

namespace FlowLang.Tests.Integration.Phase29;

/// <summary>
/// Phase 29 Gate C — license audit. Every flow-lang/Samples/{instrument}/LICENSE.md
/// must exist, declare an accepted license (CC0, Public Domain, or CC-BY), and
/// contain a Source: line. CC-BY-SA and CC-BY-NC remain rejected per SPEC-2
/// (2026-05-11 relaxation from CC0-only to CC0/CC-BY).
/// </summary>
public class LicenseAuditTests
{
    [Theory]
    [InlineData("piano")]
    [InlineData("brass")]
    [InlineData("sax")]
    [InlineData("strings")]
    [InlineData("flute")]
    [InlineData("bell")]
    public void EachInstrumentLicenseFile_HasRequiredFields(string instrument)
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string licensePath = Path.Combine(repoRoot, "flow-lang", "Samples", instrument, "LICENSE.md");

        Assert.True(File.Exists(licensePath), $"LICENSE.md missing for instrument '{instrument}' at {licensePath}");
        string contents = File.ReadAllText(licensePath);

        Assert.Contains("License:", contents);
        Assert.Contains("Source:", contents);

        bool isAccepted =
            contents.Contains("License: CC0") ||
            contents.Contains("License: Public Domain") ||
            contents.Contains("License: CC-BY 3.0") ||
            contents.Contains("License: CC-BY 4.0");
        Assert.True(isAccepted,
            $"LICENSE.md for '{instrument}' must declare CC0, Public Domain, or CC-BY 3.0/4.0. " +
            $"Got header: {contents.Substring(0, System.Math.Min(200, contents.Length))}");

        // CC-BY-SA and CC-BY-NC remain excluded (share-alike + non-commercial both
        // create downstream legal complications).
        Assert.DoesNotContain("License: CC-BY-SA", contents);
        Assert.DoesNotContain("License: CC-BY-NC", contents);

        // If license is CC-BY (any version), an Attribution: line is required so
        // the bundle-wide CREDITS.md can aggregate. CC0 / Public Domain skip this.
        bool isCcBy = contents.Contains("License: CC-BY");
        if (isCcBy)
        {
            Assert.Contains("Attribution:", contents);
        }
    }

    [Fact]
    public void BundleWideCreditsFile_ExistsWhenAnyInstrumentIsCcBy()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        string samplesRoot = Path.Combine(repoRoot, "flow-lang", "Samples");

        string[] instruments = { "piano", "brass", "sax", "strings", "flute", "bell" };
        bool anyCcBy = false;
        foreach (var instr in instruments)
        {
            string lic = Path.Combine(samplesRoot, instr, "LICENSE.md");
            if (File.Exists(lic) && File.ReadAllText(lic).Contains("License: CC-BY"))
            {
                anyCcBy = true;
                break;
            }
        }

        if (anyCcBy)
        {
            string credits = Path.Combine(samplesRoot, "CREDITS.md");
            Assert.True(File.Exists(credits),
                "At least one instrument uses CC-BY, so flow-lang/Samples/CREDITS.md " +
                "must exist with aggregated attribution.");
        }
    }
}
