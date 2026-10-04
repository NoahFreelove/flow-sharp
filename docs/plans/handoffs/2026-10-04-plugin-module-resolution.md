# Pinned module execution for Flow plugins — 2026-10-04

P7-16 executes declared module dependencies from their saved package bytes through
the isolated builder. This closes the dependency-execution rejection documented
in P7-15 for UTF-8 Flow modules; general asset/capability isolation remains open.

## Implemented

- `ModuleLoader.SourceResolver` is an optional host-owned provider of import
  identity and source text. When configured, it handles every import, including
  transitive imports, with no normal path/search-directory/filesystem fallback.
  Existing engines without a provider retain ordinary resolution.
- Generator requests optionally carry their plugin package through the bounded
  worker protocol. The child deserializes and verifies source/dependency hashes,
  checks source/entry identity, then installs the provider before evaluating code.
- Bare dependency IDs resolve only to the package's strict UTF-8 module contents.
  IDs are exact names, not filesystem paths; use `use "helper"` for ID `helper`.
  Transitive dependencies use the same flat namespace. Virtual source identities
  retain module caching/cycle handling and diagnostic attribution.
- Explicit supported `@` modules resolve to the current runtime's bundled APIs:
  core, collections, std, bars, flowDaw, audio, composition, patterns, generative,
  improv, notation and midi. An optional `.flow` suffix is accepted for these
  names. Source is cached per build. Traversal/unknown bundled names fail.
- Declared plugin builds now accept dependency snapshots instead of rejecting all
  nonempty packages. Builds still require the declared effect interface and use
  existing cancellation, process joining, latest-request acceptance and budgets.

## Verification

The real worker builds an effect whose graph helper exists only in pinned bytes.
Undeclared transitive imports fail without changing accepted state. Another test
creates a valid host file and verifies a plugin cannot import it by absolute path.
Existing package persistence, Flow export, undo and failed-build tests pass.

All 339 affected backend/module tests passed before the final host-file test
(`/tmp/flow-plugin-imports-regression.log`); all four focused plugin build tests
passed afterward (`/tmp/flow-plugin-imports-final.log`).

Full core gate passed: desktop solution build, **3,272** language/backend tests
and **21** MIDI tests, zero failures, 14 unexecuted/skipped main tests and no tracked
file mutations. Evidence: `/tmp/flow-plugin-imports-core/verification.json` plus
adjacent TRX/log files. Web publish and physical hardware gates were not rerun.

## Remaining and next

This is import control, not an OS sandbox or complete native-function capability
policy. A plugin may still invoke registered native APIs capable of external IO;
that access must be constrained before claiming default restricted plugin builds.
Bundled API source follows the installed runtime, not the package's dependency
pins. Runtime/API compatibility and fully hermetic rebuild guarantees remain open.
Dependencies here are UTF-8 Flow modules; binary asset binding needs its own typed
host contract instead of treating assets as modules.

Next establish the plugin worker's native capability policy, then add composable
signal primitives/parameter handles and required authored plugin examples. State
and tail continuity, instrument/event contracts, MIDI recording and integrated
workflow requirements remain. Native JUI frontend and historical callback-gap
investigation stay deferred; the full backend-readiness objective remains active.
