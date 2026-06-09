---
phase: 49-flowlang-dev-site
plan: 03
subsystem: ui
tags: [sveltekit, svelte5, marketing, navigation, shiki, audio, a11y, prerender, playwright]

# Dependency graph
requires:
  - phase: 49-01
    provides: SvelteKit scaffold (+layout/+page stubs, app.css, app.html theme bootstrap, playwright.config system-chromium fallback, nav/render-strategy test stubs)
  - phase: 49-02
    provides: skeuo component library (Tabs/Toggle/Button/Panel/LedIndicator), tokens.css + surfaces.css, theme.ts (getInitialTheme/setTheme)
  - phase: 49-04
    provides: shiki highlightFlow server-side highlighter + the data-flow-source "Open in playground" deep-link carrier contract
provides:
  - Persistent 5-tab brushed-metal top nav (Home/Docs/Playground/Showcase/GitHub) with theme toggle, mobile hamburger, ARIA landmarks, aria-current
  - Six-section D-49-21 marketing Home (hero + value-prop trio + how-it-sounds + code-first + CTAs + footer) in the skeuo vocabulary
  - CodeCard (server-highlighted Flow + Play-in-playground deep link) + AudioEmbed (gesture-gated, no autoplay)
  - Render-strategy + a11y-label E2E proving prerender vs client-only per-route strategy
