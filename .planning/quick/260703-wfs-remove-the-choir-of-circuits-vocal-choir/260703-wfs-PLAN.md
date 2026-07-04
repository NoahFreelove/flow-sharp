---
quick_id: 260703-wfs
slug: remove-the-choir-of-circuits-vocal-choir
date: 2026-07-04
status: in-progress
---

# Quick Task 260703-wfs — Prune vocal-choir example + flow-operator-ify windy-field, then ship

## Goal

Two example-content changes to the flowlang.dev playground, then a full runtime-refresh + deploy:

1. **Remove** the "Choir of circuits" (`vocal-choir`) example — delete
   `flow-site/static/examples/vocal-choir.flow` and its `manifest.json` entry.
2. **Rewrite** `flow-site/static/examples/windy-field.flow` so its buffer/DSP
   chains use Flow's `->` flow operator (currently the example — ironically — uses
   none, despite `->` being a headline language feature).
3. **Ship it**: sync the WASM runtime (`sync-runtime.sh` → `dotnet publish
   -p:FlowTarget=Web`), build the site (`pnpm -C flow-site build`), commit, push,
   and deploy to Cloudflare Pages with wrangler.

## Context / constraints

- Examples are force-added past the blanket `*.flow` gitignore (per prior quick
  tasks 260627-syb / 260701-vx4). `git rm` handles the deletion; `git add -f` any
  new/edited `.flow`.
- `->` syntax in this codebase is S-expression form: `x -> (func args)` ==
  `(func x args)`. Multi-line chains work when wrapped in parens (see
  `tests/test_chain_naming.flow`). NOT C-style `f(args)`.
- `fadeIn`/`fadeOut` are in-place void mutations — they stay as standalone
  statements, not chain links. gain (uniform scale) and fades (linear envelopes)
  commute, so moving a fade after a gain is perceptually identical.
- `.wrangler/` is NOT gitignored — add it to `.gitignore` so wrangler's local
  cache is never swept into a commit.
- Execute on the `dev` tree non-isolated (worktree isolation has handed out stale
  bases here — external memory `project_gsd_worktree_stale_base`). Deploy is
  outward-facing → keep direct control + report honestly.

## Tasks

### T1 — Remove vocal-choir example
- `git rm flow-site/static/examples/vocal-choir.flow`
- Delete the `vocal-choir` object (last entry) from
  `flow-site/static/examples/manifest.json`; verify the file stays valid JSON.
- **Verify:** `manifest.json` parses; no dangling reference to `vocal-choir.flow`.

### T2 — Rewrite windy-field with `->`
- Convert each buffer/DSP chain in `windy-field.flow` to `->` form; update the
  header comment to call out the flow operator.
- **Verify:** `dotnet run --project flow-interpreter flow-site/static/examples/windy-field.flow`
  renders exit 0, non-trivial stereo output, no parse/eval errors.

### T3 — Sync WASM runtime
- `bash flow-site/scripts/sync-runtime.sh` (dotnet publish Web + copy AppBundle
  into `static/wasm/`). Carries all pending flow-lang fixes to the live playground.
- **Verify:** `static/wasm/flow-runtime.js` + `_framework/` regenerated.

### T4 — Build the site
- `pnpm -C flow-site build`
- **Verify:** build succeeds → `.svelte-kit/cloudflare/` output produced.

### T5 — Commit + push + deploy
- Add `.wrangler/` to `.gitignore`.
- Atomic commit of the example + manifest + regenerated wasm bundle + gitignore.
- `git push origin dev`.
- Deploy to CF Pages via the `flow-site/node_modules/.bin/wrangler` binary
  (direct-upload, prod branch = main per external memory
  `project_flow_site_deployment`).
- **Verify:** wrangler reports a successful deployment URL.

## Must-haves
- `vocal-choir.flow` gone; `manifest.json` has 9 entries, still valid JSON.
- `windy-field.flow` uses `->` and renders clean.
- `static/wasm/` regenerated from a fresh Web publish.
- Site build green; changes committed + pushed; wrangler deploy succeeds.
