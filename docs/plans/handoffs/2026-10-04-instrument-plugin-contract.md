# Instrument plugin acceptance contract — 2026-10-04

P7-27 extends declared plugin hosting from effects to graph instruments.

## Implemented

ProjectGeneratorHost accepts Instrument manifests through the same bounded isolated
worker, pinned dependencies/native policy, stale rejection and captured source
acceptance used by effects. A valid build returns exactly one instrument layer
matching the package output name, with no score/audio/effect layers. The instrument
must contain a prepared voice graph; legacy sine/sampler settings are not accepted
as graph plugin implementations.

PluginInstrumentContract reuses public-ID target validation and immutable value
application. Internal graph input buses are pitch/gate/velocity (0/1/2), independent
of the manifest's zero external audio inputs and note input declaration. Input bus
indices cannot become public controls. Node defaults, ranges, units, unique target
ownership and declared/bound parameter coverage remain validated.

Every instrument public control must currently declare RequiresRebuild=true.
Live voice-pool parameter publication is not implemented; misleading live-control
metadata rejects. Source-instance values persist through existing project schema
and undo/redo. ProjectCompiler validates selected instrument packages and applies
all saved values to the immutable graph before preparing all voices. The original
accepted default graph and pinned source remain unchanged. Hosts use the existing
ProjectPluginCommands plus preparation request for committed control changes.
Effect live previews retain their existing behavior.

## Evidence

All ten PluginBuildTests passed (`/tmp/flow-instrument-plugin.log`). The new test
builds an instrument through the actual host/worker, accepts the package, edits a
public value, saves/reopens, verifies immutable defaults and applied values, routes
the instrument through ProjectCompiler, checks undo/redo, and rejects a false
live-control declaration and an effect/instrument output-role mismatch.
`git diff --check` passed. Previous full core/Web evidence is recorded in the
[instrument compatibility checkpoint](2026-10-04-instrument-compatibility.md);
it predates this change.

All **374** affected backend/module tests passed with zero failures
(`/tmp/flow-instrument-plugin-regression.log`).

## Next

Add a saved instrument plugin example and stronger rendered parameter parity,
then host-facing control gestures and coherent live instrument parameter updates.
Continue sampler graph primitives, public automation/device instances/presets,
asset capabilities, MIDI input/basic recording and integrated workflows. Native
JUI frontend and historical callback-gap diagnosis remain deferred. No physical
real-time qualification or full backend-readiness claim is made here.
