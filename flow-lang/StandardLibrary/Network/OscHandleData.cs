using System.Threading;
using System.Threading.Tasks;

namespace FlowLang.StandardLibrary.Network;

/// <summary>
/// Phase 38 OSC-01 — runtime state for an OSC handle. Carries the listener
/// resources when produced by <c>(oscListen Int String Function)</c> and
/// optionally an unsent <c>OscPacket</c> when produced by
/// <c>(oscBundle ...packets)</c> — the same <see cref="OscHandleData"/>
/// shape is reused as the dual-role value-type per the simplest path noted
/// in 38-PATTERNS line 197 (researcher's call: "OR add a sibling
/// <c>OscBundleType</c>"; we picked the discriminator approach).
///
/// <para>
/// Listener-handle role (from <c>oscListen</c>):
/// <see cref="Receiver"/> non-null, <see cref="ListenerTask"/> is the
/// running receive loop, <see cref="PendingPacket"/> null. <c>(oscStop)</c>
/// disposes the receiver (Pitfall #5 — disposes from outside to break the
/// blocked <c>Receive()</c>) and waits briefly for the task to drain.
/// </para>
///
/// <para>
/// Pending-packet role (from <c>oscBundle</c>):
/// <see cref="Receiver"/> null, <see cref="ListenerTask"/> = completed,
/// <see cref="PendingPacket"/> holds the assembled <c>OscBundle</c> ready
/// for <c>(oscSendBundle)</c>. <c>(oscStop)</c> is a no-op on this
/// discriminator (Cts already disposed; nothing to release).
/// </para>
/// </summary>
public sealed class OscHandleData
{
    /// <summary>UDP port bound by the listener; 0 for pending-packet role.</summary>
    public required int Port { get; init; }

    /// <summary>OSC address path the listener filters on (literal match;
    /// wildcard patterns deferred to v1.6 per CONTEXT D-38-16); empty
    /// for pending-packet role.</summary>
    public required string Path { get; init; }

    /// <summary>The underlying Rug.Osc UDP receiver, or <c>null</c> for the
    /// pending-packet discriminator. Disposed by <c>(oscStop)</c> to break
    /// the blocked <c>Receive()</c> call per Pitfall #5.</summary>
    public required Rug.Osc.OscReceiver? Receiver { get; init; }

    /// <summary>Cancellation source for the receive-loop Task. Always
    /// non-null even on the pending-packet role (kept for shape uniformity
    /// — <c>(oscStop)</c> calls <c>Cancel()</c> charitably).</summary>
    public required CancellationTokenSource Cts { get; init; }

    /// <summary>Background Task running the blocking receive loop, or
    /// <see cref="Task.CompletedTask"/> for the pending-packet role.</summary>
    public required Task ListenerTask { get; init; }

    /// <summary>For the pending-packet discriminator, the assembled
    /// <c>Rug.Osc.OscBundle</c> (or single <c>OscMessage</c>) ready for
    /// <c>(oscSendBundle)</c>. <c>null</c> when this handle represents an
    /// active listener.</summary>
    public Rug.Osc.OscPacket? PendingPacket { get; init; }
}
