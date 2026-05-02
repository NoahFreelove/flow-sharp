---
phase: 260502-lhm-setup-github-wiki-sync-workflow
plan: 01
subsystem: ci
tags: [ci, github-actions, wiki, docs-pipeline]
requires: []
provides:
  - manual GitHub Actions workflow that mirrors ./wiki/ to <owner>/<repo>.wiki.git on demand
affects:
  - .github/workflows/ (new sibling workflow alongside publish-extension.yml)
  - GitHub repository settings (user must enable Wikis + create one initial page)
tech-stack-added: []
patterns:
  - One-way docs mirror via third-party action pinned to a tagged release
  - Manual-only CI (workflow_dispatch) for human-gated publishing
key-files-created:
  - .github/workflows/wiki-sync.yml
key-files-modified: []
decisions:
  - "Pin Andrew-Chen-Wang/github-wiki-action to v4 (tagged release) rather than @main / @master to keep CI deterministic and supply-chain-safe."
  - "Use the built-in GITHUB_TOKEN with contents: write (sufficient for the same-account .wiki.git remote) instead of provisioning a PAT."
  - "Trigger only on workflow_dispatch — no push, no schedule — so wiki publication is a deliberate human action."
  - "Preserve existing wiki/Home.md (94-line index of 26 wiki pages) instead of overwriting it with the placeholder the plan prescribed; the existing file already satisfies the plan's verify check."
metrics:
  duration_seconds: 70
  tasks_completed: 1
  files_touched: 1
  completed_date: 2026-05-02
---

# Quick Task 260502-lhm: Setup GitHub Wiki Sync Workflow Summary

Added a manually-triggered GitHub Actions workflow that mirrors `./wiki/` in the main repo to the `<owner>/<repo>.wiki.git` remote, using the pinned third-party action `Andrew-Chen-Wang/github-wiki-action@v4` and the built-in `GITHUB_TOKEN`.

## Files Added

| Path | Lines | Purpose |
|------|-------|---------|
| `.github/workflows/wiki-sync.yml` | 26 | `workflow_dispatch`-only workflow that pushes `./wiki/` to the GitHub wiki repo |

## Files Left Unchanged (deliberately)

| Path | Reason |
|------|--------|
| `wiki/Home.md` | Already exists in git as a 94-line index of all 26 wiki pages. Already matches the plan's verify grep (`^# Flow Language Wiki`). Overwriting it with the plan's 2-line placeholder would have destroyed authored content. See **Deviations** below. |
| `.github/workflows/publish-extension.yml` | Untouched per plan instructions. |

## Workflow Configuration

| Field | Value |
|-------|-------|
| Trigger | `workflow_dispatch` only (no push, no schedule) |
| Runner | `ubuntu-latest` |
| Permissions | `contents: write` (workflow level) |
| Auth | `${{ secrets.GITHUB_TOKEN }}` (no PAT) |
| Sync action | `Andrew-Chen-Wang/github-wiki-action@v4` (pinned tag) |
| Source path | `wiki/` |
| Checkout action | `actions/checkout@v4` |

## Pinned Action Version

`Andrew-Chen-Wang/github-wiki-action@v4` — chosen over `@main` / `@master` to keep the CI run deterministic and reduce supply-chain risk. The `v4` tag is a stable, widely-used release of this action.

## User Setup Reminder (REQUIRED before first run)

The `<owner>/<repo>.wiki.git` remote does not exist until the GitHub wiki feature is initialized. Before the workflow can succeed:

1. **Enable Wikis** under repo `Settings -> General -> Features -> Wikis` (checkbox).
2. **Create at least one wiki page** via the GitHub web UI (e.g., a Home page with any placeholder text — it can be a single character; the workflow will overwrite it on the first manual run).
3. After step 2, the `.wiki.git` remote exists. From that point forward, manual runs of the **Sync wiki/ to GitHub Wiki** action (Actions tab -> select workflow -> Run workflow) will mirror `wiki/` into the GitHub wiki.

This reminder is also embedded as a comment inside the workflow YAML itself.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Did not overwrite existing wiki/Home.md**

- **Found during:** Task 1 setup, before any writes.
- **Issue:** The plan's `<context>` block stated `wiki/ (currently empty — Home.md will be the first file)`, and Task 1 prescribed writing a 2-line placeholder to `wiki/Home.md` as `purely additive — no existing files are modified`. In reality, `wiki/Home.md` already exists in the tracked tree as a 94-line index page (line 1: `# Flow Language Wiki`), and `wiki/` contains 26 authored wiki pages, all tracked in git at the base commit (`28695f6`). Writing the plan's placeholder would have replaced 92 lines of authored documentation with a stub — a destructive change disguised as an "additive" one.
- **Fix:** Left `wiki/Home.md` unchanged. The existing file already satisfies every plan-level requirement:
  - `must_haves.truths`: "wiki/Home.md exists at the repo root and contains a minimal placeholder" — satisfied (exists, contains content well above the minimum).
  - `must_haves.artifacts.wiki/Home.md` (`min_lines: 2`) — satisfied (94 lines).
  - `<verification>` step 1 (`exists and starts with # Flow Language Wiki`) — satisfied.
  - Task 1 `<verify>` automated grep (`grep -q "^# Flow Language Wiki" wiki/Home.md`) — passes against the existing file.
- **Files modified:** none (preserved existing file)
- **Commit:** n/a (no change made)

### Plan Followed Exactly Otherwise

The workflow file (`.github/workflows/wiki-sync.yml`) was created byte-for-byte as specified in the plan's exact-content block.

## Verification

The plan's automated verify command was run from the repo root and returned `VERIFY OK`:

```
test -f wiki/Home.md \
  && test -f .github/workflows/wiki-sync.yml \
  && grep -q "^# Flow Language Wiki" wiki/Home.md \
  && grep -q "workflow_dispatch:" .github/workflows/wiki-sync.yml \
  && ! grep -qE "^[[:space:]]*push:" .github/workflows/wiki-sync.yml \
  && ! grep -qE "^[[:space:]]*schedule:" .github/workflows/wiki-sync.yml \
  && grep -q "Andrew-Chen-Wang/github-wiki-action@v4" .github/workflows/wiki-sync.yml \
  && grep -q "secrets.GITHUB_TOKEN" .github/workflows/wiki-sync.yml \
  && grep -q "contents: write" .github/workflows/wiki-sync.yml \
  && grep -q "runs-on: ubuntu-latest" .github/workflows/wiki-sync.yml \
  && ! grep -qE "@(main|master)" .github/workflows/wiki-sync.yml
=> VERIFY OK
```

`git status` after the commit shows a clean tree (no untracked, no modified). `publish-extension.yml` was not touched (`git diff` shows no changes).

Visual confirmation of the workflow appearing in the Actions tab and a successful manual run is deferred to the user (it depends on the user-setup steps above).

## Commits

| Hash | Message |
|------|---------|
| `771bd2d` | `feat(260502-lhm): add manual wiki-sync GitHub Actions workflow` |

## Self-Check: PASSED

- File `.github/workflows/wiki-sync.yml`: FOUND
- File `wiki/Home.md`: FOUND (preserved, not overwritten)
- Commit `771bd2d`: FOUND in `git log`
- Plan verify command: PASS (all 11 checks green)
- Working tree: clean after commit
- `publish-extension.yml`: unmodified (no diff)
