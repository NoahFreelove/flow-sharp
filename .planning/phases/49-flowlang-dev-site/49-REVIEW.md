---
phase: 49-flowlang-dev-site
reviewed: 2026-06-05T00:00:00Z
depth: standard
files_reviewed: 84
files_reviewed_list:
  - flow-site/docs-categories.json
  - flow-site/.gitignore
  - flow-site/lighthouserc.cjs
  - flow-site/package.json
  - flow-site/playwright.config.ts
  - flow-site/README.md
  - flow-site/scripts/lh-serve.mjs
  - flow-site/scripts/optimize-textures.mjs
  - flow-site/scripts/sync-grammar.mjs
  - flow-site/scripts/sync-runtime.sh
  - flow-site/scripts/sync-wiki.sh
  - flow-site/src/app.css
  - flow-site/src/app.d.ts
  - flow-site/src/app.html
  - flow-site/src/lib/components/skeuo/Button.svelte
  - flow-site/src/lib/components/skeuo/Knob.svelte
  - flow-site/src/lib/components/skeuo/LedIndicator.svelte
  - flow-site/src/lib/components/skeuo/MetalRail.svelte
  - flow-site/src/lib/components/skeuo/Panel.svelte
  - flow-site/src/lib/components/skeuo/Slider.svelte
  - flow-site/src/lib/components/skeuo/Tabs.svelte
  - flow-site/src/lib/components/skeuo/Toggle.svelte
  - flow-site/src/lib/components/skeuo/skeuo.test.ts
  - flow-site/src/lib/design/surfaces.css
  - flow-site/src/lib/design/theme.ts
  - flow-site/src/lib/design/tokens.css
  - flow-site/src/lib/docs/categories.ts
  - flow-site/src/lib/docs/__fixtures__/synthetic-wiki-link.md
  - flow-site/src/lib/docs/flow.tmLanguage.json
  - flow-site/src/lib/docs/highlight.js
  - flow-site/src/lib/docs/remark-wiki-links.js
  - flow-site/src/lib/docs/shiki.ts
  - flow-site/src/lib/docs/slug.js
  - flow-site/src/lib/docs/transform.js
  - flow-site/src/lib/docs/transform.test.ts
  - flow-site/src/lib/home/AudioEmbed.svelte
  - flow-site/src/lib/home/CodeCard.svelte
  - flow-site/src/lib/home/examples.ts
  - flow-site/src/lib/monaco/flow-monarch.ts
  - flow-site/src/lib/monaco/index.ts
  - flow-site/src/lib/playground/download.ts
  - flow-site/src/lib/playground/share-controls.svelte.ts
  - flow-site/src/lib/playground/snippets.ts
  - flow-site/src/lib/playground/state.svelte.ts
  - flow-site/src/lib/runtime.ts
  - flow-site/src/lib/share/encode-node.js
  - flow-site/src/lib/share/encode-node.test.ts
  - flow-site/src/lib/share/encode.test.ts
  - flow-site/src/lib/share/encode.ts
  - flow-site/src/lib/share/gist.ts
  - flow-site/src/lib/showcase/PieceCard.svelte
  - flow-site/src/lib/showcase/pieces.ts
  - flow-site/src/lib/showcase/sources.ts
  - flow-site/src/routes/+layout.svelte
  - flow-site/src/routes/+page.svelte
  - flow-site/src/routes/+page.ts
  - flow-site/src/routes/api/auth/github/+server.ts
  - flow-site/src/routes/design/+page.svelte
  - flow-site/src/routes/docs/+page.svelte
  - flow-site/src/routes/docs/+page.ts
  - flow-site/src/routes/docs/[slug]/+page.svelte
  - flow-site/src/routes/docs/[slug]/+page.ts
  - flow-site/src/routes/playground/+page.svelte
  - flow-site/src/routes/showcase/+page.svelte
  - flow-site/src/routes/showcase/+page.ts
  - flow-site/src/routes/showcase/[slug]/+page.svelte
  - flow-site/src/routes/showcase/[slug]/+page.ts
  - flow-site/svelte.config.js
  - flow-site/tests/a11y.spec.ts
  - flow-site/tests/docs-render.spec.ts
  - flow-site/tests/docs-toc.spec.ts
  - flow-site/tests/nav.spec.ts
  - flow-site/tests/playground-audio.spec.ts
  - flow-site/tests/playground-export.spec.ts
  - flow-site/tests/playground-mobile.spec.ts
  - flow-site/tests/playground-run.spec.ts
  - flow-site/tests/playground-share.spec.ts
  - flow-site/tests/render-strategy.spec.ts
  - flow-site/tests/responsive.spec.ts
  - flow-site/tests/showcase.spec.ts
  - flow-site/tests/visual.spec.ts
  - flow-site/tests/wasm-boot.spec.ts
  - flow-site/vite.config.ts
  - flow-site/vitest.config.ts
  - flow-site/workers/gist-auth.test.ts
  - flow-site/workers/gist-auth.ts
  - flow-site/wrangler.toml
