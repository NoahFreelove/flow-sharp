---
phase: 49-flowlang-dev-site
plan: 01
subsystem: infra
tags: [sveltekit, svelte5, tailwind4, cloudflare-pages, wasm, vitest, playwright, lighthouse, monaco]

# Dependency graph
requires:
  - phase: 48-wasm-runtime-webaudio-backend
    provides: "flow-runtime.js + AppBundle (FlowTarget=Web publish) — the frozen WASM runtime consumed by /playground"
provides:
  - "flow-site/ SvelteKit 2 + Svelte 5 + TS + Tailwind v4 + adapter-cloudflare project that builds clean to .svelte-kit/cloudflare/"
  - "Phase 48 AppBundle committed under flow-site/static/wasm/ (layout-intact, runtime byte-identical)"
  - "scripts/sync-runtime.sh — regenerates static/wasm/ via dotnet publish -p:FlowTarget=Web"
  - "_headers at project root — CSP + Permissions-Policy + scoped COOP/COEP"
  - "Per-route render strategy: Home prerendered, /playground client-only SPA"
  - "Full Nyquist Wave 0 test stack: vitest + playwright (desktop/375/320) + lhci (4-axis >=90) + axe + testing-library/svelte"
affects: [49-02-design-tokens, 49-03-nav-pages, 49-04-docs-wiki, 49-05-playground-monaco-wasm, 49-06-gist-oauth, 49-07-share-encode, 49-08-verify-lighthouse-a11y, 49-09-deploy-runbook]

# Tech tracking
tech-stack:
  added:
    - "@sveltejs/kit 2.63.0, svelte 5.56.2, vite 8.0.16, typescript 6.0.3"
    - "@sveltejs/adapter-cloudflare 7.2.8, mdsvex 0.12.7, shiki 4.2.0"
    - "tailwindcss 4.3.0 + @tailwindcss/vite 4.3.0 (CSS-first, no tailwind.config.js)"
    - "monaco-editor 0.55.1, fflate 0.8.3, sharp 0.34.5"
    - "vitest 4.1.8, @playwright/test 1.60.0, @lhci/cli 0.15.1, @axe-core/playwright 4.11.3, @testing-library/svelte 5.3.1, jsdom 29.1.1"
  patterns:
    - "Per-route render strategy via module-context page-option exports (prerender/ssr/csr)"
    - "Opaque WASM AppBundle as committed static assets (Vite never processes; self-loads via dotnet.js)"
    - "pnpm 11 native-build allowlist via pnpm-workspace.yaml allowBuilds"

key-files:
  created:
    - flow-site/package.json
    - flow-site/svelte.config.js
    - flow-site/vite.config.ts
    - flow-site/src/app.css
    - flow-site/src/routes/+layout.svelte
    - flow-site/src/routes/+page.svelte
    - flow-site/src/routes/playground/+page.svelte
    - flow-site/scripts/sync-runtime.sh
    - flow-site/_headers
    - flow-site/static/wasm/ (Phase 48 AppBundle, 38 files)
    - flow-site/vitest.config.ts
    - flow-site/playwright.config.ts
    - flow-site/lighthouserc.cjs
    - flow-site/tests/ (12 E2E stub specs)
    - flow-site/src/lib/docs/transform.test.ts
    - flow-site/src/lib/share/encode.test.ts
    - flow-site/workers/gist-auth.test.ts
  modified:
    - .gitignore (root — negate flow-site/tests/)

key-decisions:
  - "Committed the published AppBundle into static/wasm/ (RESEARCH Open Q2) — keeps the CF Pages build pure-Node"
  - "_headers placed at flow-site/ project root, not static/ (RESEARCH A4, adapter-cloudflare convention)"
  - "COOP/COEP scoped to /playground/* only (RESEARCH Open Q1); CSP + Permissions-Policy global"
  - "pnpm via corepack; sv create non-interactive minimal+TS; Tailwind v4 + adapter wired manually"

patterns-established:
  - "Page-option exports live in <script module> blocks (runes-mode safe)"
  - "Test runners partitioned: vitest owns src/** + workers/**, playwright owns tests/**"

requirements-completed: [REQ-SITE-IA-02, REQ-SITE-IA-03, REQ-SITE-DEPLOY-01]

# Metrics
duration: 10min
completed: 2026-06-05
---

# Phase 49 Plan 01: SvelteKit Scaffold + Phase 48 AppBundle + Test Stack Summary

