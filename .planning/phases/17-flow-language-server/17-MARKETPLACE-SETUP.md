# Phase 17 — Marketplace + OpenVSX Setup Runbook

**Status:** Required before first tag push (e.g., `v1.0.0` or `v0.1.0`)
triggers the CI publish workflow at `.github/workflows/publish-extension.yml`.
**Owner:** Noah (only the developer with marketplace credentials can execute).
**Frequency:** One-time setup per marketplace. Rotate PATs annually.

This runbook documents the **human-only** steps that the CI workflow in
plan 17-07 (`publish-extension.yml`) cannot perform on its own. The CI
handles building the 4 per-platform VSIXs and uploading them; a human
must first create publisher accounts, mint Personal Access Tokens
(PATs), claim the OpenVSX namespace, and register both PATs as GitHub
Actions secrets.

## Prerequisites

- GitHub repository `flow-sharp` with Actions enabled.
- `.github/workflows/publish-extension.yml` committed (plan 17-07).
- Chosen publisher ID — this is the `publisher` field in
  `vscode-extension/package.json`. Current value: `flow-lang`
  (placeholder). Must be unique on **both** marketplaces. Check
  availability before committing:
  - VSCode Marketplace: browse https://marketplace.visualstudio.com and
    search for the desired publisher display name; if no hit, the
    publisher ID is likely available.
  - OpenVSX: run `npx ovsx get <id>` — a 404 means available.

If you choose a different publisher ID, update `vscode-extension/package.json`
`publisher` field and re-run the CI dry-run (Step 3 below) before claiming
identities on the marketplaces.

## Step 1 — VSCode Marketplace publisher + `VSCE_PAT`

1. **Sign in to Azure DevOps** with a Microsoft account:
   https://dev.azure.com/ (free, no credit card required).
2. **Create a publisher** at https://marketplace.visualstudio.com/manage
   - Publisher ID: matches the `publisher` field in
     `vscode-extension/package.json`.
   - Display name: "Flow Language" (or similar; user-visible).
   - Contact email: yours.
   - Agree to the Marketplace publisher agreement.
3. **Generate a Personal Access Token (PAT):**
   - In Azure DevOps, click your profile (top-right) → **User settings**
     → **Personal access tokens**.
   - **New Token:**
     - Name: `flow-sharp vsce publish`
     - Organization: **All accessible organizations**
     - Expiration: **1 year** (the maximum; set a calendar reminder
       for rotation 2 months in advance).
     - Scopes: **Custom defined** → **Marketplace** → **Manage**
       (the "Manage" scope includes Publish; VSCE_PAT needs publish
       capability). Some docs refer to this as `Marketplace (Publish)`;
       in the Azure DevOps UI it appears as `Marketplace.Manage`.
   - Click **Create** and **copy the token immediately**. Azure DevOps
     only shows the token once.
4. **Add the token to GitHub Actions secrets:**
   - Navigate to
     `https://github.com/noah-freelove/flow-sharp/settings/secrets/actions`
     → **New repository secret**.
   - Name: `VSCE_PAT`
   - Value: (the token copied in step 3)
5. **Optional local dry-run** (recommended before first tag push):
   ```bash
   cd vscode-extension
   npx vsce login <publisher-id>    # prompts for the PAT
   npx vsce package --target linux-x64 -o /tmp/flow-dryrun.vsix
   # This packages but does NOT publish — confirms the publisher is
   # recognized and the manifest is valid.
   ```

## Step 2 — OpenVSX publisher + namespace claim + `OVSX_PAT`

**Critical (Pitfall 8):** OpenVSX does NOT auto-create the namespace
from a PAT alone. The first-time publish will fail with
`namespace '<publisher-id>' does not exist` unless you claim the
namespace first via `ovsx create-namespace`.

1. **Sign in** at https://open-vsx.org (GitHub OAuth — click
   "Log In" → "Log in with GitHub").