findings:
  critical: 3
  warning: 7
  info: 6
  total: 16
status: issues_found
---

# Phase 49: Code Review Report

**Reviewed:** 2026-06-05
**Depth:** standard
**Files Reviewed:** 84
**Status:** issues_found

## Summary

Reviewed the greenfield `flow-site/` SvelteKit 2 / Svelte 5 / TS / Tailwind v4 project at
standard depth, prioritizing the security-critical surfaces: the GitHub OAuth gist worker, the
OAuth callback route, the `#code=` share encode/decode (decompression-bomb guard), gist token
handling, the frozen-WASM runtime consumption boundary, and the markdown→HTML doc pipeline
(`{@html}` XSS surface).

Overall the threat-modelling is unusually careful — `{@html}` is correctly confined to
first-party, build-time, shiki-escaped HTML; the share payload is loaded into Monaco as plain
text; the OAuth secret is genuinely confined to the server leg; CSP is tight. The headline
defect is that the **decompression-bomb guard does not actually bound allocation** — it relies
on a misunderstanding of how `fflate`'s `Inflate.push(buf, true)` chunks output, so a few-KB
fragment can still force a multi-megabyte allocation before the guard ever fires. Two further
Critical issues concern an OAuth open-redirect/token-theft vector via the unvalidated `Host`
header and an OAuth `state` cookie that is overwritten/never cleared correctly across concurrent
flows. The Warnings cover an unguarded WASM-runtime gesture race, cross-instance theme-toggle
desync, a `loadSnippet` that does not update Monaco, and several robustness gaps.

## Critical Issues

### CR-01: Decompression-bomb guard does not bound allocation — full inflated payload is materialised before the size check

**File:** `flow-site/src/lib/share/encode.ts:96-139`
**Issue:** The guard is documented (lines 11-13, 56-70, 96-112) as aborting "BEFORE the full
output is materialised … we never allocate past it." That is false. The code pushes the entire
compressed buffer in a single call: `inflater.push(compressed, true)`. With `fflate`'s `Inflate`,
a single final push inflates the **whole** stream and delivers it to the callback — empirically as
**one chunk equal to the full inflated size**. I verified this against the installed `fflate@0.8.3`:
an 8 MB zero-buffer deflates to ~8 KB and arrives in the callback as a single 8,388,608-byte chunk;
the `total > MAX_DECODED_BYTES` check only runs *after* that 8 MB allocation has already happened.
The cap therefore prevents *retention* past 256 KB but does nothing to prevent the *peak
allocation* a malicious `#code=` link can force (bounded only by deflate's ~1032:1 ratio against
the fragment length). A crafted multi-MB-or-GB fragment can OOM the tab — exactly the DoS the guard
claims to stop. Note the unit test (`encode.test.ts:58-71`) only asserts that decode *throws*, not
that allocation was bounded, so it passes against the broken guard.
**Fix:** Drive `fflate` with bounded *input* slices and abort the loop the instant the running
total crosses the cap, so no single inflate call can allocate the whole payload:
```ts
import { Inflate } from 'fflate';
// ...
const chunks: Uint8Array[] = [];
let total = 0;
let bombed = false;
let streamError: unknown = null;
const inflater = new Inflate((chunk) => {
	if (bombed) return;
	if (total + chunk.length > MAX_DECODED_BYTES) { bombed = true; return; }
	total += chunk.length;
	chunks.push(chunk);
});
const INPUT_SLICE = 4096; // small input slices → small output chunks for the guard to gate
try {
	for (let i = 0; i < compressed.length && !bombed; i += INPUT_SLICE) {
		const final = i + INPUT_SLICE >= compressed.length;
		inflater.push(compressed.subarray(i, i + INPUT_SLICE), final);
	}
} catch (e) {
	streamError = e;
}
```
This still cannot make a *single* highly-compressible 4 KB input slice expand to less than its
~4 MB worst case in one callback — so additionally cap `frag.length` itself (reject any fragment
longer than e.g. `MAX_DECODED_BYTES`/some-safe-ratio before inflating at all). The combination of a
small input-slice loop plus an input-length cap is what actually bounds peak allocation.

