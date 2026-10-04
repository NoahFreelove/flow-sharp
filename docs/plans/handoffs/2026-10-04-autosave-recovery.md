# Host autosave and recovery — 2026-10-04

`ProjectAutosaveHost` adds a control-owner polled background recovery writer. It
uses the existing atomic `ProjectFile.SaveRecovery` path and immutable snapshots.
Default cadence is 30 seconds, measured with monotonic `TimeProvider` timestamps;
the interval is configurable up to one hour. There are no hidden timers or threads
reading the mutable document. The UI/session owner calls Poll from its update loop.

Only one write runs at a time. Later edits coalesce into the next snapshot rather
than accumulating jobs. Successful saves track `ProjectDocument.ChangeVersion`,
including undo/redo changes, but never mark document history saved. Failures remain
visible in `LastError` and retry on a subsequent interval. A previous recovery file
survives write failure. When the document becomes clean, owned recovery data is
removed on a subsequent poll after pending work completes.

Dispose stops admission, captures the current dirty snapshot synchronously, joins
any running save, and writes the newest capture if needed. The caller must stop
editing and await disposal. Final I/O failure is reported, not hidden. Clean close
removes owned recovery data. The normal project file is never an autosave target;
the constructor rejects using its path for recovery or the recovery lease.

An exclusive sibling `.lock` stream gives one host ownership of a recovery path.
The empty lease file intentionally persists after close; its presence is not a live
lock. Existing recovery data is never silently overwritten at startup: inspect and
resolve it first. `ProjectRecovery.Discard` takes the same lease, so a UI cannot
discard data while an autosave host owns it.

## Recovery flow

`ProjectRecovery.Inspect` independently validates the saved file and recovery file,
returns parse/access errors separately, rejects a candidate whose project identity
differs from a valid saved project, and compares snapshot contents. Filesystem time
does not prove that a candidate is newer; `DiffersFromSaved` means only different.
A valid candidate remains available if the saved file is corrupt or missing.
Inspect neither executes source nor mutates/deletes either file.

`ProjectRecovery.Restore` explicitly creates a document from the candidate and uses
`ActionHistory.MarkUnsaved` to make it dirty without fabricating an undo entry.
Saving that document normally establishes the new saved baseline. The host then
explicitly resolves/discards the old candidate before enabling a fresh autosave
owner. These are backend APIs for the future recovery UI, not an implemented native
dialog. External asset paths remain relative to the original project directory;
recovery snapshots do not copy assets into the recovery directory.

## Verification

Initial focused suite: **4 passed**, `/tmp/flow-autosave-tests.log`. Final affected
backend regression: **499 passed**, zero failures/skips,
`/tmp/flow-autosave-regression.log`. `git diff --check` passed.
Tests cover interval throttling, unchanged snapshots, dirty
state, cleanup after a normal save, exclusive ownership, edits during a blocked
write, close-time join/latest capture, failure preservation/retry, corrupt files,
identity mismatch, explicit discard and dirty restoration without history.

## Continue with backend readiness audit

Bounce/stems and autosave/recovery now have host APIs. Do not infer that every
roadmap gate is complete from these additions. Audit the actual contracts, examples,
project lifecycle, migrations, plugin semantics and verification targets against
the current tree. Run full core and browser gates after resolving required gaps.
Update stale roadmap/architecture statements to distinguish shipped backend APIs
from native frontend work and physical-device/release qualification. Native JUI and
historical callback-gap diagnosis remain deferred. The overall goal stays active.
