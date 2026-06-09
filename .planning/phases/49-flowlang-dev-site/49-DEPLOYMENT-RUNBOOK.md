# Phase 49 — Deployment Runbook (flowlang.dev site)

**Audience:** the composer (you). **Goal:** take the autonomously-built, fully-tested
`flow-site/` from this repo to a LIVE Cloudflare Pages URL with working "Save to gist", then
run the cross-browser UAT — in ONE sitting.

**Status going in:** the BUILD is complete and green in CI (vitest 70/70, playwright 275/275,
lhci ≥0.9 ×4, axe 0-critical). Nothing in this runbook is "fix the code" — it is **account +
dashboard setup** that cannot run in CI without a Cloudflare account or leaking a GitHub
secret. The three steps below clear the three OPEN human gates (live deploy, gist OAuth,
audible/visual UAT).

> **Do this as one pass.** Steps 1–2 produce the `<project>.pages.dev` URL. Step 3's OAuth
> callback **depends** on that URL — that is why deploy + OAuth + UAT are batched. The audible
> + visual UAT itself is scripted in
> `.planning/phases/49-flowlang-dev-site/49-HUMAN-UAT.md`; this runbook gets the site LIVE so
> every row in that script can run against the real deployment.

---

## 0. Prerequisites (one-time, on your dev machine)

- A **Cloudflare account** (free tier is fine — CF Pages + the one OAuth Worker route fit the
  free plan).
- A **GitHub account** (for the OAuth App + the live gist write).
- This repo pushed to GitHub (CF Pages deploys from a connected git repo).
- `wasm-tools` workload installed **only if** you ever need to regenerate the WASM bundle:
  `dotnet workload install wasm-tools`. You do **not** need it for a normal deploy — the
  Phase 48 AppBundle is already committed under `flow-site/static/wasm/` (see §4).

Confirm the build is green locally before deploying:

```bash
pnpm -C flow-site install
pnpm -C flow-site build     # → flow-site/.svelte-kit/cloudflare/   (exit 0)
```

---

## 1. Create the Cloudflare Pages project (REQ-SITE-IA-01 deploy + REQ-SITE-DEPLOY-01)

