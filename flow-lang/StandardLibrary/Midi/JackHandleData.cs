#if !FLOW_WEB
namespace FlowLang.StandardLibrary.Midi;

/// <summary>
/// Phase 40 JACK-01 (D-40-03 / D-40-05 best-effort) — runtime state behind a
/// <c>JackHandle</c>. Models <see cref="StandardLibrary.Network.OscHandleData"/>'s
/// <c>required</c>-init record shape, but JACK transport sync is a one-shot query
/// (not a long-lived listener), so this carries a SNAPSHOT of what
/// <c>(jackSync)</c> observed rather than a live thread + CTS.
///
/// <para><b>Charitable absent-server (JACK-01 / T-40-04):</b> when no JACK server
/// is reachable, <c>(jackSync)</c> still returns a handle — with
/// <see cref="ServerPresent"/> == <c>false</c> and a null tempo — so non-JACK
/// workflows are never affected and the call never throws.</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on the Web target (T-40-03), like
/// <c>OscHandleData</c> / <see cref="MidiDeviceData"/> / <see cref="ClockHandleData"/>.</para>
/// </summary>
public sealed class JackHandleData
{
    /// <summary>Whether a running JACK server answered the transport query. When
    /// <c>false</c>, <c>(jackSync)</c> was a charitable no-op (WarnOnce advisory)
    /// and <see cref="Tempo"/> / <see cref="Bar"/> / <see cref="Beat"/> are null.</summary>
    public required bool ServerPresent { get; init; }

    /// <summary>The transport BPM read from JACK, or null when no server was
    /// present OR the transport carried no valid BBT tempo. When non-null it has
    /// already been validated via <c>MusicalContext.IsValidTempo</c> and written to
    /// the active <c>MusicalContext.Tempo</c>.</summary>
    public double? Tempo { get; init; }

    /// <summary>The transport bar (BBT), or null when unavailable.</summary>
    public int? Bar { get; init; }

    /// <summary>The transport beat (BBT), or null when unavailable.</summary>
    public int? Beat { get; init; }
}
#endif
