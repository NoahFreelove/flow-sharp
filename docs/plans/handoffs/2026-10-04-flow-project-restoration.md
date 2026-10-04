# Executable Flow project restoration — 2026-10-04

P7-10 / P8-10 adds a functional project reconstruction bridge. This preserves the
implemented project state but is not yet the final fully expanded authoring export.
The overall backend-readiness goal remains active.

## Implemented

- Flow `DawProject` and `dawProjectSnapshot` restore versioned detached project
  resource data using the same validated project codec. Loading does not execute
  saved generator source or open external files. External paths/hashes remain
  references for the preparation host to resolve later.
- Public `dawPlaceScore` and `dawPlaceAudio` append immutable clip definitions while
  preserving sources, routing, assets and automation. Their fields cover stable
  IDs, source windows, musical anchors and millisecond nudges. Normal arrangement
  validation rejects duplicate IDs and invalid geometry.
- `FlowProjectExporter` emits an initial resource snapshot plus explicit clip
  placement statements. It preserves accepted content, including editable ownership,
  sampler PCM and historical generation context, without regenerating music.
- Audio frame offsets/lengths use decimal-string arguments because Flow's ordinary
  Int is 32-bit and Double cannot represent all Int64 values. This is verbose but
  exact. Floating values use the existing Flow-compatible numeric exporter.
- Export is bounded to 64 Mi characters. This is a source-output limit, not a
  guarantee that every host execution budget permits such a large script. Each
  clip call rebuilds immutable project state; large-project export evaluation still
  needs batching/performance qualification.

## Verification

Both focused export tests passed: complete serialized project equality and exact
prepared audio after executing the export, plus exact frame counts beyond Double
integer precision. The fixture includes editable content, automation and invalid
saved generator text to prove restoration does not evaluate stored code. Escaped
quotes, backslashes, newlines and Unicode survive. Log: `/tmp/flow-project-export.log`.

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting regression
run passed **265 tests**, 0 failed. Log: `/tmp/flow-project-export-regression.log`.
Existing analyzer warnings remain; `git diff --check` passed. No full-solution run
or Web publish was performed.

## Remaining

The initial resources are snapshot data, not fully expanded score/device/routing
construction statements. Shared graph and curve exporters already exist separately;
compose them into the final authoring export and add editable-note constructors.
This restoration bridge must not be treated as completion of that broader gate.
Portable asset packaging, dependency pins, recording/modulation, additional effects,
quality resampling and full integrated qualification remain open. Native frontend
and historical callback-gap diagnosis remain deferred; no hardware capture occurred.
