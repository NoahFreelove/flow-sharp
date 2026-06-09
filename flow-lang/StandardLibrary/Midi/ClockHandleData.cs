#if !FLOW_WEB
using FlowLang.Audio;

namespace FlowLang.StandardLibrary.Midi;

/// <summary>
/// Phase 40 CLOCK-01/02 (D-40-03) — runtime state behind a <c>ClockHandle</c>.
/// Carries the live <see cref="MidiClock"/> service (which owns its OWN internal
/// <c>CancellationTokenSource</c> + background thread/task) plus a
/// <see cref="Mode"/> discriminator.
///
/// <para><c>(clockStop ClockHandle)</c> calls <see cref="MidiClock.Stop"/> which
/// cancels the clock's internal CTS, joins the master thread / disposes the slave
/// receiver, and waits ≤ 1s charitably — modeling <c>OscFunctions.StopListener</c>.</para>
///
/// <para><b>WR-06:</b> this handle deliberately does NOT carry its own
/// <c>CancellationTokenSource</c>. An earlier draft mirrored
/// <c>OscHandleData.Cts</c>, but <see cref="MidiClock"/> already owns the only CTS
/// that matters and <c>(clockStop)</c> cancels it via <see cref="MidiClock.Stop"/>;
/// a second handle-level CTS was never read/cancelled/disposed — a leaked
/// <c>IDisposable</c> plus a false "(clockStop) cancels it" docstring. The field
/// is removed so the handle's lifecycle has a single, real owner.</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on the Web target (T-40-03),
/// like <c>OscHandleData</c> + <see cref="MidiDeviceData"/>.</para>
/// </summary>
public sealed class ClockHandleData
{
    /// <summary>Whether this handle drives a clock master (emit 24 PPQN) or a
    /// clock slave (receive 24 PPQN → drive Tempo). The session mode is part of
    /// the handle identity (D-40-03 master ⊕ slave session state).</summary>
    public required ClockMode Mode { get; init; }

    /// <summary>The live clock service. Never null — even a charitable dead
    /// handle (slave bind failure / master with no real device) carries a
    /// <see cref="MidiClock"/> whose <see cref="MidiClock.Stop"/> is a safe
    /// no-op so <c>(clockStop)</c> never crashes. The clock owns the sole
    /// <c>CancellationTokenSource</c> for the timing thread / listener loop.</summary>
    public required MidiClock Clock { get; init; }
}

/// <summary>Phase 40 CLOCK-01/02 — clock session mode discriminator. Master ⊕
/// slave are mutually exclusive (D-40-03); a switch is honored only at a bar
/// boundary (CLOCK-02).</summary>
public enum ClockMode
{
    Master,
    Slave,
}
#endif
