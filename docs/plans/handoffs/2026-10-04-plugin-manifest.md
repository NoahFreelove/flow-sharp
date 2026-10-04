# Static Flow plugin manifest contract — 2026-10-04

P7-14 establishes detached discovery metadata and validates an effect graph against
its declared interface. This is a foundation for public plugin authoring, not a
claim that arbitrary Flow instruments/effects are now supported.

## Implemented

`PluginManifest` in the studio model parses strict, bounded JSON without invoking
the interpreter, opening files, resolving dependencies, or preparing audio.

- API version 1; stable plugin ID/version, name, author/license, Flow builder name,
  SHA-256 of exact UTF-8 source, plugin kind, stereo audio bus counts, note ports,
  sample-rate/block limits, state schema and explicit reset policy.
- Up to 256 parameters: stable ID separate from label, unit, range/default,
  linear/logarithmic/enumeration scaling, smoothing, reconstruction requirement
  and detached enum labels. API-v1 normalized mapping is explicit; stored values
  and automation remain in typed parameter units. Enum indices start at Minimum.
- Up to 256 dependency declarations with exact version and content hash. Explicit
  validation accepts supplied bytes; discovery never reads dependency locations.
- Required constructor fields, unknown fields, duplicates, invalid enums, missing
  values, invalid port combinations, nonfinite/degenerate ranges and excessive
  input size are rejected. Manifest limit is 1 Mi characters; source limit 2 Mi.
- The initial state policy is reset only. State migration/persisted user DSP state
  is not implied by recording a schema name. Audio ports currently use up to 64
  stereo input buses and one stereo output; note transforms have note ports only.

`PluginEffectContract` validates an already-built effect graph off the audio thread:

- Connected DAG validation and exact dense input bus agreement with the manifest.
- Up to 4096 direct parameter targets, stable public ID → node/parameter identity.
  Multiple targets per public parameter are allowed, duplicate target ownership
  is rejected, and every declared parameter must have a target.
- Declared units/range/default, smoothing and reconstruction behavior must agree
  with the target device. Input bus indices cannot be public parameters. Integer
  delay repeat counts require discrete enum metadata.
- This mapping is direct in typed units. It is not yet the general signal-handle
  layer required for user-composed modulation/DSP.

## Verification

Four new tests verify no-execution discovery using deliberately invalid Flow
source, stable serialization, exact source/dependency pins, immutable labels and
bindings, missing/unknown/duplicate rejection, numeric boundary validation,
normalized range mappings, renamed display labels preserving identity, and graph
port/parameter mismatches.

All **336** affected backend/module regression tests passed:
`/tmp/flow-plugin-manifest-regression.log`. A final numeric guard tightening and
its regression passed all four focused tests (`/tmp/flow-plugin-manifest-final.log`).
Full core/Web/hardware gates were not repeated for these model-only additions;
prior full core evidence remains in the transport-preservation handoff.

## Next

Connect manifest/source/dependency verification to the isolated plugin build and
project snapshot path. Implement stable parameter handles and composable signal
primitives, then the required public Flow synth, sampler and composed-effect
examples. Instrument/event build validation is still open. Current generator
source descriptors and saved project schemas are unchanged; plugin definitions
are not yet embedded in project files by these new model types alone.

Continue tail/state lifecycle, MIDI input/recording and integrated project/export
workflows afterward. Native JUI frontend and historical callback-gap investigation
remain deferred; backend readiness is not complete.
