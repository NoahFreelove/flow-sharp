---
phase: 17-flow-language-server
plan: 08
subsystem: docs-marketplace-phase-closure
tags: [docs, marketplace, openvsx, runbook, editor-setup, manual-verification, phase-closure, human-uat-deferred]

# Dependency graph
requires:
  - phase: 17-flow-language-server
    plan: 07
    provides: "CI workflow (.github/workflows/publish-extension.yml) with VSCE_PAT + OVSX_PAT secret references, 4-platform matrix, tag-gated publish job; scripts/lsp-smoke.sh for binary smoke-boot; 4 TM grammar snapshot baselines; vscode-extension/package.json test:grammar scripts + package-lock.json"
provides:
  - "docs/editor-setup/README.md — landing page linking every editor's setup guide + binary install options (release tarball or dotnet publish from source) + Pitfall 6 awareness (stdlib .flow files must sit beside the binary)"
  - "docs/editor-setup/nvim-lspconfig.lua — raw Neovim snippet with filetype registration + lspconfig.util.root_pattern + on_attach hook"
  - "docs/editor-setup/helix-languages.toml — raw Helix [[language]] + [language-server.flow-lsp] blocks with // comment-token"
  - "docs/editor-setup/neovim.md + helix.md — per-editor setup guides with prerequisites, configuration, troubleshooting, cross-links"
  - "docs/editor-setup/generic-lsp.md — Emacs (lsp-mode + eglot), Zed, Cursor, Windsurf, Sublime/Kate/coc.nvim guidance; shared LSP contract table for any editor not explicitly covered"
  - "docs/editor-setup/manual-smoke.md — Phase 17 closure checklist covering all 5 manual-only-verification rows from 17-VALIDATION.md (rows 1-3 executable today, rows 4-5 deferred to first release tag push as HUMAN-UAT items)"
  - ".planning/phases/17-flow-language-server/17-HUMAN-UAT.md — persistent HUMAN-UAT session file (GSD UAT template) tracking manual-smoke rows 1-3 as 3 pending tests surfaced via /gsd-progress and /gsd-audit-uat; rows 4-5 explicitly deferred to first release tag milestone via an in-file note so no circular wait blocks phase closure"
  - "vscode-extension/README.md — end-user-facing extension description: features, Marketplace + OpenVSX install, VSCode >= 1.85.0 requirement, no-.NET-runtime promise, configuration reference table (flow.server.path + flow.trace.server), manual-binary-override guidance with Pitfall 6 stdlib-files reminder, troubleshooting, non-VSCode handoff to docs/editor-setup/, repo + issues links, publisher placeholder disclosure (108 lines)"
  - ".planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md — one-time human-only runbook: VSCode Marketplace publisher creation + VSCE_PAT (Azure DevOps Marketplace.Manage scope, 1y max expiry), OpenVSX publisher + namespace claim (Pitfall 8) + OVSX_PAT, GitHub Actions secret wiring, workflow_dispatch dry-run procedure, first tag push procedure, rotation calendar (2-month advance reminder), troubleshooting table, 17-row status checklist for tracking completion of each step"
  - "README.md (top-level) — Editor support section announcing VSCode extension (Marketplace + OpenVSX) + pointer to docs/editor-setup/ for non-VSCode editors"
  - ".gitignore — allowlist docs/ + docs/**/*.md + top-level README.md so editor-setup prose tracks despite global *.md ignore"
affects: [Phase 17 closure]

# Tech tracking
tech-stack:
  added:
    - "None — documentation and runbook only. No new runtime dependencies."
  patterns:
    - "Dual-shape editor snippets: raw syntactically-valid files (nvim-lspconfig.lua, helix-languages.toml) sit alongside markdown guides (neovim.md, helix.md). Users can copy-paste the raw file verbatim OR read the guide for context. Acceptance criteria grep for both shapes — raw files satisfy the plan's literal path requirements, markdown guides satisfy the orchestrator's docs-with-prose objective."
    - "Runbook status checklist as audit trail: .planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md ends in a 17-row table Noah flips to [x] as each one-time step lands. Future maintainers can see at a glance which prerequisites for the CI publish workflow are active vs pending."
    - "Deferred-to-first-tag-push pattern: manual-smoke.md explicitly marks rows 4 (per-platform binary on non-dev OS) and 5 (Marketplace + OpenVSX publish succeeds) as HUMAN-UAT against the first release, NOT against Phase 17 closure. Prevents Phase 17 from being indefinitely blocked on marketplace-verification events that require tag push to happen first."

