# Phase 49 — HUMAN-UAT: Cross-Browser Audible Audio + Skeuomorphic Fidelity + Live Deploy

**Status:** PENDING — composer sign-off required. This is the consolidated UAT batch for Phase 49:
it folds in the still-open 49-01 (live CF Pages deploy) and 49-06 (gist OAuth round-trip) gates so
the composer runs ONE pass, not three. Mirrors the Phase 48 UAT format.

**Why human:** the automated suite (vitest 70/70, playwright 275/275, lhci ≥0.9 ×4 both form factors,
axe 0-critical) is GREEN, but three things automation cannot confirm need the composer:
1. **Audible** audio across browsers — headless only asserts `AudioContext.state === 'running'`,
   never that sound is heard. Phase 48 left Chrome/Safari audio UNVERIFIED (HANDOFF §7); Firefox PASS.
2. **Skeuomorphic visual fidelity** — "Logic Pro / Reason / vintage-synth, not glassmorphism" is a
   subjective aesthetic target (D-49-06 references).
3. **Live deploy + live gist** — require the composer's Cloudflare account + a real GitHub OAuth App,
   neither of which can run in CI without leaking a secret.

---

## Prerequisites (composer one-time, gates folded in from 49-01 + 49-06)

These two were deferred to a human across Phase 49; do them ONCE here so every browser row below
can exercise the live, deployed site.

### A. Live CF Pages deploy (folds in the 49-01 gate, REQ-SITE-IA-01)

1. In the Cloudflare dashboard, create a Pages project pointed at this repo's `flow-site/` with build
   command `pnpm build` and output dir `.svelte-kit/cloudflare` (the adapter-cloudflare output).
2. Confirm the deploy succeeds and note the assigned `<project>.pages.dev` URL.
3. Visit `<project>.pages.dev/` — confirm HTML serves, the skeuo chrome renders, and `/_headers`
   is applied (DevTools → Network → the document response carries the CSP + COOP/COEP on /playground).
4. The full deploy runbook lives in **49-09** (the closer); this UAT only needs the URL live.

### B. GitHub OAuth App for "Save to gist" (folds in the 49-06 gate, REQ-SITE-SHARE-02)

