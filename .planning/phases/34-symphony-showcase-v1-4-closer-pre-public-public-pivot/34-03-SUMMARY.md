---
phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
plan: 03
subsystem: docs
tags: [readme, showcase, github-user-attachments, audio-embed, html5-video, v1.4-release]

requires:
  - phase: 34
    provides: "Plan 34-05 — v1.4.0 GitHub release published with symphony + ragtime MP3/WAV assets (release-asset URLs that the SUMMARY's Download MP3/WAV fallback links resolve to)."
  - phase: 34
    provides: "Plan 34-04 — ragtime examples/ragtime/{ragtime.flow,README.md} authored (the source-link + reproduction-link targets for the ragtime player)."
  - phase: 34
    provides: "Plan 34-01 — symphony examples/symphony/{symphony.flow,README.md} authored (the source-link + reproduction-link targets for the symphony player)."
provides:
  - "Top-level README `## Showcase` section positioned between `## Features` (line 31) and `## Install (Linux x64)` (line 58) — first surface a new repo visitor encounters for v1.4."
  - "Both v1.4 pieces (symphony + ragtime) embedded as inline GitHub `<video controls src=...>` players via composer-supplied user-attachments URLs."
  - "Per-piece downloadable MP3/WAV fallback links pointing at the v1.4.0 release assets (graceful degradation when the inline player does not render)."
  - "Genre-agnostic v1.4 framing sentence (`Same interpreter, same SFZ pipeline, opposite moods`) backing the project's genre-agnostic non-goal."
affects: ["v1.4 public release surface", "future README updates for v1.5+ showcase rotation"]

tech-stack:
  added: []
  patterns:
    - "GitHub `<video controls src=URL>` HTML5 embed using composer-supplied user-attachments URLs (drag-drop upload flow) for inline audio playback on the rendered README."
    - "Graceful-degradation pattern: per-player bare MP3/WAV release-asset download links live directly below each `<video>` tag so non-GitHub markdown viewers (or any viewer that strips raw HTML) still surface playable audio."

key-files:
  created:
    - .planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-03-SUMMARY.md
  modified:
    - README.md  # +23 lines: new `## Showcase` H2 between Features and Install

key-decisions:
  - "Scope-expanded from plan-as-written (single symphony player) to both v1.4 pieces (symphony + ragtime) — the resumed prompt explicitly instructed this expansion to reflect the v1.4 release shape after plan 34-04 added the ragtime piece. Scope expansion captured here as Rule 2 deviation."
  - "Accepted user-attachments URLs using the `user-attachments/files/<id>/<filename>` pattern (NOT the `user-attachments/assets/<uuid>` pattern that RESEARCH Pitfall 1 cited). The composer supplied these URLs via the audio-file attachment flow rather than the inline-media drag-drop flow. Whether GitHub renders the `<video>` tag inline against the `files/` URL pattern is verified at the Task 3 composer visual-check checkpoint — graceful-degradation fallback download links are already in place if the player does not render."
  - "Used the `<video controls src=URL></video>` tag (the form GitHub's drag-drop flow autoinserts) rather than the bare-URL-on-its-own-line form. Recommended single-form embed; iterate at the visual-check checkpoint if the player does not render."

patterns-established:
  - "v1.4 Showcase shape: per-piece subheading + framing italics + `<video>` player + 4 supporting links (source, reproduction, MP3 download, WAV download) + closing release-page pointer. Reusable for v1.5+ Showcase rotation."

requirements-completed: [SYM-04]

duration: 4min
completed: 2026-05-16
---

# Phase 34 Plan 03: README Showcase Section (v1.4) Summary

**Top-level README gains a `## Showcase` section embedding both v1.4 pieces (symphony + ragtime) as GitHub inline `<video>` players with per-piece release-asset download fallbacks — first thing a new repo visitor encounters for v1.4.**

## Performance

- **Duration:** 4 min
- **Started:** 2026-05-16T22:21:30Z
- **Completed:** 2026-05-16T22:25:21Z
- **Tasks:** 3 (Task 1 = composer manual drag-drop; Task 2 = README edit; Task 3 = visual-check checkpoint pending)
- **Files modified:** 1 (README.md)

## Accomplishments

