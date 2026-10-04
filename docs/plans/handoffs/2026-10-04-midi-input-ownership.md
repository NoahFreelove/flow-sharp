# MIDI input device ownership — 2026-10-04

P9-04 adds a Linux platform adapter for native MIDI input. Host recording controls,
transport clock mapping and live monitoring remain open.

## Implemented

`MidiInputConnection` owns a polling thread and one RtMidi input port. Enumeration
returns exact names; opening rejects missing or ambiguous names. Native library,
port creation/open, read and receiver errors surface explicitly. The adapter uses
the modern RtMidi C ABI already used by the language backend, without taking a
platform-to-language dependency. The legacy clock bridge is unchanged.

Packets borrow a reusable 512-byte buffer for the receiver call. Timestamp is
Stopwatch ticks at polling receipt, not the hardware event time. SysEx, timing and
active sensing are ignored; this adapter serves channel-message recording. Packet
parsing, frame conversion and transport discontinuity handling belong to the host.
No input work runs in the audio callback.

TryStop closes the reader loop and joins before freeing the native port. A timeout
returns false and retains ownership for retry. Dispose uses a two-second deadline
and throws on timeout without freeing the live reader's port. Stop/Dispose have one
control owner; callbacks cannot dispose themselves. A read already in flight when
stop is requested is discarded. The recording host must close session admission
before stopping the connection and must poll Error to invalidate a failed take.

## Evidence and limits

Three focused fake-port tests passed: blocked-read timeout retains ownership until
join, failed reads expose errors while retaining the port until stop, and receiver
failure stops polling after a complete packet/timestamp delivery.
Evidence: `/tmp/flow-midi-input.log`. All 402 affected backend, platform, hosting
and module tests passed with zero failures (`/tmp/flow-midi-input-regression.log`).
`git diff --check` passed.

The adapter has not been exercised against a physical keyboard. Native queue loss,
hot unplug detection, port-list changes during opening and input latency require
qualification; a successful read does not prove the native queue never overflowed.
This slice does not claim an integrated recording or monitoring workflow.

## Next

Connect input packets and clock mapping to MidiRecordingSession through the studio
host. Invalidate takes automatically on input errors or transport discontinuity,
then add instrument monitoring and remaining device/preset/recovery workflows.
JUI package inspection remains recorded in `2026-10-04-jui-package.md`; native UI
implementation and the historical callback-gap diagnosis remain deferred.