1. Register a GitHub OAuth App (Settings → Developer settings → OAuth Apps → New). Authorization
   callback URL = `https://<project>.pages.dev/<the gist worker callback path from 49-06>` (the
   callback depends on the `.pages.dev` from step A — that's why these are batched).
2. Put the client id/secret into the CF Pages project's environment (the 49-06 worker reads them).
3. Re-deploy so the worker picks up the secrets.

> If A or B can't be completed in this session, mark the dependent rows **DEFER** and the audible /
> visual rows can still be run against a LOCAL `pnpm -C flow-site preview` build (set
> `<base>` to `http://localhost:4173`). Audible audio + visual fidelity do NOT require the live deploy;
> only the live-gist row (B) and the "shared link in a clean browser over HTTPS" row do.

---

## Reproducible Steps (run per browser)

For each browser row below, open `<project>.pages.dev/playground` (or the local preview):

1. **Audible default tone.** The editor pre-fills with a Web-runnable snippet. Press **Run**.
   - Expect an **audible** tone (the D-48-09 gesture chain — `await resumeAudio()` then `run()` in
     the one click frame — should satisfy even Safari's strict autoplay policy; confirm it does).
   - The LED indicator shows "Playing"; the console shows any stdout; no errors.
2. **stdout.** Replace with `(print "hello flow")`, press Run. Console stdout shows `hello flow`,
   no audio, no errors.
3. **Parse error.** Type `(print` (unclosed), press Run. A Rust-style error box appears with
   `kind: parse`; the page stays responsive (no crash).
4. **MIDI download** (when the runtime populates `RunResult.midi`). Run a `writeMidi` snippet; if a
   "Download MIDI" button appears, click it and confirm a `.mid` file downloads. *(Known stub: the
   shipped Phase 48 runtime hardcodes `Midi = null` — the button is forward-compatible and only
   lights up once a future runtime emits MIDI bytes; record DEFER if it does not appear.)*
5. **Shared link, clean browser (needs live HTTPS deploy).** Copy a `#code=` share link (Share
   button), open it in a **private/incognito window with no login**, confirm it loads + runs.
6. **Live gist (needs prereq B).** While logged in, click **Save to gist**; confirm a gist appears
   at `gist.github.com/<your-user>/<id>` and the returned link reopens the snippet.
7. **Keyboard-only nav pass.** Tab through the nav + playground controls with NO mouse: confirm the
   brass focus ring is visible on every stop, Tab order matches the visual left-to-right order, and
   there's no keyboard trap (you can Tab all the way through and back out).
8. **Screen-reader smoke** (VoiceOver / NVDA). Sweep the nav + playground: confirm interactive
   elements announce labels, the GitHub link announces "opens in new tab", and the LED status
   announces ("Playing"/"Stopped"/"Error") via the aria-live mirror — status is NOT colour-only.
9. **Skeuomorphic fidelity.** Walk `/`, `/docs`, `/playground`, `/showcase`. Confirm the look
   matches the D-49-06 references — wood / brushed-metal / paper / felt, embossed buttons, brass
   used sparingly (<~10% of any viewport) — with **NO glassmorphism / AI-template feel**. Toggle
   dark mode; confirm the deep-walnut + amber-LED vintage-gear aesthetic.

---

## Per-Browser Rows

### Row 1: Chrome 120+ (audible audio — Phase 48 UNVERIFIED, re-smoke here)

| Field | Value |
|-------|-------|
| Audible default tone (step 1) | ⬜ PASS / DEFER / SKIP |
| stdout + parse-error (2–3) | ⬜ |
| MIDI download (4) | ⬜ |
| Shared-link clean browser (5) | ⬜ |
| Skeuomorphic look + dark mode (9) | ⬜ |
| Notes | |

### Row 2: Firefox 121+ (Phase 48 PASS — re-confirm on the deployed site)

| Field | Value |
|-------|-------|
| Audible default tone (step 1) | ⬜ PASS / DEFER / SKIP |
| stdout + parse-error (2–3) | ⬜ |
| MIDI download (4) | ⬜ |
| Shared-link clean browser (5) | ⬜ |
| Skeuomorphic look + dark mode (9) | ⬜ |
| Notes | |

### Row 3: Safari 17+ (macOS — Phase 48 SKIPPED, strict autoplay policy is the key test)

| Field | Value |
|-------|-------|
| Audible default tone (step 1) | ⬜ PASS / DEFER / SKIP |
| stdout + parse-error (2–3) | ⬜ |
| MIDI download (4) | ⬜ |
| Shared-link clean browser (5) | ⬜ |
| Skeuomorphic look + dark mode (9) | ⬜ |
| Notes | |

### Row 4: Mobile Safari (iOS) + Mobile Chrome (Android)

| Field | Value |
|-------|-------|
| Audible audio on tap-Run (step 1) | ⬜ PASS / DEFER / SKIP |
| Single-column layout, Monaco read-only banner | ⬜ |
| No horizontal scroll at phone width | ⬜ |
| Skeuomorphic look holds on mobile | ⬜ |
| Notes | |

---

## Cross-Cutting Rows (run once, any browser)

| Check | Result |
|-------|--------|
| Keyboard-only nav: brass focus ring + Tab order + no trap (step 7) | ⬜ PASS / FAIL |
| Screen-reader smoke: labels + LED aria-live (step 8) | ⬜ PASS / FAIL |
| Live CF deploy serves the site (prereq A) | ⬜ PASS / DEFER |
| Live gist round-trip (prereq B + step 6) | ⬜ PASS / DEFER |

---

## REQs that flip on completion

| REQ | Flips on |
|-----|----------|
| REQ-SITE-PLAYGROUND-03 | Audible audio confirmed in ≥1 browser row (ideally all 3 + mobile) |
| REQ-SITE-DESIGN-01..04 (visual-fidelity portion) | Skeuomorphic sign-off (step 9), no glassmorphism |
| REQ-SITE-SHARE-02 | Live gist created under composer account (prereq B + step 6) |
| REQ-SITE-IA-01 (live-deploy portion) | `<project>.pages.dev` serves HTML (prereq A) |
| REQ-SITE-A11Y-* (screen-reader portion) | SR smoke PASS (step 8) — the axe/keyboard automated portion is already closed by Plan 49-08 |
| REQ-SITE-PERF-01 | Already CLOSED by Plan 49-08 lhci (≥0.9 ×4, both form factors, production-accurate); listed here only if the composer wants to re-confirm on the live `.pages.dev` |

---

## Composer Sign-Off

**Approval:** pending

> Type "approved" with the per-browser audio results + visual sign-off, or list the blockers.
> Record each row PASS / DEFER / SKIP above (mirroring the Phase 48 UAT format).
