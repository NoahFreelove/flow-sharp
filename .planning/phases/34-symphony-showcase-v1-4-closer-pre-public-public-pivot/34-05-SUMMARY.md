---
phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
plan: 05
subsystem: release-publish
tags: [release, gh-release, tag, publish, v1.4, public-pivot, distribution]

requires:
  - phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
    provides: "Plan 34-02 committed canonical examples/symphony/symphony.flow + examples/ragtime/ragtime.flow + their READMEs; plan 34-04 committed docs/announcements/v1.4.0.md (the release body); plan 34-01 left examples/output/{symphony,ragtime}.wav as the canonical renders; Phase 30 scripts/publish.sh produces the self-contained Linux x64 binary"
provides:
  - "Annotated git tag v1.4.0 on dev HEAD (74de69adb47b2a23985633a392f6ddb6f1389f21), pushed to origin"
  - "GitHub Release v1.4.0 published with 5 labeled assets (2 MP3 + 2 WAV + 1 Linux tarball), body sourced verbatim from docs/announcements/v1.4.0.md"
  - "Public release page URL https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0 — stable anchor for plan 34-03 (top-level README embed) and plan 34-06 (milestone closure docs)"
  - "5 publicly downloadable asset URLs under https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/ matching the link pattern already baked into docs/announcements/v1.4.0.md"
affects: [34-03-PLAN, 34-06-PLAN, public-distribution, downstream-installer-docs]

tech-stack:
  added: []
  patterns:
    - "Annotated tag + one-shot gh release create with `#\"label\"` asset suffixes — RESEARCH Priority 4 / PATTERNS § G"
    - "5-asset shape (dual showcase MP3+WAV pair + 1 Linux self-contained tarball) — expansion of the original PLAN's 3-asset shape, approved at the preflight checkpoint per the composer's resume signal"
    - "Release assets staged in /tmp/ then uploaded via `gh release create` — never committed to the repo (D-502/D-503 enforcement)"

key-files:
  created: []
  modified: []
  # No repo file changes — all artifacts live on GitHub (git tag + release page + 5 CDN assets)

key-decisions:
  - "Honored the composer's preflight-approved 5-asset shape (not the original PLAN's 3-asset shape): symphony.mp3 + symphony.wav + ragtime.mp3 + ragtime.wav + flow-linux-x64.tar.gz. Filenames intentionally do NOT carry the `flow-` prefix or `-v1.4.0` suffix the PLAN used — they match the link patterns already baked into docs/announcements/v1.4.0.md (`/releases/download/v1.4.0/symphony.mp3` etc.), so the release body's links resolve immediately without an edit."
  - "Annotated tag (not lightweight) per CLAUDE-locked convention. Tag object SHA 66842d6efafd5105c82521c07b977dd1113504d1; tagged commit SHA 74de69adb47b2a23985633a392f6ddb6f1389f21 (dev HEAD)."
  - "Tag message body covers the full Phase 28-34 v1.4 arc and names both showcase pieces — matches the resume-instruction wording verbatim."
  - "Used canonical Phase 30 wrapper `bash scripts/publish.sh` (not a bespoke dotnet publish invocation) — guarantees identical flag set, stdlib bundling, and SPEC-2 size budget enforcement (40 MB output, well under the 120 MB cap)."
  - "Tarball directory shape matched the wrapper's `publish/flow-linux-x64/` output as-is — no contingency rewrite needed."
  - "`--verify-tag` flag enforced: the release-create would have aborted if the tag was missing from origin. Tag was pushed first, so verify-tag passed silently."
  - "STATE.md / ROADMAP.md / requirements were intentionally NOT touched per resume-instruction — orchestrator owns those writes for this plan."
  - "Halted at Task 5 (`checkpoint:human-verify gate=\"blocking\"`) as instructed — published release is now public, awaiting composer review before plans 34-03 (top-level README + drag-drop embed) and 34-06 (milestone closure docs) consume the release URL."

patterns-established:
  - "Pattern: per-showcase-piece dual-format asset pair (MP3 for streaming bandwidth + WAV for archival fidelity) — repeatable for future v1.X releases that ship multiple curated pieces"
  - "Pattern: release-asset filenames lock to the URL pattern the announcement file pre-bakes — composer / planner / executor must agree on the URL shape BEFORE either the announcement is drafted or the release is created"

requirements-completed: [SYM-05]

duration: ~2 min (publish.sh build dominated)
completed: 2026-05-16
---

# Phase 34 Plan 05: Cut v1.4.0 Tag + Publish GitHub Release Summary

**Annotated v1.4.0 tag pushed to origin at dev HEAD 74de69a; GitHub Release v1.4.0 published with 5 labeled assets (symphony.mp3/wav, ragtime.mp3/wav, flow-linux-x64.tar.gz) and the docs/announcements/v1.4.0.md announcement as the body — public release page now live at https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0, ready for plan 34-03's top-level README embed and plan 34-06's milestone closure docs.**

## Performance

