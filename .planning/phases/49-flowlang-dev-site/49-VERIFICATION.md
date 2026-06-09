# Phase 49: flowlang.dev Site — Verification

**Phase:** 49 (flowlang-dev-site)
**Status:** EXECUTION COMPLETE — PENDING HUMAN-UAT + LIVE DEPLOY (NOT shipped, NOT verified)
**Plans:** 9/9 plans executed (autonomous build + tests complete; 3 human-action gates still OPEN)
**Requirements:** 24 REQ-SITE-* — 20 closed by automated evidence, 4 PENDING the human UAT/deploy gate (see table)

> **Honesty marker (read first).** The autonomous BUILD of Phase 49 is complete and fully
> tested in headless CI (vitest 70/70, playwright 275/275, lhci ≥0.9 ×4 both form factors,
> axe 0-critical). It is **not** yet shippable or composer-verified. Three things automation
> cannot do remain OPEN and require the composer:
>
> 1. **Live Cloudflare Pages deploy** (Plan 49-01 Task 4 — REQ-SITE-IA-01 live-deploy portion + REQ-SITE-DEPLOY-01).
> 2. **GitHub OAuth App registration + a live gist round-trip** (Plan 49-06 Task 4 — REQ-SITE-SHARE-02).
> 3. **Cross-browser AUDIBLE audio + skeuomorphic visual-fidelity + screen-reader sign-off**
>    (Plan 49-08 Task 3 — REQ-SITE-PLAYGROUND-03 audible, REQ-SITE-DESIGN-01..04 visual,
>    REQ-SITE-A11Y-* SR-smoke portion).
>
> All three are consolidated into a SINGLE composer pass at
> `.planning/phases/49-flowlang-dev-site/49-HUMAN-UAT.md`, and the step-by-step deploy/OAuth
> setup is in `.planning/phases/49-flowlang-dev-site/49-DEPLOYMENT-RUNBOOK.md`. This phase
> flips to SHIPPED only after that UAT is signed off — see "Outcome Summary" for the truthful
> status of every artifact.

---

## Outcome Summary

Phase 49 builds the greenfield `flow-site/` SvelteKit 2 / Svelte 5 / TypeScript / Tailwind v4
project (a new top-level sibling to `flow-lang/` + `flow-interpreter/` + `flow-cli/` +
`flow-jetbrains/`), deployed to Cloudflare Pages via `adapter-cloudflare`, with a skeuomorphic
visual system (D-49-06 composer-locked) across five routes — Home, Docs, Playground, Showcase,
plus an external GitHub link. The playground tab consumes the **frozen** Phase 48
`flow-runtime.js` ES module (HANDOFF §8 — never edited) to run Flow code in the browser and
play audio through `WebAudioBackend`'s `AudioContext`. Docs are synced at build time from the
26-page `wiki/` (D-49-25). Share is a default zero-backend URL-fragment path (fflate
deflate + base64url, decompression-bomb-guarded), with a "Save to gist" GitHub OAuth promote
path via a ≤50-LOC CF Worker (state CSRF, server-side secret, scope=gist).

**What is proven (headless, deterministic, in this repo):** the SvelteKit project builds clean
to `.svelte-kit/cloudflare/`; the Phase 48 AppBundle is committed byte-identical under
`static/wasm/`; the .NET-in-WASM runtime boots and executes Flow in headless chromium; Monaco
mounts client-only with hand-written Flow Monarch highlighting; the Run gesture chain reaches
`AudioContext.state === 'running'`; all 26 wiki pages prerender; the 5-tab nav + Home + Showcase
prerender; share encode/decode round-trips with a bomb guard; the OAuth worker validates `state`
and exchanges the code server-side (mocked GitHub); axe reports 0 critical/serious on every
route; the layout collapses to single column with no horizontal overflow at 320px AND 375px;
Lighthouse medians are ≥0.90 on all four axes for /, /docs, /playground on BOTH form factors,
measured against a CF-Pages-accurate brotli server.

