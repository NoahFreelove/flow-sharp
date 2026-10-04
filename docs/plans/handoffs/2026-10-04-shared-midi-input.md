# Shared MIDI input — 2026-10-04

P9-10 connects acknowledged live monitors and recording to one native input owner.
JUI 0.10.0 is available and its packaged managed/native headless smoke passed; see
the [integration note](2026-10-04-jui-package.md). Frontend implementation remains
deferred until backend readiness.

## Implemented

SharedMidiInputHub owns one exact named port and at most two subscriptions. The
native reader synchronously fans borrowed packets out without allocation. Closing
one subscription closes its admission and joins an already-entered receiver before
returning success. The other subscriber continues on the same device. Closing the
last subscriber joins the native reader. Failed joins retain ownership and reject
new subscriptions until cleanup succeeds; retained leases preserve device errors.

ProjectPlaybackSession owns the hub, recorder and ProjectMidiMonitoringHost. The
monitor requires running output and an acknowledged endpoint before connecting.
Its poll loop rebinds to acknowledged replacements without reopening the keyboard;
input is discarded while no acknowledged endpoint is available. Replacement resets
held voices. Disconnect, device loss and shutdown close admission and join owners.
Shutdown requests audio stop even when input cleanup needs a retry.

MidiChannelPacket provides the shared complete-packet decoder. System packets are
ignored; malformed channel packets fault the receiving path. Monitor queue overflow
is isolated from recording, while native device failure reaches both subscribers.
Recording retains its arming, timestamp mapping, captured take and undo contracts.
Stopping a take while monitoring remains connected uses subscription admission as
the cutoff; subsequent native errors do not retroactively invalidate a completed take.

## Evidence

- Affected Release suite: **450 passed**, zero failures or skips.
  `/tmp/flow-shared-midi-regression.log`.
- Full core: Desktop solution build succeeded; **3,382 language/backend + 21 MIDI
  tests passed**, zero failures, 14 skipped. Tracked mutation audit empty.
  `/tmp/flow-shared-midi-core/verification.json`.
- Coverage includes callback joins, retained ownership after timeout, device loss,
  isolated receiver errors, allocation-free fan-out, simultaneous monitoring and
  recording, take completion while monitoring continues, and automatic monitor
  rebinding after a committed take.

These checks use injected devices. Physical keyboard timing, native hot-unplug and
audio listening qualification remain pending. Core verification does not replace
the separate Web publish checkpoint documented in the monitor-routing handoff.

## Next

Continue device/preset workflows, remaining plugin categories/lifecycles and
integrated bounce/export/recovery paths. Audit their existing implementations before
expanding them. Native frontend, Linux distribution packaging and hardware
qualification are still open; this milestone does not complete the backend goal.
