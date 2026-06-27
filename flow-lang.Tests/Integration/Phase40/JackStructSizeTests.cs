#if !FLOW_WEB
using FlowLang.StandardLibrary.Midi;
using Xunit;

namespace FlowLang.Tests.Integration.Phase40;

/// <summary>
/// Phase 40 CR-01 regression — the managed <c>JackPositionT</c> mirror MUST be at
/// least as large as the native <c>jack_position_t</c>, or
/// <c>jack_transport_query(client, ref pos)</c> overruns the pinned managed
/// buffer (heap/stack corruption) on a real JACK server. The
/// <see cref="JackFunctions.TransportQueryOverride"/> seam hides this from every
/// other Fact, so this is the ONLY machine-proven memory-safety guard for the
/// real-libjack path.
/// </summary>
public class JackStructSizeTests
{
    /// <summary>
    /// The canonical native <c>jack_position_t</c> size on a 64-bit LP64 ABI with
    /// 8-byte alignment, field-for-field:
    /// unique_1(8) + usecs(8) + frame_rate(4) + frame(4) + valid(4) + bar(4) +
    /// beat(4) + tick(4) + bar_start_tick(8) + beats_per_bar(4) + beat_type(4) +
    /// ticks_per_beat(8) + beats_per_minute(8) + frame_time(8) + next_time(8) +
    /// bbt_offset(4) + audio_frames_per_video_frame(4) + video_offset(4) +
    /// tick_double(8) + padding[7](28) + unique_2(8) = 152 bytes (with the 4-byte
    /// tail pad before unique_2 to satisfy 8-byte alignment of the trailing ulong).
    /// </summary>
    private const int CanonicalNativeJackPositionSize = 152;

    [Fact]
    public void JackPositionT_IsAtLeastNativeSize()
    {
        int managed = JackFunctions.JackPositionTMarshalSize;
        Assert.True(managed >= CanonicalNativeJackPositionSize,
            $"CR-01 VIOLATED: managed JackPositionT marshals to {managed} bytes, " +
            $"smaller than the native jack_position_t ({CanonicalNativeJackPositionSize} bytes) — " +
            "jack_transport_query would overrun the pinned managed buffer.");
    }
}
#endif
