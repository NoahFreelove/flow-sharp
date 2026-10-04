# Default Flow project and playback read APIs — 2026-10-04

P8-12 adds a usable initial project for the DAW host. Backend readiness remains in
progress; no native frontend implementation is included.

## Implemented

`ProjectFactory.Create` prepares an empty one-track project with explicit instrument
and master-graph bindings. The default gain chain and sine instrument are evaluated
from `DefaultDeviceCode` through the normal Flow generator adapter, and that source
is saved in the document. There is no parallel C# implementation of device behavior.

The caller can supply tempo, seed and track name. The initial meter is 4/4, the
instrument has 64 voices with 5 ms attack/20 ms release, and track/master gain are
unity. No clips or musical notes are added automatically. Initial construction
has an empty, clean undo history; a subsequently created note clip and drawn note
use normal captured project actions. Independent projects get independent IDs.

Creation is synchronous startup/worker work with cooperative cancellation and a
20-second built-in template budget. It must precede device startup and must not
run in the audio callback. This API evaluates only the fixed built-in template;
user-authored generator edits continue through isolated processes.

The playback coordinator now exposes diagnostics, asset diagnostics, meter node
ordering and bounded coherent meter reads for the acknowledged active graph.
These properties follow audio publication acknowledgement, not preparation alone.
Hosts access them on the coordinator's control/UI owner, size their scratch meter
buffer for the active node list, and ignore a failed meter read. Prepared processing
objects and audio cursor mutation remain internal.

## Verification

New tests create/reopen an empty default project, verify independent identity and
clean history, draw a note through normal commands, publish/play it through the
host, read nonzero meters and verify undo/republication clears them. Invalid options
and cancellation are checked. Default devices use the shared Flow implementation.

All **292** affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting
regression tests passed, 0 failed (`/tmp/flow-project-factory-regression.log`).
Existing analyzer/package warnings remain. `git diff --check` passed. No whole
solution or Web publish validation was performed in this step.

## Next

Broaden whole-solution and Web compatibility checks before adding more host APIs.
Track/routing gestures, fuller frontend status, MIDI input/recording and remaining
Flow device/plugin capabilities still require work. The one-track default is a
starting document, not a restriction on the existing multi-track engine.
Physical-device qualification, native UI and historical gap investigation are not
claimed by this change; the latter two remain deferred.