**What is NOT proven (the three open human gates):** that the site is live on a real
`<project>.pages.dev`; that "Save to gist" creates a real gist under a composer's GitHub
account; and that audio is **audible** (not merely `AudioContext.state === 'running'`) in
Chrome / Safari / mobile, that the skeuo look reads as vintage-gear (not glassmorphism) to a
human eye, and that a screen reader announces the controls. Phase 48 left Chrome/Safari audio
UNVERIFIED (HANDOFF §7); only Firefox passed. These flip on the 49-HUMAN-UAT pass.

---

## Build / Dev / Deploy Surface

```bash
# Everything runs from flow-site/ (greenfield TS — C# conventions do NOT apply inside it):
pnpm -C flow-site install
pnpm -C flow-site dev                 # local dev (predev runs sync-wiki.sh)
pnpm -C flow-site build               # → flow-site/.svelte-kit/cloudflare/  (CF Pages output)
pnpm -C flow-site test                # vitest run (unit/component)
pnpm -C flow-site test:e2e            # playwright (desktop / 375 / 320 projects)
pnpm -C flow-site lh                  # lhci autorun (≥0.9 ×4 axes, production-accurate server)

# Regenerate the committed Phase 48 WASM AppBundle (run on the dev machine, then commit):
bash flow-site/scripts/sync-runtime.sh   # dotnet publish -p:FlowTarget=Web → static/wasm/

# Live deploy (composer's Cloudflare account) — see 49-DEPLOYMENT-RUNBOOK.md:
#   CF Pages project (name flow-music / flow-music-playground, D-49-36)
#   build command  : pnpm -C flow-site build
#   output dir     : flow-site/.svelte-kit/cloudflare
```

---

## Per-REQ Closure Table

> Evidence column cites the proving test/command/artifact from `49-VALIDATION.md` (the per-REQ
> test map) and the closing plan/commit. Status legend: ✅ closed (automated) · ⏳ PENDING the
> human gate (code complete + automated-proven, but the closing evidence requires a composer).

