---
phase: quick-260621-na4
plan: 01
subsystem: docs + flow-site
status: complete
tags: [docs, css, wiki, flow-site, typography]
requires: []
provides:
  - "Prose typography rules under .docs-prose (fixes all 26 flow-site /docs pages)"
  - "Restructured wiki/Home.md Key Features grouped into 5 thematic ### subsections"
affects:
  - flow-site/src/routes/docs/[slug]/+page.svelte
  - wiki/Home.md
tech-stack:
  added: []
  patterns:
    - "Svelte :global() prose rules consuming theme-swappable design tokens (no separate dark block)"
    - "GitHub loose-list single-blank-line spacing for breathing room"
key-files:
  created: []
  modified:
    - flow-site/src/routes/docs/[slug]/+page.svelte
    - wiki/Home.md
decisions:
  - "Reuse --color-ink/--color-ink-muted/--color-walnut/--color-brass + --space-* tokens so prose rules re-resolve under [data-theme='dark'] automatically — no separate dark block"
  - "No `a` override added — global app.css `a` rule owns prose-link color per UI-SPEC §Color"
  - "Home.md restructure is reorganization-only — sorted bullet diff is byte-identical (zero content loss)"
metrics:
  duration: ~6min
  completed: 2026-06-21
  tasks: 2
  files: 2
---

# Quick Task 260621-na4: Fix Wiki/Docs Spacing + Restore Prose CSS Summary

Restored block-level prose typography on flow-site `/docs` pages (zeroed by Tailwind v4 Preflight) and regrouped the flat `wiki/Home.md` Key Features list into five thematic `###` subsections — both render targets now read with structure and breathing room.

## What Was Done

### Task 1 — Restore prose typography in `.docs-prose`
Added `:global()` rules to the `<style>` block in `flow-site/src/routes/docs/[slug]/+page.svelte`, inserted after the `pre`/`:focus-visible` rules and before the `@media (max-width: 767px)` block. Covers `p`, `ul`/`ol` (disc/decimal markers + `padding-left` + vertical margin), `li` (margin + line-height), `li > p` (loose-list collapse), nested `li > ul`/`li > ol` (tighter margins), `blockquote` (walnut/brass border-left + muted color), `table`/`th`/`td` (collapse, hairline borders, header weight), `hr`, `strong`, `em`, plus first/last-child de-edging. All colors resolve through theme-swappable custom props so the rules re-resolve under `[data-theme='dark']` with no separate dark block. No `a` rule added — app.css owns prose-link color. This fixes all 26 wiki pages at once since they all render through the same `.docs-prose` article wrapper.

### Task 2 — Restructure `wiki/Home.md` Key Features
Replaced the flat ~40-line bullet list with the same bullets grouped under five `###` subsections: **Language Core**, **Notation & Composition**, **Generative & Expression**, **Synthesis & Audio**, **Export, Playback & Tooling**. Consistent single-blank-line spacing between bullets (GitHub loose-list "breathes"). Reorganization only — a sorted bullet-set diff against the prior commit is byte-identical, so every bullet (inline code + links) survives verbatim.

## Verification

- **Task 1 (plan gate, passed):** style block braces balanced; all 11 required prose selectors present (`ul`/`ol`/`li`/`p`/`blockquote`/`table`/`th`/`td`/`hr`/`strong`/`em`); no `.docs-prose :global(a)` override.
- **Task 2:** 5 subsections present; 27 `^- ` bullets retained (≥25 floor); backticks balanced; sorted bullet diff vs. prior commit = IDENTICAL.

## Deviations from Plan

**1. [Rule 1 — Verify-gate false positive] Task 2 raw-HTML check**
- **Found during:** Task 2 verification.
- **Issue:** The plan's automated gate flags `<[a-zA-Z]` anywhere in the section as "raw HTML that breaks mdsvex". It matched `<a, b, c` (from the inline-code span `` `<<a, b, c>>` ``) and `<K, V` (from `` `Dict<K, V>` ``). Both are pre-existing inline-code content carried over verbatim from the original Home.md — mdsvex treats `<...>` inside inline code as literal text, not HTML, so they render correctly and are NOT raw HTML.
- **Resolution:** Proved the spans exist identically in the prior commit (`git show HEAD:wiki/Home.md`), then re-ran a code-aware variant that strips inline-code regions before the HTML check — it passes cleanly. No content change made; the gate's regex is simply too naive (doesn't exclude inline-code context). Documented rather than altering content to satisfy a false positive.
- **Files modified:** none (analysis only).

No other deviations. `flow-site/static/wasm/**` untouched; no full `pnpm build` run (per scope — lightweight gates are the contract); no `a` override introduced.

## Commits

- `b3bdc3f` — fix(quick-260621-na4): restore prose typography in .docs-prose
- `8af9300` — docs(quick-260621-na4): group Home.md Key Features into thematic subsections

## Self-Check: PASSED

- FOUND: flow-site/src/routes/docs/[slug]/+page.svelte (modified, prose selectors present)
- FOUND: wiki/Home.md (modified, 5 subsections)
- FOUND commit: b3bdc3f
- FOUND commit: 8af9300
