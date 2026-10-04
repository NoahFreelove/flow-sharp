# Phase 6 — playback output session, 2026-10-04

The owner requested that gap diagnosis be deferred so feature work can proceed.
P6-14 adds the control-thread output session needed by a future DAW host. The
previous anomaly evidence is preserved; no additional stress capture was run.

## Implemented

`flow-platform-linux/PlaybackOutputSession.cs` owns one output stream and reports
Offline, Running, Faulted or Disposed. It uses the existing PortAudio adapter by
default, with an optional exact host/name selector (not a persisted device index).
The `IPlaybackOutputStream` contract permits deterministic failure testing and
requires successful disposal to join all callbacks.

- Explicit Connect, Disconnect and Poll operations; no background retries.
- Open/start errors, callback faults, inactive streams and native activity-query
  errors become visible fault state with LastError.
- Successful teardown stops and rewinds playback, discards stale queued commands,
  settles pending replacement and reclaims its retired source off the callback.
- Reconnection creates a fresh callback probe and starts the output stream with
  music stopped. The host must explicitly submit Play afterward.
- Failed teardown retains stream ownership, requests protected stop, and prevents
  a second stream from opening. Disposal reports failure and can be retried.
- Already-running Connect and successful Dispose are idempotent.

The session and queue producer must share one serialized control thread. Once a
stream is successfully closed, the session temporarily takes consumer ownership
only to acknowledge stop/publication. It never reads the queue concurrently with
native callbacks. Render processing itself is unchanged.

## Host usage

```csharp
using var output = new PlaybackOutputSession(playback, "ALSA/pipewire");
if (output.Connect()) playback.TryPlay();
// In the control-thread host update loop:
var status = output.Poll();
// Present status/LastError. Retry Connect on an explicit reconnect request.
```

Running describes the output stream, not whether musical transport is playing.
The host remains responsible for handling rejected queue commands and serializing
all calls. The selector is fixed for this session; dispose and create a new session
when selecting another output. No UI integration is claimed.

## Verification and limits

Release targeted tests: **53 passed, 0 failed, 0 skipped**, covering PlatformAudio,
PreparedSineTransport, QueuedSinePlayback and PlaybackPublication. Nine new cases
exercise output loss, callback faults, query exceptions, open/start failure,
retained ownership on failed teardown, reconnect, pending publication, stale play
commands, audible output and fresh callback state. Existing compiler/analyzer
warnings remain. The sandbox blocked the initial test runner's local sockets;
the authorized rerun completed successfully.

Device loss was simulated through the stream contract. Physical unplug/replug was
not performed. A system mixer can keep its stream active after hardware disappears;
Poll does not claim to detect that physical condition. The session still uses the
prepared sine engine and existing callback probe with one retained timing sample,
not general Flow DSP or a production telemetry history.

## Next

Proceed with clip playback and shared Flow-compatible gain/pan, metering and
instrument/effect processing. Preserve the shared Flow/UI device contract rather
than adding a separate DAW-only processing path. Physical latency, actual device
recovery, loop/EOF and reference-workload qualification remain open, alongside the
native JUI shell. Further investigation of the old callback gap is deferred per the
owner's direction.