| REQ | Behavior | Test / Artifact (VALIDATION ref) | Closed by | Status |
|-----|----------|----------------------------------|-----------|--------|
| **REQ-SITE-IA-01** | 5-tab nav renders + local routes resolve; live deploy serves it | `tests/nav.spec.ts` (E2E) — **nav portion ✅**; live `<project>.pages.dev` serves HTML — **deploy portion ⏳** | 49-03 (`87a9061`) nav; 49-01 Task 4 deploy | ⏳ |
| **REQ-SITE-IA-02** | per-route render strategy (prerender Home/Docs/Showcase; SPA Playground; one server fn) | `tests/render-strategy.spec.ts` (E2E — raw-HTML prerender vs JS-disabled SPA) | 49-03 (`7f38308`) + 49-07 (`e8eea45`) | ✅ |
| **REQ-SITE-IA-03** | SvelteKit 2 + Svelte 5 + TS + Tailwind v4 + adapter-cloudflare; `pnpm build` → `.svelte-kit/cloudflare/` | `pnpm -C flow-site build` exit 0 (51 prerendered HTML + AppBundle + `_headers`) | 49-01 (`7bf1295`) | ✅ |
| **REQ-SITE-DEPLOY-01** | CF Pages project + `<project>.pages.dev` serves; `_headers` CSP + Permissions-Policy + scoped COOP/COEP | `_headers` at project root (verified in build output); live project creation = composer's CF account | 49-01 (`98e7ba2`) headers; 49-01 Task 4 deploy | ⏳ |
| **REQ-SITE-DESIGN-01** | design tokens at `lib/design/tokens.css` + `@theme` mapping | `tokens.css` + `app.css @theme`; `/design` visual baselines | 49-02 (`564aafd`) | ✅ |
| **REQ-SITE-DESIGN-02** | four `.surface-*` material classes (degrade to flat base) | `surfaces.css`; `tests/visual.spec.ts` | 49-02 (`564aafd`) | ✅ |
| **REQ-SITE-DESIGN-03** | exactly 8 skeuo components with state/a11y/reduced-motion | `skeuo.test.ts` (21 component tests); `/design` | 49-02 (`594fe6e`) | ✅ |
| **REQ-SITE-DESIGN-04** | dark mode = second theme (not default); persists; honors prefers-color-scheme | `theme.test.ts` (6 tests); dark visual baseline | 49-02 (`7f8a528`) | ✅ |
| **REQ-SITE-DESIGN-05** | `/design` showcase renders all 8 components in all states | `tests/visual.spec.ts` (light/dark/reduced-motion baselines) | 49-02 (`7f8a528`) | ✅ |
| **REQ-SITE-HOME-01** | Home six D-49-21 sections; gesture-gated audio, no autoplay | `tests/nav.spec.ts` + a11y label sweep; no `autoplay` attr | 49-03 (`6ee9f49`) | ✅ |
| **REQ-SITE-DOCS-01** | 26 wiki pages render at `/docs/[slug]` + shiki + Open-in-playground | `tests/docs-render.spec.ts` (87 passed); 26 prerendered | 49-04 (`fd20b26`) | ✅ |
| **REQ-SITE-DOCS-02** | `sync-wiki.sh` clones wiki (env `WIKI_REPO_URL`, fails loud, seed fallback) | `scripts/sync-wiki.sh` (`set -euo pipefail` + non-empty assert) | 49-04 (`d9cd152`) | ✅ |
| **REQ-SITE-DOCS-03** | `[[link]]` + relative `.md` rewrite, idempotent, fence-aware, kebab slug | `transform.test.ts` (15/15, synthetic `[[ ]]` fixture) | 49-04 (`d9cd152`/`fd20b26`) | ✅ |
| **REQ-SITE-DOCS-04** | `/docs` index categorized TOC from `docs-categories.json` (not hard-coded) | `tests/docs-toc.spec.ts`; `docs-categories.json` | 49-04 (`704e64c`) | ✅ |
| **REQ-SITE-PLAYGROUND-01** | WASM lazy-boots in `onMount`; boot failure → top-level pane | `tests/wasm-boot.spec.ts` (runtime ready, no boot-error pane) | 49-05 (`46828b4`) | ✅ |
| **REQ-SITE-PLAYGROUND-02** | Monaco mounts; Run → `stdout` for `(print "hi")` (escaped) | `tests/playground-run.spec.ts` (`hello flow` + `3` in stdout) | 49-05 (`46828b4`) | ✅ |
| **REQ-SITE-PLAYGROUND-03** | Run chains `resumeAudio()` + `run()` one gesture frame; AudioContext `running`; **audible** | `tests/playground-audio.spec.ts` (`state==='running'` ✅); **AUDIBLE ⏳ headless cannot confirm** | 49-05 (`46828b4`) headless; **49-08 HUMAN-UAT** audible | ⏳ |
| **REQ-SITE-PLAYGROUND-04** | stdout/stderr split; `errors[]` escaped Rust boxes; MIDI download when produced | `tests/playground-export.spec.ts` (Blob download fires; button absent w/o MIDI) | 49-05 (`46828b4`) | ✅ |
| **REQ-SITE-PLAYGROUND-05** | Monaco read-only <768px + banner; Run still works | `tests/playground-mobile.spec.ts` (read-only + no 320/375 overflow) | 49-05 (`46828b4`) | ✅ |
| **REQ-SITE-SHARE-01** | URL-fragment share default; defensive decode + bomb guard; round-trips | `encode.test.ts` (12 tests: round-trip + bomb + malformed) | 49-06 (`ba812ab`) | ✅ |
| **REQ-SITE-SHARE-02** | "Save to gist" via OAuth worker (state CSRF, server secret, scope=gist); live gist | `gist-auth.test.ts` (6 mocked-GitHub tests ✅); **live OAuth App + gist round-trip ⏳** | 49-06 (`384e30f`) code; **49-06 Task 4 HUMAN** live | ⏳ |
| **REQ-SITE-SHOWCASE-01** | `/showcase` gallery (10 curated) + `/showcase/[slug]` detail (gesture audio + shiki + notes) | `tests/showcase.spec.ts` (57/57); 10 prerendered detail pages | 49-07 (`e8eea45`) | ✅ |
| **REQ-SITE-A11Y-01** | full keyboard nav, no traps; Knob/Slider arrow-key `role=slider`; brass focus ring | `tests/a11y.spec.ts` keyboard-nav; `skeuo.test.ts` | 49-02 (`594fe6e`) + 49-08 (`0a1ab28`) | ✅ |
| **REQ-SITE-A11Y-02** | SR labels on every control; LED `aria-live`; landmarks; axe 0-critical on /, /docs, /playground | `tests/a11y.spec.ts` (axe 0-critical/serious all routes) — **automated ✅**; **SR-smoke ⏳ HUMAN** | 49-03 (`7f38308`) + 49-08 (`0a1ab28`) | ✅¹ |
| **REQ-SITE-A11Y-03** | prefers-reduced-motion honored (knobs→flat, no travel, 1px border) | `skeuo.test.ts` + reduced-motion visual baseline | 49-02 (`7f8a528`) | ✅ |
| **REQ-SITE-PERF-01** | Lighthouse ≥90 ×4 on /, /docs, /playground (mobile + desktop) | `lhci autorun` (3-run median, CF-accurate server): desktop all 1.00; mobile 0.95/0.97/0.95 | 49-08 (`76e236a`) | ✅ |
| **REQ-SITE-RESPONSIVE-01** | single-column collapse <768px; no horizontal overflow; mobile inline audio | `tests/responsive.spec.ts` (54/54; 320 + 375 no-overflow) | 49-05 (`46828b4`) + 49-08 (`0a1ab28`) | ✅ |

