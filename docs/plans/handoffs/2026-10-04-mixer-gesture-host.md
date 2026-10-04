# Bounded mixer gesture host — 2026-10-04

P8-15 connects Flow-authored track addition to the DAW update loop. The overall
backend-readiness goal remains active.

## Implemented

- `ProjectMixerHost` accepts discrete Add Track intents, prepares one at a time on
  a worker, commits on the control owner and requests playback preparation. Its
  Poll also services the playback session, including offline publication.
- Requests remain FIFO. Unlike source-code edits, separate Add clicks are not
  superseded: each queued intent captures the latest committed project when it
  starts, so multiple additions allocate distinct buses and preserve prior edits.
- Running, queued and unread completed requests share a capacity of 1–64 (default
  64). A full host returns false and no request ID without allocating a build ticket
  or modifying the document. The frontend drains per-request completion records to
  release capacity; errors are not silently overwritten by later completions.
- Completion distinguishes committed, stale and failed gestures. An intervening
  document edit invalidates the prepared snapshot. The next queued intent then
  uses that current project. Failed preparation does not stop subsequent gestures.
- Shutdown cancels and joins active preparation, drops queued intents and discards
  draft tickets without committing. It is repeatable; no shutdown continuation
  mutates the document. Dispose mixer/generator hosts before their playback session.
- The generator host, mixer host and playback session share one control owner.
  They may be polled sequentially; neither mixer nor generator workers own the
  document or audio queue. Existing preparation and isolated-process limits remain.

## Verification

All eight focused mixer authoring/host tests passed (`/tmp/flow-mixer-host.log`).
Four new tests cover two queued additions becoming separate undoable actions,
correct active playback publication, bounded unread results, a stale running build
followed by a valid queued addition, recovery after preparation failure, and joined
shutdown that cannot start pending work or commit a late result.

All **307** affected backend/module regression tests passed, 0 failed
(`/tmp/flow-mixer-host-regression.log`). Existing analyzer/package warnings remain;
`git diff --check` passed. Full core and Web publication gates were not repeated
for this desktop host-only scheduling change.

## Next

Add captured parameter/effect graph edits with clear canonical-source ownership,
then continue remaining Flow device/plugin and MIDI input/recording requirements.
The host currently schedules track additions only; it is not a generic arbitrary
code executor or finished frontend command surface. Native UI and historical
callback-gap investigation remain deferred, and physical qualification is open.
