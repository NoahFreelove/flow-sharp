---
quick_id: 260611-sqk
title: Remove iOS-6 bottom tab bar — single top nav site-wide
date: 2026-06-12
status: complete
---

# Quick 260611-sqk — Remove iOS-6 bottom tab bar

## Outcome
The flowlang.dev home page (`/`) no longer ships a bottom tab bar. Every route now uses a
single top navigation bar, fixing the "weird" behavior the composer reported (bottom bar
vanishing on navigation away from home; home showing top + bottom bars at once).

## What changed
- **`flow-site/src/routes/+page.svelte`**
  - Removed the `<nav class="tabbar">` markup (the fixed bottom tab bar) and all `.tabbar*` CSS.
  - `.footer`: dropped the `padding-bottom: 70px` that reserved space for the fixed bar → `24px`.
  - **Mobile fix (load-bearing):** the home's top pill nav was previously HIDDEN at ≤600px
    (`.nav { display:none }`) because the bottom tab bar was the mobile nav. Replaced that with
    a rule that keeps the pill nav visible and **scrolls it horizontally inside the toolbar**
    (`min-width:0; overflow-x:auto; scrollbar hidden; nowrap`), and tightened the toolbar
    gap/padding on mobile — so the single top nav works at every width with no document overflow.
- **Tests retargeted from the (now-removed) tabbar to the single top nav:**
  - `src/routes/home-a11y.test.ts` — removed the "Tab bar" assertion; nav count 2 → 1; added a
    guard that no `.tabbar`/"Tab bar" nav is reintroduced.
  - `tests/nav.spec.ts` — dropped the `isHomeToolbarNavHidden`/tabbar branches; the toolbar
    `nav[aria-label="Primary"]` is asserted at all widths.
  - `tests/responsive.spec.ts` — ≤600px now asserts the Primary nav visible + `Tab bar` count 0.
  - `tests/render-strategy.spec.ts` — GitHub link located in the Primary nav at all widths.
  - `tests/a11y.spec.ts` — comment updated.

## Verification
- `pnpm -C flow-site test` → **133/133** vitest passed.
- `pnpm -C flow-site build` → clean (adapter-cloudflare).
- `pnpm -C flow-site exec playwright test nav responsive render-strategy` → **72/72** across
  desktop / 375 / 320, incl. "no horizontal overflow at 320px" and the 5-tab + GitHub mobile checks.
- `pnpm check` shows 50 pre-existing errors (vitest-global types in `home-deeplinks.test.ts` /
  `home-tokens.test.ts` + missing `node` types) — confirmed pre-existing (present with my changes
  stashed); NOT introduced here.

## Notes / scope
- Other routes' chrome unchanged. Home keeps its iOS-6 toolbar look (per composer's choice
  "top bar everywhere — home keeps its iOS-6 top toolbar").
- On very narrow screens the top nav scrolls horizontally to reach Showcase/GitHub (icons were
  only ever in the removed bottom bar; the top pill nav is text). If a bottom icon bar is ever
  wanted back as the *sole* nav, that's the other branch the composer declined.