key-files:
  created:
    - docs/editor-setup/README.md
    - docs/editor-setup/nvim-lspconfig.lua
    - docs/editor-setup/helix-languages.toml
    - docs/editor-setup/neovim.md
    - docs/editor-setup/helix.md
    - docs/editor-setup/generic-lsp.md
    - docs/editor-setup/manual-smoke.md
    - .planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md
    - .planning/phases/17-flow-language-server/17-HUMAN-UAT.md
  modified:
    - vscode-extension/README.md (expanded from 32 to 108 lines; added Installation, feature list with D-04..D-11 coverage, configuration reference table, manual-binary-override guidance, non-VSCode editor handoff, publisher disclosure)
    - README.md (added Editor support section)
    - .gitignore (added !docs/, !docs/**/*.md, !README.md allowlists for user-facing prose)

key-decisions:
  - "Ship both raw snippets AND markdown guides per editor — the plan's literal artifact paths (nvim-lspconfig.lua, helix-languages.toml) are grep-gated by acceptance criteria, and the orchestrator's objective mandates .md guides. Dual-shape delivery satisfies both contracts and gives users two consumption modes (copy-paste raw vs read-guide-first)."
  - "Helix comment-token set to // matching vscode-extension/language-configuration.json (confirmed at vscode-extension/language-configuration.json line 3) rather than ;. Single source of truth for Flow's comment convention."
  - "Runbook publisher-ID flexibility — the runbook explicitly notes that <publisher-id> must match package.json and that switching publishers requires updating package.json BEFORE claiming identities on the marketplaces. This surfaces Pitfall 8's gotcha (namespace-vs-publisher naming mismatch) ahead of the first tag push."
  - "Azure DevOps scope named `Marketplace.Manage` (not `Marketplace.Publish`) — the VSCE documentation uses Publish vocabulary but Azure DevOps UI labels it Manage. Runbook documents both vocabularies to avoid confusion when Noah executes Step 1.3."
  - "Rotation calendar entries — VSCE_PAT has 1-year max expiry (hard ceiling); OVSX_PAT has no documented expiry but rotate annually for hygiene. 2-month advance reminder for VSCE_PAT lets the replacement PAT go through one real tag push before the old one is revoked."
  - "Deferred rows 4 + 5 from manual-smoke to first-release-tag — because rows 4 (non-dev OS test) and 5 (Marketplace + OpenVSX publish) literally CANNOT happen today without the VSIX artifacts existing on the marketplaces, blocking Phase 17 on them would create a circular dependency (phase closure awaiting tag push awaiting phase closure). Treating them as HUMAN-UAT items scoped to the release-tag milestone is the clean resolution."
  - "Manual-smoke checkpoint resolution (2026-04-20): Task 3 closed by deferring rows 1-3 to HUMAN-UAT rather than blocking phase closure on a synchronous F5 session. Authored 17-HUMAN-UAT.md using the GSD UAT template so the 3 active tests surface in /gsd-progress and /gsd-audit-uat and can resolve independently of phase-close. NOT faked as a pass — each test is explicitly recorded result: [pending] awaiting human verification. This pattern (HUMAN-UAT handoff from a final manual-verify checkpoint) is reusable for any future phase that cannot auto-verify a UI/UX behavior and must not block on a synchronous human session."
  - ".gitignore allowlist for docs/**/*.md: the repo's global *.md ignore was chosen for AI-generated-documentation hygiene (only .planning/, CLAUDE.md, and the few explicitly-allowed extension files pass through). Editor-setup docs are user-facing prose and must be trackable. Added !docs/, !docs/**/*.md, and !README.md in a commented block distinct from the existing vscode-extension/ allowlist."

