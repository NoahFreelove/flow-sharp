using System.IO;
using FlowLang.Tests;
using Xunit;

namespace FlowLang.Tests.Integration.Phase39;

/// <summary>
/// Phase 39 — vendor source-code license discipline gate. Mirrors the Phase 29
/// <c>LicenseAuditTests</c> pattern: pure file-existence + content-string
/// assertions, no network.
///
/// <para>
/// CONTEXT D-39-03 / D-39-04 envisioned vendoring <c>sightreader/musicxml-schemas</c>
/// and <c>matthewcpp/ABCSharp</c>. After research, BOTH were dropped (see
/// <c>flow-lang/Vendor/README.md</c>):
/// </para>
/// <list type="bullet">
///   <item>MusicXmlSchemas — <c>XDocument</c> structural diff suffices for the
///     XML-02 round-trip CI gate (Plan 39-01 T1 decision).</item>
///   <item>ABCSharp — hand-roll fits Flow's narrow ABC needs (Plan 39-03 T4
///     decision).</item>
/// </list>
///
/// <para>
/// This test suite asserts the Vendor README documents both decisions so future
/// readers understand the gap.
/// </para>
/// </summary>
public class VendoredSourceLicenseTests
{
    private static string VendorReadmePath()
    {
        string testsRoot = FlowScriptData.FindTestsRoot();
        string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
        return Path.Combine(repoRoot, "flow-lang", "Vendor", "README.md");
    }

    [Fact]
    public void VendorReadme_Exists()
    {
        string path = VendorReadmePath();
        Assert.True(File.Exists(path), $"Vendor README missing at {path}");
    }

    [Fact]
    public void MusicXmlSchemas_IntentionallyNotVendored_DocumentedInReadme()
    {
        string content = File.ReadAllText(VendorReadmePath());
        Assert.Contains("MusicXmlSchemas: NOT vendored", content);
    }

    [Fact]
    public void AbcSharp_DocumentedAsHandRolledInReadme()
    {
        string content = File.ReadAllText(VendorReadmePath());
        Assert.Contains("ABCSharp: NOT vendored", content);
    }
}