**Greenfield `flow-site/` SvelteKit 2 / Svelte 5 / TS / Tailwind-v4 / adapter-cloudflare shell that builds clean, ships the Phase 48 WASM runtime under `static/wasm/`, declares per-route render strategy, and stands up the entire Nyquist test stack — ready for a Cloudflare Pages deploy (human checkpoint pending).**

## Performance

- **Duration:** ~10 min (autonomous tasks)
- **Started:** 2026-06-05T17:07:28-04:00
- **Completed:** 2026-06-05T17:12:34-04:00
- **Tasks:** 3 of 3 autonomous tasks complete (Task 4 = blocking human-action checkpoint, not executed)
- **Files modified:** 77 tracked under flow-site/ (39 project files + 38 AppBundle files) + root .gitignore

## Accomplishments
- `flow-site/` scaffolded and **builds clean** (`pnpm build` exits 0 → `.svelte-kit/cloudflare/`) with adapter-cloudflare, Tailwind v4 (no `tailwind.config.js`), mdsvex passthrough, and runes mode.
- Phase 48 AppBundle committed **verbatim** under `static/wasm/` (flow-runtime.js at root + `_framework/` sibling, index.html excluded); runtime **byte-identical** to `flow-lang/wasm/flow-runtime.js` (HANDOFF §8 — never edited). `sync-runtime.sh` regenerates it.
- Security `_headers` at project root (Permissions-Policy denies mic/camera/geo; CSP `script-src 'self' 'wasm-unsafe-eval'` + connect-src self+GitHub; scoped COOP/COEP on `/playground/*`).
- Full Wave 0 test stack installed + green: `vitest run` exits 0 (8 stubs skipped); playwright config has desktop/mobile-375/mobile-narrow-320 projects; lighthouserc enforces 4-axis ≥0.9 on `/`, `/docs`, `/playground`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Scaffold flow-site + adapter-cloudflare + Tailwind v4 + render strategy** — `7bf1295` (feat)
2. **Task 2: Commit the Phase 48 AppBundle + sync-runtime.sh + _headers** — `98e7ba2` (feat)
3. **Task 3: Install + configure the Nyquist Wave 0 test stack (stubs + configs)** — `dbd493f` (feat)

**Plan metadata:** committed alongside this SUMMARY.

## Files Created/Modified
- `flow-site/svelte.config.js` — adapter-cloudflare + mdsvex preprocess (`.md`/`.svx` extensions)
- `flow-site/vite.config.ts` — `@tailwindcss/vite` plugin (v4 path)
- `flow-site/src/app.css` — `@import "tailwindcss";` + `@theme` placeholder (tokens are Plan 49-02)
- `flow-site/src/routes/+page.svelte` — Home, `prerender = true`, "Flow — coming soon" placeholder
- `flow-site/src/routes/playground/+page.svelte` — `ssr=false; csr=true` SPA shell (no Monaco/WASM yet)
- `flow-site/src/routes/+layout.svelte` — imports app.css, placeholder `<a href="/">Flow</a>` nav
- `flow-site/scripts/sync-runtime.sh` — `dotnet publish -p:FlowTarget=Web` + layout-preserving cp into static/wasm/
- `flow-site/_headers` — CSP / Permissions-Policy / scoped COOP-COEP
- `flow-site/static/wasm/**` — 38-file Phase 48 AppBundle
- `flow-site/vitest.config.ts`, `flow-site/playwright.config.ts`, `flow-site/lighthouserc.cjs`
- `flow-site/tests/*.spec.ts` — 12 E2E `test.skip` stubs, each naming its REQ-SITE-* anchor
- `flow-site/src/lib/docs/transform.test.ts`, `flow-site/src/lib/share/encode.test.ts`, `flow-site/workers/gist-auth.test.ts` — 3 unit `describe.skip` stubs
- `flow-site/package.json` — pinned deps + `test`/`test:e2e`/`lh` scripts
- `flow-site/pnpm-workspace.yaml` — `allowBuilds` for esbuild/sharp/workerd
- `.gitignore` (root) — negation for `flow-site/tests/`