2. **Generate a PAT:**
   - Visit https://open-vsx.org/user-settings/tokens
   - Click **Generate new token**.
   - Description: `flow-sharp ovsx publish`
   - Copy the token immediately (shown once).
3. **Claim the namespace** (one-time per publisher, closes Pitfall 8):
   ```bash
   export OVSX_PAT='<the-token-from-step-2>'
   npx ovsx create-namespace <publisher-id> -p "$OVSX_PAT"
   ```
   - `<publisher-id>` **must match** the `publisher` field in
     `vscode-extension/package.json` AND the VSCode Marketplace
     publisher ID.
   - Expected output: `Created namespace <publisher-id>`.
   - If you get `namespace already exists`, someone else (or a past
     attempt) already claimed it — pick a different ID and update
     `package.json` first.
4. **Add the token to GitHub Actions secrets:**
   - Navigate to
     `https://github.com/noah-freelove/flow-sharp/settings/secrets/actions`
     → **New repository secret**.
   - Name: `OVSX_PAT`
   - Value: (the token from step 2)

## Step 3 — Smoke-test the workflow before a real tag

Before pushing a real release tag, validate the CI workflow end-to-end
via `workflow_dispatch`:

1. Navigate to
   `https://github.com/noah-freelove/flow-sharp/actions/workflows/publish-extension.yml`
   → **Run workflow** (top-right dropdown).
2. The `build-server` matrix should run across all 4 platforms
   (linux-x64, win-x64, osx-x64, osx-arm64) — expect ~5–10 min per row
   in parallel.
3. The `publish` job **SHOULD skip** on a `workflow_dispatch` without a
   tag (it is gated on `startsWith(github.ref, 'refs/tags/v')`). This
   is the intended behavior — you get a full smoke of the build side
   without touching either marketplace.
4. In the workflow run's **Artifacts** tab you should find 4 files:
   `vsix-linux-x64`, `vsix-win32-x64`, `vsix-darwin-x64`,
   `vsix-darwin-arm64`. Download one and inspect:
   ```bash
   unzip -l vsix-linux-x64.zip
   # Look for:
   #   extension/server/linux-x64/flow-lsp
   #   extension/server/linux-x64/audio.flow   <-- Pitfall 6 gate
   #   extension/server/linux-x64/std.flow
   #   extension/server/linux-x64/collections.flow
   #   extension/server/linux-x64/bars.flow
   #   extension/server/linux-x64/notation.flow
   #   extension/server/linux-x64/composition.flow
   ```
5. Optionally install the VSIX locally in VSCode
   (Extensions panel → `...` menu → **Install from VSIX**) for a final
   manual F5-equivalent smoke (plan 17-08 Task 3).

## Step 4 — First real tag push

Once Steps 1–3 are green and plan 17-08 Task 3 manual smoke is clear:

```bash
# From the repo root:
git tag v0.1.0   # or v1.0.0 for the first stable release
git push origin v0.1.0
```

Watch the Actions run:

- **build-server:** 4 green (one per platform; `fail-fast: true` — any
  failure aborts the others).
- **publish:** 8 green (4 targets × 2 marketplaces; `fail-fast: false`
  — one registry hiccup on one platform does not block the other 7
  uploads). If a publish step fails, re-run that specific failed job
  via the Actions UI after fixing the underlying issue; do not re-run
  the whole workflow.

After the workflow completes, verify the listings:

- VSCode Marketplace:
  `https://marketplace.visualstudio.com/items?itemName=<publisher-id>.flow-language`
- OpenVSX:
  `https://open-vsx.org/extension/<publisher-id>/flow-language`

Both should show all 4 per-platform VSIX versions attached.

## Rotation calendar

- **`VSCE_PAT`** expires after 1 year maximum. Set a calendar reminder
  2 months before expiry. Rotate by repeating Step 1.3 and Step 1.4
  (create a fresh PAT, update the GitHub secret). The old PAT can be
  revoked in Azure DevOps after the new one is confirmed working.
