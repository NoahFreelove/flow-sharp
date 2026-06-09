---
phase: 49-flowlang-dev-site
plan: 06
subsystem: ui
tags: [oauth, fflate, cloudflare-worker, sveltekit, share, gist, csrf, security]

# Dependency graph
requires:
  - phase: 49-05 (WASM playground)
    provides: "/playground route, PlaygroundState runes, Monaco editor, the #code= fragment reader (defensively stubbed) this plan replaces with the real decode"
  - phase: 49-03 (Home) + 49-04 (docs) + 49-07 (showcase)
    provides: "the data-flow-source / data-run / #code= deep-link carriers whose encoded fragment this plan fills with real fflate base64url"
  - phase: 48 (WASM runtime)
    provides: "flow-runtime.js + D-48-09 user-gesture autoplay policy the &run=1 auto-run chain honors"
provides:
  - "URL-fragment share — the default, zero-backend, anonymous share path (encode/decode, fflate base64url, decompression-bomb-guarded)"
  - "GitHub gist OAuth via a ≤50-LOC CF Worker (state CSRF, server-side secret, same-origin redirect, scope=gist)"
  - "client-side gist creation (Bearer token, sessionStorage, least privilege)"
  - "Share + Save-to-gist controls wired into the playground; #code= auto-load + &run=1 auto-run; OAuth #token= capture"
  - "the cross-wave #code= contract closed: Home + docs + showcase carriers now emit the real encoded fragment"
affects: [49-08 (a11y/perf + HUMAN-UAT), phase-41 (reach + v1.5 closer)]

# Tech tracking
tech-stack:
  added: [] # fflate 0.8.3 already scaffolded in 49-01; no new packages
  patterns:
    - "Portable worker handler (handleGistAuth(request, env)) unit-tested with mocked fetch, then delegated to from a SvelteKit +server.ts — one OAuth implementation, no CF-runtime dependency in tests"
    - "Streaming-inflate size cap (fflate Inflate + running byte total) as a decompression-bomb guard — aborts before the full payload is allocated"
    - "encode-node.js mirror of encode.ts for build-time Node carriers (docs mdsvex in svelte.config.js), pinned byte-identical by a parity test"
    - "OAuth token rides the URL FRAGMENT (#token=) so it never reaches the server/logs; client reads it into sessionStorage then history.replaceState cleans the URL"

key-files:
  created:
    - flow-site/src/lib/share/encode.ts
    - flow-site/src/lib/share/encode.test.ts
    - flow-site/src/lib/share/encode-node.js
    - flow-site/src/lib/share/encode-node.test.ts
    - flow-site/src/lib/share/gist.ts
    - flow-site/workers/gist-auth.ts
    - flow-site/workers/gist-auth.test.ts
    - flow-site/wrangler.toml
    - flow-site/src/routes/api/auth/github/+server.ts
    - flow-site/src/lib/playground/share-controls.svelte.ts
    - flow-site/tests/playground-share.spec.ts
  modified:
    - flow-site/src/routes/playground/+page.svelte
    - flow-site/src/lib/home/CodeCard.svelte
    - flow-site/src/lib/showcase/pieces.ts
    - flow-site/svelte.config.js
    - flow-site/src/app.d.ts

key-decisions:
  - "Worker mints state with crypto.getRandomValues (Security V6, never Math.random) — switched from an initial crypto.randomUUID to match the plan's exact mandate + the verify grep"
  - "encode-node.js .js mirror added so the Node-loaded svelte.config.js docs carrier can produce the real #code= fragment (Node ESM has no .ts loader); byte-parity pinned by encode-node.test.ts"
  - "Auto-run signal rides &run=1 in the fragment (and ?run=1 in the query) rather than the navigation-lost data-run anchor attribute — the playground reads the URL on arrival, not the anchor element"
  - "Token in sessionStorage (D-49-28, A6) over localStorage (Willison) — ephemeral, cleared on tab close"

patterns-established:
  - "Defensive, typed decode: ShareDecodeError (not raw throws) maps to the UI-SPEC friendly copy; the playground never crashes on an attacker-crafted #code="
  - "Build-output secret scan as a CI-able invariant: GITHUB_CLIENT_SECRET appears ONLY as a server-side env-key read, never a baked value, never in the client bundle"

