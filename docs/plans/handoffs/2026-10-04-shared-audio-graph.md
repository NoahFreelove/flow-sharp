# Shared Flow audio graph — 2026-10-04

P6-15 / P7-03 adds the first shared effect/mixing graph and connects it to existing
prepared playback. The previous goal turn completed process-isolated generators;
this turn adds processing and Flow construction/reconstruction. The complete
backend goal remains active, with frontend and gap investigation deferred.

## Implemented

`Flow.Audio.Graph` now provides a versioned catalog and immutable explicit graph:

| Device v1 | Contract |
|---|---|
| `flow.input` | Stereo external input bus, integer index 0–63; not automatable. |
| `flow.gain` | Linear gain 0–16, default 1. |
| `flow.pan` | Stereo-to-mono average followed by constant-power pan, -1 to 1. |
| `flow.drive` | Independent-channel `tanh(sample * drive)`, drive 1–32. |
| `flow.sum` | Sum 2–64 connected inputs in authored order. |

All current devices have zero latency/tail. Effects support explicit bypass as
identity processing; input/sum do not. Gain/pan/drive updates use 5 ms linear
ramps; independent instances retain separate state. The catalog exposes stable
parameter IDs, units, defaults, bounds, smoothing and automation capability.
Pan's gain calculation is shared with the legacy Flow buffer panner, preserving
that API's behavior rather than maintaining an unrelated UI effect.

Preparation validates device versions, parameter bounds, connections, cycles,
disconnected nodes, node count (1–1024) and float-buffer allocation budget. The
current prepared graph requires every node to contribute to its output; draft
unconnected document nodes will need separate document representation. Explicit
ordered sums permit buses, groups and sends without implicit routing.

`PreparedAudioGraph.Process` uses caller-owned contiguous stereo input-bus blocks
and preallocated node buffers. A bounded SPSC parameter queue applies changes at
nonempty block boundaries; full queues reject submissions. Reset applies pending
targets, finishes ramps and clears buffers/meters. Per-node peak/RMS snapshots are
available to the audio owner after processing; UI-safe snapshot publication and
meter decay remain host work. These meters are not atomic cross-thread snapshots.

## Flow and playback integration

- Registered the `AudioGraph` domain type and its array annotations through the
  existing type-name/catalog mechanism; no new expression/procedure syntax.
- `@flowDaw` exports `dawInput`, versioned `dawDevice`, convenience `dawGain`,
  `dawPan`, `dawDrive`, ordered `dawMix`/`dawSum`, and `dawProcess` buffer preview.
- `FlowGraphExporter` emits explicit valid Flow construction code, preserving
  node IDs, versions, topology, input order, parameters and bypass. Numeric output
  respects Flow's `(neg x)` and non-scientific literal syntax. It does not attempt
  to reverse arbitrary handwritten code.
- `dawProcess` accepts one stereo buffer or equal-format stereo buses, leaves its
  inputs unchanged, checks cancellation per block, and uses the same prepared
  graph as playback. It caps full-buffer output at 256 MiB; streaming remains the
  host path for larger output.
- `IPreparedAudioPlayback` generalizes the existing source contract. Historical
  `PreparedSineTransport`/`QueuedSinePlayback` names remain compatible while now
  accepting any conforming prepared stereo source, including graphs.
- `PreparedGraphPlayback` transfers independent source cursors into graph input
  buses. It zero-fills after source EOF, handles unequal source lengths, and resets
  graph state on seek/stop/loop. Pause freezes processing. The existing safe
  publication/retirement and callback adapter work with this source unchanged.

```flow
use "@flowDaw"
AudioGraph input = (dawInput "input" 0)
AudioGraph quiet = (dawGain "gain" input 0.25)
AudioGraph shaped = (dawDrive "drive" quiet 2.0)
```

## Verification

**173 tests passed**, 0 failed, across StudioModel, PlatformAudio, MusicModel and
StaticBindingTests. Twelve new graph/playback cases cover independent buses,
peak/RMS, equivalent Flow reconstruction, order-sensitive effects, independent
state, bounded queue overflow and ramps across blocks, reset behavior, exact pan
parity, invalid graphs/versions/budgets, no managed allocation after warmup, Flow
buffer preview, tiny/negative exported parameters, callback/offline equality
through EOF, loop/pause/publication, and allocation-free graph Read/Seek.

Live parity uses the actual CallbackRenderProbe processing entry with muted=false
and in-memory buffers; no physical device capture or listening claim is made.
Existing music/render/MIDI baselines passed, including after sharing the pan law.
Log: `/tmp/flow-graph-regression.log`. Existing compiler/package/analyzer warnings
remain. No full solution test suite or new hardware stress capture was run.

## Next backend work

The source cursors are still prepared sine compositions, not yet graph-owned
instrument voices. Add note/event instrument nodes and bind arrangement source
windows to the shared graph under the declared cut/retrigger/reset rules. Add
bounded releases/tails, stateful effects, modulation/control-rate ports, automation
sample timing, latency compensation where needed, sampler/assets and plugin
lifecycle/hot reload. Extend structured generator output/worker transport to graphs
and audio, then document acceptance, routing/device persistence and UI snapshots.

Do not treat this first catalog as all of Phase 6/7: general DSP, complete authoring
context, clip scheduling, full project persistence and frontend integration gates
remain open. Existing physical latency/device recovery qualification limits remain;
per owner direction, do not resume the historical gap investigation.