¹ REQ-SITE-A11Y-02: the **axe/keyboard automated portion is closed**; the manual screen-reader
smoke (VoiceOver / NVDA announces labels + LED `aria-live` status) is a HUMAN-UAT row in
49-HUMAN-UAT.md (step 8). The automated a11y contract does not regress on the open SR check —
the SR pass is confirmatory, not corrective.

**Tally:** 24 REQ-SITE-* total — **20 ✅ closed (automated)**, **4 ⏳ PENDING the human gate**
(REQ-SITE-IA-01 deploy portion, REQ-SITE-DEPLOY-01 live project, REQ-SITE-PLAYGROUND-03 audible,
REQ-SITE-SHARE-02 live gist). The 49-HUMAN-UAT pass closes all four.

---

## D-49-NN Decision Trace (D-49-01 .. D-49-38)

> Each composer-locked decision → where it landed (plan / artifact). Decisions are from
> `49-CONTEXT.md` §Implementation Decisions + the ROADMAP Phase 49 "Locked design decisions" block.

| D-49 | Decision | Landed at |
|------|----------|-----------|
| D-49-01 | No autoplay anywhere | Home `AudioEmbed` + Showcase audio behind explicit play Button (49-03/49-07); playground Run is the only audio trigger (49-05) |
| D-49-02 | Static code blocks site-wide, not inline playgrounds | shiki server-rendered blocks + "Open in playground" deep-link (49-03/49-04/49-07); runtime only on /playground |
| D-49-03 | GitHub gist for share-links | URL-fragment default + gist OAuth promote (49-06 `gist.ts` + `gist-auth.ts`) |
| D-49-04 | SvelteKit on CF Pages, `*.pages.dev` first | `adapter-cloudflare` (49-01); live deploy = composer (Plan 49-01 Task 4 / runbook) |
| D-49-05 | Docs synced from `wiki/` (26 md) | `sync-wiki.sh` clone-or-seed (49-04) |
| D-49-06 | Skeuomorphic visual direction (no glassmorphism) | tokens + 4 surfaces + 8 components (49-02); human visual sign-off pending (49-08 UAT step 9) |
| D-49-07 | Top-level 5-tab nav (Home/Docs/Playground/Showcase/GitHub) | `+layout.svelte` `<Tabs>` (49-03) |
| D-49-08 | No autoplay on Home; "Play in playground" one-gesture chain | CodeCard `#code=…&run=1` deep link + arrival auto-run (49-03 + 49-06) |
| D-49-09 | Mobile-responsive, not mobile-first; Monaco read-only <768px | `playground-mobile.spec.ts` read-only banner (49-05); responsive collapse (49-08) |
| D-49-10 | Accessibility (keyboard + SR + reduced-motion) | brass focus ring + `role=slider` + `aria-live` + reduced-motion (49-02); axe gate (49-08); SR-smoke pending UAT |
| D-49-11 | SvelteKit 2 + Svelte 5 + TS + Tailwind v4 | scaffold (49-01) — Tailwind v4 CSS-first `@theme`, no `tailwind.config.js` |
| D-49-12 | Cloudflare Pages hosting (COOP/COEP capable) | `_headers` scoped COOP/COEP (49-01); project name resolved at deploy (D-49-36) |
| D-49-13 | adapter-cloudflare (SSR route handlers), not adapter-static | `svelte.config.js` adapter + per-route render strategy (49-01); `/api/auth/github` server fn (49-06) |
| D-49-14 | Monaco Editor (simplified — highlight + builtin completion, no full LSP) | `monaco/index.ts` + hand-written Monarch tokenizer (49-05); full LSP = v1.6 backlog |
| D-49-15 | shiki + Flow TextMate grammar for static blocks | `shiki.ts` server-render via Phase 17 grammar (49-04) |
| D-49-16 | mdsvex for MDX docs | `svelte.config.js` mdsvex preprocess (49-01/49-04) |
| D-49-17 | Design tokens at `tokens.css` (color/spacing/type/shadow/radius/motion) | `tokens.css` full set (49-02) |
| D-49-18 | Material surfaces as utility classes | 4 `.surface-*` classes (49-02) |
| D-49-19 | Skeuomorphic component library ≤8 base components | exactly 8 (Button/Knob/Toggle/Panel/MetalRail/LedIndicator/Slider/Tabs) (49-02) |
| D-49-20 | Dark mode is a second theme, not the default | `[data-theme=dark]` + nav toggle + localStorage + FOUC script (49-02) |
| D-49-21 | Home six sections (hero…footer) | `+page.svelte` six sections (49-03) |
| D-49-22 | Docs index categorized TOC from config | `docs-categories.json` → categorized `/docs` (49-04) |
| D-49-23 | Playground 3-column desktop / 1-column mobile | `playground/+page.svelte` grid + status bar (49-05) |
| D-49-24 | Showcase 6–12 curated pieces | 10-piece manifest `pieces.ts` (49-07) — within the 6–12 range |
| D-49-25 | Build-time `git clone --depth 1` wiki sync (`WIKI_REPO_URL`) | `sync-wiki.sh` (49-04) |
| D-49-26 | Wiki `[[Page-Name]]` link rewriting | `remark-wiki-links.js` `[[ ]]` + relative `.md` (49-04) |
| D-49-27 | Lowercase-kebab slugs; reserved `index`; collision guard | `slug.js` (49-04) |
| D-49-28 | CF Worker `gist-auth.ts` OAuth code exchange; env `GITHUB_CLIENT_ID`/`_SECRET` | `workers/gist-auth.ts` + `/api/auth/github/+server.ts` (49-06); live App = composer (runbook) |
| D-49-29 | Gist creation client-side (no proxy) | `gist.ts` `createGist` Bearer POST (49-06) |
| D-49-30 | URL fragment is the DEFAULT share path | `encode.ts` fflate base64url `#code=` (49-06) |
| D-49-31 | Lighthouse ≥90 ×4 on /, /docs, /playground (mobile + desktop) | `lhci autorun` ≥0.9 ×4 both form factors (49-08) — UNCONDITIONAL, no carve-out |
| D-49-32 | Image optimization (AVIF/WebP/PNG, Sharp) | `optimize-textures.mjs` wood-grain pipeline (49-02) |
| D-49-33 | Critical CSS inlined; non-critical deferred | flat-base surfaces first paint + SvelteKit native (49-02) |
| D-49-34 | WASM lazy-loaded on /playground only | dynamic `import('/wasm/flow-runtime.js')` in `onMount` (49-05); confirmed off-LCP for /, /docs (49-08) |
| D-49-35 | No service worker in v1 | not added (correctly absent) — PWA/SW = v1.6 backlog |
| D-49-36 | CF Pages project name `flow-music` else `flow-music-playground` | resolved at deploy by the composer (Plan 49-01 Task 4 / runbook) — **pending** |
| D-49-37 | Custom domain deferred to post-v1.5 | CNAME steps documented in 49-DEPLOYMENT-RUNBOOK.md for later; ships on `*.pages.dev` |
| D-49-38 | HTTP headers via `flow-site/_headers` (project root) | `_headers` at project root: CSP + Permissions-Policy + scoped COOP/COEP (49-01) |