requirements-completed: [REQ-SITE-SHARE-01]  # REQ-SITE-SHARE-02 code complete; closure gated on the human OAuth-app checkpoint

# Metrics
duration: 11min
completed: 2026-06-05
---

# Phase 49 Plan 06: Share / Save (URL-fragment + gist OAuth) Summary

**URL-fragment share (fflate deflate + base64url, decompression-bomb-guarded decode) as the default zero-backend path, plus a ≤50-LOC CF Worker GitHub-gist OAuth promote-path (state CSRF, server-only secret, same-origin redirect, scope=gist) with client-side gist POST — Share/Save wired into the playground and the cross-wave #code= carrier contract closed.**

## Performance

- **Duration:** ~11 min
- **Started:** 2026-06-05T23:18:59Z
- **Completed:** 2026-06-05T23:29:39Z
- **Tasks:** 3 of 4 (Task 4 is the human-action checkpoint — OAuth App registration + live-gist UAT)
- **Files modified:** 16 (11 created, 5 modified)

## Accomplishments

- **URL-fragment share (REQ-SITE-SHARE-01, D-49-30)** — `encode(src)` → base64url `#code=` fragment (no `+`/`/`/`=`); `decode(frag)` inflates it back. `decode` is DEFENSIVE: a typed `ShareDecodeError` on malformed input (never a raw throw), and a streaming-inflate decoded-size cap (`MAX_DECODED_BYTES` = 256 KB) that aborts a decompression-bomb before allocating the full payload (T-49-CSP-FRAG).
- **GitHub gist OAuth CF Worker (REQ-SITE-SHARE-02 code, D-49-28)** — `workers/gist-auth.ts` (≤50 LOC core): mints a `crypto.getRandomValues` `state`, stashes it httpOnly, validates it against the cookie BEFORE the code exchange (T-49-OAUTH-CSRF; mismatch/absence → 400, no token call), exchanges server-side with the env secret (T-49-SECRET), and 302s to a HARD-CODED same-origin `/playground#token=…` (T-49-REDIRECT open-redirect guard), requesting `scope=gist` only (T-49-SCOPE).
- **Client-side gist creation (D-49-29)** — `gist.ts` `createGist(source, token)` POSTs `api.github.com/gists` with `Authorization: Bearer`; token cached in sessionStorage (ephemeral).
- **Wired into the playground** — Share (secondary) copies the link + "Link copied" toast; Save to gist (brass primary) runs OAuth/createGist + "Saved to gist.github.com/…" + Copy link; `onMount` real-decodes `#code=` (friendly error on a bad fragment, no crash), honors `&run=1` auto-run (D-49-08), and captures + cleans the OAuth `#token=` return.
- **Cross-wave contract CLOSED** — Home CodeCard + showcase `pieces.ts` + the docs mdsvex carrier now emit the REAL `#code=<fflate base64url>&run=1` fragment the playground's `decode` consumes (was `encodeURIComponent`). `encode-node.js` mirrors `encode.ts` for the Node-loaded docs config, byte-parity pinned.

## Task Commits

1. **Task 1: URL-fragment encode/decode (fflate, base64url, bomb guard)** — `ba812ab` (feat, TDD: RED test then GREEN impl in one commit)
2. **Task 2: GitHub OAuth CF Worker + client gist POST** — `384e30f` (feat, TDD: RED test then GREEN impl)
3. **Task 3: Wire Share/Save + #code= auto-load/auto-run + share E2E + carrier closure** — `9f7cf4f` (feat)

**Plan metadata:** (this commit — docs: complete plan)

## Files Created/Modified

