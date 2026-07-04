---
quick_id: 260703-wfs
slug: remove-the-choir-of-circuits-vocal-choir
date: 2026-07-04
status: complete
commits: b84b76b+42ae575
---

# Quick Task 260703-wfs — Summary

## What was asked

Remove the "Choir of circuits" (`vocal-choir`) playground example, rewrite the
`windy-field` soundscape example so it actually uses Flow's `->` flow operator
(the examples ironically used none), then update the WASM runtime, build the site,
commit, push, and deploy with wrangler.

## What was done

### 1. Removed the vocal-choir example (commit b84b76b)
- `git rm flow-site/static/examples/vocal-choir.flow`
- Dropped its entry from `manifest.json` → 9 entries, still valid JSON.
- Confirmed no other site source (`.ts`/`.svelte`/`.js`) referenced it — the
  manifest is the single dynamic source of truth.

### 2. Rewrote windy-field with `->` (commit b84b76b)
- Every buffer/DSP chain now threads through `->` (`x -> (f a b)` == `(f x a b)`),
  so signal chains read source-first, top-to-bottom instead of inside-out. 27 `->`
  uses across the wind bed, gusts, grass surges, bird chain, and master mix.
- Header comment updated to teach the operator.
- Two gotchas handled:
  - `fadeIn`/`fadeOut` are in-place void mutations → kept as standalone statements
    between chains (they can't be chain links). gain (uniform scale) and fades
    (linear envelopes) commute, so moving a fade after a gain is identical output.
  - A bare negative plain-Double (`-0.3`) in the first-arg slot of a `->` RHS lexes
    its `-` as a Minus operator (parse error) — unlike a music literal `-6dB` which
    is a single signed token. Used prefix `(neg 0.3)` / `(neg 0.5)` for the two
    negative pans (idiomatic prefix arithmetic).
- Render-verified: exit 0, 17.51s stereo @ 44.1 kHz, peak 0.583 FS (-4.7 dB, no
  clip) — perceptually identical to the previous version (STATE recorded 17.5s /
  0.58 FS for the original).

### 3. Synced the WASM runtime (commit 42ae575)
- Ran `flow-site/scripts/sync-runtime.sh` (`dotnet publish -p:FlowTarget=Web -c
  Release` + AppBundle copy). Minimal diff: only `flow-lang.wasm` + its
  `dotnet.boot.js` manifest hash changed; the frozen `flow-runtime.js` API and
  every other `_framework/` assembly are byte-identical.
- This carries all pending flow-lang fixes to the LIVE playground for the first
  time since 2026-06-26 (octave blocks, SFZ amp_veltrack / ampeg_release tail /
  legato offset, all-rest section length, tempo-block playback fix, the
  OverloadResolver unit-drop family, symbol-instrument renderSong, etc.).
- Added `.wrangler/` to root `.gitignore`.

### 4. Built + deployed
- `pnpm -C flow-site build` → green (adapter-cloudflare, `.svelte-kit/cloudflare/`).
- `git push origin dev` (eff83c2..42ae575).
- `wrangler pages deploy .svelte-kit/cloudflare --branch main` (production; I was on
  the `dev` git branch so `--branch main` targets the prod branch per the
  deployment convention). Success — 46 new files uploaded.

## Verification (live)
- `https://flow.noahfreelove.com/examples/manifest.json` → 9 entries, no vocal-choir.
- `https://flow.noahfreelove.com/examples/windy-field.flow` → 27 `->` uses.
- Deployment alias: `https://e1f605d9.flow-music.pages.dev`.

## Notes / non-blocking
- The `node:async_hooks` warning during deploy is the pre-existing OAuth-worker
  `nodejs_compat` notice (flag set in the CF dashboard); unrelated to the static
  playground.
- Executed non-isolated on the `dev` tree (per external memory
  `project_gsd_worktree_stale_base`); deploy handled directly (outward-facing).
