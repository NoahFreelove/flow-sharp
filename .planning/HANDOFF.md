# Handoff — note-stream/Note:/site fixes shipped + redeployed (2026-06-12)

Branch: `dev` (9 → 16 commits ahead of origin, **NOT pushed**). The previous handoff's open
tasks are all DONE and **live at https://flow-music.pages.dev** (re-verified HTTP 200 + content).

## What shipped this session

The headline finding: the live playground ran a **stale WASM bundle** (frozen `8fee14b`,
2026-06-09) that predated several engine fixes. Two of the four "audio bugs" were really one
stale bundle; one was a genuine engine bug; one was pure flow-site. All fixed + redeployed.

| # | Fix | Commit(s) | Where |
|---|-----|-----------|-------|
| 1 | **Note streams cut off after ~1 bar** — `SequenceData` counted a non-pickup bar as the timesig numerator; a note stream packs all notes into one bar, so a 9-beat 4/4 stream reported 4 beats and `MixVoicesToBuffer` truncated the render. New `BarLengthBeats()` helper: monophonic bars use `Math.Max(numerator, actualBeats)`; parallel `{voice}` bars keep the numerator. Regenerated the `piano_warmth_smoke.wav` baseline (it had pinned the truncated render). | `102775f`,`7f78cfe` | flow-lang (quick 260611-wp2) |
| 2 | **`Note:` as a TRAILING comment** (`(play x) Note: …`). Was line-start only. Naive fix broke the stdlib (`Note:` is the proc-param TYPE ANNOTATION `Type: name`) → `IsTypeAnnotationColonPosition()` excludes the `(`/`,` case. | `1b05a65`,`f4f5a3d` | flow-lang (quick 260611-x8l) |
| 3 | **`createSineTone` too quiet** — already fixed in source (`d24d72d`); only the stale bundle was wrong. Shipped via the bundle regen below — **no new code.** | (bundle) | flow-lang |
| 4 | **Removed the "How it sounds" home section** + its VU/LED state + dead CSS. (Home rack used synth tones, not files — no audio assets touched; showcase WAVs untouched.) | `2962deb` | flow-site |
| 5 | **Docs render more clearly** — boxed shiki code blocks (border + padding + radius + lift), inline-code chips, stronger headings (Fraunces, h2 hairline). | `a2d9297` | flow-site |
| 6 | **Playground LED no longer stuck "playing"** — the frozen runtime exposes no playback-ended signal, so added the guarded settle timer the comment already described (~2s `PLAYING_SETTLE_MS`, cleared by run/stop/load). | `3e0b09e` | flow-site |
| — | **Regenerated the WASM bundle** (`sync-runtime.sh`, FlowTarget=Web) so #1/#2/#3 reach the live playground. flow-lang.wasm 932117→938261 B. | `634fd3e` | flow-site/static/wasm |

**Tests:** flow-lang.Tests 2432 passed (only pre-existing/flaky Jack/Showcase/Wasm integration
failures, confirmed on clean HEAD). flow-site vitest 131, build clean, playwright home/docs/
playground/responsive/visual specs green. Web build green; new bundle boots + runs.

**Deploy:** `pnpm -C flow-site build` then
`pnpm -C flow-site exec wrangler pages deploy .svelte-kit/cloudflare --project-name flow-music --branch main --commit-dirty=true`.
Wrangler is authed as the user (noahfreelove@gmail.com, pages:write). The deploy model is still
**manual wrangler** (no git-CI) — engine fixes only reach the live playground via
`sync-runtime.sh` → commit → re-deploy. (The `node:async_hooks`/`nodejs_compat` warning is
inherent to the adapter-cloudflare output and benign — site returns 200 across all routes.)

## Not started / deferred (unchanged from before)

- **Custom domain** `flow.<godaddy-domain>.com` — add ONE CNAME `flow → flow-music.pages.dev`
  at GoDaddy, then CF dashboard → Custom domains. Do NOT move nameservers (apex on Firebase).
- **"One proper demo"** for the home page (replaces the removed "How it sounds"). Composer: "later."
- **"Save to gist" OAuth** + env vars (default `#code=` share works without it).
- 16 local commits on `dev` are **unpushed**; `git push` when ready.

## Pre-existing test debt (not mine — flagged for follow-up)

- `Phase41.Showcase_RmsWithinTolerance` fails on clean HEAD (RMS level drift ~1.06 dB, a level
  issue not a length issue). Separate from the note-stream work.
- `TODO:`/`FIXME:` lead-in comments are still **line-start only** (not relaxed like `Note:`);
  relax the same way if a composer asks.

## Housekeeping

- 6 dirty `.planning/phases/42-*/*.txt` + `48-BUNDLE-SIZE.md` are **test-regen noise** (regenerate
  on every test run) — `git restore` them; keep OUT of commits.
- Background `pnpm -C flow-site dev` (:5173) may still be running.