- `flow-site/src/lib/share/encode.ts` — fflate deflate/inflate + base64url; defensive, size-capped `decode`; `ShareDecodeError` + `MAX_DECODED_BYTES`
- `flow-site/src/lib/share/encode.test.ts` — round-trip (7 sources), base64url-safety, malformed-fragment + decompression-bomb rejection, cap-boundary acceptance (12 tests)
- `flow-site/src/lib/share/encode-node.js` — Node mirror of `encode()` for the build-time docs carrier
- `flow-site/src/lib/share/encode-node.test.ts` — byte-parity + decode round-trip vs `encode.ts` (10 tests)
- `flow-site/src/lib/share/gist.ts` — client `createGist` (Bearer, public `snippet.flow`) + sessionStorage token helpers + `beginGistAuth`
- `flow-site/workers/gist-auth.ts` — the `/api/auth/github` OAuth handler (state CSRF, server-side exchange, same-origin redirect, scope=gist)
- `flow-site/workers/gist-auth.test.ts` — 6 mocked-fetch tests: scope/state/cookie authorize leg; state-mismatch + no-cookie CSRF rejection (no token call); valid-state exchange + same-origin redirect; secret never echoed; OAuth-error rejection
- `flow-site/wrangler.toml` — declares `GITHUB_CLIENT_ID` (public) + documents `GITHUB_CLIENT_SECRET` as a dashboard-only encrypted secret + the callback URL contract
- `flow-site/src/routes/api/auth/github/+server.ts` — SvelteKit route delegating to the portable handler (adapter-cloudflare `platform.env`)
- `flow-site/src/lib/playground/share-controls.svelte.ts` — runes `ShareControls` (shareLink/saveToGist/copyToastLink) + `captureOAuthToken`
- `flow-site/tests/playground-share.spec.ts` — share E2E: copy a `#code=` link, round-trip into the editor, malformed-fragment friendly error (3 tests × 3 viewports)
- `flow-site/src/routes/playground/+page.svelte` — wired Share/Save buttons + toast + decode-error pane; real `decode`; `&run=1` auto-run; OAuth token capture + URL clean; `__flowEditorValue` test hook
- `flow-site/src/lib/home/CodeCard.svelte` — real `encode()` `#code=…&run=1` deep link
- `flow-site/src/lib/showcase/pieces.ts` — `playgroundHref` uses real `encode()` + `&run=1`
- `flow-site/svelte.config.js` — docs mdsvex carrier emits the real `#code=…&run=1` deep link via `encode-node.js`
- `flow-site/src/app.d.ts` — typed `App.Platform.env` for the cloudflare gist route

## Decisions Made

- **`crypto.getRandomValues` for `state`** — the plan + Security V6 mandate it explicitly (never Math.random) and the verify gate greps for it; switched from an initial `crypto.randomUUID()`.
- **`encode-node.js` `.js` mirror** — `svelte.config.js` runs under Node's ESM loader which can't import a `.ts`, so the docs carrier needs a `.js` encode that is byte-identical to `encode.ts`; parity pinned by `encode-node.test.ts` so a future drift breaks CI rather than docs deep-links silently.
- **`&run=1` fragment marker for auto-run** — the navigation-lost `data-run` anchor attribute can't carry the signal across a page load; the playground reads the arrival URL, so the auto-run flag rides `&run=1` in the fragment (and `?run=1` in the query as a fallback).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 — Missing Critical] Added `encode-node.js` + parity test to close the docs carrier**
- **Found during:** Task 3 (carrier closure)
- **Issue:** The environment notes require the docs "Open in playground" carrier to emit the REAL `#code=` fragment, but that carrier lives in `svelte.config.js` (Node-loaded), which cannot import the browser `encode.ts` (no `.ts` ESM loader in Node). Without a Node-side encoder the docs links would have stayed plain `/playground` while Home/showcase deep-linked properly — an incomplete cross-wave contract.
- **Fix:** Added `encode-node.js` (a byte-identical Node mirror of `encode()`), wired it into the docs mdsvex carrier, and pinned `encode-node === encode.ts` + decode round-trip in `encode-node.test.ts`.
- **Files modified:** `encode-node.js`, `encode-node.test.ts`, `svelte.config.js`
- **Verification:** 10 parity/round-trip tests green; build shows a real base64url `#code=…&run=1` fragment in the prerendered docs HTML.
- **Committed in:** `9f7cf4f` (Task 3 commit)

