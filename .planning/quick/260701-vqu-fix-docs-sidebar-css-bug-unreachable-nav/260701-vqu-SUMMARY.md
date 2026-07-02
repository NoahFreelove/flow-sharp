---
phase: 260701-vqu
plan: 01
subsystem: flow-site
status: complete
tags: [css, responsive, playwright, docs, bugfix]
requires: []
provides: [scrollable-docs-sidebar, shared-toolbar-height-token]
affects:
  - flow-site/src/lib/design/tokens.css
  - flow-site/src/routes/docs/[slug]/+page.svelte
  - flow-site/src/lib/components/SiteToolbar.svelte
  - flow-site/tests/responsive.spec.ts
key-files:
  created: []
  modified:
    - flow-site/src/lib/design/tokens.css
    - flow-site/src/routes/docs/[slug]/+page.svelte
    - flow-site/src/lib/components/SiteToolbar.svelte
    - flow-site/tests/responsive.spec.ts
decisions:
  - "--toolbar-height (58px) declared once in tokens.css :root as a theme-independent layout dimension; both consumers reference it with a 58px fallback."
metrics:
  duration: ~8m
  completed: 2026-07-01
commit: 8bd3d06
---

# Phase 260701-vqu Plan 01: Fix Docs Sidebar CSS Bug (Unreachable Nav) Summary

Made the pinned `position: sticky` docs sidebar scroll its own content on short desktop viewports so the lower docs nav links are reachable, via a shared `--toolbar-height` token and a focused Playwright regression guard.

## What Changed

### Task 1 — CSS fix (three coordinated edits)

**flow-site/src/lib/design/tokens.css** — added one theme-independent layout token in the `:root` block, just above the spacing scale:
```css
--toolbar-height: 58px;
```

**flow-site/src/lib/components/SiteToolbar.svelte** — `.toolbar` height now references the shared token (fallback preserved so the component stays self-contained):
```css
height: var(--toolbar-height, 58px);   /* was: height: 58px; */
```

**flow-site/src/routes/docs/[slug]/+page.svelte** — desktop `.docs-sidebar` rule: `top` now clears the sticky toolbar and the sidebar caps its height + scrolls internally:
```css
top: calc(var(--toolbar-height, 58px) + var(--space-4));            /* was: top: var(--space-4); */
max-height: calc(100dvh - var(--toolbar-height, 58px) - 2 * var(--space-4));  /* added */
overflow-y: auto;                                                    /* added */
```
Existing `padding`, `border-radius`, `min-width: 0` and the explanatory comment kept. The mobile `@media (max-width: 767px)` block (`.docs-sidebar { position: static; }`) is untouched — those desktop declarations do not apply there. Arithmetic: `top + max-height = 100dvh - var(--space-4)`, leaving a bottom gap equal to `--space-4`.

### Task 2 — Playwright regression test

**flow-site/tests/responsive.spec.ts** — appended a new `test.describe` block (existing blocks untouched), gated to the desktop project via `if (testInfo.project.name !== 'desktop') return;`. At `1280x500` on `/docs/flow-operator` it asserts `.docs-sidebar` `scrollHeight > clientHeight` (fails against pre-fix CSS where they are equal), then scrolls the last `.docs-cat__list a` link into view, asserts visible, clicks it, and asserts URL matches `/\/docs\//` — the click is the load-bearing reachability proof.

## Verification / Test Results

| Check | Result |
|-------|--------|
| `pnpm -C flow-site test` (vitest) | 140 passed (17 files) |
| `pnpm -C flow-site build` | built in 9.75s, adapter-cloudflare done |
| `pnpm -C flow-site exec playwright test tests/responsive.spec.ts` | 24 passed (incl. new regression; desktop runs real assertions 1.2s, mobile projects early-return) |
| `pnpm -C flow-site exec playwright test tests/docs-render.spec.ts` | 81 passed (no 1280x800 desktop regression) |

Plan automated verify checks: `css-ok` and `test-scaffold-ok` both passed.

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None.

## Self-Check: PASSED

- Commit `8bd3d06` exists (`git rev-parse --short HEAD`).
- All four modified files present in the commit (`4 files changed, 52 insertions(+), 2 deletions(-)`).
- Only the four source files staged; concurrent flow-lang/*.cs changes left unstaged and out of the commit.
