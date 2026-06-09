---
phase: 49-flowlang-dev-site
plan: 02
subsystem: ui
tags: [skeuomorphism, design-tokens, tailwind-v4, svelte5, accessibility, aria-slider, visual-regression, self-hosted-fonts, sharp, playwright]

# Dependency graph
requires:
  - phase: 49-01
    provides: "SvelteKit 2 + Svelte 5 + TS + Tailwind v4 + adapter-cloudflare scaffold (app.css @theme placeholder, vitest/playwright config, _headers CSP, committed Phase 48 AppBundle under static/wasm/)"
provides:
  - "flow-site/src/lib/design/tokens.css — full D-49-17 token set (light :root + [data-theme=dark]) + @font-face for 3 self-hosted woff2 faces"
  - "flow-site/src/lib/design/surfaces.css — 4 .surface-* material classes (brushed-metal/wood/paper/felt), flat-base-first + prefers-reduced-data degrade"
  - "flow-site/src/lib/design/theme.ts — getInitialTheme/setTheme/toggleTheme/applyTheme (D-49-20 localStorage + prefers-color-scheme persistence)"
  - "flow-site/src/lib/components/skeuo/*.svelte — the 8 base components (Button/Knob/Toggle/Panel/MetalRail/LedIndicator/Slider/Tabs) with states + reduced-motion + a11y"
  - "flow-site/scripts/optimize-textures.mjs — deterministic Sharp wood-grain → AVIF/WebP/PNG pipeline"
  - "flow-site/src/routes/design/+page.svelte — component showcase (every component, every state, theme toggle, reduced-motion preview)"
  - "flow-site/tests/visual.spec.ts + baselines — light/dark/reduced-motion visual-regression captures"
  - "static/fonts/ (4 woff2) + static/textures/ (wood-grain.{avif,webp,png})"
  - "app.html inline early-theme script (FOUC-prevention) + _headers CSP sha256 hash for it"
affects:
  - "49-03 Home — consumes Button/Panel/LedIndicator/Tabs/Toggle + .surface-* + tokens"
  - "49-04 Docs — .surface-paper (staff variant) + Panel header + Tabs"
  - "49-05 Playground — Panel/Button/LedIndicator/Slider/Toggle + status-bar surface-brushed-metal + theme toggle"
  - "49-07 Showcase — Panel framed cards + felt-grille LedIndicator + Button"
  - "49-08 Lighthouse/a11y audit — brass focus ring, ARIA slider, reduced-motion, contrast pairs all pinned here"

# Tech tracking
tech-stack:
  added:
    - "@fontsource/inter 5.2.8, @fontsource/jetbrains-mono 5.2.8, @fontsource/fraunces 5.2.9 (dev-only — source of the self-hosted woff2 files; not runtime deps)"
  patterns:
    - "Tailwind v4 CSS-first @theme: raw tokens in tokens.css (runtime-swappable via [data-theme]), exposed to Tailwind's utility generator via @theme in app.css — NO tailwind.config.js"
    - "Material surface = flat-base fallback FIRST + ::before overlay (inline-SVG data-URI feTurbulence for metal/paper/felt, image-set AVIF/WebP/PNG raster for wood) + prefers-reduced-data degrade"
    - "Reduced-motion fallbacks live in components, not just global: Knob renders the flat <Slider>; Panel swaps shadow→1px walnut border; LED pulse→steady; Button loses 50ms travel"
    - "WAI-ARIA role=slider keyboard contract shared by Knob + Slider: Arrow/Home/End/PageUp-Down + aria-valuemin/max/now/valuetext + clamp"
    - "FOUC-prevention inline theme script in app.html (sets [data-theme] before paint), CSP-hashed in _headers (no unsafe-inline)"
    - "TDD for behaviour-bearing components: skeuo.test.ts pins a11y contracts (RED) before the GREEN implementation"
    - "Deterministic Sharp texture pipeline (xorshift32, no Math.random) → byte-stable output → stable visual-regression baselines"

