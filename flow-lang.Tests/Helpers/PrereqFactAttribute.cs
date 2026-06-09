using System;
using System.Runtime.InteropServices;

namespace FlowLang.Tests.Helpers;

/// <summary>
/// Audit-0609 §8.1 — xUnit FactAttribute subclass that statically skips a test
/// unless a named prerequisite condition is satisfied.
///
/// <para>Usage examples:</para>
/// <code>
/// [PrereqFact("linux")]  // only runs on Linux
/// [PrereqFact("macos")]  // only runs on macOS
/// </code>
///
/// <para>When the prerequisite is not met, the attribute sets the inherited
/// <see cref="Xunit.FactAttribute.Skip"/> property so xUnit reports the test
/// as skipped with a specific reason rather than running it.</para>
///
/// <para>Supported token values (case-insensitive):</para>
/// <list type="bullet">
///   <item><c>"linux"</c>     — skips unless OperatingSystem.IsLinux()</item>
///   <item><c>"macos"</c>     — skips unless OperatingSystem.IsMacOS()</item>
///   <item><c>"windows"</c>   — skips unless OperatingSystem.IsWindows()</item>
/// </list>
/// </summary>
public sealed class PrereqFactAttribute : Xunit.FactAttribute
{
    public PrereqFactAttribute(string prereq)
    {
        if (string.IsNullOrWhiteSpace(prereq))
            throw new ArgumentException("PrereqFact requires a non-empty prerequisite token.", nameof(prereq));

        bool met = prereq.ToLowerInvariant() switch
        {
            "linux"   => OperatingSystem.IsLinux(),
            "macos"   => OperatingSystem.IsMacOS(),
            "windows" => OperatingSystem.IsWindows(),
            _ => throw new ArgumentException(
                $"PrereqFact: unknown prereq token '{prereq}'. " +
                "Supported: linux, macos, windows.", nameof(prereq)),
        };

        if (!met)
        {
            Skip = $"Prerequisite '{prereq}' not satisfied on " +
                   $"{RuntimeInformation.OSDescription} — skipping.";
        }
    }
}
