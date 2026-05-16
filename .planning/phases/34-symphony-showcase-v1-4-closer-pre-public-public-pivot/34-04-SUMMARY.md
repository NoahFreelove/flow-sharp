---
phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
plan: 04
subsystem: release-docs
tags: [docs, announcement, release, v1.4, public-pivot, gh-release]

requires:
  - phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
    provides: "Plan 34-01 sign-off (composer-approved In Five Voices + Stride & Stomp); plan 34-02 finalized examples/{symphony,ragtime}/README.md reproduction guides; CONTEXT D-603 announcement-file location lock + D-603b ragtime scope expansion"
provides:
  - "docs/announcements/v1.4.0.md — 20-line, 3-paragraph + dual-showcase block public announcement draft, ready for both per-platform composer adaptation AND plan 34-05 `gh release create --notes-file` consumption"
  - "New docs/announcements/ directory (first inhabitant) — sibling to docs/editor-setup/ and docs/plans/ per RESEARCH § Recommended Project Structure"
affects: [34-05-PLAN, 34-06-PLAN, future-announcements]

tech-stack:
  added: []
  patterns:
    - "Single-source announcement file: composer adapts per-platform (Reddit, HN, X, Discord) AND `gh release create` consumes verbatim as the Release body"
    - "Dual-piece showcase block: orchestral + ragtime side-by-side with planned asset URLs (live once plan 34-05 publishes the v1.4.0 release)"

key-files:
  created:
    - "docs/announcements/v1.4.0.md (20 lines)"
  modified: []

key-decisions:
  - "Resolved repo path baked in: `NoahFreelove/flow-sharp` (from `git remote get-url origin`) — not `<user>`/`<repo>` placeholders. Required by the plan's negative-grep verifier `! grep -Eq '...|<user>|<repo>'`."
  - "Substantive scope expansion honored: BOTH *In Five Voices* (orchestral) AND *Stride & Stomp* (ragtime) referenced as parallel showcase pieces with per-piece source + MP3 + WAV links — per CONTEXT D-603b scope expansion locked at the start of Phase 34."
  - "Working titles taken verbatim from the committed sources: `examples/symphony/symphony.flow` line 2 (`In Five Voices`) and `examples/ragtime/ragtime.flow` line 2 (`Stride & Stomp`)."
  - "Release-asset URLs use the planned filename pattern `https://github.com/NoahFreelove/flow-sharp/releases/download/v1.4.0/{symphony,ragtime}.{mp3,wav}` — these become live when plan 34-05 runs `gh release create v1.4.0 <assets>`; the announcement file MUST exist before then so 34-05 can pass it as `--notes-file`."
  - "ASCII-only enforced (`LANG=C grep -nP '[^\\x00-\\x7F]'` clean) per CLAUDE.md Conventions; em-dashes rendered as `--` to avoid the U+2014 character (matches the symphony.flow / ragtime.flow comment style)."
  - "Markdown italics (`*In Five Voices*`, `*Stride & Stomp*`) used for piece titles — standard composer convention for work names."
  - "Single H1 + 3 paragraphs + 1 bullet-list highlight block + 1 piece-list block + 1 call-to-action paragraph. No H2 sections (D-603 'no H2s' rule)."

patterns-established:
  - "Pattern: 3-paragraph announcement (elevator pitch / phase highlights / listen-try-source) lives at docs/announcements/vMAJOR.MINOR.PATCH.md — composer-adaptable + release-tool-readable"
  - "Pattern: dual-showcase piece block formats each piece as a single bullet `*Title* (genre): [source] -- [MP3] -- [WAV]` keeping discovery cheap on small screens"

requirements-completed: [SYM-05]

duration: ~4 min
completed: 2026-05-16
---

# Phase 34 Plan 04: Draft v1.4.0 Public Announcement Summary

**Single-source v1.4.0 announcement at `docs/announcements/v1.4.0.md` — 20-line, 3-paragraph Markdown serving both composer per-platform adaptation AND plan 34-05's `gh release create --notes-file` consumption; references both showcase pieces (In Five Voices + Stride & Stomp) with resolved `NoahFreelove/flow-sharp` GitHub URLs.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-05-16T~19:42:00Z
- **Completed:** 2026-05-16T19:45:53Z
- **Tasks:** 1/1
- **Files created:** 1
- **Files modified:** 0

## Accomplishments

- Created the new `docs/announcements/` directory (sibling to `docs/editor-setup/` + `docs/plans/`) as the canonical home for per-release announcement drafts.
- Wrote `docs/announcements/v1.4.0.md` in the D-603 3-paragraph shape: elevator pitch / Phase 28-34 highlight summary / call-to-action with showcase listening links.
- Honored CONTEXT D-603b scope expansion: both showcase pieces (orchestral symphony + ragtime) appear as parallel entries with per-piece source + MP3 + WAV URLs.
- Resolved `OWNER/REPO` to `NoahFreelove/flow-sharp` via `git remote get-url origin`; no `<user>` / `<repo>` placeholders remain.
- Verified ASCII-only, no emoji, no `TODO` / `TBD` / `FIXME` tokens, 20 lines (meets `min_lines: 20`), all 7 Phase 28-34 references present, both piece filenames referenced.

## Task Commits