key-files:
  created:
    - "flow-site/src/lib/design/tokens.css"
    - "flow-site/src/lib/design/surfaces.css"
    - "flow-site/src/lib/design/theme.ts (+ theme.test.ts)"
    - "flow-site/src/lib/components/skeuo/{Button,Knob,Toggle,Panel,MetalRail,LedIndicator,Slider,Tabs}.svelte"
    - "flow-site/src/lib/components/skeuo/skeuo.test.ts"
    - "flow-site/scripts/optimize-textures.mjs"
    - "flow-site/src/routes/design/+page.svelte"
    - "flow-site/static/fonts/{inter-400,inter-600,fraunces-700,jetbrains-mono-400}.woff2"
    - "flow-site/static/textures/wood-grain.{avif,webp,png}"
    - "flow-site/tests/visual.spec.ts-snapshots/design-{light,dark,reduced-motion}-desktop-linux.png"
  modified:
    - "flow-site/src/app.css (@import tokens+surfaces, @theme map, global focus ring + reduced-motion + .sr-only)"
    - "flow-site/src/app.html (inline early-theme FOUC script)"
    - "flow-site/_headers (CSP script-src sha256 hash for the theme script)"
    - "flow-site/vitest.config.ts (svelte + svelteTesting plugins)"
    - "flow-site/playwright.config.ts (system-chromium fallback for ubuntu 26.04)"
    - "flow-site/package.json (prebuild textures hook + font devDeps)"
    - "flow-site/.gitignore (ignore Playwright test-results/)"
    - "flow-site/tests/visual.spec.ts (replaced the Wave-0 test.skip stub)"

key-decisions:
  - "Display font: Fraunces (SIL OFL) substituted for Recoleta (commercial, no redistributable woff2) — Rule 3 blocking, closest freely-self-hostable warm hand-set serif to D-49-06's brief"
  - "FOUC-prevention via an inline app.html theme script, CSP-hashed in _headers (not unsafe-inline) — Rule 2 missing functionality (nothing applied the stored theme on load)"
  - "Wood-grain raster generated procedurally + deterministically by Sharp (no committed binary source) so baselines stay byte-stable"
  - "Tailwind v4 @theme references the tokens.css custom props so one utility class re-resolves per [data-theme] with no Tailwind variants"

patterns-established:
  - "skeuo component + a11y test pair under src/lib/components/skeuo/ — every interactive control carries the brass focus ring, 44px hit, and reduced-motion fallback"
  - "Visual-regression workflow: /design showcase + visual.spec.ts captures light/dark/reduced-motion baselines for downstream plans to diff against"

requirements-completed: [REQ-SITE-DESIGN-01, REQ-SITE-DESIGN-02, REQ-SITE-DESIGN-03, REQ-SITE-DESIGN-04, REQ-SITE-DESIGN-05, REQ-SITE-A11Y-01, REQ-SITE-A11Y-03]

# Metrics
duration: 20min
completed: 2026-06-05
---

# Phase 49 Plan 02: Skeuomorphic Design System Summary

**D-49-17 design tokens (light+dark) wired into Tailwind v4 @theme, 4 material `.surface-*` classes, 8 accessible skeuo Svelte 5 components (Knob/Slider as WAI-ARIA role=slider), self-hosted Inter/Fraunces/JetBrains Mono, a Sharp wood-grain pipeline, and a `/design` showcase with light/dark/reduced-motion visual-regression baselines.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-06-05T21:16:00Z
- **Completed:** 2026-06-05T21:36:00Z
- **Tasks:** 3 (Task 2 was TDD: RED → GREEN)
- **Files modified/created:** 34 (flow-site/)

## Accomplishments

