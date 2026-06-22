---
quick_id: 260621-vb6
slug: on-the-flow-website-home-page-widen-the-
date: 2026-06-22
status: complete
---

# Summary: Widen the home-page hero on desktop

## What changed

`flow-site/src/routes/+page.svelte` — `.layout` `max-width` raised from
`1080px` → `1440px`. The hero (and the rest of the home-page column it shares
with "Why Flow" and the feature cards) now extends a fair bit wider on desktop,
per the composer's chosen scope (whole-page column) and width (~1440px).

## Why this is desktop-only

`max-width` only constrains the column above ~1080px viewports. Below that the
column already fills available width minus the existing
`padding: 34px clamp(20px, 4vw, 48px) 90px` side buffer, so no media query was
needed and narrow/mobile layouts are unchanged.

## Verification

- Only one `1080` reference existed in `src/` — the `.layout` rule — so nothing
  else hard-coded the old width.
- Both card grids (`.cards3`, `.feat`) use `repeat(3, minmax(0, 1fr))`, so they
  scale fluidly into the wider column (no overflow, no fixed track widths).
- No test asserts on the old width (`home-tokens.test.ts` does not reference it).
- `pnpm -C flow-site build` passes (✓ built, adapter-cloudflare done).

## Files

- `flow-site/src/routes/+page.svelte`
