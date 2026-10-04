# Expanded project graphs and curves — 2026-10-04

P7-12 expands effects and automation in the executable project export. The overall
backend-readiness goal remains active.

## Implemented

- Project export now reconstructs every accepted effect graph through the existing
  `dawInput`, `dawDevice` and mixing constructors. Device IDs/versions, parameters,
  connections and bypass are visible Flow code, including independently named
  graph variables that avoid collisions across outputs.
- Resource seed data retains each stable output slot with an inert placeholder;
  explicit `dawProjectGraph` calls install the reconstructed definitions. These
  intermediate project values are pure construction data and are never published
  to an audio session. Graph definitions no longer hide solely in snapshot data.
- `ProjectGraphConstruction.Replace` preserves source/result identity, historical
  source code/context and output binding IDs while replacing one graph definition.
  It is a construction API, not source-code regeneration or a live edit command.
  Changing an exported parameter therefore changes the reconstructed graph without
  pretending to rewrite the historical generator code.
- Project automation is omitted from the seed and emitted through `dawCurve`
  declarations. `dawProjectCurves` installs the complete validated curve list before
  the final batched clip construction. The standalone curve exporter is shared.

## Verification

The initial focused project-export/graph test run passed **12 tests**. Additional
coverage executes multiple graphs with reused node IDs, checks exact project
reconstruction and edits an exported gain parameter to verify the reconstructed
value changes. Existing project export coverage includes automation and exact
prepared playback. Initial log: `/tmp/flow-expanded-graphs.log`.

The final affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting run
passed **267 tests**, 0 failed. Log: `/tmp/flow-expanded-project-regression.log`.
Existing analyzer warnings remain; `git diff --check` passed. No full-solution run
or Web publish was performed.

## Remaining

Scores, instruments, routing, dependency metadata and asset data still use the
versioned snapshot seed. Expand their public construction APIs before claiming the
fully expanded authoring export complete. Portable asset packaging and large-output
worker transfer remain open. Native frontend and historical callback-gap diagnosis
remain deferred; no hardware capture was performed.