affects: [49-06, 49-07, 49-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Prerender-time shiki highlighting via +page.ts load (zero client highlight JS on marketing routes)"
    - "Deep-link carrier contract: /playground#code=<encoded> + data-flow-source + data-run=1 (49-06 fills the real encoder)"
    - "Gesture-gated audio: explicit play Button + LedIndicator mirror of real <audio> events, no autoplay attribute"
    - "Per-route render strategy proven by E2E (raw-HTML content for prerendered; JS-disabled context proves client-only playground)"

key-files:
  created:
    - flow-site/src/routes/+page.ts
    - flow-site/src/lib/home/examples.ts
    - flow-site/src/lib/home/CodeCard.svelte
    - flow-site/src/lib/home/AudioEmbed.svelte
    - flow-site/static/audio/flow-showcase.wav
    - flow-site/static/audio/microtonal-ji.wav
  modified:
    - flow-site/src/routes/+layout.svelte
    - flow-site/src/routes/+page.svelte
    - flow-site/svelte.config.js
    - flow-site/tests/nav.spec.ts
    - flow-site/tests/render-strategy.spec.ts
    - .gitignore

key-decisions:
  - "CodeCard deep link uses contiguous /playground#code=<url-encoded source> + data-run=1 signal (matches 49-04 docs carrier; 49-06 swaps in deflate/base64url)"
  - "How-it-sounds audio uses first-party rendered Flow audio (flow_showcase.wav + microtonal_ji.wav) — examples/symphony/ was deleted, so no fabrication (charitable per plan)"
  - "Render-strategy E2E covers / + /docs (prerendered routes that exist at wave 3); /showcase added once 49-07 lands"

patterns-established:
  - "Pattern: highlight all Home snippets at prerender time in +page.ts, pass HTML into components — no client-side shiki on marketing routes"
  - "Pattern: mobile nav slide-down keyed off a previous-pathname guard so the close-on-nav effect can't fire on hydration"

requirements-completed: [REQ-SITE-IA-01, REQ-SITE-HOME-01, REQ-SITE-A11Y-02]

# Metrics
duration: 24min
completed: 2026-06-05
---

# Phase 49 Plan 03: Home + 5-Tab Nav Summary

**Six-section skeuomorphic marketing Home (hero with shiki Play-in-playground cards + no-autoplay audio, value-prop trio, code-first annotated snippet, install/try CTAs, footer) on a persistent 5-tab brushed-metal nav, all prerendered, with render-strategy + a11y E2E green.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-06-05T18:32:00Z
- **Completed:** 2026-06-05T18:56:20Z
- **Tasks:** 3
- **Files modified:** 12 (6 created, 6 modified)

## Accomplishments

- **Persistent 5-tab top nav** (D-49-07) on `.surface-brushed-metal`: Recoleta "Flow" wordmark (embossed text-shadow, home link), `<Tabs>` (Home/Docs/Playground/Showcase + GitHub external with `target="_blank"` + `rel="noopener noreferrer"` + outbound glyph + "opens in new tab"), `<Toggle>` theme switch, mobile `<768px` hamburger slide-down. `aria-current="page"` wired from the `$app/state` `page` rune. ARIA landmarks throughout.
- **Six-section Home** (D-49-21) inside one `<main>`: (1) hero `.surface-paper` inlay framed by `.surface-wood` with the clamp-48→72px wordmark, lead tagline, 3 `<CodeCard>` Play-in-playground cards, and a symphony-flavoured `<AudioEmbed>`; (2) value-prop trio (`<Panel framed>` cards sourced from CLAUDE.md Goals); (3) "How it sounds" audio embeds; (4) code-first ~20-line shiki snippet with `->`/note-stream/musical-context margin annotations; (5) install copy-command + brass "Try in browser" CTAs; (6) `.surface-wood` `<footer>` band.
- **Reuse, not recreation:** consumed Plan 49-02's `Tabs/Toggle/Panel/Button/LedIndicator` + Plan 49-04's `highlightFlow` (server-rendered shiki). No component or highlighter was recreated.
- **No autoplay anywhere** (D-49-01): every `<audio>` is behind an explicit play Button with a felt `<LedIndicator>` + "Press play to listen" caption; no `autoplay` attribute exists.
- **Prerendered Home** (D-49-13/34): `prerender = true`; shiki runs at build time in `+page.ts`; the Home HTML carries no `flow-runtime.js`/WASM reference.
- **E2E green** across 3 viewport projects (desktop/mobile/mobile-narrow): nav (5 tabs, routing, external safety, aria-current, theme toggle) + render-strategy (prerendered raw-HTML content for `/` + `/docs`; client-only playground proven by a JS-disabled context) + Home/nav a11y-label sweep.

## Task Commits

Each task was committed atomically:

1. **Task 1: 5-tab top nav + theme toggle + ARIA landmarks** - `87a9061` (feat)
2. **Task 2: Home six sections + CodeCard + AudioEmbed** - `6ee9f49` (feat)
3. **Task 3: Render-strategy E2E + a11y label sweep (+ mobile-nav race fix)** - `7f38308` (feat)

## Files Created/Modified

- `flow-site/src/routes/+layout.svelte` - Persistent `.surface-brushed-metal` chrome: wordmark + `<Tabs>` + `<Toggle>` + mobile hamburger; `aria-current` via `$app/state`; nav-close effect guarded by previous-pathname.
- `flow-site/src/routes/+page.svelte` - The six D-49-21 sections in one `<main>` with the skeuo vocabulary.
- `flow-site/src/routes/+page.ts` - `prerender = true` + parallel prerender-time shiki highlight of all Home snippets.
- `flow-site/src/lib/home/examples.ts` - 3 hero snippets + the code-first snippet + margin annotations (all Web-target-safe).
- `flow-site/src/lib/home/CodeCard.svelte` - Server-highlighted Flow card on a `<Panel>` + brass Play-in-playground deep link (`#code=` + `data-flow-source` + `data-run=1`).
- `flow-site/src/lib/home/AudioEmbed.svelte` - Gesture-gated `<audio>` (no autoplay) + play/pause Button + `<LedIndicator>` + caption.
- `flow-site/static/audio/{flow-showcase,microtonal-ji}.wav` - First-party rendered Flow audio for the embeds.
- `flow-site/tests/nav.spec.ts` - Nav E2E (replaces the stub).
- `flow-site/tests/render-strategy.spec.ts` - Render-strategy + a11y-label E2E (replaces the stub).
- `flow-site/svelte.config.js` - Prerender `handleHttpError` tolerates the not-yet-routed `/showcase` nav link.
- `.gitignore` - Allow-lists `flow-site/static/audio/` past the global `*.wav` ignore.

## Decisions Made

- **Deep-link form:** `/playground#code=<url-encoded source>` (contiguous, decodable today) + a `data-run="1"` auto-run signal + `data-flow-source`, matching Plan 49-04's docs "Open in playground" carrier so Plan 49-06 can fill the real deflate/base64url `#code=` encoder without changing the contract. Chose `data-run` over `?run=1` so the `playground#code=` substring stays contiguous and the fragment stays cleanly decodable.
- **Audio assets:** `examples/symphony/` was removed from the worktree, so per the plan's charitable guidance the "how it sounds" embeds use available first-party rendered Flow audio (`flow_showcase.wav` as the multi-voice/symphony stand-in, `microtonal_ji.wav` as the v1.5 generative-flavoured sketch) rather than fabricated audio.
- **No Lucide dependency:** the external-tab and theme-toggle glyphs reuse the existing text-glyph affordances the 49-02 `Tabs`/`Toggle` already ship (`↗`, `☀`/`☾`), so no new icon package was introduced.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Prerender failed on the not-yet-routed `/showcase` nav link**
- **Found during:** Task 1 (5-tab nav)
- **Issue:** The D-49-07 nav links `/showcase`, whose route ships in Plan 49-07. SvelteKit's prerenderer treated the dangling internal link as a fatal 404, failing `pnpm build`.
- **Fix:** Extended `svelte.config.js` `handleHttpError` to warn (not fail) on `/showcase` paths, with a comment to remove the allowance once 49-07 lands. Every real route still prerenders.
- **Files modified:** flow-site/svelte.config.js
- **Verification:** `pnpm build` exits 0; the warning prints once at prerender.
- **Committed in:** `87a9061` (Task 1 commit)

**2. [Rule 3 - Blocking] Home audio assets blocked by the global `*.wav` .gitignore**
- **Found during:** Task 2 (Home audio embeds)
- **Issue:** The Home `<AudioEmbed>`s reference `/audio/*.wav`, but the repo's root `.gitignore` has a global `*.wav` ignore, so the curated assets would never be committed/served.
- **Fix:** Added a `flow-site/static/audio/` allow-list block (mirrors the committed `static/wasm/` AppBundle + the Phase 29/33 baseline-WAV allow-list precedents).
- **Files modified:** .gitignore
- **Verification:** `git check-ignore` confirms the negation rule matches; `git add` tracks the WAVs.
- **Committed in:** `6ee9f49` (Task 2 commit)

**3. [Rule 1 - Bug] Mobile hamburger menu closed itself on hydration**
- **Found during:** Task 3 (E2E stabilization)
- **Issue:** The `+layout` mobile-menu-close `$effect` read `current` and set `mobileOpen = false` on every run, including hydration's `null → pathname` transition — slamming the menu shut right after the user opened it, producing intermittent mobile nav E2E failures.
- **Fix:** Guarded the close with a previous-pathname check so it fires only on an actual navigation.
- **Files modified:** flow-site/src/routes/+layout.svelte
- **Verification:** Three consecutive clean full-suite runs (42 passed each); the mobile nav-open tests are stable.
- **Committed in:** `7f38308` (Task 3 commit)

---

**Total deviations:** 3 auto-fixed (2 blocking, 1 bug)
**Impact on plan:** All three were necessary to ship a building, deployable, stable Home + nav. No scope creep — the `/showcase` allowance and the audio allow-list are forward-compatible (documented in deferred-items.md), and the menu-close guard is a correctness fix in this plan's own nav.

## Known Stubs

None — the Home renders real curated content and real first-party audio. The `#code=` deep link uses a working URL-encoded fallback today; Plan 49-06 swaps in the deflate/base64url encoder behind the same `data-flow-source`/`data-run` contract (forward-compatible, not a stub).

## Issues Encountered

- **E2E parallel-execution flakiness.** Under the 3-viewport parallel run sharing one preview origin, the theme-toggle and mobile-nav-open tests flaked. Resolved by: (a) the Rule 1 menu-close guard above, (b) isolating the theme-toggle test in its own context with a forced light start + `networkidle` settle, asserting the applied `[data-theme]` effect, and (c) adding `toBeVisible` waits before querying the slide-down. Three back-to-back clean full-suite runs confirm stability.
- **Preview-server SSR vs SPA.** `vite preview` server-renders the playground's static markup even with `ssr=false` (that directive is honored by the production adapter, not the preview), so the render-strategy SPA assertion was rewritten to the robust discriminators: no `flow-runtime.js` in raw HTML, and the Monaco editor mounts only with JS enabled (proven via a `javaScriptEnabled: false` context).

## User Setup Required

None - no external service configuration required. (Curated audio is committed under `static/audio/`; no env vars or dashboards.)

## Next Phase Readiness

- **49-06 (Share + Save):** the CodeCard deep link already emits `#code=` + `data-flow-source` + `data-run=1`; 49-06 fills the real deflate/base64url encoder and the playground-side auto-run honoring behind that exact contract.
- **49-07 (Showcase):** the nav `/showcase` tab is live; when 49-07 lands the route, remove the `svelte.config.js` `/showcase` prerender allowance and add `/showcase` to the render-strategy `PRERENDERED` list.
- **49-08 (Lighthouse + a11y audit):** the Home + nav a11y-label contract is in place (icon labels, external-link affordance, audio accessible names, landmarks). The auto-generated `visual.spec.ts` mobile baselines were left uncommitted (logged in deferred-items.md) for 49-08/49-02 to review.

## Self-Check: PASSED

- All 6 created files present on disk (+page.ts, examples.ts, CodeCard.svelte, AudioEmbed.svelte, 2 audio WAVs) + SUMMARY.
- All 3 task commits present in git history (`87a9061`, `6ee9f49`, `7f38308`).
- `pnpm build` exits 0; nav + render-strategy E2E (42 tests across 3 viewports) green; full E2E suite 144 passed / 6 pre-existing skips; vitest 42 passed / 5 pre-existing skips.

---
*Phase: 49-flowlang-dev-site*
*Completed: 2026-06-05*