- **Design tokens (D-49-17):** full token set as CSS custom properties — light `:root` + `[data-theme="dark"]` (D-49-20 deep-walnut + amber-LED) — for colour, spacing (4/8/12/16/24/32/48/64), type scale (12→48 with line-height+tracking pairs), shadows (1/3/8/16/32 + inset + bevel), radius (2/4/8/12/24), and motion curves. Mapped into Tailwind v4 via `@theme` (CSS-first, no `tailwind.config.js`).
- **4 material surfaces (D-49-18):** `.surface-brushed-metal` (slate gradient + inline-SVG horizontal-brush feTurbulence), `.surface-wood` (walnut gradient + Sharp AVIF/WebP/PNG grain raster via `image-set`), `.surface-paper` (cream + paper-fiber speckle + optional staff-line ruling), `.surface-felt` (felt + dot/weave + inset shadow). Each declares its flat-base fallback first; all degrade under `prefers-reduced-data`.
- **8 components (D-49-19), all to the UI-SPEC contracts:** Button (5 variants, INSET 50ms press), Knob (role=slider + full keyboard model + reduced-motion→flat Slider), Slider (vertical fader + horizontal), Toggle (role=switch + theme persistence), Panel (framed/inset/header, elevated/seated, reduced-motion→1px border), MetalRail (aria-hidden), LedIndicator (idle/rendering/playing/error + visually-hidden aria-live mirror), Tabs (5-tab nav, aria-current, external `rel=noopener noreferrer` + "opens in new tab"). Every interactive element has a brass 2px `:focus-visible` ring + 44px hit area.
- **Dual themes + persistence:** `theme.ts` (localStorage wins, else prefers-color-scheme) + an inline `app.html` script applies `[data-theme]` before first paint (no FOUC); toggle persists in localStorage.
- **Self-hosted fonts:** Inter 400/600, Fraunces 700 (Recoleta substitute), JetBrains Mono 400 as woff2 with `font-display: swap`, no Google Fonts CDN (keeps CSP `font-src 'self'`).
- **`/design` showcase + visual baselines:** every component in every state, theme toggle, reduced-motion preview; `visual.spec.ts` captures light/dark/reduced-motion and asserts two-run clean.

## Task Commits

1. **Task 1: Design tokens + @theme + surfaces + self-hosted fonts** — `564aafd` (feat)
2. **Task 2 (TDD): 8 components + theme persistence** — `30b305a` (test/RED) → `594fe6e` (feat/GREEN)
3. **Task 3: /design showcase + visual baselines** — `7f8a528` (feat)

**Plan metadata:** this commit (docs: complete plan)

## Files Created/Modified

See frontmatter `key-files`. Highlights:
- `src/lib/design/tokens.css` / `surfaces.css` / `theme.ts` — the design foundation.
- `src/lib/components/skeuo/*.svelte` (8) + `skeuo.test.ts` + `theme.test.ts` — components + 27 pinned a11y tests.
- `scripts/optimize-textures.mjs` — deterministic Sharp wood-grain → AVIF(82%)/WebP/PNG.
- `src/routes/design/+page.svelte` + `tests/visual.spec.ts` + 3 baseline PNGs.

## Decisions Made

- **Fraunces for the display face** (see deviation 1).
- **Inline FOUC theme script** (see deviation 2).
- **Tailwind v4 `@theme` references tokens.css custom props** so a single utility class re-resolves per `[data-theme]` at runtime — no per-theme Tailwind variants.
- **Procedural deterministic wood-grain** (xorshift32 seed, no `Math.random`) so the texture bytes — and therefore the visual baselines — are stable across runs.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Display font Recoleta → Fraunces (self-hostable substitute)**
- **Found during:** Task 1 (self-hosted fonts).
- **Issue:** The UI-SPEC names Recoleta as a DEFAULT pick from D-49-06's Cooper Black / Recoleta / Krona display set, but Recoleta is a commercial font with no freely-redistributable woff2 — it cannot be self-hosted (the plan's `! grep googleapis` + CSP `font-src 'self'` rules forbid a CDN, and there is no licensed file to ship).
- **Fix:** Substituted Fraunces (SIL OFL, soft warm optical-display serif), the closest freely-licensable match to the "warm hand-set serif" brief. Self-hosted as `fraunces-latin-700.woff2`; mapped to `--font-display`. Documented in tokens.css.
- **Files modified:** `tokens.css`, `static/fonts/fraunces-latin-700.woff2`, `package.json` (dev font deps).
- **Verification:** Build clean; wordmark/hero renders the warm serif (see /design light baseline).
- **Committed in:** `564aafd`.