patterns-established:
  - "User-facing prose under docs/: first Phase-17 directory with tracked markdown outside .planning/ and vscode-extension/. Pattern reusable for any future phase that ships user documentation — add a docs/<topic>/ path and extend the .gitignore allowlist."
  - "Runbook status-table audit trail: .planning/phases/<phase>/<phase>-<scope>-SETUP.md pattern (17-MARKETPLACE-SETUP.md is the first instance) captures one-time human-only prerequisites that CI cannot execute. Future phases needing secret provisioning or publisher-identity bootstrap can mirror this shape."
  - "Checkpoint-before-close pattern: the plan declared autonomous: false with exactly one checkpoint: human-verify task at the end. Tasks 1 + 2 committed normally; Task 3 authors the test instructions THEN pauses. Future multi-task plans with a final-manual-verify gate should follow this — artifacts land first, checkpoint payload returns a stable reference for the reviewer."

requirements-completed-partial: [D-13 (second clause — non-VSCode editor docs), D-15 (prerequisites documented, execution deferred to Noah + first tag push)]
requirements-pending-human-uat: [D-13 (first clause — VSCode extension activation), D-04, D-05, D-06, D-07, D-08, D-09, D-10, D-11 (tracked in 17-HUMAN-UAT.md tests 1-3), D-14, D-15 (execution — pending first release tag, NOT tracked in 17-HUMAN-UAT.md per deferral note)]

# Metrics
duration: ~6min (authoring) + ~3min (HUMAN-UAT deferral + finalization)
completed: 2026-04-20 (plan closed; HUMAN-UAT rows 1-3 deferred to ongoing human testing)
status: complete (with HUMAN-UAT deferred)
phase17-fact-count-at-close: 96 (0 failed, 0 skipped, all green under `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"`, duration ~1s; docs-only plan — regression unchanged from 17-07)
---

# Phase 17 Plan 08: Non-VSCode editor docs + Marketplace/OpenVSX setup + Extension Dev Host manual smoke + phase closure Summary

**Wave 7 closed (complete with HUMAN-UAT deferred): docs/editor-setup/ landed with 4 markdown guides (README, neovim, helix, generic-lsp, manual-smoke) + 2 raw-snippet reference files (nvim-lspconfig.lua, helix-languages.toml) closing D-13's second clause; 17-MARKETPLACE-SETUP.md runbook captures every one-time human-only step for VSCode Marketplace (Azure DevOps publisher + VSCE_PAT with Marketplace.Manage scope + 1y rotation) and OpenVSX (namespace claim via `npx ovsx create-namespace` closing Pitfall 8 + OVSX_PAT + annual hygiene rotation) with a 17-row status checklist for audit-trail tracking; vscode-extension/README.md expanded 32→108 lines with features list, Marketplace + OpenVSX install path, flow.server.path + flow.trace.server configuration reference table, manual-binary-override guidance, and cross-link to docs/editor-setup/ for non-VSCode editors; top-level README.md gained an Editor support section announcing the extension. Manual smoke checklist authored into docs/editor-setup/manual-smoke.md covering all 5 manual-only-verification rows from 17-VALIDATION.md (rows 1-3 executable today via F5 in EDH, rows 4-5 deferred to first release tag push as HUMAN-UAT items). Task 3 resolved by explicit user direction: rows 1-3 tracked as pending tests in .planning/phases/17-flow-language-server/17-HUMAN-UAT.md (surfaced via /gsd-progress and /gsd-audit-uat) and rows 4-5 deferred to first release tag — phase 17 closes now without blocking on a synchronous F5 session. Phase17 Fact count at plan close: 96/96 green (docs-only, unchanged from 17-07).**

## Performance

