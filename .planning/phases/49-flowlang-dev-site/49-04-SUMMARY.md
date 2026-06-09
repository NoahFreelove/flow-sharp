---
phase: 49-flowlang-dev-site
plan: 04
subsystem: docs
tags: [sveltekit, mdsvex, shiki, textmate-grammar, wiki-sync, prerender, vitest, playwright]

# Dependency graph
requires:
  - phase: 49-01
    provides: flow-site SvelteKit scaffold (mdsvex passthrough, vitest+playwright configs, gitignore for src/docs/wiki/)
  - phase: 49-02
    provides: skeuo components (Panel, Button) + surfaces (.surface-wood/.surface-paper/.surface-paper--staff) + design tokens
  - phase: 17
    provides: Flow TextMate grammar at vscode-extension/syntaxes/flow.tmLanguage.json (scopeName source.flow)
provides:
  - sync-wiki.sh (clone-or-seed wiki into src/docs/wiki/)
  - rewriteWikiLinks [[link]] + relative .md cross-link transform (fence-aware, idempotent)
  - lowercase-kebab slug + collision/reserved-index guard
  - config-driven docs-categories.json -> categorized /docs TOC
  - shiki.ts highlightFlow(code, lang) server-rendered Flow highlighting (Plan 49-03 contract)
  - /docs index (categorized) + /docs/[slug] (26 prerendered wiki pages)
