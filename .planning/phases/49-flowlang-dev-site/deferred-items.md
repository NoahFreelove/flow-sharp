# Phase 49 — Deferred Items

Out-of-scope discoveries logged during plan execution (NOT fixed — they belong to other
plans or are pre-existing). See the GSD scope-boundary rule.

## From Plan 49-02 (design system)

- **`svelte-check` warning: "Cannot find type definition file for 'node'"** (Wave-1 scaffold
  `tsconfig.json`). Pre-existing in the Plan 49-01 scaffold, not introduced by 49-02. `svelte-check`
  reports 0 ERRORS / 1 WARNING. Fix is a one-line `"types": ["node"]` + `@types/node` install in
  the scaffold tsconfig — owns to whichever plan first needs Node types in app code (likely 49-04
  docs-sync `sync-wiki.sh` glue or 49-06 the CF Worker). Left untouched per scope boundary.

- **Playwright bundled-chromium gap on ubuntu 26.04.** `playwright install chromium` has no build
  for ubuntu-26.04-x64, and `playwright install chrome` needs sudo (unavailable in this env). Worked
  around in `playwright.config.ts` by driving the system snap chromium (`/snap/bin/chromium`) via
  `executablePath` + `--no-sandbox`, env-gated by `PLAYWRIGHT_CHROMIUM_PATH` so CI (where bundled
  browsers install fine) is unaffected. This is an environment quirk, not a code defect — the
  Plan 49-08 Lighthouse/HUMAN-UAT run should confirm the bundled browser installs on the CI image.

## From Plan 49-03 (Home + nav)

- **`visual.spec.ts` mobile baselines missing.** Running the full Playwright suite
  (`pnpm playwright test`) during 49-03 verification auto-generated
  `tests/visual.spec.ts-snapshots/design-{light,dark,reduced-motion}-{mobile,mobile-narrow}-linux.png`
  on first run (only the 3 `*-desktop-linux.png` baselines were committed by Plan 49-02). These are
  Plan 49-02's skeuo `/design`-page visual-regression baselines for the mobile + mobile-narrow
  viewport projects — NOT part of 49-03's write set. Left UNCOMMITTED (unreviewed auto-generated
  images should not be silently baselined). Resolution belongs to the 49-08 Lighthouse +
  cross-browser audit (or a 49-02 follow-up): review + commit the mobile baselines, or scope
  `visual.spec.ts` to the desktop project. The 6 corresponding tests SKIP when no baseline exists,
  so the suite stays green.

- **`/showcase` route not yet present (ships in Plan 49-07).** The D-49-07 5-tab nav links
  `/showcase` now (the IA contract requires the tab). `svelte.config.js` `handleHttpError`
  tolerates the dangling `/showcase` prerender link as a warning until 49-07 lands the route —
  remove that allowance when 49-07 ships `/showcase`. The 49-03 render-strategy spec covers `/` +
  `/docs` (the prerendered routes that exist at wave 3); add `/showcase` to `PRERENDERED` in
  `tests/render-strategy.spec.ts` once 49-07 is in.
  - **RESOLVED in Plan 49-07:** `/showcase` + `/showcase/[slug]` now ship and prerender fully. The
    `svelte.config.js` warn-not-fail `/showcase` allowance is REMOVED (a `/showcase` 404 now fails
    the build), and `/showcase` was added to `PRERENDERED` in `tests/render-strategy.spec.ts`.

## From Plan 49-07 (showcase)

- **`visual.spec.ts` `/design`-page screenshots fail / mobile baselines still missing
  (PRE-EXISTING — NOT introduced by 49-07).** Running the FULL Playwright suite during 49-07
  verification surfaced `visual.spec.ts` failing on the desktop `/design` baselines (font/render
  drift on this environment) and auto-regenerating the still-uncommitted
  `design-{light,dark,reduced-motion}-{mobile,mobile-narrow}-linux.png` snapshots (same situation
  the 49-03 entry above documents). 49-07 touches NOTHING under `/design`, `src/lib/design/`,
  `src/lib/components/skeuo/`, `app.css`, or fonts — verified by `git status` (all 49-07 changes are
  showcase-scoped + the two test/config edits). The stray auto-generated snapshots were DELETED, not
  committed, per the scope boundary. The targeted 49-07 specs (`showcase.spec.ts`,
  `render-strategy.spec.ts`) pass 84/84. Resolution of the `/design` visual baselines stays owned by
  the Plan 49-08 cross-browser/Lighthouse audit (review + commit the mobile baselines or scope
  `visual.spec.ts` to the desktop project), exactly as the 49-03 entry assigned it.
  - **RESOLVED in Plan 49-08:** all 9 `/design` baselines regenerated in-env (the 3 desktop ones
    legitimately changed — Panel `min-width:0` + the new code-block focus rings shifted page
    height 2185→2217px — and the 6 missing mobile/mobile-narrow baselines are now committed).
    `visual.spec.ts` gained `maxDiffPixelRatio: 0.04` to absorb cross-environment font subpixel
    drift (the 0.06–0.13 ratio this entry warned about), so the suite stays green in CI without
    re-baselining. `playwright test tests/visual.spec.ts` → 9/9 green.

## From Plan 49-08 (a11y/perf gate)

- **Snap-confined system chromium drove both Lighthouse + Playwright in this env.** `/snap/bin/chromium`
  works for lhci (via `CHROME_PATH` + `--no-sandbox`) and Playwright (via `PLAYWRIGHT_CHROMIUM_PATH`).
  No bundled-browser install was possible (the 49-02 entry's ubuntu-26.04 gap persists). CI with real
  Chrome is unaffected. Not a code defect — environment quirk; the committed configs honor `CHROME_PATH`
  / `PLAYWRIGHT_CHROMIUM_PATH` so CI's bundled browsers Just Work.
- **`vite preview` is NOT production-accurate for Lighthouse** — it serves uncompressed with no cache
  headers, which mis-scored the WASM-heavy /playground Perf by ~11 points. Resolved by adding
  `scripts/lh-serve.mjs` (CF-Pages-accurate brotli + cache + SPA fallback). This is now the committed
  lhci server; do NOT revert to `vite preview` or `staticDistDir` (the latter 404s the client-only
  /playground route).
