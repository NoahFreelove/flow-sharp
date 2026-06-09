---
phase: 49
slug: flowlang-dev-site
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-05
---

# Phase 49 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from
> `49-RESEARCH.md` § Validation Architecture. This is a **greenfield** `flow-site/`
> SvelteKit project — no test infrastructure exists yet, so Wave 0 must install it.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework (unit/component)** | Vitest 4.x (+ `@testing-library/svelte` or `vitest-browser-svelte` for Svelte 5 component tests) |
| **Framework (E2E/smoke)** | Playwright 1.6x |
| **Lighthouse gate** | `@lhci/cli` (Lighthouse CI) — Performance / A11y / Best-Practices / SEO ≥90 (D-49-31) |
| **A11y assertions** | `@axe-core/playwright` inside E2E specs |
| **Config files** | `flow-site/vitest.config.ts`, `flow-site/playwright.config.ts`, `flow-site/lighthouserc.cjs` — none exist (Wave 0 installs) |
| **Quick run command** | `pnpm -C flow-site vitest run` |
| **Full suite command** | `pnpm -C flow-site vitest run && pnpm -C flow-site playwright test && pnpm -C flow-site lhci autorun` |
| **Estimated runtime** | ~30s unit / ~90s E2E / ~120s Lighthouse |

---

## Sampling Rate

- **After every task commit:** `pnpm -C flow-site vitest run` (fast unit pass: docs transform, slug kebab, share encode/decode, worker CSRF)
- **After every plan wave:** `pnpm -C flow-site vitest run && pnpm -C flow-site playwright test`
- **Before `/gsd:verify-work` (Plan 49-08 gate):** full suite incl. `pnpm -C flow-site lhci autorun` green; cross-browser HUMAN-UAT (Chrome / Firefox / Safari + mobile) re-smokes audio (Phase 48 HANDOFF §7 left Chrome/Safari audio unverified)
- **Max feedback latency:** ~30s (unit) — no watch-mode flags in CI

---

## Per-Task Verification Map

> Requirement IDs are the planner-formalized `REQ-SITE-*` anchors. Threat refs from the
> Plan 49-06 OAuth/CSP threat model. All "File Exists" are ❌ at phase start (greenfield).

| Req | Behavior | Threat Ref | Test Type | Automated Command | File Exists | Status |
|-----|----------|------------|-----------|-------------------|-------------|--------|
| REQ-SITE-IA-01 | 5-tab nav renders + local routes resolve | — | E2E | `playwright test tests/nav.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-IA-02 | per-route render strategy (prerender/SPA/SSR) | — | E2E | `playwright test tests/render-strategy.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-DOCS-01 | 26 wiki pages render at /docs/[slug] | — | E2E (loop slugs) | `playwright test tests/docs-render.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-DOCS-02 | `[[link]]` transform + slug kebab-case | — | unit | `vitest run src/lib/docs/transform.test.ts` | ❌ W0 | ⬜ |
| REQ-SITE-DOCS-03 | categorized TOC from docs-categories.json | — | unit + E2E | `vitest run` + `playwright test tests/docs-toc.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-PLAYGROUND-01 | WASM runtime boots (smoke, no boot error) | — | E2E | `playwright test tests/wasm-boot.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-PLAYGROUND-02 | Run produces stdout for `(print "hi")` | — | E2E | `playwright test tests/playground-run.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-PLAYGROUND-03 | Run+audio gesture chain resumes AudioContext | — | E2E | `playwright test tests/playground-audio.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-PLAYGROUND-04 | MIDI/WAV download appears when produced | — | E2E (download event) | `playwright test tests/playground-export.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-PLAYGROUND-05 | Monaco read-only <768px | — | E2E (mobile viewport) | `playwright test tests/playground-mobile.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-SHARE-01 | URL-fragment encode↔decode round-trips | T-49-CSP | unit | `vitest run src/lib/share/encode.test.ts` | ❌ W0 | ⬜ |
| REQ-SITE-SHARE-02 | OAuth worker validates `state` + exchanges code (mocked) | T-49-OAUTH | unit/integration | `vitest run workers/gist-auth.test.ts` | ❌ W0 | ⬜ |
| REQ-SITE-A11Y-01..03 | axe: 0 critical violations on /, /docs, /playground | — | E2E + axe | `playwright test tests/a11y.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-DESIGN-01..04 | skeuo components render both themes + reduced-motion | — | component + visual-reg | `vitest run` + `playwright test tests/visual.spec.ts` | ❌ W0 | ⬜ |
| REQ-SITE-PERF-01 | Lighthouse ≥90 ×4 axes on /, /docs, /playground | — | Lighthouse CI | `lhci autorun` | ❌ W0 | ⬜ |
| REQ-SITE-RESPONSIVE-01 | layout collapses to single column <768px | — | E2E visual | `playwright test tests/responsive.spec.ts` | ❌ W0 | ⬜ |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-site/vitest.config.ts` + `flow-site/playwright.config.ts` + `flow-site/lighthouserc.cjs` — no test infra exists (greenfield)
- [ ] `flow-site/tests/` E2E directory + per-spec stub files (nav, render-strategy, docs-render, docs-toc, wasm-boot, playground-run/audio/export/mobile, a11y, visual, responsive)
- [ ] `src/lib/docs/transform.test.ts` — with a **synthetic** `[[Quick-Start]]` fixture (the real wiki contains zero `[[ ]]` links — RESEARCH Pitfall 7)
- [ ] `src/lib/share/encode.test.ts` — fflate deflate/inflate round-trip property test
- [ ] `workers/gist-auth.test.ts` — mocked GitHub token-exchange + `state` CSRF-rejection case
- [ ] Visual-regression baselines for the ≤8 skeuo components (light + dark + reduced-motion)
- [ ] Component test harness install: `@testing-library/svelte` or `vitest-browser-svelte` (pin version at planning)
- [ ] `@lhci/cli` + `@axe-core/playwright` install

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Audible audio output across browsers | REQ-SITE-PLAYGROUND-03 | Headless E2E can assert `AudioContext.state === 'running'` but cannot confirm sound is *audible*; Phase 48 left Chrome/Safari audio unverified | Plan 49-08 HUMAN-UAT: open /playground in Chrome 120+ / Firefox 121+ / Safari 17+ + mobile, run a tone snippet, confirm audible |
| Skeuomorphic look matches references | REQ-SITE-DESIGN-01..04 | "Logic Pro / Reason / vintage-synth feel" is a subjective aesthetic target; automation checks render + contrast only | Plan 49-08 visual review against D-49-06 reference set; confirm no glassmorphism / AI-template feel |
| Live gist created under composer account | REQ-SITE-SHARE-02 | Real OAuth + real GitHub account write cannot run in CI without leaking a secret | Plan 49-06 manual: click "Save to gist" while logged in, confirm gist appears at gist.github.com/<user>/<id> |
| Live CF Pages deploy serves the site | REQ-SITE-IA-01 | Requires composer's Cloudflare account + dashboard project creation | Plan 49-01 / 49-09: confirm `<project>.pages.dev` serves HTML; runbook in 49-09 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (greenfield — entire test stack is Wave 0)
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s (unit pass)
- [ ] `nyquist_compliant: true` set in frontmatter (planner/checker confirms coverage)

**Approval:** pending
