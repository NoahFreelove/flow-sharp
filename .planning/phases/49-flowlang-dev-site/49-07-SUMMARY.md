---
phase: 49-flowlang-dev-site
plan: 07
subsystem: ui
tags: [sveltekit, svelte5, showcase, shiki, playwright, prerender, skeuomorphic]

# Dependency graph
requires:
  - phase: 49-01
    provides: SvelteKit scaffold + adapter-cloudflare + prerender pipeline + Playwright config
  - phase: 49-02
    provides: skeuo component library (Panel/Button/LedIndicator) + design tokens + surface classes
  - phase: 49-03
    provides: AudioEmbed/CodeCard gesture-gated patterns + #code= deep-link contract + /showcase nav tab
  - phase: 49-04
    provides: highlightFlow (shiki + Phase 17 grammar) server-rendered code blocks
provides:
  - /showcase gallery (10 curated piece cards, prerendered)
  - /showcase/[slug] detail pages (gesture-gated audio + shiki source + composer notes + Open-in-playground)
  - flow-site/src/lib/showcase/pieces.ts curated manifest + sources.ts verbatim .flow
  - flow-site/tests/showcase.spec.ts (19 assertions × 3 viewports)
affects: [49-08, 49-09]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Showcase manifest as single source of truth (pieces.ts) — gallery, detail, entries(), and E2E all derive from it"
    - "Verbatim .flow source INLINED (sources.ts) rather than ?raw-imported across the project root — keeps the CF Pages build self-contained"
    - "Honest worktree reality: absent-source pieces link out to GitHub (no fabricated .flow); non-Web-runnable pieces show source for reading but no Open-in-playground"

key-files:
  created:
    - flow-site/src/lib/showcase/pieces.ts
    - flow-site/src/lib/showcase/sources.ts
    - flow-site/src/lib/showcase/PieceCard.svelte
    - flow-site/src/routes/showcase/+page.ts
    - flow-site/src/routes/showcase/+page.svelte
    - flow-site/src/routes/showcase/[slug]/+page.ts
    - flow-site/src/routes/showcase/[slug]/+page.svelte
    - flow-site/tests/showcase.spec.ts
  modified:
    - flow-site/svelte.config.js
    - flow-site/tests/render-strategy.spec.ts
    - .planning/phases/49-flowlang-dev-site/deferred-items.md

key-decisions:
  - "Curated 10 pieces (D-49-24 6–12 range): 8 with embedded in-repo source, 2 (symphony/ragtime) linked out to GitHub since their .flow was deleted from the worktree — no fabrication"
  - "Reused 49-03's AudioEmbed for gesture-gated hero audio rather than building a new one — the two real audio assets (flow-showcase.wav, microtonal-ji.wav) map to In Five Voices + Carlos Alpha"
  - "Open-in-playground #code= deep-link only for genuinely Web-runnable pieces (no filesystem reads / content-only packs) — Carlos Scala + improv style packs show source but no playground button"
  - "Inlined verbatim source (sources.ts) instead of ?raw cross-root imports to keep the build independent of CF Pages build context + Vite server.fs.allow"

patterns-established:
  - "Manifest-driven showcase: PIECES[] drives gallery cards, prerender entries(), build-time shiki highlight, and the E2E assertions"
  - "Pre-rendered-HTML title assertions use escapeHtml() because SvelteKit escapes & → &amp; in the raw bytes"

requirements-completed: [REQ-SITE-SHOWCASE-01]

# Metrics
duration: 30min
completed: 2026-06-05
---

# Phase 49 Plan 07: Showcase Gallery Summary

**10-piece curated `/showcase` gallery + prerendered `/showcase/[slug]` detail pages with gesture-gated audio, server-rendered shiki source, faithful composer notes, and honest link-outs for deleted-source pieces — REQ-SITE-SHOWCASE-01 closed.**

## Performance

- **Duration:** ~30 min
- **Started:** 2026-06-05T22:59Z (context load)
- **Completed:** 2026-06-05T23:14Z
- **Tasks:** 2
- **Files created:** 8 · **Files modified:** 3

## Accomplishments

- **`/showcase` gallery** — a prerendered grid of 10 `<Panel framed>` `PieceCard`s spanning genres (classical, ragtime, jazz, sound design, generative, song-structure, microtonal, improv packs), each linking to its detail page inside a single `<main>` landmark.
- **`/showcase/[slug]` detail** — per-piece pages (all 10 prerender to static HTML via `entries()`): hero audio behind an explicit play `<Button>` + `<LedIndicator>` (reused `AudioEmbed`, no autoplay, D-49-01) or a "hear it in the playground" poster when no asset exists; composer notes; a server-rendered shiki source block (build-time `highlightFlow`, D-49-15); and an Open-in-playground `#code=` deep-link for Web-runnable pieces.
- **Honest worktree reality (49-CONTEXT)** — the two pieces whose `.flow` source was deleted from the worktree ("In Five Voices" symphony, "Stride & Stomp" ragtime) link out to GitHub with a clear note; NO fabricated source. The v1.5 third-genre piece (Phase 41, unbuilt) is omitted, not invented.
- **/showcase nav tab now resolves** — dropped the 49-03 `svelte.config.js` warn-not-fail `/showcase` prerender allowance (a `/showcase` 404 now fails the build) and added `/showcase` to `PRERENDERED` in `render-strategy.spec.ts`, per STATE.md's next-step.
- **Showcase E2E green** — `tests/showcase.spec.ts` (gallery + every detail + no-autoplay + shiki + link-out + navigation) passes 57/57 across the 3 viewport projects; combined with render-strategy, 84/84.

## Task Commits