### CR-02: OAuth redirect_uri / token-return origin is derived from the attacker-controllable request URL (Host header) — token-theft / open-redirect vector

**File:** `flow-site/workers/gist-auth.ts:45-47,90-94`
**Issue:** Both the `redirect_uri` sent to GitHub (line 47: `${url.origin}/api/auth/github`) and
the final token-bearing redirect (line 93: `${url.origin}/playground#token=...`) are built from
`new URL(request.url).origin`, i.e. from the inbound request's Host. On Cloudflare Pages the Host
is request-controlled and the OAuth App also accepts any host that matches its configured callback
pattern only at GitHub's side — the worker itself imposes no allow-list. The file header claims
(lines 9-10) the redirect target is "HARD-CODED same-origin" with an "open-redirect guard", but it
is *not* hard-coded — it is reflected from `url.origin`. If the site is ever reachable under more
than one hostname (preview deploys `*.pages.dev`, a custom domain, or a proxied host), an attacker
who can influence the Host on the callback leg can cause the freshly-minted **access token** to be
appended to a `#token=` fragment on a host they observe. The "hard-coded `/playground`" claim only
covers the *path*, not the *origin* — the security-relevant part (where the token lands) is dynamic.
**Fix:** Pin the origin to a server-known constant rather than reflecting the request Host:
```ts
// Read from env (CF Pages var), NOT from request.url.
const SITE_ORIGIN = env.SITE_ORIGIN; // e.g. https://flow-music.pages.dev
const redirectUri = `${SITE_ORIGIN}/api/auth/github`;
// ...
location: `${SITE_ORIGIN}/playground#token=${encodeURIComponent(data.access_token)}`,
```
Add `SITE_ORIGIN` to `GistAuthEnv`, `app.d.ts`, and `wrangler.toml` `[vars]`, and reject the
callback if `url.origin !== SITE_ORIGIN`. Update the test (`gist-auth.test.ts`) to assert the
redirect origin equals the configured constant, not `ORIGIN` derived from the request.

### CR-03: OAuth `state` cookie is not validated as single-use and is left set on the authorize leg without clearing prior state — replay / fixation surface

**File:** `flow-site/workers/gist-auth.ts:49-70`
**Issue:** Two related correctness/security gaps in the CSRF-state handling:
(1) On the callback leg the `state` cookie is compared (line 68) but the *match* path (lines 72-86)
proceeds to the exchange and only clears the cookie on the **success** redirect (line 94). If the
token exchange fails (line 84-85 returns 400) the `state` cookie is left intact, so the same
captured `code`+`state` pair can be retried — the state is not consumed atomically on validation.
(2) The authorize leg (lines 50-62) unconditionally overwrites the cookie. Because validation is a
plain string compare against whatever cookie currently rides the request, an attacker who can set a
cookie on the victim's browser (e.g. via a sibling subdomain on `*.pages.dev`, which shares the
parent domain for cookie-setting) can fixate a known `state` and then complete a login-CSRF that
binds the victim's session to an attacker-chosen token flow. The `state` is also not bound to any
per-session nonce beyond its own value.
**Fix:** Clear/rotate the state cookie on *every* terminal callback outcome (success **and**
failure), and treat the cookie as strictly single-use:
```ts
const clearState = `${STATE_COOKIE}=; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=0`;
// ...validation fail OR exchange fail: return Response(..., { headers: { 'set-cookie': clearState } })
```
Set the cookie with `__Host-` prefix (`__Host-flow_oauth_state`, which forbids `Domain=` and
requires `Path=/; Secure`) so a sibling subdomain cannot write it, closing the fixation vector.
Update `STATE_COOKIE` and `cookieState()`/`stateFromSetCookie()` accordingly.

## Warnings

### WR-01: WASM-runtime gesture race — Run is enabled, then `onRun` can dereference a not-yet-ready runtime via the deep-link auto-run path

**File:** `flow-site/src/routes/playground/+page.svelte:99-116,193-202`
**Issue:** The deep-link auto-run (lines 113-116) calls `await onRun()` immediately after
`runtimeReady = true`. `onRun` (line 194) guards `if (!runtime || !editor) return`, which is fine,
but the auto-run path runs `resumeAudio()` + `run()` *outside* a real user gesture frame for the
`?run=1` query-string case (line 148: `queryAutoRun`). A bare `?run=1` arrival (no `#code=`, e.g.
a hand-typed or copied URL) sets `autoRun=true` with no preceding click, so `resumeAudio()` is
invoked without a user gesture and the browser autoplay policy will reject it — audio silently
fails and `audioState` reads `suspended`, contradicting the D-48-09 "must be called from a
user-gesture frame" contract the code comments rely on. Only the `data-run="1"` *click* path is a
real gesture; the query-string path is not.
**Fix:** Only honor auto-run when arrival came through an in-app gesture link. Drop the
`queryAutoRun` source (or gate it behind `document.referrer` being same-origin AND a fresh
`history` navigation), and rely solely on the `#code=…&run=1` fragment that the CodeCard/showcase
anchors produce on actual clicks. At minimum, do not call `resumeAudio()` on a non-gesture auto-run
— run silently and let the first real Run click resume audio.

