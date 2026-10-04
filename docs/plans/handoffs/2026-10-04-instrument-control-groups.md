# Shared instrument control groups — 2026-10-04

P7-30 adds an audio-layer update boundary for one instrument instance shared by
multiple track voice pools. Project discovery/gesture routing is still next.

## Implemented

PreparedInstrumentControlGroup owns references to 1–64 independent prepared note
pools. A single producer validates each target against every pool before publishing
one complete coalesced snapshot. Graph layouts may differ because commands compile
per pool. Failed validation leaves the earlier pending update intact. The existing
4,096-target budget remains; duplicate targets and duplicate pools reject.

PreparedGraphPlayback optionally owns up to 64 distinct control groups. It applies
pending group updates before reading any source bus, and before seeking any source.
This prevents a producer update arriving between track reads from reaching only
some tracks within the same parent block. Group application allocates nothing and
takes no locks. Each voice still retains target values across reset/steal.

Ownership contract: group-controlled pools must not also receive independent pool
setter updates. Groups must refer to pools owned by that parent playback; setup
happens before publication. ProjectCompiler wiring will enforce this construction
pattern. No public instrument live-control manifest relaxation is made yet.

## Evidence

A concurrent test publishes 10,000 updates while two independent track pools render
into a difference graph. Output remains exactly zero, proving neither track renders
a different control revision in the same parent block. It also checks invalid
values, seek/reset and allocation-free parent rendering. The first run exposed a
test assertion expecting ArgumentException rather than its actual exact subtype
ArgumentOutOfRangeException; the assertion was corrected without changing runtime
validation behavior.

All **378** affected backend/module tests passed, zero failures
(`/tmp/flow-control-group-regression.log`). `git diff --check` passed. Full core,
Web and physical qualification were not repeated for this slice.

## Next

Retain prepared pools by track, build control groups from shared instrument binding
IDs during project compilation, and route acknowledged-project parameter gestures
through these groups. Preserve last-good retirement, cancellation and captured
undo/redo. Continue sampler primitives and the remaining recording/device workflows.
Native frontend and historical callback-gap investigation remain deferred.
