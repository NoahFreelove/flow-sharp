# Web test boundary and build-runner repair — 2026-10-04

This closes the compilation gap found during broader backend compatibility work.
The overall backend-readiness goal remains active.

## Changes

- The Web test project no longer references the desktop CLI/interpreter executables.
  Direct REPL/watch/config/process tests stay Desktop-only, along with additional
  native SFZ/OSC/CoreAudio/Pulse tests added since the original Web split. The
  desktop captured-input corpus also remains on Desktop, where its golden imports
  and microphone substitution apply.
- Within shared DAW test files, only facts needing a native callback adapter or
  child process are guarded. The remaining graph, sampler, project, persistence,
  authoring and music-model tests still compile for Web. Desktop coverage remains
  present, not deleted.
- Two previously dormant Web facts now enforce current contracts: no implicit
  prelude, explicit `@std` import, and one stripped-builtin warning per name per
  engine session. They use explicit diagnostic writers instead of global Console
  redirection. Production behavior is unchanged (one explanatory comment corrected).
- Broader Web validation exposed a separate build-test hang. A captured managed
  stack showed `BundleSizeBudgetTests` blocked in `ReadToEnd`, before its timeout
  could run, with inherited output handles surviving the build process. That run
  was stopped after diagnosis; it was not reported as complete.
- Five build/publish test helpers now share `DotnetBuildProcess`: concurrent output
  draining and process exit share a deadline, cancelled work kills/joins the child,
  and single-node/no-reuse build options prevent build servers retaining pipes.
  An expired-deadline regression checks cancellation rather than only successful
  publication. This helper is test infrastructure, not the DAW generator worker.

## Verified

- Web test project builds: `/tmp/flow-web-test-boundary.log`.
- Web Phase47/48 compatibility plus shared StudioModel/MusicModel/StaticBinding
  checks excluding platform/long categories: **260 passed, 7 skipped, 0 failed**
  (`/tmp/flow-web-shared-tests.log`). The target-specific skips are intentional.
- The exact two bundle-size publish tests that stalled both passed with the new
  runner, in 27 seconds (`/tmp/flow-web-publish-runner-tests.log`). Their bundle
  hard-cap assertions passed. This validates real publish execution, not browser UI.
- The expired-deadline regression passed (`/tmp/flow-build-deadline-test.log`).
- Final Desktop core verification restored normal host assets and passed **3,226
  language/backend tests and 21 MIDI tests**, with 14 target-specific/unexecuted
  cases and no tracked-file mutations. The solution build passed. Evidence:
  `/tmp/flow-web-boundary-desktop-core/verification.json` and its TRX/log files.
- `git diff --check` passed; existing analyzer/package warnings remain.

## Remaining

This is targeted Web compatibility coverage, not a claim that every Desktop test
has Web semantics or that the browser DAW UI has been exercised. Preserve the
successful Web runtime while continuing track/routing commands, MIDI input and
recording, fuller device/plugin support and frontend host APIs. Native UI and the
historical callback-gap investigation remain deferred; physical-device qualification
remains distinct.