- **Duration:** ~2 min (dominated by `scripts/publish.sh` self-contained build + asset uploads)
- **Started:** 2026-05-16T20:47:42Z
- **Completed:** 2026-05-16T20:49:48Z (pre-final-checkpoint halt)
- **Tasks completed:** 4 / 5 (Task 5 is a `checkpoint:human-verify gate="blocking"` — halted as instructed)
- **Files created/modified in repo:** 0 (all artifacts live on GitHub: tag + release page + 5 CDN assets)
- **Commits added to dev:** 1 (this SUMMARY only — no per-task code commits per the no-repo-changes plan shape)

## Accomplishments

### Step 1 — Assets staged + encoded in `/tmp/`
- Copied `examples/output/symphony.wav` → `/tmp/symphony.wav` (11 MB / 11,007,404 bytes).
- Copied `examples/output/ragtime.wav` → `/tmp/ragtime.wav` (9.7 MB / 10,160,684 bytes).
- Encoded `/tmp/symphony.mp3` via `ffmpeg -c:a libmp3lame -b:a 192k` (1.5 MB / 1,499,053 bytes).
- Encoded `/tmp/ragtime.mp3` via `ffmpeg -c:a libmp3lame -b:a 192k` (1.4 MB / 1,383,696 bytes).

### Step 2 — Phase 30 Linux binary built + tarred
- Ran `bash scripts/publish.sh` (canonical Phase 30 wrapper).
- Output: `publish/flow-linux-x64/` containing `flow` self-contained executable + 6 bundled .flow stdlib files + 4 DryWetMidi native runtime libs.
- Publish size: 40 MB (SPEC-2 budget: 120 MB).
- Smoke test: `./flow version` reported `flow 0.1.0-phase30+74de69adb47b2a23985633a392f6ddb6f1389f21` — built from the same commit being tagged.
- Tarred via `tar -czvf /tmp/flow-linux-x64.tar.gz -C publish flow-linux-x64` (34 MB / 35,398,037 bytes compressed).