All 38 D-49-NN decisions trace to a landed artifact or a documented deferral. The three
deferrals (D-49-35 service worker, D-49-37 custom domain, the v1.6 portions of D-49-14
full-LSP) are intentional and logged in the v1.6 backlog.

---

## RESEARCH Corrections Honored

The plans honored three corrections that surfaced against the RESEARCH/CONTEXT assumptions —
recording them so the audit trail is honest about what was assumed vs. what the codebase
actually required:

1. **Grammar path correction.** RESEARCH/CONTEXT pointed the Flow TextMate grammar at
   `flow-lsp/grammars/flow.tmLanguage.json`; the real grammar lives at
   `vscode-extension/syntaxes/flow.tmLanguage.json`. Plan 49-04 (`sync-grammar.mjs`) +
   Plan 49-05 (Monarch tokenizer derivation) both use the corrected path. The CF
   flow-site-only build copies the grammar in rather than reaching the sibling project.
2. **No real wiki `[[links]]`.** The transform was framed around GitHub-wiki `[[Page-Name]]`
   syntax (RESEARCH Pitfall 7), but the real `wiki/` uses ordinary relative
   `[Label](Page-Name.md#anchor)` cross-links and ZERO `[[ ]]` links. Plan 49-04 extended
   `remark-wiki-links.js` to rewrite BOTH forms (the `[[ ]]` path is kept + unit-tested
   against a synthetic fixture so the documented contract still holds).