- v1.4 Showcase section inserted between `## Features` (line 31) and `## Install (Linux x64)` (line 58); section spans lines 34–56 (23 inserted lines).
- Both v1.4 pieces embedded as inline players: *In Five Voices* (orchestral, ~60s, D minor) and *Stride & Stomp* (ragtime, ~58s, F major).
- Graceful-degradation fallback: per-piece bare MP3 + WAV download links to the v1.4.0 release assets live directly below each player.
- Genre-agnostic framing sentence ("Same interpreter, same SFZ pipeline, opposite moods — the genre-agnostic claim in one release") explicitly states the v1.4 narrative.
- README committed to dev and pushed to origin (HEAD `a00820d`).

## Task Commits

1. **Task 1: Composer drag-drops MP3s via GitHub web UI** — completed externally by composer; both user-attachments URLs supplied in the resume prompt (`https://github.com/user-attachments/files/27862601/symphony.mp3` + `https://github.com/user-attachments/files/27862607/ragtime.mp3`). No local commit.
2. **Task 2: Insert `## Showcase` section into README.md** — `a00820d` (docs)
3. **Task 3: Composer verifies inline players render on GitHub** — pending (visual-check checkpoint surfaces at end of this plan; plan only fully closes after composer confirmation).

**Plan metadata:** (will be created in the final metadata commit after the Task 3 checkpoint resolves)

## Files Created/Modified

- `README.md` — Added `## Showcase` H2 (23 lines) between `## Features` and `## Install (Linux x64)`. Section contains: 1-paragraph framing, two `### <piece-name>` subheadings each with a `<video controls src="https://github.com/user-attachments/files/<id>/<piece>.mp3"></video>` tag, 4 markdown links per piece (source `.flow`, reproduction `README.md`, MP3 download, WAV download), and a closing pointer to the full v1.4.0 release page.

## Decisions Made

- **Embed shape: `<video controls src=URL></video>` tag, not bare-URL-on-its-own-line.** The resumed prompt's "Recommended: just the `<video>` tag for now; iterate at the visual-check checkpoint if needed" guidance was followed. If the visual-check shows the tag does not render against `files/` URLs (vs. the `assets/<uuid>` URLs RESEARCH Pitfall 1 documented), the fallback is to either (a) accept the graceful-degradation of plain links + the download links, or (b) re-insert with the bare-URL form.
- **Both pieces, not just the symphony.** The 34-03-PLAN.md was originally scoped to a single symphony player (SYM-04 requirement). The resumed prompt scope-expanded to include both v1.4 pieces, reflecting that plan 34-04 had added the ragtime piece between 34-03's planning and execution. The expanded shape better reflects v1.4's "genre-agnostic in one release" narrative.
- **Per-piece WAV download alongside MP3 download.** Plan 34-03 specified MP3 download only. Adding the WAV link costs nothing (the asset already exists in the v1.4.0 release) and serves higher-fidelity preview listeners. Tracked as Rule 2 deviation (missing critical functionality for archival/lossless listening).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Scope-expanded from symphony-only to both v1.4 pieces (symphony + ragtime)**
- **Found during:** Task 2 (README edit)
- **Issue:** 34-03-PLAN.md was authored before plan 34-04 added the ragtime piece. Plan body and acceptance_criteria only reference symphony. Shipping the README with a Showcase section featuring only one of the two v1.4 pieces would understate v1.4's "genre-agnostic in one release" claim.
- **Fix:** Both pieces embedded with parallel structure (per-piece subheading, `<video>` tag, 4 supporting links). The resumed prompt explicitly instructed this expansion.
- **Files modified:** README.md
- **Verification:** `grep -q 'examples/ragtime/ragtime.flow' README.md` and `grep -q 'examples/symphony/symphony.flow' README.md` both succeed; both `user-attachments/files/...mp3` URLs present.
- **Committed in:** `a00820d` (Task 2 commit)