### WR-02: `loadSnippet` updates editor state but the Monaco editor is only set imperatively — console is not cleared, and active-snippet highlight can desync

**File:** `flow-site/src/lib/playground/state.svelte.ts:52-57` and `flow-site/src/routes/playground/+page.svelte:208-211`
**Issue:** `PlaygroundState.loadSnippet` sets `this.editorValue` and `this.activeSnippetId`, then
the page's `onLoadSnippet` calls `editor?.setValue(pg.editorValue)`. But `loadSnippet` deliberately
does **not** clear the console (`stdout`/`stderr`/`errors`/`midi`) — so after running snippet A then
loading snippet B, the right-rail console still shows A's output and the MIDI-download button stays
visible for a buffer that no longer corresponds to the loaded source. A user who clicks "Download
MIDI" after switching snippets downloads stale bytes. This is a correctness/UX defect, not just
style.
**Fix:** Clear the run outputs when switching snippets (mirror `newBlank`'s reset of
`stdout`/`stderr`/`errors`/`midi`/`runStatus`), or document explicitly that switching snippets
preserves the last run and disable the download button when `activeSnippetId` changed since the run.

### WR-03: Two `Toggle theme` instances bind independent `checked` state — flipping one does not update the other, and the `$effect` can clobber a user toggle

**File:** `flow-site/src/lib/components/skeuo/Toggle.svelte:29-41` (instances at `+layout.svelte:70`, `playground/+page.svelte:324`)
**Issue:** The theme Toggle derives its initial `checked` from `getInitialTheme()` inside a
`$effect` (lines 30-32). There are two live theme toggles (site chrome + playground rail). When the
user flips one, `setTheme` writes localStorage and `[data-theme]`, but the *other* toggle's
`checked` is local component state and is never notified — the two switches visibly disagree until
a reload. Worse, the `$effect` reads `getInitialTheme()` (localStorage) which is not a reactive
dependency, so on Svelte 5 it runs once on mount; but any future reactive read added inside that
effect would re-run it and snap `checked` back to the stored value, silently undoing an in-flight
toggle. The pattern (deriving bindable state from a side-effecting read in `$effect`) is fragile.
**Fix:** Make theme a single source of truth — a shared rune/store updated by `setTheme` and read
by every toggle — instead of per-instance `$state`. Replace the `$effect`-initialised `checked`
with `$derived(themeStore === 'dark')` so all toggles reflect the same value, and have `flip()`
update the store. This also removes the clobber hazard.

### WR-04: `+server.ts` swallows a missing OAuth env into empty-string credentials — silently attempts a doomed token exchange instead of failing fast

**File:** `flow-site/src/routes/api/auth/github/+server.ts:17-22`
**Issue:** When `platform?.env` is undefined or the secret is unset, the handler passes
`GITHUB_CLIENT_ID: '' , GITHUB_CLIENT_SECRET: ''` to the worker. On the callback leg the worker
will then POST to GitHub's token endpoint with empty credentials (after the state check passes),
get a non-token response, and return a generic 400 "OAuth exchange failed." This masks a deployment
misconfiguration (forgotten dashboard secret) as a user-facing failure, and it issues an
unnecessary outbound request with blank secrets. The comment (lines 14-16) treats this as expected
in dev, but production should not silently degrade.
**Fix:** Fail fast on the callback leg when credentials are absent:
```ts
const env = platform?.env;
if (!env?.GITHUB_CLIENT_ID || !env?.GITHUB_CLIENT_SECRET) {
	// Authorize leg can still mint state; but a callback with no secret is a server misconfig.
	if (new URL(request.url).searchParams.get('code')) {
		return new Response('Gist sign-in is not configured.', { status: 503 });
	}
}
```
Or have `handleGistAuth` itself return 503 when `code` is present but the secret is empty.