## Decisions Made
- **Used `sv create` non-interactively** (`--template minimal --types ts --no-add-ons --no-install`) then layered Tailwind v4 + adapter-cloudflare + mdsvex manually — avoids the headless-agent prompt hang while keeping full control over pinned versions (matches RESEARCH §Standard Stack).
- **Single source of truth for pnpm native-build approval** moved to `pnpm-workspace.yaml allowBuilds` (pnpm 11 canonical location); dropped the duplicate `pnpm.onlyBuiltDependencies` from package.json.
- **Copied the AppBundle from the already-fresh main-repo publish** rather than re-running `dotnet publish` (env note confirmed it is current; runtime diff confirms byte-identity), saving a slow WASM rebuild. `sync-runtime.sh` is committed for future refreshes.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking issue] Root `.gitignore` `tests/` rule hid the Playwright E2E specs**
- **Found during:** Task 3 (test-stack configuration)
- **Issue:** The repo-root `.gitignore` has a global `tests/` rule (for C# test outputs) with per-project negation exceptions. It silently ignored `flow-site/tests/*.spec.ts`, so the 12 E2E stubs would never be committed — leaving the test stack incomplete.
- **Fix:** Added `!flow-site/tests/` + `!flow-site/tests/**` negations to the root `.gitignore`, mirroring the existing `vscode-extension/tests/` exception pattern.
- **Files modified:** `.gitignore` (root)
- **Verification:** `git check-ignore flow-site/tests/nav.spec.ts` returns non-zero (not ignored); all 12 specs appear in `git add --dry-run`.
- **Committed in:** `dbd493f` (part of Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1× Rule 3).
**Impact on plan:** Necessary for correctness — without it the Wave 0 E2E specs (a plan deliverable) could not be tracked. No scope creep.

## Issues Encountered
- pnpm 11's native-build security default initially ignored `esbuild`/`sharp`/`workerd` build scripts. Resolved by configuring `pnpm-workspace.yaml allowBuilds` and reinstalling; `sharp` confirmed loadable, build green.

## Known Stubs

All stubs here are **intentional Wave-0 scaffolding** explicitly mandated by the plan (foundation-only — no design/docs/playground logic yet). None block this plan's goal; each names the downstream plan that resolves it:

| Stub | File(s) | Resolved by |
|------|---------|-------------|
| "Flow — coming soon" placeholder Home | `src/routes/+page.svelte` | Plan 49-02/03 |
| "Playground" placeholder shell (no Monaco/WASM) | `src/routes/playground/+page.svelte` | Plan 49-05 |
| Placeholder `<a href="/">Flow</a>` nav | `src/routes/+layout.svelte` | Plan 49-02/03 |
| `@theme` placeholder comment (no tokens) | `src/app.css` | Plan 49-02 |
| 12 E2E `test.skip` specs | `tests/*.spec.ts` | Plans 49-02..49-08 |
| 3 unit `describe.skip` stubs | `src/lib/docs/transform.test.ts`, `src/lib/share/encode.test.ts`, `workers/gist-auth.test.ts` | Plans 49-04 / 49-07 / 49-06 |
| mdsvex highlight passthrough | `svelte.config.js` | Plan 49-04 (shiki + Flow grammar) |

## User Setup Required

**Cloudflare Pages deploy requires the composer's Cloudflare account (D-49-36) — Task 4 is a blocking `checkpoint:human-action`, NOT executed by this agent.** See the checkpoint message returned to the orchestrator. The composer must:
- Create a CF Pages project (`flow-music`, or `flow-music-playground` if taken)
- Build command `pnpm -C flow-site build`; output dir `flow-site/.svelte-kit/cloudflare`
- Report back the assigned `<project>.pages.dev` URL (needed by Plan 49-06 OAuth redirect + Plan 49-09 runbook)

## Verification

- `pnpm -C flow-site build` → exit 0, produces `.svelte-kit/cloudflare/` (with `_headers` + `wasm/` shipped).
- `pnpm -C flow-site vitest run` → exit 0 (3 files / 8 tests skipped, no failures).
- `static/wasm/flow-runtime.js` + `static/wasm/_framework/dotnet.js` present; `diff -q flow-lang/wasm/flow-runtime.js flow-site/static/wasm/flow-runtime.js` → identical.
- `_headers` at project root contains `Permissions-Policy` + `Content-Security-Policy` + `wasm-unsafe-eval`.
- `playwright.config.ts` defines `mobile` (375px) + `mobile-narrow` (320px); `lighthouserc.cjs` references `/`, `/docs`, `/playground` with 4-axis `0.9` thresholds.
- **Live `<project>.pages.dev` deploy:** PENDING human checkpoint (Task 4).

## Self-Check: PASSED

All 17 created files verified present on disk; all 3 task commits (`7bf1295`, `98e7ba2`, `dbd493f`) verified in git history.