3. **`_headers` at the project root, not `static/`.** Per adapter-cloudflare convention
   (RESEARCH A4), `_headers` lives at `flow-site/_headers` (project root) — a `static/_headers`
   would be served as a literal asset and silently ignored. Plan 49-01 placed it correctly.

## Discretionary Calls (planner/executor latitude)

- **pnpm** as the package manager (D-49-CONTEXT noted "likely pnpm") — chosen for disk
  efficiency + monorepo-friendliness; pinned via corepack; native-build allowlist in
  `pnpm-workspace.yaml`.
- **Self-hosted Monaco** (no CDN) — bundled via Vite so the CSP needs no CDN allowance
  (`script-src 'self' 'wasm-unsafe-eval'`); single base `editor.worker?worker` (one custom
  language, no TS/JSON workers).
- **Hand-written Monarch tokenizer** (49-05) for Monaco rather than TextMate-in-Monaco
  (no onigasm/WASM-regex dependency) — derived from the Phase 17 grammar scopes per RESEARCH
  Open Q3.
- **COOP/COEP scoping** to `/playground/*` only (RESEARCH Open Q1) — the v1.6 AudioWorklet +
  SharedArrayBuffer foundation; CSP + Permissions-Policy stay global. May be removed with no
  security loss if they cause subresource friction (HANDOFF §3).
- **Fraunces (SIL OFL)** substituted for the commercial Recoleta display face (49-02 Rule 3) —
  the closest freely-self-hostable warm hand-set serif to the D-49-06 brief.
- **Committed AppBundle** under `static/wasm/` (RESEARCH Open Q2) — keeps the CF Pages build
  pure-Node; `sync-runtime.sh` regenerates it on the dev machine.
- **CF-Pages-accurate Lighthouse server** (`lh-serve.mjs`, brotli + cache + SPA fallback)
  rather than uncompressed `vite preview` — the production-honest measurement (49-08).