- **`OVSX_PAT`** has no documented hard expiry, but rotate annually
  for hygiene. Revoke the old token at https://open-vsx.org/user-settings/tokens
  once the new one is in GitHub secrets.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `publisher '<id>' not found` on vsce publish | Step 1.2 not completed | Create the publisher on Marketplace |
| `401 Unauthorized` on vsce publish | `VSCE_PAT` missing, expired, or wrong scope | Regenerate PAT with `Marketplace.Manage` scope; update GitHub secret |
| `namespace '<id>' does not exist` on OVSX publish | Step 2.3 not completed | Run `npx ovsx create-namespace <id> -p $OVSX_PAT` once |
| `extension with same version already exists` | `version` field in `package.json` not bumped | Bump version, re-tag |
| Only some platforms published | `fail-fast: false` in the publish job worked as intended | Re-run only the failed matrix rows after fixing the cause |
| VSIX missing `audio.flow` beside the binary | Pitfall 6 gate regressed | Inspect the `Copy stdlib .flow files` step in build-server logs |
| Unverified publisher badge on Marketplace | New publisher accounts are unverified for ~30 days | Wait or request verification from Microsoft |

## Status tracking

Flip each checkbox to `[x]` and add the date as the step completes.
This table is the audit trail that closes D-15's manual prerequisites.

| Step | Owner | Done? | Date |
|------|-------|-------|------|
| 1.1 Sign in to Azure DevOps | Noah | [ ] | — |
| 1.2 Create VSCode Marketplace publisher | Noah | [ ] | — |
| 1.3 Generate `VSCE_PAT` (Marketplace.Manage scope, 1y expiry) | Noah | [ ] | — |
| 1.4 Add `VSCE_PAT` to GitHub Actions secrets | Noah | [ ] | — |
| 1.5 Local `vsce package --target linux-x64` dry-run | Noah | [ ] | — |
| 2.1 Sign in to OpenVSX | Noah | [ ] | — |
| 2.2 Generate `OVSX_PAT` | Noah | [ ] | — |
| 2.3 Claim OpenVSX namespace (`ovsx create-namespace`) | Noah | [ ] | — |
| 2.4 Add `OVSX_PAT` to GitHub Actions secrets | Noah | [ ] | — |
| 3 Dry-run the workflow via `workflow_dispatch` | Noah | [ ] | — |
| 3 Inspect one VSIX artifact for stdlib files (Pitfall 6) | Noah | [ ] | — |
| 3 Install VSIX locally + F5-equivalent smoke (plan 17-08 T3) | Noah | [ ] | — |
| 4 First real tag push (e.g. `git push origin v0.1.0`) | Noah | [ ] | — |
| 4 Verify listing on Marketplace | Noah | [ ] | — |
| 4 Verify listing on OpenVSX | Noah | [ ] | — |
| Rotation Y1: refresh `VSCE_PAT` before expiry | Noah | [ ] | — |
| Rotation Y1: refresh `OVSX_PAT` for hygiene | Noah | [ ] | — |

## Cross-references

- CI workflow that consumes these secrets:
  `.github/workflows/publish-extension.yml` (plan 17-07).
- Pitfall register:
  `.planning/phases/17-flow-language-server/17-RESEARCH.md` §"Common
  Pitfalls" — Pitfall 8 is the OpenVSX namespace claim this runbook
  documents.
- Manual smoke handoff:
  `.planning/phases/17-flow-language-server/17-08-PLAN.md` Task 3 /
  `docs/editor-setup/manual-smoke.md`.
- Environment availability gap (Green-field secrets):
  `.planning/phases/17-flow-language-server/17-RESEARCH.md`
  §"Environment Availability" flagged `VSCE_PAT`, `OVSX_PAT`, and the
  OpenVSX namespace as **missing with no fallback**. This runbook
  closes all three.