1. **Task 1: Curated piece manifest + gallery grid** — `3cd00ef` (feat)
2. **Task 2: Piece detail page + gesture-gated audio + shiki source + E2E** — `e8eea45` (feat)

## Files Created/Modified

- `flow-site/src/lib/showcase/pieces.ts` — 10-piece manifest (`{slug,title,genre,phase,source?,sourcePath?,sourceRef?,audioSrc?,runnableOnWeb?,notes}`) + `isRunnable`/`playgroundHref`/`pieceBySlug` helpers.
- `flow-site/src/lib/showcase/sources.ts` — verbatim `.flow` source for the 8 in-repo pieces (auto-derived from the worktree files, `JSON.stringify`-safe string literals).
- `flow-site/src/lib/showcase/PieceCard.svelte` — gallery card (`<Panel framed>` + genre tag + felt-grille play affordance + honest source/audio badge), links to `/showcase/<slug>`.
- `flow-site/src/routes/showcase/+page.{ts,svelte}` — prerendered gallery (load returns `PIECES`; grid in one `<main>`).
- `flow-site/src/routes/showcase/[slug]/+page.ts` — `prerender=true`, `entries()` from manifest slugs, build-time `highlightFlow` of embedded source.
- `flow-site/src/routes/showcase/[slug]/+page.svelte` — detail layout (audio/poster + notes + shiki block or GitHub link + Open-in-playground).
- `flow-site/tests/showcase.spec.ts` — manifest-driven E2E (no `test.skip`).
- `flow-site/svelte.config.js` — removed the `/showcase` warn-not-fail allowance.
- `flow-site/tests/render-strategy.spec.ts` — added `/showcase` to `PRERENDERED`.
- `.planning/phases/49-flowlang-dev-site/deferred-items.md` — marked the two 49-03 `/showcase` items RESOLVED; logged the pre-existing `visual.spec.ts` `/design` failure as out-of-scope.

## Decisions Made

- **Audio mapping:** the two genuine first-party renders under `static/audio/` map to *In Five Voices* (`flow-showcase.wav`, a multi-voice render) and *Carlos Alpha & Friends* (`microtonal-ji.wav`). The other 8 pieces have no pre-rendered asset, so per D-49-01 they show a poster + (where Web-runnable) Open-in-playground rather than fabricated audio — real audible playback for those is a 49-08 HUMAN-UAT item.
- **Runnable gating:** Open-in-playground `#code=` is emitted only for pieces whose source actually runs on the Phase 48 Web target. The Carlos Scala piece (`loadScala` reads `.scl` off disk) and the improv style packs (engine-init content, not a render) show their source for reading with a clear note, but no playground button — it would print nothing audible.
- **Inlined source over `?raw`:** `examples/` + `flow-lang/` live outside `flow-site/`; a `?raw` import across the project root would depend on Vite `server.fs.allow` and the CF Pages build context including sibling dirs. Inlining (matching `$lib/home/examples.ts`) keeps the build self-contained.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] E2E asserted raw HTML against unescaped piece titles**
- **Found during:** Task 2 (showcase.spec.ts first run)
- **Issue:** `expect(html).toContain(piece.title)` failed for titles with `&` ("Stride & Stomp", "Time & Pitch", "Carlos Alpha & Friends") — SvelteKit escapes `&` → `&amp;` in the prerendered bytes, so the raw-HTTP-response checks (12 tests across 3 viewports) couldn't match the literal title.
- **Fix:** Added an `escapeHtml()` helper in the spec and used it for the raw-HTML title assertions (the DOM/`getByRole` assertions already decode correctly and were untouched).
- **Files modified:** flow-site/tests/showcase.spec.ts
- **Verification:** `pnpm playwright test tests/showcase.spec.ts` → 57/57 green across all 3 viewports.
- **Committed in:** e8eea45 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug).
**Impact on plan:** Test-only correction; no production-code or scope change.

## Issues Encountered

- **Pre-existing `visual.spec.ts` `/design` screenshot failures (OUT OF SCOPE).** The full Playwright suite showed `visual.spec.ts` failing on the `/design`-page baselines (environment font/render drift) and auto-regenerating the still-uncommitted `*-mobile*`/`*-mobile-narrow*` snapshots — the same situation Plan 49-03 logged. 49-07 touches nothing under `/design`, the design system, `app.css`, or fonts (confirmed by `git status`); the stray snapshots were deleted, not committed, per the scope boundary. Resolution stays owned by the Plan 49-08 cross-browser/Lighthouse audit. The targeted 49-07 specs pass 84/84.
- One pre-existing `nav.spec.ts [mobile]` flake (mobile hamburger slide-down timing under full-suite parallelism) passed cleanly on isolated re-run — not a 49-07 regression.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- `/showcase` + all `/showcase/[slug]` detail pages prerender; `pnpm build` exits 0 with zero `/showcase` 404s; showcase E2E green. The nav `/showcase` tab now resolves to a real route.
- **For Plan 49-08:** the gallery + details are ready for the Lighthouse + a11y + cross-browser audit. Real audible playback of the 8 poster/playground pieces (those without a pre-rendered asset) is a HUMAN-UAT item; the `/design` visual baselines still need review/commit or a desktop-only scope.
- **For Plan 49-09 (closer):** REQ-SITE-SHOWCASE-01 is closed; the third-genre showcase piece (Phase 41 SHOWCASE-01) can be added to the manifest when it ships.

## Self-Check: PASSED

All 8 created files + the SUMMARY verified present on disk; both task commits (`3cd00ef`, `e8eea45`) verified in git log.

---
*Phase: 49-flowlang-dev-site*
*Completed: 2026-06-05*