### Step 3 — Annotated tag created + pushed
- `git tag -a v1.4.0 -m "v1.4 Audio Fidelity, Distribution & Public Showcase\n\nPhases 28-34: per-voice polyphony + articulation envelopes (Phase 28), sampled tonal instruments (Phase 29), self-contained CLI + install + XDG config (Phase 30), LSP polish + JetBrains plugin scaffolding (Phase 31), full Scala (.scl) microtonal tuning loader (Phase 32), SFZ orchestral sampler with VSCO Community CE 1.1.0 (Phase 33), and the v1.4 showcase pieces (In Five Voices orchestral + Stride & Stomp ragtime) flipping Flow from pre-public to public."`
- Tag object SHA: `66842d6efafd5105c82521c07b977dd1113504d1` (annotated — `objecttype=tag`, not `commit`).
- Tagged commit SHA: `74de69adb47b2a23985633a392f6ddb6f1389f21` (dev HEAD, matching the resume-instruction's named commit).
- `git push origin v1.4.0` succeeded: `* [new tag]         v1.4.0 -> v1.4.0`.
- Remote verification: `git ls-remote --tags origin v1.4.0` returned `66842d6efafd5105c82521c07b977dd1113504d1\trefs/tags/v1.4.0`.

### Step 4 — GitHub Release created with 5 labeled assets
- `gh release create v1.4.0 --title "v1.4 Audio Fidelity, Distribution & Public Showcase" --notes-file docs/announcements/v1.4.0.md --verify-tag <5 assets>` returned the canonical URL.
- All 5 assets uploaded cleanly (`state: "uploaded"` per `gh release view --json assets`):

| Asset                    | Label                                                          | Size (bytes) | Content-Type      |
|--------------------------|----------------------------------------------------------------|--------------|-------------------|
| `symphony.mp3`           | In Five Voices (orchestral) — MP3, 192 kbps, ~1.5 MB           | 1,499,053    | audio/mpeg        |
| `symphony.wav`           | In Five Voices (orchestral) — WAV, uncompressed, ~11 MB        | 11,007,404   | audio/vnd.wave    |
| `ragtime.mp3`            | Stride & Stomp (ragtime) — MP3, 192 kbps, ~1.5 MB              | 1,383,696    | audio/mpeg        |
| `ragtime.wav`            | Stride & Stomp (ragtime) — WAV, uncompressed, ~10 MB           | 10,160,684   | audio/vnd.wave    |
| `flow-linux-x64.tar.gz`  | Flow CLI binary — Linux x64, self-contained                    | 35,398,037   | application/x-gtar |

- Release flags verified: `isDraft=false`, `isPrerelease=false`, `tagName=v1.4.0`, `name="v1.4 Audio Fidelity, Distribution & Public Showcase"`.
- Body upload verified: `gh release view v1.4.0 --json body` returns the full announcement Markdown with all 5 asset download links (`/releases/download/v1.4.0/{symphony,ragtime}.{mp3,wav}`) resolving correctly because the release filenames match the URL pattern pre-baked in plan 34-04's announcement draft.

### Step 5 — Release URL captured for downstream consumers
- Release page: `https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0`
- Per-asset download URLs (for plan 34-03's top-level README embed + plan 34-06's milestone closure docs):
  - `https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/symphony.mp3`
  - `https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/symphony.wav`
  - `https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/ragtime.mp3`
  - `https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/ragtime.wav`
  - `https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/flow-linux-x64.tar.gz`

## Task Commits

This plan changes no repository files (all artifacts live on GitHub: tag, release page, 5 CDN assets), so there are no per-task code commits. The only commit added to `dev` is this SUMMARY itself:

1. **SUMMARY commit** — see the final commit below for SHA. Subject: `docs(34-05): summarize v1.4.0 tag + GitHub Release publish`.

## Files Created/Modified

None in the working tree.

GitHub-side artifacts created:
- Annotated tag `v1.4.0` (object `66842d6e`, pointing at commit `74de69ad`) on origin.
- GitHub Release `v1.4.0` with 5 uploaded assets and body sourced verbatim from `docs/announcements/v1.4.0.md`.

## Decisions Made

See `key-decisions` in frontmatter above. Most consequential:
- **5-asset shape vs original 3-asset shape:** Honored the composer's preflight approval — dual showcase pieces (orchestral + ragtime) each got both MP3 + WAV, plus the Linux self-contained tarball. The asset filenames intentionally match the URL pattern that plan 34-04's announcement already pre-baked, so the release body's download links work without any post-publish edit.
- **STATE.md / ROADMAP.md / requirements left untouched:** Per resume-instruction. The orchestrator owns those writes for plan 34-05.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `gh release view --json isLatest` not exposed in installed gh CLI version**
- **Found during:** Step 4 verification
- **Issue:** Original PLAN's `<automated>` verifier and `<acceptance_criteria>` reference `gh release view v1.4.0 --json isLatest --jq '.isLatest'`. The installed gh CLI version on this host (2.46.0 per RESEARCH) does NOT expose `isLatest` as a JSON field — `gh release view --json isLatest` errors with `Unknown JSON field: "isLatest"` and lists the available fields.
- **Fix:** Verified the equivalent invariants via the available fields: `isDraft=false` + `isPrerelease=false` + the canonical release URL `https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0`. Per `gh release create` defaults (RESEARCH Priority 4), the release is implicitly marked latest because it is not a draft, not a pre-release, and is the newest published release on the repo. Composer can visually confirm the "Latest" badge on the release page in Step 5.
- **Files modified:** none
- **Commit:** none (verification-only adjustment)

### Plan-Shape Deviation Honored (Not Auto-Fix — Pre-Approved at Preflight)

The shipped release uses a 5-asset shape instead of the original PLAN's 3-asset shape (which only included one MP3 + one WAV + the binary). This is a pre-approved deviation per the preflight `checkpoint:human-verify` resume signal — composer explicitly approved the dual-showcase expansion to match plan 34-04's pre-baked announcement-body link pattern. Documented here for traceability; not auto-fixed mid-execution.

## Verification

### Automated checks passed
- `git tag --list v1.4.0` returns `v1.4.0`.
- `git for-each-ref refs/tags/v1.4.0 --format='%(objecttype)'` returns `tag` (annotated, not lightweight).
- `git ls-remote --tags origin v1.4.0` returns the tag-object SHA on remote.
- `gh release view v1.4.0 --json assets --jq '.assets | length'` returns `5`.
- All 5 asset filenames present in `gh release view v1.4.0 --json assets --jq '.assets[].name'`.
- All 5 assets in `state: "uploaded"`.
- `gh release view v1.4.0 --json body` returns a non-empty body matching the announcement draft.

### Pending human verification (Task 5)
- Composer opens the release page in browser via `gh release view v1.4.0 --web` and confirms: title renders, body renders cleanly, 5 assets listed with correct human labels, "Latest" badge appears, MP3 plays correctly when downloaded, source link in body resolves.

## Threat Flags

None. No new attack surface introduced — public release of pre-reviewed audio + binary artifacts via composer's pre-authenticated `gh` token; tag is annotated and immutable post-push per CONTEXT D-803-style precedent.

## Self-Check: PASSED

Verified before commit:
- `git tag --list v1.4.0` → `v1.4.0` (FOUND)
- `git ls-remote --tags origin v1.4.0` → matches local tag object (FOUND)
- `gh release view v1.4.0 --json assets --jq '.assets | length'` → 5 (FOUND)
- `/tmp/symphony.mp3`, `/tmp/symphony.wav`, `/tmp/ragtime.mp3`, `/tmp/ragtime.wav`, `/tmp/flow-linux-x64.tar.gz` all exist and were uploaded.
- Release URL `https://github.com/NoahFreelove/flow-sharp/releases/tag/v1.4.0` is reachable (returned by `gh release view --json url`).
- No repo file changes outside this SUMMARY itself.