- **Duration (authoring):** ~6 min
- **Duration (HUMAN-UAT deferral + finalization):** ~3 min
- **Started:** 2026-04-21T00:14:12Z
- **Authoring completed:** 2026-04-21T00:20:40Z (approximate)
- **Checkpoint resolution:** 2026-04-20 via user direction ("Defer to HUMAN-UAT. Close the phase now without blocking on F5")
- **Tasks:** 3 (2 autonomous + 1 manual-verify checkpoint DEFERRED to HUMAN-UAT)
- **Files created:** 9 (7 under docs/editor-setup/ + 1 marketplace runbook + 1 HUMAN-UAT session file)
- **Files modified:** 3 (vscode-extension/README.md, README.md, .gitignore)
- **Tests added:** 0 C# (documentation only — plan 17-07 left 96/96 Phase17 Facts green)
- **Phase17 Fact count at plan close:** 96 passed, 0 failed, 0 skipped, duration ~1s (`dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"`)

## Accomplishments

### Task 1 — Non-VSCode editor setup snippets + finalized VSCode README (commit `ec8e18f`)

- `docs/editor-setup/nvim-lspconfig.lua` — working nvim-lspconfig snippet with `configs.flow` registration, `cmd = { 'flow-lsp' }`, `filetypes = { 'flow' }`, `root_dir` on `.git` + `.flowproject`, and a `vim.filetype.add` call so `.flow` files pick up the Flow filetype without external ftdetect. Includes commented-out on_attach example keybindings (`K` = hover, `gd` = definition, `<C-s>` = signature_help).
- `docs/editor-setup/helix-languages.toml` — Helix `[[language]]` + `[language-server.flow-lsp]` blocks with `scope = "source.flow"`, `file-types = ["flow"]`, `roots = [".git", ".flowproject"]`, `comment-token = "//"` (matches vscode-extension/language-configuration.json), `indent = { tab-width = 4, unit = "    " }`, `language-servers = ["flow-lsp"]`.
- `docs/editor-setup/neovim.md` — prose guide with the two install options (release tarball + `dotnet publish`), configuration notes (plugin-manager-agnostic), troubleshooting (flow-lsp-not-on-PATH, definition-not-found, completion-sparse-snippetSupport).
- `docs/editor-setup/helix.md` — parallel prose guide with Helix-specific reload command (`:config-reload`) and note about tree-sitter-vs-semantic-tokens rendering.
- `docs/editor-setup/generic-lsp.md` — covers Emacs (lsp-mode + eglot snippets), Zed (extension-scaffold caveat), Cursor / Windsurf (OpenVSX path + direct VSIX install), and a generic contract table (`language id = flow`, `scope = source.flow`, `file-types = [.flow]`, `binary = flow-lsp`, `transport = stdio`, `root markers = .git + .flowproject`) for any editor not explicitly named.
- `docs/editor-setup/README.md` — landing page with binary install options (Option A tarball, Option B `dotnet publish` from source with correct `-r` target names per `.NET RID`), a per-editor configuration table linking raw snippets + guides, a non-VSCode-editor invitation referencing the LSP spec, manual smoke handoff, troubleshooting, contributing guidance.
- `vscode-extension/README.md` — expanded from 32 lines to 108 lines. New content: overview paragraph referencing Flow's signature syntax (`->` operator, Note/Chord/Song types, `| C4 D4 E4 |` note streams, `tempo` / `key` / `timesig` / `swing` blocks); features bullet list mapped to D-04..D-11 LSP decisions; Marketplace + OpenVSX install guidance with Cursor/VSCodium/Windsurf explicit callout; requirements (VSCode >= 1.85.0, no .NET SDK); configuration reference table covering both settings; "Using a custom flow-lsp binary" subsection documenting the Pitfall 6 stdlib-files-beside-binary contract; troubleshooting; non-VSCode editor cross-link; repo + issues + language-reference links; publisher placeholder disclosure referencing 17-MARKETPLACE-SETUP.md.
- `.gitignore` — added `!docs/`, `!docs/**/*.md`, `!README.md` allowlists so user-facing prose tracks despite the global `*.md` ignore.

### Task 2 — Marketplace + OpenVSX setup runbook + top-level README announcement (commit `888f432`)

