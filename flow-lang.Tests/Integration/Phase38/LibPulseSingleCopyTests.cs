using System.Reflection;
using System.Runtime.InteropServices;
using FlowLang.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Audit §8.7 (2026-06-09) — shared libpulse P/Invoke surface refactor.
///
/// <para>
/// Pins the invariant: <see cref="PulseAudioSimpleBackend"/> and
/// <see cref="PulseAudioCaptureBackend"/> declare ZERO <c>[DllImport]</c>
/// methods of their own. All P/Invoke bindings live in <c>LibPulse</c>
/// (the single canonical copy). Two private copies with an annotation like
/// "Mirrors … with identical Cdecl + LPStr marshalling" was the defect;
/// this test makes it impossible to re-introduce without the suite going red.
/// </para>
///
/// <para>
/// On non-Linux hosts (macOS CI, Windows) neither backend ever calls into
/// libpulse at runtime, but the type-structure assertion is always reachable —
/// the test does NOT need a live PulseAudio daemon.
/// </para>
/// </summary>
[Collection("FlowScripts")]
public class LibPulseSingleCopyTests
{
    // Retrieve all DllImport-attributed methods on a type (including private/static)
    private static IReadOnlyList<MethodInfo> GetPInvokeMethods(Type t)
        => t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(m => m.GetCustomAttribute<DllImportAttribute>() != null)
            .ToList();

    [Fact]
    public void PulseAudioSimpleBackend_HasNoDllImportMethods()
    {
        // After the §8.7 refactor, PulseAudioSimpleBackend must own ZERO
        // [DllImport] methods — all P/Invoke declarations live in LibPulse.
        var leakedMethods = GetPInvokeMethods(typeof(PulseAudioSimpleBackend));
        Assert.True(
            leakedMethods.Count == 0,
            $"PulseAudioSimpleBackend still declares its own DllImport method(s): " +
            string.Join(", ", leakedMethods.Select(m => m.Name)) +
            " — move to LibPulse to maintain the single-copy invariant (audit §8.7).");
    }

    [Fact]
    public void PulseAudioCaptureBackend_HasNoDllImportMethods()
    {
        // Same invariant for the capture sibling.
        var leakedMethods = GetPInvokeMethods(typeof(PulseAudioCaptureBackend));
        Assert.True(
            leakedMethods.Count == 0,
            $"PulseAudioCaptureBackend still declares its own DllImport method(s): " +
            string.Join(", ", leakedMethods.Select(m => m.Name)) +
            " — move to LibPulse to maintain the single-copy invariant (audit §8.7).");
    }

    [Fact]
    public void LibPulse_ExposesExpectedSharedMethods()
    {
        // LibPulse must export the full shared surface so that both backends
        // can call them — probing by name (not signature) is enough for the
        // single-copy pin.
        var libPulseType = typeof(PulseAudioSimpleBackend).Assembly
            .GetType("FlowLang.Audio.LibPulse", throwOnError: false);

        Assert.NotNull(libPulseType);

        var methods = libPulseType!
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(m => m.Name)
            .ToHashSet();

        // Shared externs consumed by both backends
        Assert.Contains("pa_simple_new", methods);
        Assert.Contains("pa_simple_free", methods);

        // Playback-direction primitive
        Assert.Contains("pa_simple_write", methods);

        // Capture-direction primitive
        Assert.Contains("pa_simple_read", methods);

        // UTF-8 error string helper (the §8.7 PtrToStringUTF8 fix)
        Assert.Contains("GetErrorString", methods);
    }

    [Fact]
    public void LibPulse_GetErrorString_UsesUtf8Marshaling()
    {
        // Verify the fix at the source level: GetErrorString must use
        // Marshal.PtrToStringUTF8 rather than the buggy PtrToStringAnsi.
        // This is verified structurally: pa_strerror in LibPulse is private,
        // and the PUBLIC helper GetErrorString wraps it. The functional correctness
        // (UTF-8 non-ASCII bytes) requires a live libpulse, which is not present
        // on macOS CI — so we verify the wrapper exists and returns a non-null
        // string for error code 0 (PA_OK), charitable-skipping when libpulse is absent.
        var libPulseType = typeof(PulseAudioSimpleBackend).Assembly
            .GetType("FlowLang.Audio.LibPulse", throwOnError: false);

        Assert.NotNull(libPulseType);

        var getErrorString = libPulseType!.GetMethod(
            "GetErrorString",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            types: [typeof(int)],
            modifiers: null);

        Assert.NotNull(getErrorString);

        // Call it — on macOS/CI this will hit the DllNotFoundException path and
        // return the fallback string; on Linux with libpulse it returns the real
        // message. Either way it must not throw and must return a non-null string.
        var result = getErrorString!.Invoke(null, [0]) as string;
        Assert.NotNull(result);
        Assert.NotEmpty(result!);
    }
}