1. **Task 1: Create docs/announcements/v1.4.0.md (3-paragraph public announcement)** — `4547204` (`docs(34-04): draft v1.4.0 public announcement`)

## Files Created/Modified

- `docs/announcements/v1.4.0.md` (NEW, 20 lines) — public v1.4.0 announcement Markdown:
  - **Paragraph 1 (elevator pitch):** adapted from PROJECT.md "What This Is" + the Goals/Non-Goals framing from CLAUDE.md — "interpreted, statically-typed language for music production" + the flow operator + musical-context blocks + the ergonomics-first / genre-agnostic stance.
  - **Paragraph 2 + phase-bullet list (highlight summary):** one bullet per shipping Phase 28..34 user-facing feature — voice polyphony + 5 articulation envelopes (28), sampled tonal instruments (29), self-contained Linux x64 CLI binary (30), LSP polish + JetBrains scaffolding (31), Scala microtonal tuning loader (32), SFZ orchestral sampler with VSCO-CE blessing (33), curated dual showcase (34).
  - **Paragraph 3 (call to action):** "Listen to the showcase" introductory sentence + per-piece bullet list (orchestral + ragtime, each with source + MP3 + WAV links) + "Try it yourself" install link + Source repo link + per-piece README reproduction links.

## Decisions Made

See `key-decisions` in frontmatter above. Most consequential:

1. **Resolved the repo path NoahFreelove/flow-sharp** instead of leaving `<USER>/<REPO>` placeholders (as the runtime-notes section of the executor prompt suggested). The plan file's automated verifier (`! grep -Eq '...|<user>|<repo>'`) explicitly forbids those placeholders — the PLAN file is the source of truth, and the verifier proves it. The composer can still adapt the URLs per-platform after the fact, but the canonical file ships with real URLs ready for `gh release create --notes-file`.
2. **Included both pieces** (symphony + ragtime) as parallel showcase entries. CONTEXT D-603b locks both pieces into the v1.4 release; the plan's verifier doesn't require the ragtime mention but the success criteria from the executor prompt does, and the broader CONTEXT clearly intends both to ship together.
3. **Dropped the implicit "no-H2" expectation by reading the rule literally**: D-603 says "3 paragraphs Markdown" — I treated the 3 paragraphs as the load-bearing structure, sandwiched two bullet lists inside them (one for Phase 28-34 highlights, one for the dual-piece showcase), and avoided H2 section headers. The result reads as 3 connected paragraphs even though it has 2 inline bullet groups for scannability.

## Deviations from Plan

None — plan executed exactly as written.

The plan-text example showed a single piece (symphony only) in paragraph 3; I expanded to both pieces per the CONTEXT D-603b scope expansion. This is not a deviation but the explicit follow-through on the scope expansion CONTEXT codified after plan 34-01 closed and before plan 34-04 ran. The plan's automated verifier passes either way (the ragtime grep was not in the verifier), and the CONTEXT D-603b language ("both pieces share the announcement draft") is unambiguous.

## Issues Encountered

- **Worktree was created from an older commit (HEAD at Phase 32 closure)** — `.planning/phases/34-.../` directory did not exist in my worktree. Resolved via `git merge --ff-only dev` (clean fast-forward: HEAD was a strict ancestor of dev), which brought 69 commits including all the Phase 33 + Phase 34 prerequisite artifacts. No code changes; safe replay of already-landed dev work into the agent branch. This is a worktree-orchestrator setup issue (the orchestrator should have used a more-recent base SHA), not a Plan 34-04 issue — surfaced here for visibility.

## User Setup Required

None — this plan ships a single Markdown file. No environment variables, no service configuration, no external dashboards.

## Self-Check: PASSED

- File `docs/announcements/v1.4.0.md` exists: confirmed via `test -f`.
- Directory `docs/announcements/` exists: confirmed via `test -d`.
- Commit `4547204` is HEAD: confirmed via `git log -1 --pretty=fuller`.
- Plan's automated verifier passes: confirmed via the full one-line conjunction returning `ALL VERIFY CHECKS PASS`.
- All 7 phase references (Phase 28..Phase 34) present: confirmed via 7-way `grep` loop.
- Both showcase pieces referenced (symphony.flow + ragtime.flow): confirmed.
- ASCII-only: confirmed via `LANG=C grep -nP '[^\x00-\x7F]'` returning empty.
- No forbidden placeholder tokens (`TODO` / `TBD` / `FIXME` / `<user>` / `<repo>`): confirmed via negative grep.
- No emoji: confirmed via negative grep against `😀|🎵|🎉|🚀`.
- Line count >= 20 (min_lines requirement): 20 lines exactly.

## Next Phase Readiness

- `docs/announcements/v1.4.0.md` is ready for plan 34-05's `gh release create v1.4.0 --notes-file docs/announcements/v1.4.0.md ...` invocation.
- Plan 34-05 will publish the v1.4.0 GitHub Release; once published, the asset URLs in this announcement become live downloads.
- Plan 34-06 (milestone closure) does NOT depend on this file beyond the existing reference patterns; STATE.md / ROADMAP.md / PROJECT.md updates orchestrated by the closure plan are independent.
- Composer can adapt this file per-platform (Reddit r/programminglanguages, HN, X, Discord) at release time — the file is the single source.

---
*Phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot*
*Completed: 2026-05-16*
