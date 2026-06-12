---
quick_id: 260611-sqk
title: Remove iOS-6 bottom tab bar — single top nav site-wide
created: 2026-06-12
status: in-progress
must_haves:
  truths:
    - Home page (`/`) has NO bottom tab bar; every route has a single top nav.
    - Home's top nav (the iOS-6 `.nav` pill in `.toolbar`) is usable on ALL widths, including ≤600px (previously hidden on mobile, where the tabbar was the only nav).
    - No horizontal document overflow at 320px / 375px.
    - flow-site vitest + the nav/responsive/render-strategy playwright specs pass; `pnpm check` clean; `pnpm build` succeeds.
  artifacts:
    - flow-site/src/routes/+page.svelte (tabbar removed; nav scrollable on mobile)
    - flow-site/src/routes/home-a11y.test.ts (tabbar assertions removed; one-nav)
    - flow-site/tests/nav.spec.ts (mobile branch → toolbar nav, not tabbar)
    - flow-site/tests/responsive.spec.ts (≤600px → toolbar nav visible)
    - flow-site/tests/render-strategy.spec.ts (GitHub link via toolbar nav)
    - flow-site/tests/a11y.spec.ts (comment only)
---

# Quick 260611-sqk — Remove iOS-6 bottom tab bar

## Why
User: the bottom bar randomly disappears (leaving home) and home shows top + bottom
bars at once — "pick one and stick with it." Chosen: **top bar everywhere**. The home
page is iOS-6 (top `.toolbar` + fixed bottom `.tabbar`); other routes have only the
shared top `.site-chrome`. Remove the home's bottom tab bar so nav is one top bar
site-wide.

## Gotcha (load-bearing)
On `/` at ≤600px, `@media (max-width:600px){ .nav { display:none } }` HIDES the top
pill nav — the bottom `.tabbar` IS the mobile nav. So removing the tabbar requires
making the top `.nav` usable on mobile (scrollable pill), else home loses nav on mobile.
Several tests assert the tabbar exists/visible — they must be retargeted to the top nav.

## Tasks
1. **`+page.svelte` — remove tabbar + fix mobile nav**
   - Delete the `<!-- bottom tab bar --><nav class="tabbar">…</nav>` markup (keep the
     `.ios6-page` closing `</div>`).
   - Delete the `/* ---- bottom tab bar ---- */` + all `.tabbar*` CSS rules.
   - `.footer`: drop `padding-bottom: 70px` (was clearing the fixed bar) → small value.
   - Replace `@media (max-width:600px){ .nav{display:none} }` with a rule that keeps
     `.nav` visible + horizontally scrollable (`min-width:0; overflow-x:auto;
     scrollbar-width:none; white-space:nowrap`), tighten `.toolbar` gap/padding on mobile
     so brand + scrollable nav + toggle fit with NO document overflow at 320px.
2. **Tests** — retarget tabbar assertions to the single top `nav[aria-label="Primary"]`:
   - `home-a11y.test.ts`: remove the `aria-label="Tab bar"` test; "exactly two navs" → one.
   - `nav.spec.ts`: nav now visible at all widths; mobile branch checks toolbar nav, not tabbar.
   - `responsive.spec.ts`: ≤600px asserts toolbar Primary nav visible (no tabbar).
   - `render-strategy.spec.ts`: GitHub link located in toolbar nav at narrow widths.
   - `a11y.spec.ts`: update the descriptive comment.
3. **Verify**: `pnpm -C flow-site check`; `pnpm -C flow-site test`; `pnpm -C flow-site build`;
   `pnpm -C flow-site exec playwright test nav.spec.ts responsive.spec.ts render-strategy.spec.ts`.

## Out of scope
- Other routes' chrome (unchanged). No visual redesign of the toolbar beyond mobile scroll.
