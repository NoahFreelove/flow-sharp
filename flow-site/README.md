# flow-site — the website

The marketing + docs + interactive playground site for the [Flow](../) music language.
Greenfield **SvelteKit 2 / Svelte 5 / TypeScript / Tailwind v4** project, deployed to
**Cloudflare Pages** via `adapter-cloudflare`. The `/playground` tab runs Flow code in the
browser by consuming the **frozen** Phase 48 WASM runtime (`flow-runtime.js`).

> **Greenfield TypeScript — the repo-root C# conventions do NOT apply inside `flow-site/`.**
> This is a web project: TS/Svelte conventions, pnpm, Vite, ESM. The `CLAUDE.md` C# rules
> (.NET 10, file-scoped namespaces, record AST nodes, etc.) govern `flow-lang/` +
> `flow-interpreter/` + `flow-cli/`, not this directory. See the repo-root `CLAUDE.md`
> `## flowlang.dev Site` section.

## Routes

| Route | Render | What |
|-------|--------|------|
| `/` | prerendered | Home — marketing landing (hero + value-prop trio + audio embeds + code-first + CTAs) |
| `/docs` + `/docs/[slug]` | prerendered | 26 wiki pages, build-time synced + shiki-highlighted |
| `/playground` | client-only SPA | Monaco editor + Phase 48 WASM runtime + audio out + share |
| `/showcase` + `/showcase/[slug]` | prerendered | 10 curated pieces (gesture-gated audio + source + notes) |
| `/design` | prerendered | skeuo component showcase (visual-regression baseline source) |
| `/api/auth/github` | server fn | the ONE dynamic route — GitHub gist OAuth code exchange |

## Prerequisites

```sh
corepack enable          # pnpm via corepack (pinned)
pnpm -C flow-site install
```

## Develop

```sh
pnpm -C flow-site dev            # vite dev (predev runs sync-wiki.sh to populate src/docs/wiki/)
pnpm -C flow-site dev -- --open  # …and open a browser tab
```

## Build

```sh
pnpm -C flow-site build          # → flow-site/.svelte-kit/cloudflare/  (the CF Pages output dir)
pnpm -C flow-site preview        # local preview of the built output
```

`prebuild` runs `sync-wiki.sh` (clone-or-seed the wiki) + `optimize-textures.mjs` (Sharp
wood-grain → AVIF/WebP/PNG).

## Test

```sh
pnpm -C flow-site test           # vitest run — unit/component (docs transform, share encode, OAuth worker, skeuo a11y)
pnpm -C flow-site test:e2e       # playwright — desktop / mobile-375 / mobile-narrow-320 projects
pnpm -C flow-site lh             # lhci autorun — Lighthouse ≥90 ×4 axes, production-accurate (brotli) server
pnpm -C flow-site check          # svelte-check (0 errors gate)
```

The full gate (mirrors CI):

```sh
pnpm -C flow-site test && pnpm -C flow-site test:e2e && pnpm -C flow-site lh
```

## Scripts (`flow-site/scripts/`)

| Script | Purpose |
|--------|---------|
| `sync-wiki.sh` | `git clone --depth 1 "$WIKI_REPO_URL"` (or in-repo `wiki/` seed) → `src/docs/wiki/`. Runs in `predev`/`prebuild`. Fails loudly (`set -euo pipefail` + non-empty assert). |
| `sync-runtime.sh` | Regenerate the committed Phase 48 WASM AppBundle: `dotnet publish ../flow-lang -p:FlowTarget=Web -c Release` → layout-preserving copy into `static/wasm/`. **Run on the dev machine, then commit** (the CF Pages build is pure-Node and never runs `dotnet`). |
| `sync-grammar.mjs` | Copy the Phase 17 Flow TextMate grammar (`vscode-extension/syntaxes/flow.tmLanguage.json`) into `src/lib/docs/` so the flow-site-only build never reaches the sibling project. |
| `optimize-textures.mjs` | Deterministic Sharp wood-grain → AVIF/WebP/PNG (byte-stable for visual baselines). |
| `lh-serve.mjs` | CF-Pages-accurate static server (brotli + cache + SPA fallback) used by `lhci`. |

## The committed WASM runtime (`static/wasm/`)

The Phase 48 `flow-runtime.js` ES module + its `_framework/` AppBundle are **committed
verbatim** under `static/wasm/` (RESEARCH Open Q2) so the Cloudflare Pages build stays
pure-Node — CF never runs `dotnet`. The runtime is **frozen** (HANDOFF §8 — never edited);
`/playground` dynamically imports it in `onMount`. To refresh it after a future WASM-runtime
phase, run `scripts/sync-runtime.sh` on a dev machine with the `wasm-tools` workload and commit
the regenerated bundle. See `../.planning/phases/48-wasm-runtime-webaudio-backend/48-PHASE49-HANDOFF.md`.

## Deploy

The composer deploys to Cloudflare Pages from their own CF account — full step-by-step
(project creation, env vars, GitHub OAuth App, `_headers`, custom-domain CNAME) is in
**`../.planning/phases/49-flowlang-dev-site/49-DEPLOYMENT-RUNBOOK.md`**. Quick reference:

| Setting | Value |
|---------|-------|
| Project name | `flow-music` (else `flow-music-playground`) — D-49-36 |
| Build command | `pnpm -C flow-site build` |
| Output directory | `flow-site/.svelte-kit/cloudflare` |
| Env vars | `WIKI_REPO_URL` (wiki sync), `GITHUB_CLIENT_ID` (public), `GITHUB_CLIENT_SECRET` (encrypted secret) |
| OAuth callback | `https://<project>.pages.dev/api/auth/github` (scope `gist`) |

`_headers` (CSP + Permissions-Policy + scoped COOP/COEP) lives at the project root and is
copied into the build output by `adapter-cloudflare`.