**2. [Rule 2 - Missing Critical] Early theme application on load (FOUC-prevention)**
- **Found during:** Task 3 (dark-theme visual baseline failed — `[data-theme]` never set on load).
- **Issue:** `theme.ts` only applied `[data-theme]` on an explicit toggle; nothing applied the stored/preferred theme at page load, so a dark-preference visitor would flash the light theme and the dark baseline couldn't be captured.
- **Fix:** Added an inline synchronous script to `app.html` (mirrors `getInitialTheme()`) that sets `[data-theme]` before first paint; added its SHA-256 to the `_headers` CSP `script-src` so the policy stays tight (no `unsafe-inline`).
- **Files modified:** `src/app.html`, `_headers`.
- **Verification:** Dark-theme visual baseline now captures `<html data-theme="dark">`; both themes render correctly.
- **Committed in:** `7f8a528`.

**3. [Rule 3 - Blocking] Playwright system-chromium fallback (env)**
- **Found during:** Task 3 (visual baselines).
- **Issue:** Playwright 1.60.0 has no bundled chromium build for ubuntu-26.04-x64, and `playwright install chrome` needs sudo (unavailable).
- **Fix:** `playwright.config.ts` drives the system snap chromium (`/snap/bin/chromium`) via `executablePath` + `--no-sandbox`, env-gated by `PLAYWRIGHT_CHROMIUM_PATH` so CI is unaffected. Logged to `deferred-items.md`.
- **Files modified:** `playwright.config.ts`.
- **Verification:** Visual spec runs green (3 baselines written + asserted, two-run clean).
- **Committed in:** `7f8a528`.

**4. [Rule 3 - Blocking] vitest svelte plugin for component tests**
- **Found during:** Task 2 (component tests couldn't import `.svelte`).
- **Issue:** The Wave-0 `vitest.config.ts` lacked the svelte compiler plugin, so `.svelte` imports failed under jsdom.
- **Fix:** Added `@sveltejs/vite-plugin-svelte` + `@testing-library/svelte/vite` `svelteTesting()` to the vitest plugins.
- **Files modified:** `vitest.config.ts`.
- **Verification:** 27 component+theme tests green.
- **Committed in:** `594fe6e`.

---

**Total deviations:** 4 auto-fixed (1 missing-critical, 3 blocking). **Impact:** All necessary for correctness/a11y/test-infra. No scope creep — the 8-component ceiling, the brass-accent reserved list, and the token/surface contracts were implemented exactly to the UI-SPEC.

## Issues Encountered

- A stale `pnpm preview` on port 4173 caused a transient `ERR_EMPTY_RESPONSE` on the first visual run; freed the port and rebuilt — resolved.
- `svelte-check` reports 0 errors / 1 pre-existing warning ("Cannot find type definition file for 'node'") from the Wave-1 scaffold tsconfig — out of scope, logged to `deferred-items.md`.

## Known Stubs

None. The `/design` page wires real component instances with real props; no placeholder/empty-data stubs. The Wave-0 `share/encode.test.ts` and `docs/transform.test.ts` `describe.skip` stubs are owned by plans 49-07/49-04 respectively and were left untouched.

## Threat Flags

None. Static design-system content, no user input, no secrets, no network calls. Self-hosted fonts keep `font-src 'self'`; inline-SVG textures carry no external fetches; the one new inline script is CSP-hashed (not `unsafe-inline`).

## User Setup Required

None — no external service configuration required for this plan.

## Next Phase Readiness

- The full skeuo vocabulary (tokens, surfaces, 8 components, dual themes, fonts) is ready for plans 49-03..07 to consume.
- `/design` + visual baselines give downstream plans a stable visual-regression reference.
- Verifications green: `pnpm build` 0, `pnpm vitest run` 0 (27 passed / 8 pre-existing skips), `playwright test tests/visual.spec.ts --project=desktop` 0 (two-run clean).

## Self-Check: PASSED

All 15 spot-checked created files exist on disk; all 4 task commits (`564aafd`, `30b305a`, `594fe6e`, `7f8a528`) present in git history.

---
*Phase: 49-flowlang-dev-site*
*Completed: 2026-06-05*
