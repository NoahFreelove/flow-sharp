# Shared monitor routing and automation clocks — 2026-10-04

P9-08 routes prepared live voices through project effects. Native MIDI input attachment,
preparation/activation ownership and UI-facing monitor controls remain unfinished.

## Implemented

ProjectCompiler.Prepare accepts an optional monitoredTrack. The preparation worker
creates one PreparedLiveInstrument with that track's resolved instrument settings,
plugin values, graph sample assets and lowered instrument automation. PreparedArrangement
exposes Monitors by track ID. The track's stable input bus feeds both clips and live
notes into the same prepared mixer graph. No second effected output is summed later.
This preserves nonlinear processing: drive receives clip + monitor, not two separately
distorted signals. Default preparation creates no monitors and retains finite offline
export semantics. Monitoring changes neither project data/history nor TotalFrames.

PreparedInstrumentControlGroup can contain scheduled and live pools (maximum 128).
The parent mixer applies a complete public parameter snapshot before rendering any
bus. Existing individual-pool setter exclusivity still applies. This keeps shared
instrument controls coherent between clip and live voices. Monitoring adds one voice
pool per explicitly selected track; each retains the existing preparation budgets.

PreparedAudioGraph.ProcessAt explicitly positions project automation without resetting
DSP. With advanceTimeline=false, automation lane values stay at the supplied frame,
while oscillators, envelopes, samples, delays and manual control smoothing continue.
Backward timeline repositioning seeks lane cursors without clearing effect history.
Meters publish the supplied project clock. Ordinary Process retains advancing behavior.
Live instrument voices use this project clock independently of their elapsed voice
clock; note release and phase continue while project automation is frozen.

PreparedGraphPlayback implements IPreparedMonitoringPlayback. During ordinary playback
it sums each monitored bus with its arrangement source before processing effects.
ReadMonitoring skips all arrangement reads and does not advance their cursors. Transport
uses that path while stopped/paused, after EOF and during recording extension. Frozen
transport freezes automation, not monitor/effect DSP. During extended recording the
project clock advances even though the finite arrangement source is at EOF.

With monitoring attached, paused mixer tails decay rather than freezing until resume.
Without monitoring, the existing pause behavior remains unchanged. Stop/seek/loop and
source replacement reset monitored voices and discard queued input observed at reset;
later input can sound unless admission was closed. Monitoring does not start transport.
Transport's return value still counts arrangement/recording frames, not audition audio.

## Evidence

The initial shared-render/clock tests passed 26 tests (`/tmp/flow-monitor-mix.log`).
The expanded affected selection passed **436** tests, zero failures
(`/tmp/flow-monitor-routing-regression.log`), including compiled default Flow project
monitoring and concurrent public controls across scheduled/monitored voices. Final
paused-transport/tail tests were added for the subsequent core verification.

Tests verify pre-effect summation for a nonlinear mixer, stopped/paused cursor isolation,
voice envelope progression with frozen automation, oscillator/echo history across
backward automation repositioning, finite duration during empty-project recording,
no allocation during monitored transport rendering, shared control coherence, and no
project edits or autoplay from monitor preparation. `git diff --check` passed.

Final compatibility checkpoint:

- Full core: Desktop solution build, **3,369 language/backend + 21 MIDI tests**,
  zero failures and 14 skipped/unexecuted tests. Tracked-content mutation audit empty.
  Evidence: `/tmp/flow-monitor-routing-core/verification.json` and associated TRX/logs.
- Web shared-backend/music/static-binding/native-policy/Phase47/48 selection:
  **345 passed, 7 skipped**, zero failures (`/tmp/flow-monitor-web.log`).
- Actual WebAssembly publication succeeded and produced the AppBundle
  (`/tmp/flow-monitor-wasm.log`).
- Desktop Release solution restored afterward: zero errors, 322 warnings
  (`/tmp/flow-monitor-desktop-restore.log`).

These are software verification results, not browser UI or physical device qualification.

## Next

Carry the selected monitor track in bounded playback-preparation requests and expose
only acknowledged monitor endpoints. Own native input attachment/replacement so one
producer fans out to monitoring and recording, closes retired endpoints and invalidates
stale takes. Detach/device failure must close admission; panic alone permits later
messages to sound. Preparation and asset decoding must remain outside callbacks.

Hardware latency, hot unplug, overload/deadline behavior and sustained clock alignment
remain unqualified. Device instances/presets and integrated export/recovery are still
open. Native JUI frontend and historical callback-gap investigation remain deferred.
The full backend goal remains active.
