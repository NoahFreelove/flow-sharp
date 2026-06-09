using FlowLang.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase41;

/// <summary>
/// Phase 41 COREAUDIO-01 — <c>CoreAudioBackend.IsAvailable()</c> returns <c>false</c>
/// on this Linux build host and does NOT throw (the AudioToolbox.framework P/Invoke
/// resolves only on macOS; the probe catches <c>DllNotFoundException</c>).
///
/// This test is LIVE (not skipped): <c>CoreAudioBackend</c> already exists (the
/// shipping macOS path per D-18), so the Linux-side machine half is verifiable NOW.
/// The macOS-audible + &lt;20 ms-latency half is a HUMAN-UAT row (D-05).
///
/// (Confirmed no pre-existing duplicate of this test before creating it, per the
/// 41-VALIDATION.md confirm-before-add note.)
/// </summary>
[Trait("Category", "Phase41")]
public class CoreAudioBackendAvailabilityTests
{
    [Fact]
    public void IsAvailable_FalseOnLinux_NoThrow()
    {
        // On the Linux build host, AudioToolbox.framework does not resolve, so the
        // probe must return false. The probe itself must not throw — it catches
        // DllNotFoundException internally (CoreAudioBackend.cs IsAvailable()).
        bool available = CoreAudioBackend.IsAvailable();
        Assert.False(available,
            "CoreAudioBackend.IsAvailable() must return false on Linux (AudioToolbox.framework absent).");
    }
}