---

## Known Caveats

1. **Three OPEN human gates — the phase is NOT shipped.** Live CF deploy, GitHub OAuth App +
   live gist, and cross-browser audible/visual/SR UAT are all consolidated in
   `49-HUMAN-UAT.md`. Until the composer signs off, Phase 49 status is
   "execution complete — pending HUMAN-UAT + live deploy", NOT "shipped".

2. **Chrome / Safari audio AUDIBILITY unverified (inherited from Phase 48).** Headless E2E
   asserts only `AudioContext.state === 'running'`. Phase 48 HANDOFF §7 left Chrome/Safari
   audio unverified; only Firefox passed audibly. The D-48-09 gesture chain (`resumeAudio()`
   then `run()` in one async frame) should satisfy even Safari's strict autoplay policy — the
   UAT confirms it does. Recorded as a 49-HUMAN-UAT row per browser.

3. **MIDI download button is forward-compatible (a documented stub, not a regression).** The
   SHIPPED Phase 48 `WasmEntry.cs` hardcodes `RunResult.midi = null` (the in-memory `writeMidi`
   capture hook is reserved — HANDOFF §9 is the INTENDED contract, not current behavior). The
   playground wires the Blob/anchor download + a `{#if pg.hasMidi}` button so it lights up the
   moment a future runtime emits MIDI bytes; the frozen runtime was NOT edited (HANDOFF §8). The
   UAT records DEFER if the button does not appear. Resolution path: a v1.6 WASM-runtime phase
   wires the hook.

4. **Live-gist write is manual / composer-account-bound.** "Save to gist" requires a registered
   GitHub OAuth App under the composer's account and a real `gist`-scope token; it cannot run
   in CI without leaking a secret. The worker + client POST are built + unit-tested with mocked
   GitHub; only the live round-trip remains (49-HUMAN-UAT prereq B + step 6).

5. **Two showcase pieces link out to GitHub (no fabricated source).** "In Five Voices" symphony
   + "Stride & Stomp" ragtime had their `.flow` deleted from the worktree; their cards link to
   GitHub source rather than inventing it (49-07, honest-worktree posture). The v1.5 third-genre
   piece (Phase 41 SHOWCASE-01, unbuilt) is OMITTED, not invented — add to the manifest when it
   ships.

6. **Custom domain deferred (D-49-37).** Ships on the `*.pages.dev` URL; the CNAME steps for a
   real domain (e.g. `flowmusic.dev`) are documented in 49-DEPLOYMENT-RUNBOOK.md for post-v1.5.

7. **Phase 40 (Studio Sync) + Phase 41 (Reach + v1.5 Closer) remain pending for milestone
   close.** Phase 49 closing does not close v1.5 — Phase 40 and Phase 41 are still open.

---

## Verification Status

**status: human_needed**

Open gates (all consolidated in `49-HUMAN-UAT.md`):

1. **Live Cloudflare Pages deploy** — REQ-SITE-IA-01 (deploy portion) + REQ-SITE-DEPLOY-01.
   Composer creates the CF Pages project (D-49-36) and confirms `<project>.pages.dev` serves
   the site. Runbook: `49-DEPLOYMENT-RUNBOOK.md` §1–§2.
2. **GitHub OAuth App + live gist round-trip** — REQ-SITE-SHARE-02. Composer registers the
   OAuth App (callback `https://<project>.pages.dev/api/auth/github`, scope `gist`), sets the
   CF env vars, and confirms a real gist is created. Runbook: §3.
3. **Cross-browser AUDIBLE audio + skeuo visual fidelity + screen-reader smoke** —
   REQ-SITE-PLAYGROUND-03 (audible), REQ-SITE-DESIGN-01..04 (visual), REQ-SITE-A11Y-* (SR).
   49-HUMAN-UAT.md per-browser rows (Chrome / Firefox / Safari / mobile) + cross-cutting rows.

This phase flips to SHIPPED only after the 49-HUMAN-UAT composer sign-off.
