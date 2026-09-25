# Phase 4 complete — 2026-09-24

Continue with roadmap **Phase 5 — extract the music model and efficient offline
renderer**. Phases 0–4 are complete. Read `CLAUDE.md`, `docs/TESTING.md`, the
restructuring roadmap, progress ledger and session-lifetime decision first.

## Completed Phase 4

`33c7207` shares import-aware analysis with editor tooling. `c7476b5` adds parsed-tree
evaluation, coded results/HostFailure and explicit CLI/browser session adapters.
`85725de` preserves suffixed stdlib imports and removes retired private browser
mappers. The original frontend/metadata slices were `ab5ea71` and `39eb9bc`.

The all-tier gate passed 3,012 main + 21 MIDI tests, 19 skips, no failures and no
tracked mutations. Follow-up targeted tests passed. Real LSP completion/hover ran
under strace without sample/device/native audio access. The trimmed Web bundle
booted through the unchanged JS adapter: fresh repeated sessions, located parse
errors and music arithmetic all passed. See `docs/baselines/phase4/README.md`.

Analysis is deliberately conservative: syntax and top-level imports are checked;
lexical-name and source-procedure call concerns are warnings. Dynamic values,
host-specific overloads/defaults, non-literal inference, nested imports and domain
expansion remain runtime checks. Do not describe this as a complete static type
checker. The API decision records these limits, which do not block the Phase 4
gate. Browser JS has no new analysis export; the website bundle was not refreshed.

## Phase 5 starting points

The roadmap requires evaluated composition snapshots separate from executable
section definitions; timing/provenance/stable IDs/render options; shared note
transforms; MIDI import/export model audit; chunked output/progress/cancellation
and explicit memory ownership; preserved rendering baselines.

Start by inventorying `flow-lang/Music`, `TypeSystem/SpecialTypes`, section/song
runtime values, `StandardLibrary/Audio` renderers and `flow-midi`. Establish the
snapshot/render API and a non-Flow host proof before broad renderer migration.
Preserve musical context, tuplets, overlap, articulation, tuning and tempo changes.
Measure long-song allocation/copy behavior and remove quadratic accumulation.
Consult `docs/decisions/2026-09-22-session-lifetime.md` for the remaining renderer
ambient-session adapters to replace with explicit render contexts.

## Working rules

Commit each slice and update `docs/plans/progress/flow-restructuring.md`. Stage
explicit paths and use `Co-Authored-By: Codex <noreply@openai.com>`; do not push.
Never hand-edit frozen WASM JavaScript. Keep semantic fixes separate from
extraction and review every baseline change. Run full suites serially, including
across checkouts (legacy fixtures share fixed `/tmp` WAV files). Do not edit tracked
files while the verifier hashes them. Two ignored local Flow fixtures explain
working-checkout versus clean-clone counts; leave them alone. New `.flow` and some
Markdown files may require explicit force-add because of broad ignore patterns.