- `.planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md` — 200+ line runbook structured as 4 steps:
  - **Step 1** — VSCode Marketplace publisher creation (Azure DevOps sign-in → publisher registration at marketplace.visualstudio.com/manage) + VSCE_PAT generation (custom-defined Marketplace.Manage scope, 1y max expiry) + GitHub Actions secret wiring + optional `vsce login + vsce package` dry-run.
  - **Step 2** — OpenVSX publisher (GitHub OAuth sign-in) + PAT generation at open-vsx.org/user-settings/tokens + `npx ovsx create-namespace <publisher-id> -p $OVSX_PAT` namespace claim (explicit Pitfall 8 callout — first publish will fail without this) + GitHub Actions secret wiring.
  - **Step 3** — workflow_dispatch dry-run of the CI workflow (build-server matrix runs, publish job correctly skips on non-tag refs, 4 VSIX artifacts appear in the Actions run) + VSIX-contents inspection showing the 6 stdlib .flow files beside the flow-lsp binary (Pitfall 6 verification).
  - **Step 4** — first real tag push procedure (`git tag v0.1.0 && git push origin v0.1.0`) + expected CI shape (4 green build-server rows + 8 green publish rows = 4 targets × 2 registries) + listing verification on both marketplaces.
  - Rotation calendar: 2-month advance reminder for VSCE_PAT refresh, annual OVSX_PAT hygiene rotation.
  - Troubleshooting table covering 7 failure modes (publisher-not-found, 401-Unauthorized, namespace-missing, version-collision, partial-publish, stdlib-missing-in-VSIX, unverified-publisher-badge).
  - 17-row status checklist Noah flips to `[x]` as each one-time step completes — audit trail for D-15.
  - Cross-references to 17-07's CI workflow, 17-RESEARCH's Pitfall 8, 17-08's manual-smoke doc, and the Environment Availability gap both PATs + namespace closed.
- `README.md` top-level — new "Editor support" section announcing the VSCode extension (Marketplace + OpenVSX install, per-platform flow-lsp bundling, no-.NET-runtime), and pointing non-VSCode editors at `docs/editor-setup/`.

### Task 3 — Manual smoke checklist authored; DEFERRED to HUMAN-UAT (commits `9d33c90`, finalization)

- `docs/editor-setup/manual-smoke.md` — 247-line checklist (commit `9d33c90`):
  - Prerequisite build section: complete `dotnet test --filter Phase17` + `dotnet publish` + stdlib cp + `scripts/lsp-smoke.sh` sequence so any developer can reproduce the test environment.
  - Row 1 (D-04/D-05 syntax highlighting): 11-item tick list covering each flow-editor/FlowSyntaxHighlighter.cs category (keywords, types, strings, numbers, comments, notes, chords, roman numerals, note-stream delimiters, operators, booleans).
  - Row 2 (D-04 TM→semantic transition): 3-option outcome record (not-noticeable / subtle-acceptable / jarring).
  - Row 3 (D-13 extension activation): 7 embedded sanity checks covering D-06 (diagnostics), D-07 (completion — proc bodies, `use "@`), D-08 (hover), D-09 (go-to-definition), D-10 (signature help), D-11 (note-stream completion — roman numerals inside key + note letters outside), plus snippet expansion.
  - Row 4 (D-14 non-dev OS binary): explicit DEFERRED status — HUMAN-UAT against first release tag.
  - Row 5 (D-15 Marketplace + OpenVSX publish): explicit DEFERRED status — HUMAN-UAT against first release tag.
  - Summary table: rows 1–3 required for Phase 17 closure, rows 4–5 deferred.
  - Resume signal format: `smoke: clear` / `smoke: partial - <desc>` / `smoke: blocked - <desc>`.

**Task 3 — DEFERRED to HUMAN-UAT (final commit).** Per user direction ("Defer to HUMAN-UAT. Close the phase now without blocking on F5. Record rows 1–3 as HUMAN-UAT items so they appear in /gsd-progress and /gsd-audit-uat. Rows 4–5 stay deferred to first release tag. Do NOT fake a pass — record explicitly as pending human verification."), rows 1–3 are now tracked in `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` as 3 pending tests using the GSD UAT template. Rows 4–5 remain deferred to the first release tag milestone and are NOT tracked in this HUMAN-UAT file — they surface after `git push origin v*` triggers the publish workflow. Phase 17 closes without a synchronous F5 session; the UAT results drip in over time via `/gsd-verify-work` sessions.

