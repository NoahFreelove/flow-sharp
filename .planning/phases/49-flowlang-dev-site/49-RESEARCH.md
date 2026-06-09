# Phase 49: flowlang.dev Site - Research

**Researched:** 2026-06-05
**Domain:** SvelteKit 2 + Svelte 5 web app (marketing + docs + WASM playground + showcase) on Cloudflare Pages
**Confidence:** HIGH on stack/versions/integration shapes; MEDIUM on skeuomorphic-component a11y specifics; HIGH on Phase 48 runtime contract (read from the frozen HANDOFF)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Tech Stack (Area 1)**
- **D-49-11:** SvelteKit 2.x + Svelte 5 + TypeScript + Tailwind CSS. Svelte 5 runes for state; file-based routing; TypeScript for editor/runtime safety; Tailwind utilities layered with custom CSS for skeuomorphic materials.
- **D-49-12:** Cloudflare Pages hosting (free tier, global CDN, native COOP/COEP support, git-based deploys). Project name TBD at Plan 49-01 (likely `flow-music` / `flow-lang-playground`; `flowlang` taken). URL `<project>.pages.dev`.
- **D-49-13:** SvelteKit `adapter-cloudflare` (NOT adapter-static) — gives optional server route handlers for gist OAuth (single endpoint `/api/auth/github`); rest statically prerendered. SSR for marketing, SPA for /playground, static prerender for /docs + /showcase.
- **D-49-14:** Monaco Editor for the playground editor. Simplified config (no full LSP wiring; syntax highlighting from Phase 17 grammar + Tab-completion of builtins). Full Monaco-LSP bridge is v1.6.
- **D-49-15:** shiki + custom Flow TextMate grammar for static code blocks (Home/Docs/Showcase). Reuse the Phase 17 grammar. Server-rendered HTML — no client JS for highlighting outside playground.
- **D-49-16:** mdsvex for MDX-flavored markdown in /docs. Lets docs embed Svelte components (`<OpenInPlayground source="...">`) while staying wiki-compatible markdown.

**Visual Design System (Area 2)**
- **D-49-17:** Design tokens at `flow-site/src/lib/design/tokens.css` as CSS custom properties (color palette, spacing 4/8/12/16/24/32/48/64, type scale 12/14/16/18/24/32/48, shadow scale 1/3/8/16/32, radius 2/4/8/12/24, motion curves).
- **D-49-18:** Material surfaces as utility classes layered on Tailwind (`.surface-wood`, `.surface-brushed-metal`, `.surface-paper`, `.surface-felt`). SVG patterns inlined into CSS where possible; raster textures only where SVG can't capture the look (wood grain candidate).
- **D-49-19:** Skeuomorphic component library at `flow-site/src/lib/components/skeuo/`. Components: `<Knob>`, `<Button>`, `<Toggle>`, `<Panel>`, `<MetalRail>`, `<LedIndicator>`, `<Slider>`. ≤8 base components — restraint matters.
- **D-49-20:** Dark mode is a SECOND skeuomorphic theme, not the default. Light default (paper + walnut + brass). Dark = darker walnut + amber-LED accents. Toggle in top nav; `prefers-color-scheme` honored first visit; explicit toggle persists in localStorage.

**Page Surfaces (Area 3)**
- **D-49-21:** Home sections: (1) Hero wordmark + tagline + 3 "play in playground" examples + Phase 34 symphony embed; (2) Value-prop trio (Ergonomics-first / Genre-agnostic / Music-notation roots) as skeuo cards; (3) "How it sounds" audio embeds; (4) Code-first 20-line snippet with annotations; (5) Install + Try-in-browser CTAs; (6) Footer.
- **D-49-22:** Docs index renders the wiki TOC grouped: Getting Started / Music Concepts / Audio+Output / Reference. Grouping from `docs-categories.json` config (Plan 49-04), NOT hard-coded.
- **D-49-23:** Playground layout — three-column desktop, single-column mobile. Left 30% snippet list + share/save + theme. Center 50% Monaco + Run. Right 20% console (stdout/stderr split) + audio player + MusicXML/LilyPond/MIDI download buttons. Bottom status bar (runtime version + bundle size + last-run timestamp).
- **D-49-24:** Showcase gallery — 6-12 curated pieces at v1.5 (symphony, ragtime, third-genre, Markov jazz, OSC live-control recording, granular showpiece, microtonal Carlos Alpha, Bohlen-Pierce). Each piece: hero audio + source + composer notes.

**Wiki Sync (Area 4)**
- **D-49-25:** Build-time `git clone --depth 1 https://github.com/<user>/flow-sharp.wiki.git src/docs/wiki/` in `flow-site/scripts/sync-wiki.sh`. CF Pages build step calls this before `vite build`. Auth via Pages env var `WIKI_REPO_URL`. v1 rebuilds on flow-sharp main push; v1.6 adds wiki-webhook.
- **D-49-26:** Wiki link rewriting `[[Page-Name]]` → `[Page Name](/docs/page-name)` in `flow-site/src/lib/docs/transform.ts` (~80 LOC), after sync, before SvelteKit reads markdown.
- **D-49-27:** Slugs are lowercase-kebab from filename (`Quick-Start.md` → `/docs/quick-start`). Reserved slug `index` (TOC). Collisions → build-time error.

**GitHub Gist Share (Area 5)**
- **D-49-28:** CF Worker at `flow-site/workers/gist-auth.ts` handles GitHub OAuth code exchange. ≤50 LOC. Env vars `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET`. Route `/api/auth/github`. Flow: click "Save to gist" → `github.com/login/oauth/authorize?...&scope=gist` → callback `/api/auth/github?code=...` → worker exchanges → redirect back to playground with token in URL fragment (sessionStorage caches).
- **D-49-29:** Gist creation client-side — playground JS POSTs to `https://api.github.com/gists` with token (no proxy through worker). Display "Saved to gist.github.com/<...>" + copy-link.
- **D-49-30:** URL fragment encoding is the DEFAULT share path. "Share" copies `https://<site>/playground#code=BASE64(deflate(source))`. Anyone opens without GitHub auth. "Save to gist" = "promote to permanent". Both ship v1.5.

**Performance + A11y (Area 6)**
- **D-49-31:** Lighthouse ≥90 on Performance, Accessibility, Best Practices, SEO for / + /docs + /playground. Mobile + desktop. Verified Plan 49-08. /playground loses Performance points to WASM bundle — baseline at Plan 49-01 and tune.
- **D-49-32:** Image optimization — raster textures as AVIF + WebP with PNG fallback, Sharp-based build step. Do NOT drop below 80% AVIF quality.
- **D-49-33:** Critical CSS inlined; non-critical deferred (SvelteKit handles natively). tokens.css + above-fold component CSS inline; texture overlays deferred.
- **D-49-34:** WASM bundle lazy-loaded on /playground only. `playground/+page.svelte` dynamically `import('flow-runtime')` in `onMount`. Not fetched until composer navigates to /playground.
- **D-49-35:** Service worker NOT in v1. v1.6: PWA + offline playground + IndexedDB persistence.

