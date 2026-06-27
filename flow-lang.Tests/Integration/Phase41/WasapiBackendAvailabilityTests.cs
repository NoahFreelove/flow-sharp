using FlowLang.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase41;

/// <summary>
/// Phase 41 WASAPI-01 — <c>WasapiBackend.IsAvailable()</c> returns <c>false</c> on
/// this Linux build host WITHOUT crashing (probe-gated, mirroring
/// <c>CoreAudioBackend.IsAvailable()</c>). WASAPI is Windows-only by nature; the
/// probe returns <c>RuntimeInformation.IsOSPlatform(OSPlatform.Windows)</c> and
/// catches Platform/DllNotFound defensively (T-41-04-PINVOKE — no crash on
/// non-Windows).
///
/// LIVE as of 41-04 (was a Wave-0 Skip stub from 41-01) — <c>WasapiBackend.cs</c>
/// now exists. This is the Linux-side machine half; the Windows-AUDIBLE half is a
/// HUMAN-UAT row in 41-HUMAN-UAT.md (D-05) and is NOT asserted here.
/// </summary>
[Trait("Category", "Phase41")]
public class WasapiBackendAvailabilityTests
{
    [Fact]
    public void IsAvailable_FalseOnLinux_NoCrash()
    {
        // On the Linux build host, WASAPI is not the running platform, so the probe
        // must return false. The probe itself must not throw — it returns the
        // Windows OS-platform check and swallows Platform/DllNotFound exceptions
        // internally (WasapiBackend.cs IsAvailable()).
        bool available = WasapiBackend.IsAvailable();
        Assert.False(available,
            "WasapiBackend.IsAvailable() must return false on Linux (WASAPI is Windows-only).");
    }
}