### WR-05: `errorHeading` interpolates runtime-supplied error text — confirm escaping holds (it does today) and keep it that way

**File:** `flow-site/src/routes/playground/+page.svelte:232-238,414-427`
**Issue:** `errorHeading(err)` returns `✕ ${err.kind}: ${err.message}` and is rendered via
`{errorHeading(err)}` (escaped) — correct today. However `err.message`, `err.sourceSnippet`, and
`err.kind` come from the WASM runtime's `RunResult.errors[]`, which is ultimately derived from
*user-supplied Flow source* (e.g. a parse error echoing a token from the source). These are rendered
in `<pre>` blocks (lines 422, 424) and headings. All current sinks use Svelte curly interpolation
(auto-escaped), so there is no XSS today — but the data is untrusted, and any future change to a
`{@html}` sink here (or a `set:html`-style helper) would be an injection. This is flagged as a
standing hazard to keep on the radar, not a present vulnerability.
**Fix:** Add a code comment marking `err.*` as untrusted-must-stay-escaped, and add a test asserting
that a source whose parse error contains `<script>` renders escaped in the error box. No code change
required today.

### WR-06: `captureOAuthToken` accepts any `#token=` fragment as a gist token with no shape validation

**File:** `flow-site/src/lib/playground/share-controls.svelte.ts:105-119`
**Issue:** On mount the playground reads `window.location.hash` and, if it starts with `#token=`,
`decodeURIComponent`s the remainder and stores it verbatim in sessionStorage as the gist token. Any
page that can navigate the playground to `…/playground#token=ATTACKER` (a link, a redirect, an
embedded iframe navigation) seeds the victim's sessionStorage with an attacker-chosen string. While
the token is only ever sent to `api.github.com` (so an arbitrary string just yields a 401), pairing
this with CR-02 (token landing on a request-controlled origin) means the fragment-capture path is
the second half of a token-injection chain. There is no check that the fragment arrived from the
worker's redirect.
**Fix:** Validate the token shape (GitHub tokens have known prefixes, e.g. `gho_`/`ghu_`) before
storing, and/or require a one-time nonce the worker also returns so the playground can confirm the
`#token=` came from its own OAuth round-trip rather than an arbitrary navigation. At minimum,
`history.replaceState` to strip the fragment *before* doing anything else (currently done in
`+page.svelte:73-75` only when `captureOAuthToken()` returns true, which is fine, but the token is
already stored by then).

### WR-07: `offerDownload` revokes the object URL on a 0ms timeout — fragile in some browsers; no cleanup if `a.click()` throws synchronously

**File:** `flow-site/src/lib/playground/download.ts:30-38`
**Issue:** The download helper appends an anchor, clicks it, removes it, then revokes the blob URL
via `setTimeout(…, 0)`. If `a.click()` throws (e.g. a sandboxed context blocking programmatic
downloads), the `finally` still schedules the revoke, but the anchor may already be removed and the
click never dispatched — the download silently no-ops with no surfaced error. More subtly, a 0ms
timeout can fire before the download dialog has read the URL in some engines under load. This is a
robustness issue for the MIDI-download path.
**Fix:** Wrap the click in try/catch and surface a failure to the caller (so the playground can
toast "couldn't start download"); consider revoking on the anchor's `click` completion or a longer
delay, and guard `document.body` existence. Low severity but worth hardening since it is the only
export affordance.

## Info

### IN-01: `hasGistToken` is dead — declared, `void`-ed, never wired to UI

**File:** `flow-site/src/routes/playground/+page.svelte:171-174`
**Issue:** `function hasGistToken()` is defined and immediately discarded with `void hasGistToken;`.
The comment says it "drives the Save button's first-click behavior," but the Save button (lines
296-301) is unconditionally enabled and `saveToGist` decides the OAuth-vs-create branch at click
time. The helper is dead code retained only to satisfy an unused-symbol lint.
**Fix:** Remove `hasGistToken` and the `void` line, or actually use it to label the Save button
("Save to gist" vs "Sign in & save") for clearer first-click affordance.

### IN-02: `sources.ts` mislabels `.flow` comment syntax in several inlined showcase sources (`Note:` vs `//`)