**Deployment (Area 7)**
- **D-49-36:** CF Pages project `flow-music` if available else `flow-music-playground` (resolved Plan 49-01 by creating the project on CF dashboard — composer's CF account needed).
- **D-49-37:** Custom domain deferred to post-v1.5. v1 ships on pages.dev.
- **D-49-38:** HTTP headers via `flow-site/_headers` (CF Pages convention). Sets `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Embedder-Policy: require-corp` (Phase 48 v1.6 AudioWorklet foundation), CSP restricting scripts to self + Monaco CDN + GitHub OAuth domains, `Permissions-Policy: microphone=(), camera=(), geolocation=()`.

### Claude's Discretion
- Package manager choice (CONTEXT leans pnpm — disk-efficient, monorepo-friendly; verified installed at 11.12.1). See §Standard Stack note: `sv create` defaults work with pnpm.
- Exact skeuomorphic-component internals, texture authoring approach (SVG vs raster), Knob interaction model — within D-49-10/17/18/19 constraints.
- Test framework selection for the Validation Architecture (Playwright + Vitest recommended below).
- Whether COOP/COEP headers ship in v1.5 or are deferred (see Open Question 1 — HANDOFF §3 explicitly says they MAY complicate the v1 Monaco-CDN path and can be deferred).

### Deferred Ideas (OUT OF SCOPE)
- v1.6 custom domain; v1.6 Monaco full LSP; v1.6 PWA/service worker/IndexedDB; v1.6 community showcase submissions; v1.6 wiki auto-rebuild GitHub Action; v1.6 anonymous "Save" fallback; v1.6 inline-runnable docs code; v1.6 unify wiki with `flow doc`; v1.6 i18n; v1.6 dark-mode CRT scanlines; v1.6 AnalyserNode waveform; v1.6 mobile editing affordances.
</user_constraints>

<phase_requirements>
## Phase Requirements

REQ-SITE-* IDs are TBD (formalized by the planner). The candidate anchors map to research findings as follows:

| ID (candidate) | Description | Research Support |
|----|-------------|------------------|
| REQ-SITE-IA-01..05 | Five-tab nav + IA + per-route render strategy | §Architecture Patterns "Per-route render strategy"; adapter-cloudflare `fallback`/`routes` (verified) |
| REQ-SITE-DESIGN-01..04 | Skeuomorphic visual system, tokens, materials, ≤8 components | §Skeuomorphic Design System; Tailwind v4 `@theme` CSS-first (verified); accessible Knob pattern |
| REQ-SITE-DOCS-01..03 | Wiki sync + render + categorized TOC | §Wiki Docs Sync; mdsvex 0.12.7 + shiki 4.2.0 custom grammar (verified); CRITICAL: only 1 false `[[...]]` in wiki |
| REQ-SITE-PLAYGROUND-01..05 | Monaco + Phase 48 runtime + WebAudio + exports | §Phase 48 Runtime Integration; §Monaco in SvelteKit/Vite; HANDOFF gesture chain |
| REQ-SITE-SHARE-01..02 | Gist OAuth (CF Worker) + URL-fragment | §GitHub Gist Share; Simon Willison TIL pattern (CITED); fflate/pako round-trip (verified) |
| REQ-SITE-A11Y-01..03 | Keyboard nav, SR labels, focus rings, reduced-motion | §A11y; WAI-ARIA slider role (CITED); prefers-reduced-motion |
| REQ-SITE-PERF-01 | Lighthouse ≥90 all 4 axes | §Lighthouse; lazy-load keeps WASM off LCP (verified scoring weights) |
| REQ-SITE-RESPONSIVE-01 | Mobile-responsive, Monaco read-only <768px | §Responsive; D-49-09 |
</phase_requirements>

## Summary

This phase ships a brand-new top-level `flow-site/` SvelteKit 2 / Svelte 5 / TypeScript / Tailwind-v4 project, sibling to the existing C# projects, deployed to Cloudflare Pages. There is **no existing JS/TS web tooling in this repo** (only a Node-side `flow-lsp` server and the `vscode-extension` package), so this is greenfield within an otherwise .NET monorepo. The four surfaces (Home / Docs / Playground / Showcase) each have a distinct render strategy under one `adapter-cloudflare` build, and the playground is the only page that boots the ~1.6-3 MB Phase 48 WASM runtime — lazily, in `onMount`, after first paint.

The single highest-risk integration is **Monaco Editor under Vite** (web-worker wiring, SSR-unsafe — must live entirely in `onMount`). The second is the **Phase 48 runtime consumption**, which is fully de-risked by the frozen HANDOFF contract: copy the published `AppBundle/` into `static/wasm/` untouched (Vite never processes it — it self-loads via its own `dotnet.js`), dynamic-import `/wasm/flow-runtime.js`, and call `resumeAudio()` + `run()` in the SAME user-gesture async frame. Everything else (shiki custom grammar, mdsvex, gist OAuth via a tiny CF Worker, URL-fragment deflate share) is well-trodden with current, healthy, verified packages.

**Two CONTEXT.md path/assumption corrections the planner MUST act on** (charitable — these are minor and don't touch any locked design decision): (1) the Phase 17 grammar is at `vscode-extension/syntaxes/flow.tmLanguage.json`, **NOT** `flow-lsp/grammars/flow.tmLanguage.json` as D-49-15/code_context state — that path does not exist. (2) The wiki contains **zero real `[[Page-Name]]` links** — the only `[[` match is array data inside a code block in `Collections.md` — so D-49-26's link-rewriter is a near-no-op (still build it defensively, but the acceptance bar "inter-page links work" needs at least one synthetic test fixture, or relax to "transform runs without error"). Also: the HANDOFF says `_headers` lives in `static/_headers`, but the official adapter-cloudflare docs place `_headers` in the **project root** — verify at Plan 49-01.

**Primary recommendation:** Scaffold with `npx sv create flow-site` (TypeScript + Tailwind + Playwright add-ons), pin `adapter-cloudflare`, treat the .NET AppBundle as opaque static assets under `static/wasm/`, wrap the frozen runtime in a thin `src/lib/runtime.ts` (never edit `flow-runtime.js`), and keep all Monaco + WASM code SSR-guarded in `onMount`/`browser`-gated dynamic imports.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Marketing Home render | Frontend Server (SSR/prerender) | CDN/Static | Content known at build time; SSR/prerender for SEO + fast LCP (D-49-13) |
| Docs render (26 pages) | CDN/Static (prerender) | Build step (wiki sync) | Markdown known at build; mdsvex → static HTML; no runtime fetch (D-49-13/16) |
| Static code-block highlighting | Build step (shiki) | — | shiki runs at build/SSR, ships zero client JS (D-49-15) |
| Playground editor | Browser/Client (SPA) | — | Monaco is SSR-incompatible; runs only in browser onMount (D-49-13/14) |
| Flow execution + audio | Browser/Client (WASM) | — | Phase 48 runtime + WebAudioBackend run entirely client-side (HANDOFF) |
| Gist OAuth code exchange | API/Backend (CF Worker) | — | client_secret must stay server-side (D-49-28; CITED Willison TIL) |
| Gist creation | Browser/Client | GitHub API | Client POSTs to api.github.com with token (D-49-29) |
| URL-fragment share encode/decode | Browser/Client | — | deflate+base64 in-browser; fragment never hits the server (D-49-30) |
| Wiki sync | Build step (CF Pages env) | — | `git clone --depth 1` before `vite build` (D-49-25) |
| Texture/image optimization | Build step (Sharp) | CDN/Static | AVIF/WebP generated at build; served as static assets (D-49-32) |
| HTTP security headers | CDN/Static (CF Pages `_headers`) | — | COOP/COEP/CSP/Permissions-Policy on static responses (D-49-38) |

## Standard Stack

All versions below were verified against the **npm registry** on 2026-06-05 via `npm view <pkg> version` and `time.modified`. See §Package Legitimacy Audit for the slopcheck cross-ecosystem note.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `@sveltejs/kit` | 2.63.0 | App framework + routing + build | Locked D-49-11. `[VERIFIED: npm registry]` (pub 2026-06-04) |
| `svelte` | 5.56.2 | Component runtime (runes) | Locked D-49-11. `[VERIFIED: npm registry]` (pub 2026-06-04) |
| `@sveltejs/adapter-cloudflare` | 7.2.8 | CF Pages/Workers build target | Locked D-49-13. `[VERIFIED: npm registry]` (pub 2026-02-18) |
| `@sveltejs/vite-plugin-svelte` | 7.1.2 | Svelte↔Vite bridge | Pulled in by `sv create`. `[VERIFIED: npm registry]` |
| `vite` | 8.0.16 | Bundler/dev server | SvelteKit 2 peer. `[VERIFIED: npm registry]` (pub 2026-06-01) |
| `typescript` | (sv-create default) | Type safety for runtime/editor glue | Locked D-49-11 |
| `tailwindcss` | 4.3.0 | Utility CSS | Locked D-49-11. **v4 = CSS-first, no `tailwind.config.js`** `[VERIFIED: npm registry]` |
| `@tailwindcss/vite` | 4.3.0 | Tailwind v4 Vite plugin (replaces PostCSS) | v4 install path `[VERIFIED: npm registry]` |
| `mdsvex` | 0.12.7 | MDX-flavored markdown preprocessor | Locked D-49-16. `[VERIFIED: npm registry]` (pub 2026-03-08) |
| `shiki` | 4.2.0 | Build-time syntax highlighting | Locked D-49-15. 15.4M weekly dl `[VERIFIED: npm registry]` |
| `monaco-editor` | 0.55.1 | Playground editor | Locked D-49-14. 6.5M weekly dl `[VERIFIED: npm registry]` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `fflate` | 0.8.3 | deflate/inflate for URL-fragment share | **Recommended over pako** — smaller, tree-shakeable, actively maintained (pub 2026-05-16), 47M weekly dl. `[VERIFIED: npm registry]` |
| `pako` | 2.1.0 | Alternative zlib in JS | Fallback if fflate API friction; battle-tested but last published 2022, larger. `[VERIFIED: npm registry]` |
| `sharp` | 0.34.5 | AVIF/WebP/PNG texture generation (D-49-32) | Build-step image optimization. `[VERIFIED: npm registry]` |
| `@playwright/test` | 1.60.0 | E2E + smoke + Lighthouse-adjacent tests | Validation Architecture (pub 2026-06-05). `[VERIFIED: npm registry]` |
| `vitest` | 4.1.8 | Unit/component tests (transform.ts, slug, encode) | Validation Architecture. `[VERIFIED: npm registry]` |
| `@lhci/cli` | (verify at plan) | Lighthouse CI thresholds (D-49-31) | Plan 49-08 gate. Verify version at planning. `[ASSUMED]` |
| `@axe-core/playwright` | (verify at plan) | Automated a11y assertions | Plan 49-08. Verify version at planning. `[ASSUMED]` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `adapter-cloudflare` | `adapter-static` | Static can't host the gist-OAuth server route. D-49-13 already rejected this. Keep cloudflare. |
| Monaco | CodeMirror 6 | Lighter, easier Vite story, no web-worker dance. BUT D-49-14 locks Monaco (LSP-future + Phase 17 alignment). Do NOT substitute — note the cost in Open Questions. |
| shiki | Prism / highlight.js | Both need a custom-language definition too, and ship client JS. shiki gives zero-JS build-time HTML + reuses the existing TextMate grammar directly. Keep shiki. |
| fflate | pako / `CompressionStream` | Native `CompressionStream('deflate')` exists in all modern browsers but Safari history is spotty and it's async/stream-only — fflate is sync, tiny, universal. Prefer fflate. |
| `npm create svelte` | `npx sv create` | The old `create-svelte` flow is superseded — **use `sv create`** (the official Svelte CLI). CONTEXT's "pnpm create svelte@latest" wording is stale. |

**Installation (scaffold):**
```bash
# from repo root
npx sv create flow-site          # choose: SvelteKit minimal, TypeScript, add-ons: tailwindcss, prettier, eslint, playwright, vitest
cd flow-site
pnpm install
pnpm add -D @sveltejs/adapter-cloudflare mdsvex shiki
pnpm add monaco-editor fflate
pnpm add -D sharp
# Tailwind v4 (sv-create's tailwindcss add-on already wires @tailwindcss/vite; verify)
```

**Version verification performed:** all Core + Supporting packages confirmed present on npm with 2026 publish dates (commands run this session). Note Tailwind is **v4** (a breaking redesign from v3 — CSS-first `@theme` config, `@tailwindcss/vite` plugin, NO `tailwind.config.js` required, NO PostCSS). Plan 49-02's design-token work must use the v4 `@theme` directive, not a JS config object.

## Package Legitimacy Audit

slopcheck 0.6.1 was installed and run. **Critical methodology note:** slopcheck defaults to the **PyPI** ecosystem. Running it without `--ecosystem npm` produced 9 false `[SLOP]` verdicts ("does not exist on pypi") for npm packages — a textbook cross-ecosystem false positive, NOT a hallucination signal. Re-running with `slopcheck install --ecosystem npm ...` returned `[OK]` for all checked packages. The table below reflects the **correct npm-ecosystem** verification, cross-checked against `npm view` (existence + version + source repo) and `api.npmjs.org` download counts.

| Package | Registry | Source Repo | Weekly DL | slopcheck (npm) | Disposition |
|---------|----------|-------------|-----------|-----------------|-------------|
| @sveltejs/kit | npm | github.com/sveltejs/kit | (millions) | OK | Approved |
| svelte | npm | github.com/sveltejs/svelte | (millions) | OK | Approved |
| @sveltejs/adapter-cloudflare | npm | github.com/sveltejs/kit | (millions) | OK (verified via npm view) | Approved |
| @sveltejs/vite-plugin-svelte | npm | github.com/sveltejs/vite-plugin-svelte | (millions) | OK (verified via npm view) | Approved |
| vite | npm | github.com/vitejs/vite | (tens of M) | OK | Approved |
| tailwindcss | npm | github.com/tailwindlabs/tailwindcss | (tens of M) | OK | Approved |
| @tailwindcss/vite | npm | github.com/tailwindlabs/tailwindcss | (millions) | OK (verified via npm view) | Approved |
| mdsvex | npm | github.com/pngwn/MDsveX | 195,273 | OK | Approved |
| shiki | npm | github.com/shikijs/shiki | 15,376,373 | OK | Approved |
| monaco-editor | npm | github.com/microsoft/monaco-editor | 6,555,389 | OK (npm; PyPI SUS = wrong-ecosystem) | Approved |
| pako | npm | github.com/nodeca/pako | 92,888,987 | OK | Approved |
| fflate | npm | github.com/101arrowz/fflate | 47,074,115 | OK (verified via npm view) | Approved |
| sharp | npm | github.com/lovell/sharp | (tens of M) | OK | Approved |
| @playwright/test | npm | github.com/microsoft/playwright | (millions) | OK (verified via npm view) | Approved |
| vitest | npm | github.com/vitest-dev/vitest | (tens of M) | OK (npm; PyPI SUS "close to pytest" = wrong-ecosystem) | Approved |

**Packages removed due to slopcheck [SLOP] verdict:** none (all [SLOP] verdicts were PyPI cross-ecosystem false positives; corrected via `--ecosystem npm`).
**Packages flagged as suspicious [SUS]:** none on the correct ecosystem. (`monaco-editor` and `vitest` showed PyPI [SUS] only because they don't exist on PyPI — irrelevant; both are first-party Microsoft/Vitest-team packages with millions of npm downloads.)

**No `postinstall` network/filesystem red flags** identified in the core set. Note `sharp` ships native binaries (libvips) via optional platform deps — standard and expected, not a slop signal; it builds fine on CF Pages' Linux build image and on the dev machine.

## Architecture Patterns

### System Architecture Diagram

```
                          ┌─────────────────────── BUILD TIME (CF Pages build container) ───────────────────────┐
                          │                                                                                       │
  wiki repo (git) ──clone─┼─► src/docs/wiki/*.md ──transform.ts──► linked md ──mdsvex+shiki──► prerendered /docs  │
  textures/*.png ─────────┼─► sharp ──► *.avif / *.webp / *.png (static/)                                         │
  flow-lang (C#) ─publish─┼─► AppBundle/ ──cp──► static/wasm/ (flow-runtime.js + _framework/*.wasm) [OPAQUE]      │
  Home/Showcase .svelte ──┼─► shiki static code blocks ──► prerendered HTML                                       │
                          │                          vite build → .svelte-kit/cloudflare/ (_worker.js + static)   │
                          └───────────────────────────────────────────────────────────────────────────────────────┘
                                                              │ deploy
                                                              ▼
  ┌──────────────────────────────────── RUNTIME (browser + CF edge) ────────────────────────────────────┐
  │                                                                                                       │
  │  GET /           ──► prerendered Home (no WASM)                                                        │
  │  GET /docs/:slug ──► prerendered Docs (no WASM, static shiki HTML)                                     │
  │  GET /showcase   ──► prerendered Showcase (gesture-gated <audio>)                                      │
  │                                                                                                       │
  │  GET /playground (SPA) ──onMount──► import('/wasm/flow-runtime.js') ──loadFlowRuntime()──► Runtime     │
  │        │                                  │ (lazy, after first paint)                                  │
  │        │   Monaco (onMount, ?worker)      ▼                                                            │
  │        │   ──Run click (1 gesture)──► resumeAudio() + run(src) ──► RunResult{stdout,stderr,midi,errors}│
  │        │                                  └─► WebAudioBackend ──► AudioContext (audible)               │
  │        │                                                                                               │
  │        ├─ Share ──► fflate.deflate(src)→base64 ──► copy /playground#code=...                           │
  │        └─ Save to gist ──► GET /api/auth/github (CF Worker exchanges code→token) ──► POST api.github   │
  │                                                                                                        │
  └────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

The reader can trace the primary use case (compose → hear): navigate to /playground → Monaco mounts → edit Flow → click Run → `resumeAudio()`+`run()` in one gesture frame → WebAudioBackend plays the tone and `RunResult` populates the console.

### Recommended Project Structure
```
flow-site/
├── src/
│   ├── routes/
│   │   ├── +layout.svelte          # nav (5 tabs), theme toggle, ARIA landmarks
│   │   ├── +page.svelte            # Home (D-49-21); export const prerender = true
│   │   ├── docs/
│   │   │   ├── +page.svelte        # categorized TOC (D-49-22)
│   │   │   └── [slug]/+page.svelte # renders synced wiki page; prerender = true
│   │   ├── playground/+page.svelte # SPA; ssr = false; onMount WASM + Monaco
│   │   └── showcase/
│   │       ├── +page.svelte        # gallery
│   │       └── [slug]/+page.svelte # piece detail
│   ├── lib/
│   │   ├── runtime.ts              # THIN wrapper around flow-runtime.js (never edit the runtime)
│   │   ├── design/tokens.css       # CSS custom properties (D-49-17)
│   │   ├── components/skeuo/       # Knob, Button, Toggle, Panel, MetalRail, LedIndicator, Slider (D-49-19)
│   │   ├── docs/transform.ts       # [[link]] rewrite + slug (D-49-26/27)
│   │   ├── share/encode.ts         # fflate deflate↔inflate + base64url (D-49-30)
│   │   └── monaco/                 # editor setup, flow-lang registration, worker env
│   ├── docs/wiki/                  # git-cloned at build (gitignored)
│   └── app.css                     # @import "tailwindcss"; @theme { ... }
├── static/
│   ├── wasm/                       # AppBundle copied here (flow-runtime.js + _framework/) — OPAQUE to Vite
│   └── textures/                   # sharp-optimized AVIF/WebP/PNG
├── workers/gist-auth.ts            # CF Worker OAuth (D-49-28)
├── scripts/
│   ├── sync-wiki.sh                # git clone --depth 1 (D-49-25)
│   ├── sync-runtime.sh             # dotnet publish + cp AppBundle → static/wasm/ (HANDOFF §2)
│   └── optimize-textures.mjs       # sharp (D-49-32)
├── _headers                        # COOP/COEP/CSP (D-49-38) — VERIFY root vs static/ placement
├── docs-categories.json            # wiki filename → category (D-49-22)
├── svelte.config.js                # adapter-cloudflare + mdsvex preprocess
├── vite.config.ts                  # @tailwindcss/vite + monaco worker config
└── package.json
```

### Pattern 1: Per-route render strategy (D-49-13)
**What:** SvelteKit page options control prerender/SSR/SPA per route via module-level exports.
**When to use:** marketing+docs+showcase prerender; playground is client-only.
```ts
// src/routes/+page.svelte (Home) and docs/showcase pages
export const prerender = true;

// src/routes/playground/+page.svelte
export const prerender = false;
export const ssr = false;   // Monaco + WASM are browser-only; no SSR attempt
export const csr = true;
```
`adapter-cloudflare` honors these; prerendered pages become static assets, the playground a client-rendered SPA shell, and only `/api/auth/github` becomes a real server function. `[CITED: svelte.dev/docs/kit/adapter-cloudflare]` + `[CITED: svelte.dev/docs/kit/page-options]`

### Pattern 2: Consume the Phase 48 runtime (HANDOFF — frozen contract)
**What:** dynamic-import the opaque AppBundle module; never edit it; wrap in `runtime.ts`.
```ts
// src/lib/runtime.ts — thin adapter; do NOT modify flow-runtime.js (HANDOFF §8)
export async function bootRuntime() {
  const { loadFlowRuntime } = await import(/* @vite-ignore */ '/wasm/flow-runtime.js');
  return await loadFlowRuntime();   // throws 'Flow runtime boot failed: ...' on boot failure
}
```
```svelte
<!-- playground/+page.svelte -->
<script lang="ts">
  import { onMount } from 'svelte';
  let runtime = $state<Runtime | null>(null);
  let bootError = $state<string | null>(null);
  onMount(async () => {
    try { runtime = await (await import('$lib/runtime')).bootRuntime(); }
    catch (e) { bootError = (e as Error).message; }   // top-level pane, distinct from per-run errors
  });
  async function onRun() {                              // MUST be the user-gesture frame
    await runtime!.resumeAudio();                       // HANDOFF §5 — same async frame as run()
    const r = await runtime!.run(editorValue);
    stdout = r.stdout; stderr = r.stderr; errors = r.errors;
    if (r.midi) offerDownload(r.midi, 'flow.mid', 'audio/midi');  // HANDOFF §9
  }
</script>
```
`/* @vite-ignore */` on the dynamic import string keeps Vite from trying to analyze/bundle the .NET module — it must be fetched verbatim from `static/wasm/` at runtime. `[CITED: 48-PHASE49-HANDOFF.md §2,§5,§8,§9]`

### Pattern 3: Monaco under Vite/SvelteKit (the hard one)
**What:** Monaco is SSR-incompatible and needs web workers wired via Vite's `?worker` suffix; `MonacoEnvironment.getWorker` returns the right worker per label.
**When to use:** playground editor only, inside `onMount`.
```ts
// src/lib/monaco/index.ts — imported ONLY from onMount (never top-level in a +page that SSRs)
import * as monaco from 'monaco-editor';
import EditorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';
self.MonacoEnvironment = { getWorker: () => new EditorWorker() };  // Flow has no JSON/TS workers → editor worker only
```
- For a **single custom language** (Flow) you only need the base `editor.worker` — no TS/JSON/CSS/HTML language workers, which removes most of the classic Monaco-Vite pain.
- Firefox requires `type: 'module'` worker registration (the `?worker` import handles this).
- Register Flow via `monaco.languages.register({ id: 'flow' })` + a Monarch tokenizer OR load the TextMate grammar through `monaco-textmate`/`shiki`'s Monaco bridge (v1 can ship a hand-written Monarch tokenizer derived from the grammar to avoid the onigasm/WASM-in-Monaco complexity; full TextMate-in-Monaco is a reasonable v1 stretch — see Open Question 3).
`[CITED: github.com/sveltejs/kit discussions #3539]` + `[VERIFIED: github.com/choas/sveltekit-monaco-editor-example]`

### Pattern 4: shiki custom Flow grammar for static blocks (D-49-15)
**What:** shiki v1+ is fs-agnostic — you import the grammar JSON object and pass it to `createHighlighter({ langs: [...] })`.
```ts
import { createHighlighter } from 'shiki';
import flowGrammar from '../../../vscode-extension/syntaxes/flow.tmLanguage.json';  // CORRECTED PATH
const hl = await createHighlighter({
  themes: ['github-light', 'github-dark'],
  langs: [flowGrammar as any],   // scopeName 'source.flow', aliases ['flow']
});
const html = hl.codeToHtml(src, { lang: 'flow', themes: { light: 'github-light', dark: 'github-dark' } });
```
Wire into mdsvex via its `highlight` option so ```flow fenced blocks in docs render server-side. The wiki uses only ```flow and ```bash fences (verified) — both shiki-known once Flow is loaded. `[CITED: shiki.style/guide/load-lang]`

### Pattern 5: mdsvex preprocess for docs (D-49-16)
```js
// svelte.config.js
import { mdsvex } from 'mdsvex';
const config = {
  preprocess: [mdsvex({ extensions: ['.md', '.svx'], highlight: { highlighter: flowShikiHighlighter } })],
  extensions: ['.svelte', '.svx', '.md'],
  kit: { adapter: cloudflare() }
};
```
mdsvex lets docs embed `<OpenInPlayground source="...">` Svelte components while keeping the source wiki-compatible markdown. `[CITED: mdsvex.pngwn.io/docs]`

### Pattern 6: GitHub gist OAuth via CF Worker (D-49-28, Willison pattern)
**What:** the canonical "OAuth for a static site" — worker holds the secret, browser keeps the token.
1. Browser → `https://github.com/login/oauth/authorize?client_id=...&redirect_uri=.../api/auth/github&scope=gist&state=<random>`
2. Callback → worker POSTs `https://github.com/login/oauth/access_token` with `{client_id, client_secret, code, state}` (Accept: application/json)
3. Worker validates `state` (CSRF) against the value it stashed (cookie), then redirects back to `/playground` with the token in the URL fragment → `sessionStorage` (D-49-28 specifies fragment; Willison uses localStorage — sessionStorage is the more conservative choice and matches CONTEXT).
4. Gist creation is client-side: `POST https://api.github.com/gists` with `Authorization: Bearer <token>` (D-49-29).
`[CITED: til.simonwillison.net/cloudflare/workers-github-oauth]`

### Pattern 7: URL-fragment share (D-49-30)
```ts
import { deflateSync, inflateSync, strToU8, strFromU8 } from 'fflate';
export const encode = (src: string) =>
  btoa(String.fromCharCode(...deflateSync(strToU8(src)))).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
export const decode = (frag: string) =>
  strFromU8(inflateSync(Uint8Array.from(atob(frag.replace(/-/g,'+').replace(/_/g,'/')), c => c.charCodeAt(0))));
```
The fragment (`#code=...`) never leaves the browser → no server cost, works anonymously. base64url avoids `+//=` URL-encoding issues. `[VERIFIED: fflate npm API]`

### Anti-Patterns to Avoid
- **Importing Monaco or `flow-runtime.js` at module top-level in an SSR'd route** → build/runtime crash (`window`/`document`/`self` undefined). Keep them in `onMount` or dynamic imports guarded by `browser`.
- **Running the AppBundle through Vite's WASM pipeline** (`?init`, `vite-plugin-wasm`) → breaks .NET's self-loader. Treat the bundle as opaque `static/` assets fetched at runtime.
- **Calling `resumeAudio()` on page load** → silent no-op; tone never plays. It MUST be inside the Run click handler (HANDOFF §5 — this was Phase 48's final boot-fix bug).
- **Editing `flow-runtime.js` / `WasmEntry.cs` / adding `[JSImport]` names** → forbidden by HANDOFF §8. Wrap, don't fork.
- **Lazy-loading the LCP hero image** → costs LCP points (verified: lazy-loaded LCP scores 52% "good" vs 79%). Eager-load above-fold; lazy-load only below-fold textures.
- **Using a `tailwind.config.js`** with Tailwind v4 → unnecessary; use `@theme` in `app.css`. A stale v3 config is a common drift trap.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Flow execution in browser | A JS reimplementation | Phase 48 `flow-runtime.js` (frozen) | The whole language is already compiled to WASM; HANDOFF is the contract |
| Syntax highlighting (static) | Regex highlighter | shiki + existing TextMate grammar | Zero client JS, reuses Phase 17 grammar, VS Code-accurate |
| Code editor | contenteditable + custom | Monaco (D-49-14) | Selection, undo, multi-cursor, a11y are huge; locked decision |
| deflate/inflate | Custom LZ | fflate | Correctness + cross-browser; tiny |
| OAuth secret exchange | Client-side token flow | CF Worker (secret server-side) | client_secret cannot be exposed (CITED) |
| AVIF/WebP encoding | ImageMagick shellout | sharp | Mature libvips bindings, builds on CF Pages |
| Markdown→Svelte | Custom parser | mdsvex (D-49-16) | MDAST/HAST pipeline + component embedding |
| Accessible rotary control | Pointer-drag-only knob | `role="slider"` + arrow keys (WAI-ARIA) | Keyboard + SR support is required (D-49-10); see §A11y |

**Key insight:** the single biggest lever is that Flow already runs in the browser as a published WASM artifact — Phase 49 is a *consumer*, not a re-implementer. Every temptation to "just do it in JS" (run Flow, highlight Flow, encode shares) has a mature off-the-shelf answer.

## Common Pitfalls

### Pitfall 1: Monaco SSR crash
**What goes wrong:** `ReferenceError: self is not defined` / `document is not defined` at build or first render.
**Why it happens:** Monaco touches browser globals at import time; SvelteKit SSRs routes by default.
**How to avoid:** `export const ssr = false` on `/playground`, and import Monaco only inside `onMount`. Never `import * as monaco` at the top of an SSR'd module.
**Warning signs:** build fails in `vite build` prerender step, or the route 500s server-side.

### Pitfall 2: AppBundle relative-layout breakage
**What goes wrong:** runtime boots but 404s on `dotnet.boot.js` or `_framework/*.wasm`.
**Why it happens:** `flow-runtime.js` does `import './_framework/dotnet.js'` then fetches `dotnet.boot.js` from the same dir. Flattening or renaming breaks the relative resolution (this exact 404 was Phase 48's Plan 48-06 boot blocker).
**How to avoid:** copy the AppBundle verbatim into `static/wasm/` preserving `flow-runtime.js`-at-root + `_framework/`-sibling. Verify the served URLs in the network tab early (HANDOFF §1, §8).
**Warning signs:** boot error pane fires; network tab shows 404 on `dotnet.boot.js`.

### Pitfall 3: Autoplay-policy silent failure
**What goes wrong:** Run "works" (console populates) but no sound.
**Why it happens:** `resumeAudio()` not called inside the gesture frame, or called on load.
**How to avoid:** `await runtime.resumeAudio()` then `await runtime.run(...)` in the same click handler (HANDOFF §5). Re-smoke in Chrome/Chromium early (Phase 48 only ear-verified Firefox) and on Safari (strictest policy, unverified).
**Warning signs:** Firefox plays, Chrome/Safari silent; AudioContext stuck in `suspended`.

### Pitfall 4: COOP/COEP breaks Monaco CDN / fonts (D-49-38 vs HANDOFF §3)
**What goes wrong:** turning on `Cross-Origin-Embedder-Policy: require-corp` makes every cross-origin subresource on /playground (Monaco CDN chunks, web fonts) fail to load unless they send CORP/CORS headers.
**Why it happens:** `require-corp` is strict; v1 doesn't even need it (Phase 48 v1 is single-threaded, no SharedArrayBuffer).
**How to avoid:** **Self-host Monaco** (bundle via Vite, no CDN) OR leave COOP/COEP OFF for v1.5 and add them only when the v1.6 AudioWorklet/SAB path lands. HANDOFF §3 explicitly sanctions deferring them. See Open Question 1.
**Warning signs:** Monaco fails to load only when headers are on; fonts fall back.

### Pitfall 5: Tailwind v4 vs v3 muscle memory
**What goes wrong:** following a v3 tutorial → broken `tailwind.config.js` + PostCSS plugin that v4 doesn't use.
**Why it happens:** most online content is still v3.
**How to avoid:** v4 = `@import "tailwindcss";` + `@theme { --color-walnut: #5C3A21; ... }` in `app.css` + `@tailwindcss/vite` plugin in `vite.config.ts`. Map D-49-17 design tokens into `@theme`.
**Warning signs:** `npx tailwindcss init` produces a config the build ignores; utilities don't generate.

### Pitfall 6: Wiki sync auth in CF Pages build env
**What goes wrong:** `git clone` of the wiki fails or the build can't reach it.
**Why it happens:** wiki repo may be private; CF build container has no SSH key; `WIKI_REPO_URL` env var not set.
**How to avoid:** for a public wiki, HTTPS clone needs no auth. If private, use a tokenized HTTPS URL in `WIKI_REPO_URL` (CF dashboard secret). Make `sync-wiki.sh` fail loudly (non-zero exit) so the deploy fails visibly rather than shipping empty docs. Cache/commit a fallback copy if resilience matters.
**Warning signs:** /docs renders empty in production but works locally.

### Pitfall 7: The `[[link]]` rewriter has nothing to rewrite
**What goes wrong:** Plan 49-04 acceptance "inter-page links work" can't be demonstrated — the wiki has zero real `[[Page-Name]]` links (the single `[[` is array data in a Collections.md code block).
**Why it happens:** CONTEXT.md assumed GitHub-wiki link syntax is in use; it isn't (these docs use prose, not cross-links).
**How to avoid:** still build `transform.ts` defensively (idempotent, skips fenced code blocks so it doesn't mangle `[[1,10],...]`), but relax the acceptance to "transform runs without error + a synthetic fixture round-trips," and add a unit test with a hand-written `[[Quick-Start]]` fixture. Flag to composer that cross-linking the wiki is a content task, not a code task.
**Warning signs:** transform regex matches inside a code block and corrupts an array literal.

## Runtime State Inventory

> Phase 49 is greenfield (new `flow-site/` directory + new web stack). It does NOT rename/refactor existing code. Most categories are N/A, but two cross-system facts matter for planning:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — new project, no datastore. URL-fragment + gist are stateless/external. | none |
| Live service config | **Cloudflare Pages project** (created at Plan 49-36, lives in CF dashboard, NOT in git): project name, build command, output dir, env vars (`WIKI_REPO_URL`, `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET`). **GitHub OAuth App** (registered in GitHub dev settings, NOT in git): client id/secret + callback URL. Both are manual dashboard setup — document in the Plan 49-09 deployment runbook. | manual dashboard setup + runbook |
| OS-registered state | None. | none |
| Secrets/env vars | `GITHUB_CLIENT_SECRET` (CF Pages secret), `WIKI_REPO_URL` (may embed a token). Never commit. `.gitignore` `flow-site/.dev.vars` + `src/docs/wiki/`. | gitignore + CF dashboard |
| Build artifacts | The .NET `AppBundle/` copied into `static/wasm/` is a build artifact regenerated by `sync-runtime.sh` (`dotnet publish -p:FlowTarget=Web`). Decide: commit it (simpler CF build, no dotnet in CF container) vs regenerate in CF build (needs wasm-tools workload in CF container — heavy). **Recommend: commit the published AppBundle into the repo** (or a release asset) so the CF Pages build is pure-Node. See Open Question 2. | decide commit-vs-rebuild |

**Verified by:** filesystem inspection — no `flow-site/` exists yet; `.planning/config.json` confirms greenfield; HANDOFF documents the AppBundle as a `dotnet publish` output.

## Code Examples

### Tailwind v4 design tokens (D-49-17)
```css
/* src/app.css */
@import "tailwindcss";
@theme {
  --color-paper:  #F5F0E6;
  --color-walnut: #5C3A21;
  --color-brass:  #C9A567;
  --color-slate:  #2C2E33;
  --color-walnut-dark: #2A1F18;
  --radius-knob: 24px;
  --ease-overshoot: cubic-bezier(0.16, 1, 0.3, 1);
  /* spacing/type/shadow scales per D-49-17 */
}
/* dark theme (D-49-20): re-declare custom props under a [data-theme="dark"] selector */
[data-theme="dark"] { --color-paper: var(--color-walnut-dark); /* ... amber-LED accents */ }
@media (prefers-reduced-motion: reduce) {
  *, ::before, ::after { animation-duration: 0.001ms !important; transition-duration: 0.001ms !important; }
}
```
`[CITED: tailwindcss.com/docs/guides/sveltekit (v4)]`

### Accessible rotary Knob skeleton (D-49-10/19)
```svelte
<!-- role=slider per WAI-ARIA; keyboard arrows adjust; visually a rotating knob -->
<div role="slider" tabindex="0"
     aria-label={label} aria-valuemin={min} aria-valuemax={max}
     aria-valuenow={value} aria-valuetext={`${value} ${unit}`}
     onkeydown={(e)=>{ if(e.key==='ArrowUp'||e.key==='ArrowRight') value=Math.min(max,value+step);
                       if(e.key==='ArrowDown'||e.key==='ArrowLeft') value=Math.max(min,value-step); }}
     style={`--rot:${((value-min)/(max-min))*270-135}deg`}>
  <!-- brass 2px focus outline via :focus-visible; rotation via transform: rotate(var(--rot)) -->
</div>
```
WAI-ARIA: the `slider` role is the only interactive range role; must be focusable, support arrow keys, and carry `aria-valuemin/max/now` (+ `aria-valuetext` for the unit). Reduced-motion → render as a flat `<input type=range>` styled slider. `[CITED: developer.mozilla.org ARIA slider role]`

### MIDI/notation download (HANDOFF §9)
```ts
function offerDownload(bytes: Uint8Array, name: string, mime: string) {
  const url = URL.createObjectURL(new Blob([bytes], { type: mime }));
  Object.assign(document.createElement('a'), { href: url, download: name }).click();
  URL.revokeObjectURL(url);
}
// result.midi → 'audio/midi'; notation strings → 'application/xml' etc.
```

## State of the Art

| Old Approach | Current Approach (2026) | When Changed | Impact |
|--------------|-------------------------|--------------|--------|
| `npm create svelte@latest` | `npx sv create` (Svelte CLI) | 2024 | CONTEXT's scaffold wording is stale; use `sv create` |
| Tailwind v3 (JS config + PostCSS) | Tailwind v4 (`@theme` CSS-first + `@tailwindcss/vite`) | 2025 (v4) | No `tailwind.config.js`; tokens go in CSS — affects Plan 49-02 |
| Svelte 4 stores | Svelte 5 runes (`$state`/`$derived`/`$effect`) | Svelte 5 GA | Playground state uses runes (D-49-11) |
| shiki `path` option | Pass parsed grammar object (fs-agnostic) | shiki v1 | Import the JSON, pass to `createHighlighter` |
| pako default | fflate for new JS projects | ongoing | Smaller/tree-shakeable; pako still fine |

**Deprecated/outdated:**
- `create-svelte` package → superseded by `sv`.
- Tailwind v3 config patterns → v4 CSS-first.
- shikiji (the fork) → merged back into shiki v1+.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `@lhci/cli` + `@axe-core/playwright` are the right validation tools; exact versions unverified this session | Standard Stack / Validation | Low — both are standard; planner verifies version at Plan 49-08 |
| A2 | A hand-written Monarch tokenizer (derived from the TextMate grammar) is acceptable for v1 Monaco highlighting if full TextMate-in-Monaco proves heavy | Pattern 3 / Open Q3 | Medium — affects highlighting fidelity in the editor (static shiki blocks are unaffected) |
| A3 | The published `.NET` AppBundle should be committed/release-asset'd rather than rebuilt in the CF Pages container | Runtime State / Open Q2 | Medium — rebuilding needs `wasm-tools` + .NET SDK in CF's Node build image (likely infeasible); commit is safer |
| A4 | `_headers` placement (root vs static/) — official docs say project root; HANDOFF says static/ | Summary / D-49-38 | Low — easy to verify at Plan 49-01; wrong location = headers silently ignored |
| A5 | CF Pages Git integration build output dir is `.svelte-kit/cloudflare` | Deployment | Low — documented; verify in CF dashboard |
| A6 | sessionStorage (per D-49-28) over localStorage (per Willison) for the gist token | Pattern 6 | Low — sessionStorage is the more conservative choice; matches CONTEXT |

## Open Questions (RESOLVED)

1. **Ship COOP/COEP headers in v1.5, or defer to v1.6?**
   - What we know: D-49-38 wants them set now (AudioWorklet foundation); HANDOFF §3 says v1 needs NO special headers and `require-corp` complicates the Monaco-CDN path.
   - What's unclear: whether Monaco is self-hosted (then headers are safe) or CDN-loaded (then `require-corp` breaks it).
   - Recommendation: **self-host Monaco (bundle via Vite, no CDN)** so the headers are harmless, OR scope COOP/COEP to `/playground/*` and confirm every subresource is same-origin. If any friction at Plan 49-05/08, defer the two headers per HANDOFF's explicit sanction — keep CSP + Permissions-Policy regardless. (CSP in D-49-38 mentions "Monaco CDN" — reconcile with self-hosting decision.)
   - **RESOLVED** — scoped/self-host approach (Monaco self-hosted via Vite, COOP/COEP scoped to `/playground/*`), see Plan 49-01 Task 2.

2. **Commit the .NET AppBundle, or rebuild it in CF Pages?**
   - What we know: CF Pages' default build container is Node-centric; rebuilding needs the .NET 10 SDK + `wasm-tools` workload (heavy, slow, possibly unavailable).
   - Recommendation: run `sync-runtime.sh` (`dotnet publish -p:FlowTarget=Web`) on the dev machine and **commit `static/wasm/`** (or attach as a release asset the CF build downloads). Keep the CF build pure-Node (`pnpm build`). Add a CI check that the committed bundle matches a fresh publish.
   - **RESOLVED** — commit the prebuilt AppBundle into `flow-site/static/wasm/`, see Plan 49-01 Task 2.

3. **Monaco Flow highlighting: Monarch tokenizer vs TextMate-in-Monaco for v1?**
   - What we know: shiki uses the TextMate grammar directly (static blocks: solved). Monaco natively speaks Monarch; using the TextMate grammar in Monaco needs `monaco-textmate` + onigasm/onig WASM (extra complexity, another WASM payload on /playground).
   - Recommendation: v1 ships a **hand-written Monarch tokenizer** mirroring the grammar's keyword/type/note/operator scopes (small, fast, no extra WASM). Reserve TextMate-in-Monaco for v1.6 alongside the LSP bridge. Single source of truth stays the grammar; the Monarch tokenizer is a derived view. (D-49-15 only mandates the grammar for *static* blocks via shiki — it does not require TextMate inside Monaco.)
   - **RESOLVED** — hand-written Monarch tokenizer for v1, see Plan 49-05 Task 1.

4. **Grammar path discrepancy** — D-49-15 says `flow-lsp/grammars/flow.tmLanguage.json`; the file is actually at `vscode-extension/syntaxes/flow.tmLanguage.json`. Recommendation: planner uses the real path (verified to exist, scope `source.flow`). Consider symlinking or a copy step so `flow-site` doesn't reach across two sibling projects at build time.
   - **RESOLVED** — real path `vscode-extension/syntaxes/flow.tmLanguage.json`, see Plan 49-04 Task 2.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js | SvelteKit/Vite build + dev | ✓ | v24.15.0 | — |
| pnpm | package manager (D-49 discretion) | ✓ | 11.12.1 | npm 10 also present |
| npm | registry access / `npx sv create` | ✓ | 10.x | — |
| .NET SDK | `dotnet publish -p:FlowTarget=Web` (AppBundle) | ✓ | 10.0.108 | — |
| wasm-tools workload | the Web publish | ✓ (machine prereq per CLAUDE.md) | installed Phase 48 | — |
| git | wiki clone + repo | ✓ | 2.53.0 | — |
| sharp/libvips | texture optimization | ✗ (not yet installed) | — | install via pnpm; native binary auto-fetched |
| Cloudflare account | CF Pages project + Worker deploy | ✗ (composer's, needed Plan 49-01/36) | — | **BLOCKING for deploy** — composer must provide |
| GitHub OAuth App | gist OAuth (client id/secret) | ✗ (must be registered) | — | **BLOCKING for Save-to-gist** — composer registers |
| flow-sharp.wiki.git | docs sync | ✓ exists on GitHub (local `wiki/` copy present) | — | local `wiki/` copy is a fallback seed |

**Missing dependencies with no fallback (composer action required, not code-blocking for build):**
- Cloudflare account/login (Plan 49-01 project creation, Plan 49-08 deploy) — D-49-36 already flags "composer's CF account needed."
- GitHub OAuth App registration (Plan 49-06) — needed for Save-to-gist only; URL-fragment share works without it.

**Missing dependencies with fallback:**
- `sharp` — install at Plan 49-02; native binary fetched automatically on `pnpm add`.
- Live wiki repo — the in-repo `wiki/` directory is an exact local copy usable as a build seed if the remote clone is unavailable.

## Validation Architecture

> nyquist_validation is enabled (config.json). This section maps each phase capability to an automated validation.

### Test Framework
| Property | Value |
|----------|-------|
| Framework (unit/component) | Vitest 4.1.8 (+ `vitest-browser-svelte` or `@testing-library/svelte` for component tests) |
| Framework (E2E/smoke) | Playwright 1.60.0 |
| Lighthouse gate | `@lhci/cli` (Lighthouse CI) — thresholds Performance/A11y/BestPractices/SEO ≥90 (D-49-31) |
| A11y assertions | `@axe-core/playwright` in E2E specs |
| Config files | `vitest.config.ts`, `playwright.config.ts`, `lighthouserc.cjs` — all Wave 0 (none exist yet) |
| Quick run command | `pnpm vitest run` (unit) |
| Full suite command | `pnpm vitest run && pnpm playwright test && pnpm lhci autorun` |

### Phase Requirements → Test Map
| Req (candidate) | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SITE-IA-01 | 5-tab nav renders + routes resolve | E2E | `playwright test tests/nav.spec.ts` | ❌ Wave 0 |
| SITE-IA-02 | per-route render strategy (prerender/SPA) | E2E (check served HTML vs hydrated) | `playwright test tests/render-strategy.spec.ts` | ❌ Wave 0 |
| SITE-DOCS-01 | 26 wiki pages render at /docs/[slug] | E2E (loop over slugs) | `playwright test tests/docs-render.spec.ts` | ❌ Wave 0 |
| SITE-DOCS-02 | `[[link]]` transform + slug kebab-case | unit | `vitest run src/lib/docs/transform.test.ts` | ❌ Wave 0 |
| SITE-DOCS-03 | categorized TOC from docs-categories.json | unit + E2E | `vitest run` + `playwright test tests/docs-toc.spec.ts` | ❌ Wave 0 |
| SITE-PLAYGROUND-01 | WASM runtime boots (smoke) | E2E (navigate, await runtime ready, no boot error) | `playwright test tests/wasm-boot.spec.ts` | ❌ Wave 0 |
| SITE-PLAYGROUND-02 | Run produces stdout for `(print "hi")` | E2E | `playwright test tests/playground-run.spec.ts` | ❌ Wave 0 |
| SITE-PLAYGROUND-03 | Run + audio gesture chain (AudioContext resumes) | E2E (assert context state running post-click) | `playwright test tests/playground-audio.spec.ts` | ❌ Wave 0 |
| SITE-PLAYGROUND-04 | MIDI download appears when writeMidi called | E2E (assert download event) | `playwright test tests/playground-midi.spec.ts` | ❌ Wave 0 |
| SITE-PLAYGROUND-05 | Monaco read-only <768px | E2E (mobile viewport) | `playwright test tests/playground-mobile.spec.ts` | ❌ Wave 0 |
| SITE-SHARE-01 | URL-fragment encode↔decode round-trips | unit | `vitest run src/lib/share/encode.test.ts` | ❌ Wave 0 |
| SITE-SHARE-02 | OAuth worker state-validates + exchanges (mocked) | unit/integration (Worker test w/ mocked fetch) | `vitest run workers/gist-auth.test.ts` | ❌ Wave 0 |
| SITE-A11Y-01..03 | axe: 0 critical violations on /, /docs, /playground | E2E + axe | `playwright test tests/a11y.spec.ts` | ❌ Wave 0 |
| SITE-DESIGN-01..04 | skeuo components render both themes; reduced-motion | component + visual-regression | `vitest run` + `playwright test tests/visual.spec.ts` (screenshot baselines) | ❌ Wave 0 |
| SITE-PERF-01 | Lighthouse ≥90 ×4 on /, /docs, /playground | Lighthouse CI | `pnpm lhci autorun` | ❌ Wave 0 |
| SITE-RESPONSIVE-01 | layout collapses to single column <768px | E2E visual | `playwright test tests/responsive.spec.ts` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `pnpm vitest run` (fast unit pass: transform, slug, encode, worker)
- **Per wave merge:** `pnpm vitest run && pnpm playwright test`
- **Phase gate (Plan 49-08):** full suite incl. `pnpm lhci autorun` green before `/gsd:verify-work`; cross-browser HUMAN-UAT (Chrome/Firefox/Safari + mobile) re-smokes audio (HANDOFF §7 — Chrome/Safari unverified by Phase 48).

### Wave 0 Gaps
- [ ] `vitest.config.ts` + `playwright.config.ts` + `lighthouserc.cjs` — no test infra exists (greenfield)
- [ ] `tests/` E2E directory + per-spec files above
- [ ] `src/lib/docs/transform.test.ts` — with a synthetic `[[Quick-Start]]` fixture (real wiki has none — Pitfall 7)
- [ ] `src/lib/share/encode.test.ts` — fflate round-trip property test
- [ ] `workers/gist-auth.test.ts` — mocked GitHub token-exchange + CSRF-state rejection
- [ ] Visual-regression baselines for the ≤8 skeuo components (light + dark + reduced-motion)
- [ ] Component test harness install: `vitest-browser-svelte` or `@testing-library/svelte` (verify version at planning)
- [ ] `@lhci/cli` + `@axe-core/playwright` install

## Security Domain

> security_enforcement is not set to false in config → treat as enabled. This is a public web app with OAuth + user-supplied code execution, so several ASVS categories apply.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | yes (OAuth) | GitHub OAuth web flow; client_secret server-side only (CF Worker); never in client bundle |
| V3 Session Management | partial | gist token in sessionStorage (ephemeral per D-49-28); no server session |
| V4 Access Control | minimal | No protected resources of our own; gist scope is least-privilege (`scope=gist` only) |
| V5 Input Validation | yes | URL-fragment `#code=` is attacker-controllable → decode defensively (catch inflate errors); Flow source runs in the WASM sandbox, not eval |
| V6 Cryptography | no (use platform) | OAuth `state` = `crypto.getRandomValues` (never `Math.random`); no custom crypto |
| V7/V11 Errors/Headers | yes | CSP (D-49-38) restricts script-src; no .NET stack traces leak (HANDOFF: `RunError.message` is sanitized, T-48-15) |

### Known Threat Patterns for {SvelteKit static site + CF Worker OAuth + WASM playground}
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| client_secret leak | Information Disclosure | Secret only in CF Worker env (D-49-28); never in client/build output; scan bundle for it |
| OAuth CSRF (code injection) | Spoofing | `state` param generated with `crypto.getRandomValues`, validated server-side before exchange (CITED Willison) |
| Open redirect after OAuth | Tampering | Redirect only to same-origin `/playground`; never reflect a user-supplied `redirect` param |
| Malicious `#code=` fragment | DoS / Tampering | Defensive inflate (try/catch → "could not decode shared snippet"); cap decoded size before loading into editor |
| XSS via rendered wiki / shared source | Tampering | shiki outputs escaped HTML; never `{@html}` raw user source; render shared code as text in Monaco, not innerHTML |
| WASM tab hang (runaway script) | DoS (self-inflicted) | HANDOFF §4: 30s cap is best-effort/non-preemptive in single-threaded WASM — a runaway script hangs its own tab only; document, optionally warn |
| Token over-scope | Elevation | Request `scope=gist` only — least privilege |
| Supply-chain (npm) | Tampering | All deps verified on npm with healthy download counts + first-party repos (§Package Legitimacy Audit); pin versions; lockfile committed |

## Sources

### Primary (HIGH confidence)
- `.planning/phases/48-wasm-runtime-webaudio-backend/48-PHASE49-HANDOFF.md` — frozen runtime contract (§1-10)
- npm registry (`npm view`, `api.npmjs.org/downloads`) — all package versions + download counts (this session)
- https://svelte.dev/docs/kit/adapter-cloudflare — adapter config, `_headers` root placement, build output dir
- https://svelte.dev/docs/kit/creating-a-project + https://svelte.dev/docs/cli/sv-create — `sv create` scaffold
- https://shiki.style/guide/load-lang — custom TextMate grammar loading (fs-agnostic, pass object)
- https://mdsvex.pngwn.io/docs — mdsvex preprocess + highlight option
- https://til.simonwillison.net/cloudflare/workers-github-oauth — gist OAuth via CF Worker (the canonical pattern)
- https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Roles/slider_role — accessible knob (role=slider)
- https://tailwindcss.com/docs/guides/sveltekit — Tailwind v4 CSS-first + `@tailwindcss/vite`
- Local inspection: `vscode-extension/syntaxes/flow.tmLanguage.json` (scope `source.flow`), `wiki/*.md` (fences, `[[link]]` audit)

### Secondary (MEDIUM confidence)
- https://github.com/sveltejs/kit/discussions/3539 + https://github.com/choas/sveltekit-monaco-editor-example — Monaco/Vite SSR + `?worker` patterns
- https://vite.dev/guide/features — WASM/`?worker`/asset handling
- https://www.debugbear.com/docs/metrics/lighthouse-performance — Lighthouse scoring weights (TBT 30% + LCP 25%)
- https://dev.to/fedor-pasynkov/...tailwind-css-v4-in-sveltekit... — Tailwind v4 Vite plugin in SvelteKit

### Tertiary (LOW confidence — verify at planning)
- `@lhci/cli`, `@axe-core/playwright`, `vitest-browser-svelte` exact versions — confirm on npm at Plan 49-08
- CF Pages build-container .NET availability (drives Open Q2) — verify empirically at Plan 49-01

## Metadata

**Confidence breakdown:**
- Standard stack + versions: HIGH — every package verified live on npm with 2026 dates + repos + download counts
- Phase 48 runtime integration: HIGH — read directly from the frozen HANDOFF; corrections (path, `_headers` location) flagged
- Architecture patterns: HIGH — official SvelteKit/adapter-cloudflare/shiki/mdsvex docs + Willison OAuth TIL
- Monaco-in-Vite specifics: MEDIUM — well-documented pattern but version-sensitive; single-custom-language case is simpler than the usual multi-worker pain
- Skeuomorphic-component a11y: MEDIUM — WAI-ARIA slider role is authoritative; exact Knob interaction model is discretionary
- Lighthouse/perf: MEDIUM-HIGH — scoring weights verified; the lazy-load-keeps-WASM-off-LCP reasoning is sound but should be measured at Plan 49-01 baseline

**Research date:** 2026-06-05
**Valid until:** ~2026-07-05 (30 days; SvelteKit/Vite/Tailwind move fast — re-verify versions if planning slips past July)