affects: [49-03 (highlightFlow + Open-in-playground deep-link), 49-06 (encode.ts fills the /playground#code= href)]

# Tech tracking
tech-stack:
  added: [unist-util-visit 5.1.0, rehype-slug 6.0.0]
  patterns:
    - "Node-importable .js core (slug/transform/highlight/remark) so svelte.config.js (Node ESM, no .ts loader) can consume it; .ts shim re-exports the typed contract"
    - "mdsvex pipeline: remark plugin rewrites wiki cross-links, rehype-slug adds heading ids, highlight hook wraps flow blocks with Open-in-playground"
    - "import.meta.glob('/src/docs/wiki/*.md') + entries() to prerender every synced page"
    - "build-time grammar copy (sync-grammar.mjs) so the CF flow-site-only build never reaches the sibling vscode-extension project"

key-files:
  created:
    - flow-site/scripts/sync-wiki.sh
    - flow-site/scripts/sync-grammar.mjs
    - flow-site/src/lib/docs/transform.js
    - flow-site/src/lib/docs/transform.test.ts
    - flow-site/src/lib/docs/slug.js
    - flow-site/src/lib/docs/categories.ts
    - flow-site/src/lib/docs/highlight.js
    - flow-site/src/lib/docs/shiki.ts
    - flow-site/src/lib/docs/remark-wiki-links.js
    - flow-site/src/lib/docs/flow.tmLanguage.json
    - flow-site/src/lib/docs/__fixtures__/synthetic-wiki-link.md
    - flow-site/docs-categories.json
    - flow-site/src/routes/docs/+page.ts
    - flow-site/src/routes/docs/+page.svelte
    - flow-site/src/routes/docs/[slug]/+page.ts
    - flow-site/src/routes/docs/[slug]/+page.svelte
  modified:
    - flow-site/svelte.config.js
    - flow-site/package.json
    - flow-site/tests/docs-render.spec.ts
    - flow-site/tests/docs-toc.spec.ts
    - .gitignore

key-decisions:
  - "Core docs logic authored as .js (not .ts) so svelte.config.js can import it directly; shiki.ts is a typed re-export shim satisfying the Plan 49-03 contract + the tmLanguage/createHighlighter key-link"
  - "Transform handles BOTH [[Page]] wiki-syntax AND relative Page.md(#anchor) links — the wiki actually uses the .md style heavily (RESEARCH only anticipated [[ ]])"
  - "Grammar copied into flow-site/src/lib/docs/flow.tmLanguage.json (committed) via sync-grammar.mjs; CF flow-site-only build cannot reach the sibling vscode-extension"
  - "prerender.handleHttpError/handleMissingId warn (not fail) on genuine wiki content gaps (dangling Articulations.md/Tuning.md links, missing section anchors)"

patterns-established:
  - "Node-importable .js + .ts contract shim for code shared between svelte.config.js and app/test"
  - "config-driven TOC: docs-categories.json is the single source; categories.ts warns + Uncategorized-groups any orphan wiki page"

requirements-completed: [REQ-SITE-DOCS-01, REQ-SITE-DOCS-02, REQ-SITE-DOCS-03, REQ-SITE-DOCS-04]

# Metrics
duration: 50min
completed: 2026-06-05
---

# Phase 49 Plan 04: Wiki Docs Sync + Rendering Summary

**The 26-page hand-written wiki becomes the site's documentation: build-time synced, GitHub-wiki + relative cross-links rewritten to /docs routes, every page prerendered at /docs/[slug] with server-rendered shiki Flow highlighting, and a config-driven categorized /docs index.**

## Performance

- **Duration:** ~50 min
- **Started:** 2026-06-05T17:43Z
- **Completed:** 2026-06-05T17:58Z
- **Tasks:** 3 of 3
- **Files modified/created:** 22 (1470 insertions)

## Accomplishments

- `sync-wiki.sh` populates `src/docs/wiki/` two ways (D-49-25): `git clone --depth 1 "$WIKI_REPO_URL"` for the CF build, in-repo `wiki/` seed fallback for local/CI; `set -euo pipefail` + a final non-empty assertion fail the build loudly rather than ship empty docs (threat T-49-04-SYNC).
- `rewriteWikiLinks` is fence-aware (the `[[1,10],...]` array in `Collections.md` survives), idempotent, and unit-proven against a SYNTHETIC `[[Quick-Start]]` fixture (the real wiki has zero `[[ ]]` links — RESEARCH Pitfall 7). Slugs are lowercase-kebab with reserved-`index` + collision guards (D-49-27).
- All 26 wiki pages prerender at `/docs/[slug]` (52 client+cloudflare HTML files) in a two-column wood-sidebar + paper-staff layout with `aria-current` nav (UI-SPEC §Docs page). Server-rendered shiki Flow highlighting loads the REAL Phase 17 grammar (RESEARCH-corrected path), zero client JS (D-49-15); each `flow` block carries an Open-in-playground secondary button.
- `/docs` index renders the four-category TOC (`<Panel header>` cards) from `docs-categories.json` — config-driven, not hard-coded (D-49-22).

## Task Commits

1. **Task 1: wiki sync + [[link]] transform + slug + categories (tdd)** - `d9cd152` (feat)
2. **Task 2: shiki highlighter + mdsvex wiring + /docs/[slug]** - `fd20b26` (feat)
3. **Task 3: categorized /docs index TOC** - `704e64c` (feat)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Missing functionality] Wiki uses relative `Page.md` cross-links, not `[[ ]]`**
- **Found during:** Task 2 (prerender 404'd on `/docs/Playback-and-Export.md`)
- **Issue:** The plan/RESEARCH framed the transform purely around `[[Page-Name]]` syntax. The real wiki cross-links pages with ordinary markdown `[Label](Page-Name.md)` / `[Label](Page-Name.md#anchor)` links, which 404 against lowercase-kebab `/docs/<slug>` routes.
- **Fix:** Extended `remark-wiki-links.js` to also rewrite relative `.md` link nodes to `/docs/<slug>(#anchor)` (external/absolute/non-`.md` left untouched). The `[[ ]]` path stays wired + unit-tested.
- **Files:** `flow-site/src/lib/docs/remark-wiki-links.js` — **Commit:** `fd20b26`

**2. [Rule 3 - Blocking] svelte.config.js (Node ESM) cannot import .ts**
- **Found during:** Task 2 (config load failed importing `shiki.ts`/`transform.ts`)
- **Fix:** Authored the shared core as `.js` (`slug.js`, `transform.js`, `highlight.js`, `remark-wiki-links.js`); `shiki.ts` is a typed re-export shim that still references `createHighlighter` + `flow.tmLanguage.json` (the Plan 49-03 contract + key-link). JSON grammar import uses an `with { type: 'json' }` attribute for Node ESM.
- **Files:** `slug.js`, `transform.js`, `highlight.js`, `shiki.ts` — **Commit:** `fd20b26`

**3. [Rule 3 - Blocking] Heading anchors + dangling content links broke prerender**
- **Found during:** Task 2 (`handleMissingId` on `#voice-pool`; dangling `Articulations.md`)
- **Fix:** Added `rehype-slug` (heading ids so `#anchor` links resolve) + `prerender.handleHttpError`/`handleMissingId: 'warn'` for genuine wiki CONTENT gaps (a few links target pages/sections that don't exist) so a content gap never fails the deploy.
- **Files:** `flow-site/svelte.config.js` — **Commit:** `fd20b26`

**4. [Rule 3 - Blocking] Global `*.md` gitignore hid the test fixture**
- **Found during:** Task 1 (`git add` refused the synthetic fixture)
- **Fix:** Added a `.gitignore` negation for `flow-site/src/lib/docs/__fixtures__/**` (the build-time wiki clone at `src/docs/wiki/` stays ignored).
- **Files:** `.gitignore` — **Commit:** `d9cd152`

## New Packages

- `unist-util-visit` 5.1.0 (remark AST walk) + `rehype-slug` 6.0.0 (heading ids) — both first-party unified-collective, MIT, millions of weekly downloads. No package-install failures.

## Known Stubs

- **Open-in-playground href → `/playground` (plain fallback).** Each docs `flow` block's "Open in playground" button links to `/playground` and stashes the source in a `data-flow-source` attribute. The real `/playground#code=...` deep-link encoding is Plan 49-06's `encode.ts` (explicitly deferred by the plan: "tolerating its absence with a plain /playground fallback until 49-06 lands"). The button + source payload are wired; 49-06 fills the href. Not goal-blocking — the docs render + link out correctly today.

## Threat Flags

None — no new network endpoints, auth paths, or trust boundaries beyond the plan's `<threat_model>` (build-time wiki clone → escaped static HTML; all three registered threats mitigated: shiki escapes HTML, sync-wiki fails loudly with seed fallback, the link regex is fence-aware + idempotency-tested).

## Verification

- `pnpm -C flow-site vitest run src/lib/docs/transform.test.ts` → 15/15 green (full suite: 42 passed, 5 skipped = pre-existing 49-06/49-07 Wave-0 stubs).
- `pnpm -C flow-site build` → exit 0; 26 docs pages + index prerendered (27 cloudflare HTML).
- `pnpm -C flow-site exec playwright test tests/docs-render.spec.ts tests/docs-toc.spec.ts` → 87 passed (driven by the system-chromium fallback already in playwright.config.ts).
- `pnpm -C flow-site check` (svelte-check) → 0 errors (2 pre-existing Wave-0 warnings).

## Self-Check: PASSED

All 17 created files exist on disk; all 3 task commits (d9cd152, fd20b26, 704e64c) present in git history.