**File:** `flow-site/src/lib/showcase/sources.ts:13,19,25` (GRANULAR_SOURCE, STRETCH_SOURCE, SCALA_INTRO_SOURCE)
**Issue:** Several inlined sources use `Note:` as a line prefix where the surrounding examples use
`//` or `;` comments (compare MARKOV_JAZZ_SOURCE which uses `//`). If `Note:` is not a valid Flow
comment token, these showcase sources are not actually runnable/parseable as shown — and the
`runnableOnWeb: false` pieces (scala) won't run anyway, but `granular`/`stretch` are marked
`runnableOnWeb: true` (pieces.ts:126,141) and deep-link into the playground with `&run=1`. If
`Note:` lines parse as errors, clicking "Open in playground" auto-runs straight into an error box.
**Fix:** Verify against the Flow lexer whether `Note:` is a comment form. If not, regenerate these
constants from the real `examples/*.flow` (which the header says they were auto-derived from) so the
comment syntax matches what the parser accepts, or set `runnableOnWeb: false` for any piece whose
inlined text won't parse on Web.

### IN-03: `CodeCard` comment block is stale — describes a pre-encode.ts fallback that no longer exists

**File:** `flow-site/src/lib/home/CodeCard.svelte:11-14`
**Issue:** The header comment still says `#code=` "holds the URL-encoded source (the playground
already reads `#code=` defensively); 49-06 swaps in the deflate/base64url encoder without touching
this contract." But the file already imports `encode` (line 18) and uses the real fflate encoder
(line 40). The comment describes a superseded interim state and will mislead the next reader.
**Fix:** Update the comment to reflect that `encode()` (fflate deflate + base64url) is now the
shipped path; drop the "until Plan 49-06 lands" language.

### IN-04: `svelte.config.js` `handleHttpError` lets `/docs/*` dangling links pass as warnings but throws for everything else — wiki content gaps can ship broken links silently

**File:** `flow-site/svelte.config.js:88-97`
**Issue:** Prerender errors under `/docs/` are downgraded to a `console.warn` and swallowed (return).
This is intentional (wiki cross-links to non-existent pages are a content gap), but it means a real
routing bug that happens to live under `/docs/` — e.g. a broken `[slug]` loader — would also be
silently warned past instead of failing the build. The blast radius of the allow-rule is wider than
the stated intent.
**Fix:** Narrow the allowance to genuine dangling *wiki* links by matching only the known
content-gap pattern (e.g. paths not in the synced slug set), and let unexpected `/docs/*` errors
fail the build. Lower priority since the docs routes are otherwise well-tested.

### IN-05: `remark-wiki-links` `rewriteMdLink` only rejects multi-segment paths but does not normalize `..` before the slug — defense-in-depth gap

**File:** `flow-site/src/lib/docs/remark-wiki-links.js:51-55`
**Issue:** `rewriteMdLink` guards against directory traversal by rejecting any cleaned path
containing `/` (line 52), which is sufficient for the flat wiki. But a target like `..%2Ffoo.md`
(URL-encoded slash) or `..\foo.md` (backslash) would pass the `includes('/')` check, then
`toSlug('..\\foo.md')` collapses non-alphanumerics to `-`, yielding a harmless slug. So there is no
present traversal, but the guard relies on `toSlug`'s aggressive normalization rather than an
explicit check. Since this content is build-time and first-party (synced wiki), risk is low.
**Fix:** Make the intent explicit: reject any path containing `/`, `\`, or `..` segments up front
with a clear comment, rather than depending on `toSlug` to sanitize.

### IN-06: `_headers` CSP comment claims Monaco needs no CDN, but `script-src` lacks `'unsafe-eval'` — confirm Monaco's tokenizer/worker doesn't need it

**File:** `flow-site/_headers` (CSP line) and `flow-site/src/lib/monaco/index.ts`
**Issue:** The CSP is `script-src 'self' 'wasm-unsafe-eval' 'sha256-…'`. Monaco's editor worker is
self-hosted (good), but some Monaco features (and the Mono-WASM runtime) historically need
`'unsafe-eval'` or at least `'wasm-unsafe-eval'` (present). If any Monaco code path uses `eval`/`new
Function` (older builds did for the tokenizer state machine), the playground will throw CSP
violations at runtime that won't surface in the unit/vitest suite (jsdom doesn't enforce CSP) and
may not surface in Playwright unless it asserts console-error-free. This is a verification gap, not
a confirmed bug.
**Fix:** Add a Playwright assertion on `/playground` that no CSP violation / console error fires
during editor mount + a Run, so the tight CSP is proven against the real Monaco + WASM at CI time.

---

_Reviewed: 2026-06-05_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
