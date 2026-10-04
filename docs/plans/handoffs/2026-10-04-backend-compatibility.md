# Backend compatibility gate — 2026-10-04

This checkpoint broadens validation beyond the focused DAW tests. It does not mark
the backend roadmap complete.

## Finding and fix

The first core verification run built the solution and passed 3,225 language tests
and all 21 MIDI tests. Its only failure was the native-registration characterization
snapshot. The fixture loaded only the legacy modules, so the 44 added DAW signatures
were incorrectly listed as unreachable.

The module-surface characterization now imports `@flowDaw` explicitly for native
reachability and includes its complete import surface in the module snapshot. The
legacy overload-resolution fixture stays unchanged. Reviewed snapshot differences:

- Registrations: 643 → 687; reachable signatures: 637 → 681.
- The six legacy unreachable signatures are unchanged.
- The complete existing import-surface snapshot is preserved; a new 419-line section
  records `@flowDaw` and its dependencies. No existing module section changed.

Only these two snapshots were deliberately regenerated, with the two corresponding
tests passing. The initial core verifier reported no tracked-file mutations.

The final core verifier completed successfully: **3,226 language/backend tests
passed, 0 failed**, with 14 unexecuted/skipped cases; **21 MIDI tests passed**.
The solution build passed and tracked-file mutations were empty. Authoritative
TRX files, logs and counters are in `/tmp/flow-backend-core-verified/`, including
`verification.json`. This rerun used normal snapshot comparisons, not regeneration.
`git diff --check` passed; existing analyzer/package warnings remain.

## Build and Web evidence

Release whole-solution build succeeded with zero errors (`/tmp/flow-solution-build.log`).
The Web runtime publish succeeded and emitted the browser-wasm AppBundle, including
the new model/audio dependencies (`/tmp/flow-backend-web-publish.log`).

The separate Web-target test-project build remains broken: its desktop interpreter
reference compiles calls to `FlowConfigLoader`, `GeneratorWorkerServer` and
`EvaluationWorkerServer`, which the Web library deliberately excludes. Evidence:
`/tmp/flow-web-tests-build.log`. This is not a successful Web test run or browser
execution check. Desktop restore/build follows Web publication to restore the normal
host dependency assets. Correct test-project separation remains follow-up work.

## Remaining scope

Core excludes platform and long-running categories. Real-device timing, physical
unplug recovery, MIDI hardware and sustained-load qualification are not established
by this checkpoint. Native frontend and historical callback-gap diagnosis remain
deferred. Continue backend feature work and close the Web test-harness boundary;
do not conflate a successful runtime publish with a complete compatibility gate.