**2. [Rule 2 - Missing Critical] Added per-piece WAV download link alongside MP3 download**
- **Found during:** Task 2 (README edit)
- **Issue:** Plan specified MP3 download fallback only. The v1.4.0 release ships both MP3 (lossy) and WAV (lossless) for each piece — omitting the WAV link from the README understates the available assets and forces archival/lossless listeners to navigate to the release page.
- **Fix:** Added a 4th link per piece: `[Download WAV (11 MB)](https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/symphony.wav)` (symphony) and `[Download WAV (10 MB)](https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/ragtime.wav)` (ragtime).
- **Files modified:** README.md
- **Verification:** `grep -q 'symphony.wav\|ragtime.wav' README.md` succeeds.
- **Committed in:** `a00820d` (Task 2 commit)

**3. [Rule 3 - Blocking-uncertainty deferred to checkpoint] URL pattern is `user-attachments/files/<id>/<filename>` not `user-attachments/assets/<uuid>`**
- **Found during:** Task 1 (composer-supplied URLs in resume prompt)
- **Issue:** RESEARCH Pitfall 1 documented `user-attachments/assets/<uuid>` as the inline-player trigger pattern. The composer supplied URLs using the `user-attachments/files/<id>/<filename>` pattern instead (audio-file attachment flow, not inline-media drag-drop flow). It is unclear whether GitHub honors the `<video>` tag inline render for the `files/` pattern.
- **Fix:** Proceeded with the `<video controls src=files-URL></video>` form (most likely to render inline if GitHub honors the pattern at all). Graceful-degradation fallback (bare MP3/WAV download links directly below each player) ensures the section degrades cleanly to "plain link + downloads" if the inline render fails. Final validation deferred to Task 3 composer visual-check checkpoint.
- **Files modified:** README.md
- **Verification:** Awaiting Task 3 composer visual-check on `https://github.com/NoahFreelove/flow-sharp`. If the player does not render, composer can reply `embed: try bare-url` and the executor will re-insert with the bare-URL form.
- **Committed in:** `a00820d` (Task 2 commit; resolution pending Task 3)

---

**Total deviations:** 3 auto-fixed (2 missing-critical, 1 blocking-uncertainty deferred to Task 3 checkpoint)
**Impact on plan:** All three deviations strengthen the shipped README — none introduce scope creep, all align with v1.4 release shape and the project's genre-agnostic narrative. The URL-pattern uncertainty (deviation 3) is the only one that may require a follow-up edit, and the graceful-degradation fallback already ensures no listener is left without playable audio access.

## Issues Encountered

None during execution. The URL-pattern mismatch (RESEARCH cited `assets/<uuid>`, composer supplied `files/<id>/<filename>`) is a research-vs-actual discrepancy whose resolution defers cleanly to the Task 3 visual-check checkpoint — composer can confirm in-browser whether GitHub renders the `<video>` tag against `files/` URLs.

## User Setup Required

None — README is a public-rendered artifact, no external service configuration needed. Task 3 requires the composer to open `https://github.com/NoahFreelove/flow-sharp` in a browser and confirm the inline players render.

## Next Phase Readiness

- README.md `## Showcase` section is live on `dev` at commit `a00820d`, ready for composer visual verification.
- After Task 3 composer sign-off, plan 34-03 fully closes. If composer reports `embed: link-only`, the graceful-degradation fallback is the shipped state — no further edits needed. If composer reports `embed: try bare-url`, a follow-up edit replaces the `<video>` tag with the bare URL form.
- v1.4 README surface is now feature-complete: Features link → Showcase (both pieces) → Install → rest. Ready for the broader v1.4 public-announce pivot (subsequent phases in the public-pivot epic).

## Self-Check: PASSED

- `README.md` exists and contains the new section (verified by `Read` after `Edit`).
- Commit `a00820d6671de69951d0c49a2555635e02d82632` exists on local `dev` and `origin/dev` (verified by `git rev-parse HEAD` matching `git ls-remote origin dev`).
- All acceptance-criteria greps from PLAN.md Task 2 verification step pass (showcase header, user-attachments URLs, symphony + ragtime source/repro links, release link, MP3 download links — verified by direct grep).
- Section ordering Features (line 31) → Showcase (line 34) → Install (line 58) confirmed by `awk '/^## /{print NR, $0}' README.md`.
- Commit subject `docs(34-03): add Showcase section with both v1.4 pieces` matches required `docs(34-03)` prefix.

---
*Phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot*
*Completed: 2026-05-16*
