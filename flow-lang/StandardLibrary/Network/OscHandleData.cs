using System.Threading;
using System.Threading.Tasks;

namespace FlowLang.StandardLibrary.Network;

/// <summary>
/// Phase 38 OSC-01 — runtime state for an active OSC listener handle.
/// Returned (wrapped via <see cref="FlowLang.Runtime.Value.OscHandle"/>)
/// from the <c>(oscListen Int String Function)</c> builtin and consumed
/// by <c>(oscStop OscHandle)</c>.
///
/// <para>
/// Holds the underlying Rug.Osc receiver, the CancellationTokenSource
/// used to cancel the background receive loop, and the listener Task
/// itself so <c>(oscStop)</c> can dispose the receiver (Pitfall #5 per
/// 38-RESEARCH §K line 1445 — disposing the socket forces the blocked
/// <c>Receive()</c> to throw and the loop to exit charitably) and wait
/// briefly for the Task to drain.
/// </para>
/// </summary>
public sealed class OscHandleData
{
    /// <summary>UDP port bound by the listener.</summary>
    public required int Port { get; init; }

    /// <summary>OSC address path the listener filters on (literal match;
    /// wildcard patterns deferred to v1.6 per CONTEXT D-38-16).</summary>
    public required string Path { get; init; }

    /// <summary>The underlying Rug.Osc UDP receiver. Disposed by
    /// <c>(oscStop)</c> to break the blocked <c>Receive()</c> call.</summary>
    public required Rug.Osc.OscReceiver Receiver { get; init; }

    /// <summary>Cancellation source for the receive-loop Task.</summary>
    public required CancellationTokenSource Cts { get; init; }

    /// <summary>Background Task running the blocking receive loop.</summary>
    public required Task ListenerTask { get; init; }
}