**2. [Rule 3 — Blocking] Typed `App.Platform.env` for the cloudflare gist route**
- **Found during:** Task 3 (`pnpm check`)
- **Issue:** `platform.env` on the `+server.ts` route was untyped (`Property 'env' does not exist on type 'Readonly<Platform>'`) — a blocking `svelte-check` error.
- **Fix:** Declared `App.Platform.env` with the two GitHub env-var names in `app.d.ts`; removed the now-unnecessary cast in `+server.ts`.
- **Files modified:** `app.d.ts`, `+server.ts`
- **Verification:** `pnpm check` → 0 errors.
- **Committed in:** `9f7cf4f` (Task 3 commit)

**3. [Rule 1 — Bug] `encode-node.js` JSDoc param type for `toBase64Url`**
- **Found during:** Task 3 (`pnpm check`)
- **Issue:** `Parameter 'bytes' implicitly has an 'any' type` under `checkJs`.
- **Fix:** Added `@param {Uint8Array} bytes` JSDoc.
- **Committed in:** `9f7cf4f`

---

**Total deviations:** 3 auto-fixed (1 missing-critical, 1 blocking, 1 bug)
**Impact on plan:** All three were necessary to complete the cross-wave carrier contract and pass `pnpm check`. No scope creep — every change serves the plan's stated goals.

## Issues Encountered

None beyond the deviations above. The 3 remaining `pnpm check` warnings (unused dark-theme CSS selectors in `+page.svelte`/`design/+page.svelte`, the `node` type-def warning) are pre-existing from 49-02/49-03 and out of scope.

## Threat-Model Compliance (all mitigations implemented)

| Threat ID | Mitigation | Evidence |
|-----------|-----------|----------|
| T-49-OAUTH-CSRF | `crypto.getRandomValues` state, httpOnly cookie, validated before exchange; mismatch → 400, no token call | `gist-auth.test.ts` "rejects state mismatch / no cookie" |
| T-49-SECRET | secret read from env, sent only server→GitHub, never echoed | "never echoes GITHUB_CLIENT_SECRET" test + build-output client scan = clean |
| T-49-REDIRECT | hard-coded same-origin `/playground#token=` redirect, no reflected redirect param | "same-origin redirect" test |
| T-49-CSP-FRAG | typed `ShareDecodeError` + streaming-inflate size cap (decompression-bomb guard); decoded source loaded as text, never `{@html}` | `encode.test.ts` bomb + malformed tests; share E2E malformed-fragment friendly error |
| T-49-SCOPE | `scope=gist` only; token in sessionStorage | "scope=gist" test + `gist.ts` |
| T-49-XSS-SHARE | toasts + decode error render as escaped Svelte text, never innerHTML | `+page.svelte` curly-expr interpolation |

## Known Stubs

None. The `data-flow-source` / `data-run` carrier attributes now carry real fflate-encoded data (the cross-wave contract). The forward-compat MIDI-capture stub is from 49-05 and out of this plan's scope.

## User Setup Required

**External service requires manual configuration — this is the Task 4 human-action checkpoint.** Save-to-gist needs a registered GitHub OAuth App (the composer's account) and a live gist round-trip that cannot run in CI without leaking a secret. ALL code is built + unit-tested with mocked GitHub; only the live OAuth App registration + UAT remain. See the CHECKPOINT REACHED message returned to the orchestrator for exact steps (OAuth App registration, env vars `GITHUB_CLIENT_ID` + `GITHUB_CLIENT_SECRET`, callback URL `https://<project>.pages.dev/api/auth/github`, the live "Save to gist" round-trip).

## Next Phase Readiness

- **REQ-SITE-SHARE-01 closed** — URL-fragment share is live, default, and bomb-guarded.
- **REQ-SITE-SHARE-02 code complete, pending the human OAuth-app checkpoint** — the worker + client gist POST are implemented and unit-proven; the live round-trip is the Task 4 human-verify.
- **49-08 (a11y/perf + HUMAN-UAT)** can fold the live gist UAT into its HUMAN-UAT pass; the redirect/callback URL depends on the still-pending CF `<project>.pages.dev` URL from 49-01.

## Self-Check: PASSED

All 9 tracked created files exist on disk; all 3 task commits (`ba812ab`, `384e30f`, `9f7cf4f`) are present in the git history.

---
*Phase: 49-flowlang-dev-site*
*Completed: 2026-06-05*