## Task Commits

1. **Task 1: editor-setup docs + finalized VSCode README** — `ec8e18f` (feat)
2. **Task 2: marketplace runbook + top-level README announcement** — `888f432` (docs)
3. **Task 3: manual smoke checklist authored (checkpoint instructions)** — `9d33c90` (docs)
4. **SUMMARY draft (pre-resolution)** — `7026982` (docs)
5. **Task 3 finalization: HUMAN-UAT deferral + plan close** — TBD (this commit, `docs(17-08): finalize plan — HUMAN-UAT deferral for manual smoke`)

## Files Created/Modified

### Created (9)

- `docs/editor-setup/README.md` — landing page
- `docs/editor-setup/nvim-lspconfig.lua` — raw Neovim snippet
- `docs/editor-setup/helix-languages.toml` — raw Helix snippet
- `docs/editor-setup/neovim.md` — Neovim guide
- `docs/editor-setup/helix.md` — Helix guide
- `docs/editor-setup/generic-lsp.md` — Emacs + Zed + Cursor + Windsurf + others guide
- `docs/editor-setup/manual-smoke.md` — Phase 17 closure checklist
- `.planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md` — one-time setup runbook
- `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` — HUMAN-UAT session file (GSD UAT template; 3 pending tests for rows 1-3)

### Modified (3)

- `vscode-extension/README.md` (32 → 108 lines; finalized for end users)
- `README.md` (added Editor support section)
- `.gitignore` (allowlist for docs/ + README.md)

## Acceptance criteria verification (Tasks 1 + 2)

Verified after each commit:

- Task 1 acceptance (from plan): `test -f docs/editor-setup/nvim-lspconfig.lua && grep -q "filetypes.*flow"` — VERIFIED. `test -f docs/editor-setup/helix-languages.toml && grep -q 'name = "flow"'` — VERIFIED. `test -f docs/editor-setup/README.md && grep -q "dotnet publish"` — VERIFIED. `grep -qE "stdlib .flow files|CopyToOutputDirectory|stdlib files" docs/editor-setup/README.md` — VERIFIED (captures Pitfall 6 awareness). `vscode-extension/README.md` is 108 lines, in the 30–199 bound — VERIFIED. `grep -q "flow.server.path" && grep -q "flow.trace.server" vscode-extension/README.md` — VERIFIED. `python3 -c "import json; json.load(...).publisher"` returns `flow-lang` — VERIFIED (publisher field present, still placeholder — documented in runbook status table).
- Task 2 acceptance (from plan): `test -f 17-MARKETPLACE-SETUP.md && grep -q VSCE_PAT && grep -q OVSX_PAT && grep -q create-namespace && grep -qE "Marketplace \(Publish\)|Marketplace.Publish|Marketplace.Manage"` — VERIFIED. `grep -qE "Rotation|rotate|expir"` — VERIFIED. `! grep -qE "VSCE_PAT=[a-zA-Z0-9]" && ! grep -qE "OVSX_PAT=[a-zA-Z0-9]{10,}"` — VERIFIED (no literal tokens committed).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `.gitignore` blocked `docs/editor-setup/*.md` files**