1. Cloudflare dashboard → **Workers & Pages** → **Create** → **Pages** → **Connect to Git**.
2. Pick this repo + the branch you ship from (the repo's default branch — currently `dev`).
3. **Project name** (D-49-36): try **`flow-music`** first. If it is taken, use
   **`flow-music-playground`**. (The public language `flowlang` already owns the obvious
   names, hence the `flow-music*` choice.) Your live URL becomes
   **`https://<project>.pages.dev`** — note it down; you need it in §3.
4. **Build settings:**
   | Setting | Value |
   |---------|-------|
   | Framework preset | None / SvelteKit (either works — the explicit command below is authoritative) |
   | **Build command** | `pnpm -C flow-site build` |
   | **Build output directory** | `flow-site/.svelte-kit/cloudflare` |
   | Root directory | repo root (leave default — the `-C flow-site` handles the subdir) |
5. **Save and Deploy.** The first build runs `prebuild` (`sync-wiki.sh` + `optimize-textures.mjs`)
   then `vite build`. It is pure-Node — Cloudflare never runs `dotnet` (the WASM runtime is
   pre-committed, see §4).
6. When the deploy succeeds, visit **`https://<project>.pages.dev/`**. Confirm:
   - the skeuomorphic chrome renders (wood / brushed-metal nav, brass wordmark);
   - DevTools → Network → the document response carries the **CSP** + **Permissions-Policy**
     headers, and `/playground` additionally carries **COOP/COEP** (the `_headers` file, §5);
   - `/docs`, `/playground`, `/showcase` all load.

> If the wiki clone fails on the first build (no `WIKI_REPO_URL` yet — §2), the in-repo `wiki/`
> seed fallback still populates docs, so the build will not break. Set `WIKI_REPO_URL` in §2 to
> get live wiki updates.

---

## 2. Environment variables (Cloudflare Pages → Settings → Environment variables)

Set these on the **Production** environment (add them to **Preview** too if you want preview
deploys to work identically). The "Encrypt" toggle matters for the secret.

| Variable | Value | Encrypted? | Why |
|----------|-------|-----------|-----|
| `WIKI_REPO_URL` | `https://github.com/<you>/flow-sharp.wiki.git` | No (public URL) | `sync-wiki.sh` clones this at build time (D-49-25). Omit → the in-repo `wiki/` seed is used instead. |
| `GITHUB_CLIENT_ID` | (from the OAuth App in §3) | No (it ships in the authorize URL anyway) | the gist OAuth client id (D-49-28). |
| `GITHUB_CLIENT_SECRET` | (from the OAuth App in §3) | **YES — Encrypt** | the gist OAuth secret. **Never committed** (T-49-SECRET). The CF Worker reads it server-side; it never enters the client bundle. |

After setting env vars you must **re-deploy** (CF Pages → Deployments → Retry / or push a
commit) so the build + worker pick them up.

> The repo's `flow-site/wrangler.toml` documents these names and intentionally ships
> `GITHUB_CLIENT_ID = "set-in-cloudflare-dashboard"` as a placeholder so local builds do not
> break; the real values are dashboard-managed. `GITHUB_CLIENT_SECRET` is deliberately ABSENT
> from `wrangler.toml` — it is a dashboard-only encrypted secret.

---

## 3. GitHub OAuth App for "Save to gist" (REQ-SITE-SHARE-02)

1. GitHub → **Settings → Developer settings → OAuth Apps → New OAuth App**.
2. Fill in:
   | Field | Value |
   |-------|-------|
   | Application name | `Flow Playground` (anything) |
   | Homepage URL | `https://<project>.pages.dev` |
   | **Authorization callback URL** | `https://<project>.pages.dev/api/auth/github` |
3. **Register application.** Copy the **Client ID** → set it as `GITHUB_CLIENT_ID` (§2).
   **Generate a new client secret** → set it as the encrypted `GITHUB_CLIENT_SECRET` (§2).
4. The worker requests **`scope=gist`** only (least privilege) and validates a
   `crypto.getRandomValues` `state` param (CSRF) before exchanging the code server-side. It
   redirects only to the same-origin `/playground#token=…` — the token rides the URL fragment
   (never the server/logs), and the client caches it in `sessionStorage`.
5. **Re-deploy** so the worker sees the new env vars.
6. **Live round-trip test** (this is the gate): on `https://<project>.pages.dev/playground`,
   click **Save to gist**, complete the GitHub OAuth consent, and confirm a real gist appears
   at `gist.github.com/<your-user>/<id>` and the returned link reopens the snippet.

> The OAuth callback path is **fixed** at `/api/auth/github` (the SvelteKit route
> `src/routes/api/auth/github/+server.ts` → `workers/gist-auth.ts`). Do not invent a different
> callback — it must match what the worker hard-codes.

---

## 4. The committed-AppBundle deploy model (why CF never runs `dotnet`)

The Phase 48 WASM runtime (`flow-runtime.js` + the `_framework/` AppBundle) is **committed
verbatim** under `flow-site/static/wasm/` (RESEARCH Open Q2). The Cloudflare Pages build is
**pure-Node** — it runs `pnpm build` and copies `static/` into the output; it never invokes the
.NET SDK. This keeps the CF build environment simple and fast.

**To refresh the runtime** (only after a future WASM-runtime phase changes it):

```bash
# On your dev machine (needs the wasm-tools workload):
bash flow-site/scripts/sync-runtime.sh   # dotnet publish ../flow-lang -p:FlowTarget=Web -c Release
                                         #   → layout-preserving copy into flow-site/static/wasm/
git add flow-site/static/wasm
git commit -m "chore(flow-site): refresh committed WASM runtime"
git push                                 # CF Pages auto-redeploys on push
```

The runtime is **frozen for v1.5** (HANDOFF §8 — never hand-edit `flow-runtime.js`). The
playground dynamically imports it in `onMount` (D-49-34 lazy-load — Home/Docs never fetch it).

---

## 5. The `_headers` model (`flow-site/_headers`, project root)

`adapter-cloudflare` copies `_headers` from the project root into the build output (it must be
at the root, NOT `static/` — a `static/_headers` is served as a literal asset and silently
ignored, RESEARCH A4). It sets:

- **Global (`/*`)** — always on:
  - `Content-Security-Policy` — `script-src 'self' 'wasm-unsafe-eval'` (the Mono-WASM runtime
    needs `wasm-unsafe-eval`; Monaco is self-hosted so NO CDN is allowed) + a `sha256-…` hash
    for the one inline early-theme script + `connect-src 'self' https://api.github.com
    https://github.com` (gist + OAuth only).
  - `Permissions-Policy: microphone=(), camera=(), geolocation=()` — explicit deny (Flow's web
    target needs none of these).
  - `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
    `frame-ancestors 'none'`.
- **Scoped (`/playground/*`)** — the v1.6 AudioWorklet + SharedArrayBuffer foundation:
  - `Cross-Origin-Opener-Policy: same-origin`
  - `Cross-Origin-Embedder-Policy: require-corp`

> v1's WASM is single-threaded and needs no cross-origin isolation; the scoped COOP/COEP are a
> forward-looking foundation. If they ever cause subresource friction they can be removed with
> no security loss (HANDOFF §3) — CSP + Permissions-Policy stay regardless.
>
> **If you edit the inline theme script in `src/app.html`,** recompute its CSP hash:
> ```bash
> node -e 'const fs=require("fs"),c=require("crypto");const b=fs.readFileSync("flow-site/src/app.html","utf8").match(/<script>([\s\S]*?)<\/script>/)[1];console.log("sha256-"+c.createHash("sha256").update(b).digest("base64"))'
> ```
> and paste it into `_headers` `script-src`.

---

## 6. Custom domain (deferred to post-v1.5 — D-49-37)

v1.5 ships on the free `<project>.pages.dev` URL. To attach a real domain later (e.g.
`flowmusic.dev` / `flow-music.dev` / `composeflow.dev` — `flowlang.dev` is taken by an unrelated
language):

1. Register the domain (any registrar).
2. CF dashboard → your Pages project → **Custom domains** → **Set up a domain** → enter the
   domain.
3. Add the **CNAME** record Cloudflare shows you:
   `<your-domain>  CNAME  <project>.pages.dev` (or use Cloudflare as your DNS and it wires the
   record automatically). Cloudflare provisions the TLS cert.
4. Update the GitHub OAuth App's Homepage + **Authorization callback URL** to the new domain
   (`https://<your-domain>/api/auth/github`) — or add a second OAuth App if you want both URLs
   live during the transition.

No code change is needed for a custom domain — it is dashboard + DNS only.

---

## 7. Wiki re-sync model

Docs are synced at **build time** from the GitHub wiki (`sync-wiki.sh`, D-49-25), not at
runtime. The update flow today:

```
You push a doc edit to the flow-sharp WIKI on GitHub
        │
        ▼
You push any commit to the flow-sharp default branch (or hit "Retry deployment" in CF)
        │
        ▼
CF Pages rebuilds → sync-wiki.sh re-clones the wiki → /docs re-rendered → redeployed
```

So a wiki edit goes live on the **next** flow-sharp build (or a manual CF "Retry deployment").
**v1.6 backlog:** a GitHub Action on the wiki repo that triggers a CF Pages deploy hook so wiki
pushes auto-rebuild without a flow-sharp push.

---

## 8. After deploy — run the UAT

With the site live and gist working, run the consolidated cross-browser UAT:

**`.planning/phases/49-flowlang-dev-site/49-HUMAN-UAT.md`**

It folds in this runbook's deploy (prereq A) + OAuth (prereq B) gates so you do deploy + OAuth +
**audible** audio (Chrome / Firefox / Safari / mobile) + **skeuomorphic** visual fidelity +
**screen-reader** smoke as ONE pass. Record each row PASS / DEFER / SKIP and sign off.

> Phase 49 flips to **SHIPPED** only after that UAT sign-off. Until then its honest status is
> **"execution complete — pending HUMAN-UAT + live deploy"**.

---

## Quick reference card

| Thing | Value |
|-------|-------|
| CF Pages project name | `flow-music` else `flow-music-playground` (D-49-36) |
| Build command | `pnpm -C flow-site build` |
| Output directory | `flow-site/.svelte-kit/cloudflare` |
| Live URL | `https://<project>.pages.dev` |
| Env: wiki | `WIKI_REPO_URL` (public) |
| Env: OAuth | `GITHUB_CLIENT_ID` (public) + `GITHUB_CLIENT_SECRET` (encrypted secret) |
| OAuth callback | `https://<project>.pages.dev/api/auth/github` (scope `gist`) |
| Refresh WASM | `bash flow-site/scripts/sync-runtime.sh` → commit |
| Headers | `flow-site/_headers` (project root; CSP + Permissions-Policy global, COOP/COEP on `/playground/*`) |
| Custom domain | post-v1.5, CNAME `<domain> → <project>.pages.dev` (D-49-37) |
| UAT script | `49-HUMAN-UAT.md` |
