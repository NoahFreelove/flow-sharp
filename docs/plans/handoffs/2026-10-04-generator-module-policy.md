# Ordinary generator import boundary — 2026-10-04

Ordinary DAW builds now install a source resolver before evaluating user code.
Only explicitly supported bundled API modules resolve; absolute paths, relative
paths, traversal-like bundled names and unsupported device modules fail at import.
Packaged plugins retain their pinned-dependency resolver and previous module set.
Ordinary builds additionally allow the bundled `@test` helper. General-purpose
FlowEngine resolution is unchanged.

This is an import boundary, not complete worker capability enforcement. Ordinary
generators still have unrestricted native signatures. A source can declare native
procedures itself, so denying imports alone cannot deny filesystem/device calls.
Do not claim worker sandboxing or completion of readiness work item 4.

Focused tests: 17 passed in `/tmp/flow-generator-modules-tests.log`. They reject a
real temporary module in both direct and isolated DAW builds, then import and call
it successfully in an independent general FlowEngine. Path/name rejection and
existing complete map/context and mixed score/audio generation are also covered.

Final affected backend/API regression: **530 passed**, zero failures/skips,
`/tmp/flow-generator-modules-regression.log`. `git diff --check` passed. Full-core,
browser and physical-device qualification remain open.

Next capability work: install a reviewed ordinary-generator native signature set,
retaining in-memory musical construction, transforms, patterns and offline DSP.
Review indirect native work as well as exported IO functions. `StyleRegistry`
loads user styles; `SampleCache.EnsureLoaded` probes and reads installed sample
files when song rendering dispatches sampled instruments. A pure-native catalog
alone is insufficient if allowed render paths still trigger uncontrolled IO.
Host asset resolution and tuning snapshots are still required. Keep ordinary
CLI/general FlowEngine behavior unchanged and test direct/process paths.