- **Found during:** Task 1 staging (`git add` rejected `docs/editor-setup/README.md` et al. as ignored).
- **Issue:** The repo's `.gitignore` has `*.md` globally ignored with only `!.planning/**/*.md` and `!CLAUDE.md` exceptions — a deliberate hygiene choice for AI-generated markdown. The new `docs/editor-setup/` markdown files (5 of them) fell under the block.
- **Fix:** Added `!docs/`, `!docs/**/*.md`, and `!README.md` allowlists to `.gitignore` in a commented block distinct from the existing `vscode-extension/` allowlist so the distinction is self-documenting.
- **Files modified:** `.gitignore`.
- **Verification:** `git check-ignore -v docs/editor-setup/README.md` reports the unignore match (`!docs/**/*.md`). `git add docs/editor-setup/` now succeeds with no warnings.
- **Committed in:** `ec8e18f` (bundled into Task 1's commit since it was a same-task prerequisite).

### Orchestrator objective vs plan wording reconciliation

- **Scope expansion (non-deviation, documented):** The plan's Task 1 action section listed `docs/editor-setup/nvim-lspconfig.lua`, `docs/editor-setup/helix-languages.toml`, and `docs/editor-setup/README.md` as required artifacts. The orchestrator's objective additionally required markdown guides (`neovim.md`, `helix.md`) and optionally a `generic-lsp.md`. Both contracts were satisfied: the plan's acceptance grep hits `nvim-lspconfig.lua` / `helix-languages.toml` / `README.md` as the raw snippets; the orchestrator's objective hits `neovim.md` / `helix.md` / `generic-lsp.md` as the prose guides. No deviation — both sets shipped.

### Known Stubs / Placeholders

- `vscode-extension/package.json` `publisher` field remains `flow-lang` as a placeholder. The runbook (`17-MARKETPLACE-SETUP.md` "Prerequisites" section) explicitly flags this and instructs Noah to change it before claiming marketplace identities. Fix path documented; execution deferred to Noah's first actual marketplace publisher signup. This is NOT a stub blocking Phase 17 closure — the CI workflow works with any publisher value (dry-run), and the publisher value can be updated in a single file before the first tag push. Status: INTENTIONAL; resolved by Noah executing Step 1 of the runbook.

## Deferred Issues / HUMAN-UAT Items

### Tracked in 17-HUMAN-UAT.md (pending human verification of Phase 17 artifacts)

Rows 1–3 of `docs/editor-setup/manual-smoke.md` are recorded as 3 explicitly-pending tests in `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md`. They surface in `/gsd-progress` and `/gsd-audit-uat` output and resolve asynchronously whenever Noah runs the Extension Development Host F5 session — Phase 17 closes now and the tests drip in over time, NOT faked as a pass:

- **HUMAN-UAT Test 1 — D-04/D-05 syntax highlighting matches flow-editor/ categories** (11-item tick list) — pending.
- **HUMAN-UAT Test 2 — D-04 TM→semantic token transition (0–300ms window)** — pending.
- **HUMAN-UAT Test 3 — D-13 extension activation + embedded feature sanity** (D-06 diagnostics, D-07 completion, D-08 hover, D-09 go-to-def, D-10 signature help, D-11 note-stream context-aware completion, snippet expansion) — pending.

### Deferred to first release tag milestone (NOT tracked in 17-HUMAN-UAT.md)

The following items literally cannot execute today without VSIX artifacts on the marketplaces — creating a circular dependency (phase closure awaiting tag push awaiting phase closure). They surface when `git push origin v*` triggers the publish workflow:

- **M-04 (D-14) — non-dev OS binary:** install VSIX on macOS or Windows, repeat rows 1–3 of manual-smoke.md. Tracked for first release tag only.
- **M-05 (D-15) — Marketplace + OpenVSX publish success:** verify both listings appear with all 4 per-platform VSIX entries after tag push. Tracked by `17-MARKETPLACE-SETUP.md` Step 4 and its status-checklist.
- **Publisher ID finalization:** Noah chooses and commits the final publisher ID in `vscode-extension/package.json` before Step 2.3 (OpenVSX namespace claim). If the chosen ID differs from the current `flow-lang` placeholder, update the package.json field, re-run the CI dry-run, then execute Step 2.3.
- **Per-platform stdlib copy in dev builds (from plan 17-01's TODO):** the debug flow-lsp binary still hangs per plan 17-07's smoke-script observation. This is orthogonal to Phase 17 closure — the shipped self-contained binary is a different artifact and is expected to handle LSP correctly. If the first tag push's CI smoke step fails for this reason, plan 17-01 gets a follow-up fix-up plan.

## Remaining Blockers to `git tag v1.0.0 && git push origin v1.0.0`

1. Steps 1–4 of `17-MARKETPLACE-SETUP.md` must be executed by Noah — **pending human action** (tracked in the runbook's 17-row status checklist).
2. Publisher ID in `vscode-extension/package.json` must be finalized (or left as `flow-lang` if that is Noah's actual chosen ID) — **pending**.
3. 17-HUMAN-UAT.md rows 1–3 should resolve `pass` before the first release tag — **pending** but NOT a hard blocker (the tag push works on-its-own-CI-machinery; a UAT issue becomes a gap-closure plan after the fact).

Phase 17 closure does NOT require any of these to complete today — the artifacts for each are committed, the runbook captures every remaining human step, the UAT session file is persistent, and the CI workflow is dormant until a `v*` tag is pushed.

## Self-Check: PASSED

All files created/modified are present on disk; all 4 plan commits are reachable in `git log`; 17-HUMAN-UAT.md is authored using the GSD UAT template and surfaces 3 pending tests; Phase17 Fact count at plan close is 96/96 green.

Files:

- `docs/editor-setup/nvim-lspconfig.lua` — FOUND, contains `'flow-lsp'` + `filetypes = { 'flow' }`
- `docs/editor-setup/helix-languages.toml` — FOUND, contains `name = "flow"` + `command = "flow-lsp"`
- `docs/editor-setup/README.md` — FOUND, contains `dotnet publish` + stdlib-files reminder
- `docs/editor-setup/neovim.md` — FOUND
- `docs/editor-setup/helix.md` — FOUND
- `docs/editor-setup/generic-lsp.md` — FOUND
- `docs/editor-setup/manual-smoke.md` — FOUND, covers all 5 manual-verification rows
- `.planning/phases/17-flow-language-server/17-MARKETPLACE-SETUP.md` — FOUND, passes all 8 grep gates (`VSCE_PAT`, `OVSX_PAT`, `create-namespace`, `Marketplace.Manage`, `Rotation`, no `VSCE_PAT=<literal>`, no `OVSX_PAT=<literal>`, `rotate|expir`)
- `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` — FOUND, frontmatter `status: partial`, `phase: 17-flow-language-server`, 3 pending tests (rows 1-3), rows 4-5 deferred via in-file note, `Summary.total=3 passed=0 pending=3`
- `vscode-extension/README.md` — MODIFIED, 108 lines, contains `flow.server.path` + `flow.trace.server` + "Non-VSCode editors" section linking `docs/editor-setup/`
- `README.md` — MODIFIED, contains `VSCode extension` + `docs/editor-setup`
- `.gitignore` — MODIFIED, contains `!docs/` + `!docs/**/*.md` + `!README.md`
- `vscode-extension/package.json` publisher = `flow-lang` (placeholder, documented)

Commits:

- `ec8e18f` (Task 1) — FOUND
- `888f432` (Task 2) — FOUND
- `9d33c90` (Task 3 instructions) — FOUND
- `7026982` (SUMMARY draft) — FOUND
- Finalization commit (HUMAN-UAT deferral + plan close) — landed with this SUMMARY revision

Regression:

- `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase17"` — **96 passed, 0 failed, 0 skipped, ~1s** (unchanged from 17-07 close since 17-08 is docs-only)

Deletion safety:

- No accidental file deletions in any plan 17-08 commit — VERIFIED via `git diff --diff-filter=D --name-only` across all 17-08 commits = empty

## Closeout notes

- Plan 17-08 is the final plan in Phase 17. ROADMAP Phase 17 row advances to 8/8 Complete with this commit.
- Phase 17 itself is ready for `/gsd-verify-work` (or tagged as complete) now that 17-08 closes. The 3 pending HUMAN-UAT tests are persistent and do not block phase-close under the deferral pattern — they resolve asynchronously in a subsequent `/gsd-verify-work` session.
- First release tag push (`git tag v0.1.0 && git push origin v0.1.0`) remains a separate human-action event gated by the 17-MARKETPLACE-SETUP.md runbook steps, the publisher-ID finalization, and a green rows 1–3 in 17-HUMAN-UAT.md.

---

*Phase: 17-flow-language-server*
*Plan: 08*
*Authored: 2026-04-20*
*Status: complete (with HUMAN-UAT deferred — rows 1-3 pending in 17-HUMAN-UAT.md, rows 4-5 deferred to first release tag)*
