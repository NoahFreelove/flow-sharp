---
phase: 260502-lhm-setup-github-wiki-sync-workflow
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - wiki/Home.md
  - .github/workflows/wiki-sync.yml
autonomous: true
requirements:
  - WIKI-SYNC-01
user_setup:
  - service: github-wiki
    why: "The <repo>.wiki.git remote does not exist until the GitHub wiki feature is enabled and at least one page is created via the UI. The workflow will fail to push until this is done."
    dashboard_config:
      - task: "Enable Wikis under repo Settings -> Features -> Wikis (checkbox)."
        location: "GitHub repo Settings -> General -> Features"
      - task: "Create at least one page in the wiki via the GitHub web UI to initialize the <repo>.wiki.git remote (e.g., create a Home page with any placeholder text and save). After the first manual page exists, future runs of this workflow will overwrite it from wiki/Home.md."
        location: "GitHub repo Wiki tab -> Create the first page"

must_haves:
  truths:
    - "wiki/Home.md exists at the repo root and contains a minimal placeholder."
    - "A GitHub Actions workflow file exists at .github/workflows/wiki-sync.yml."
    - "The workflow triggers ONLY on workflow_dispatch (no push, no schedule)."
    - "The workflow uses ${{ secrets.GITHUB_TOKEN }} (no PAT)."
    - "The workflow uses a third-party wiki sync action pinned to a specific tagged version (not @main / @master)."
    - "The workflow grants contents: write permission and runs on ubuntu-latest."
    - "Manual run of the workflow from the Actions tab pushes the contents of wiki/ to <owner>/<repo>.wiki.git (assuming the wiki has been initialized once via the UI)."
  artifacts:
    - path: "wiki/Home.md"
      provides: "Seed page for the GitHub wiki"
      min_lines: 2
    - path: ".github/workflows/wiki-sync.yml"
      provides: "One-way sync workflow from ./wiki/ to GitHub wiki repo"
      contains: "workflow_dispatch"
  key_links:
    - from: ".github/workflows/wiki-sync.yml"
      to: "wiki/"
      via: "action input pointing the sync action at the ./wiki/ directory"
      pattern: "wiki"
    - from: ".github/workflows/wiki-sync.yml"
      to: "GITHUB_TOKEN"
      via: "secrets.GITHUB_TOKEN passed as the auth token to the sync action"
      pattern: "secrets\\.GITHUB_TOKEN"
---

<objective>
Add a one-way sync from the in-repo `./wiki/` directory to the GitHub wiki
repository (`<owner>/<repo>.wiki.git`) via a manually-triggered GitHub Actions
workflow.

Purpose: Allow wiki content to be authored, version-controlled, and reviewed
in the main repo, then published to the GitHub wiki on demand without giving
up `git` workflow ergonomics.

Output:
- `wiki/Home.md` — minimal placeholder seed page
- `.github/workflows/wiki-sync.yml` — manual (`workflow_dispatch`) sync workflow
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md

# Existing workflow directory (do NOT modify the existing publish-extension.yml — only ADD wiki-sync.yml alongside it)
# Path: .github/workflows/publish-extension.yml (sibling, untouched)

# Empty seed directory already exists
# Path: wiki/ (currently empty — Home.md will be the first file)
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create wiki seed page and wiki-sync workflow</name>
  <files>wiki/Home.md, .github/workflows/wiki-sync.yml</files>
  <action>
Create two files. Both are purely additive — no existing files are modified.

**File 1: `wiki/Home.md`**

Write a minimal placeholder page. Exactly this content (one heading + one short sentence — do not expand into a real docs page):

```markdown
# Flow Language Wiki

Wiki content for the Flow language project. Pages are authored in the `wiki/` directory of the main repo and synced here via the `wiki-sync` GitHub Actions workflow.
```

**File 2: `.github/workflows/wiki-sync.yml`**

Create a GitHub Actions workflow with exactly the structure below. Notes:

- Trigger: `workflow_dispatch` ONLY. Do NOT add `push:` or `schedule:` triggers.
- Runner: `ubuntu-latest`.
- Permissions: `contents: write` at the workflow level (the sync action needs to push to the wiki repo, which lives under the same repository auth scope as `GITHUB_TOKEN`).
- Auth: pass `${{ secrets.GITHUB_TOKEN }}` — do NOT introduce a PAT input.
- Third-party action: use `Andrew-Chen-Wang/github-wiki-action` pinned to tag `v4` (a stable, widely-used release of this action — NOT `@main` and NOT `@master`).
- Source path: `./wiki/` (matches the `path` input expected by the action).
- Strategy: full one-way mirror — wiki content is overwritten on each manual run.

Exact file contents:

```yaml
name: Sync wiki/ to GitHub Wiki

# Manual trigger only. Per user decision, no push or schedule triggers.
on:
  workflow_dispatch:

permissions:
  contents: write

jobs:
  sync-wiki:
    name: Push ./wiki to <repo>.wiki.git
    runs-on: ubuntu-latest
    steps:
      - name: Check out main repo
        uses: actions/checkout@v4

      - name: Sync wiki/ to GitHub Wiki
        # NOTE: The GitHub wiki for this repo MUST be enabled in
        # Settings -> Features -> Wikis AND have at least one page created
        # via the web UI before this workflow can succeed. The
        # <repo>.wiki.git remote does not exist until then.
        uses: Andrew-Chen-Wang/github-wiki-action@v4
        with:
          path: wiki/
          token: ${{ secrets.GITHUB_TOKEN }}
```

Implementation steps:

1. Create the `wiki/Home.md` file with the exact content above.
2. Create the `.github/workflows/wiki-sync.yml` file with the exact content above.
3. Do NOT modify `.github/workflows/publish-extension.yml` or any other existing file.
4. Do NOT add a README badge, do NOT update CLAUDE.md, do NOT add any other workflow.
  </action>
  <verify>
    <automated>test -f wiki/Home.md && test -f .github/workflows/wiki-sync.yml && grep -q "^# Flow Language Wiki" wiki/Home.md && grep -q "workflow_dispatch:" .github/workflows/wiki-sync.yml && ! grep -qE "^[[:space:]]*push:" .github/workflows/wiki-sync.yml && ! grep -qE "^[[:space:]]*schedule:" .github/workflows/wiki-sync.yml && grep -q "Andrew-Chen-Wang/github-wiki-action@v4" .github/workflows/wiki-sync.yml && grep -q "secrets.GITHUB_TOKEN" .github/workflows/wiki-sync.yml && grep -q "contents: write" .github/workflows/wiki-sync.yml && grep -q "runs-on: ubuntu-latest" .github/workflows/wiki-sync.yml && ! grep -qE "@(main|master)" .github/workflows/wiki-sync.yml && echo "VERIFY OK"</automated>
  </verify>
  <done>
    - `wiki/Home.md` exists with the minimal placeholder heading and sentence.
    - `.github/workflows/wiki-sync.yml` exists with `workflow_dispatch`-only trigger, `contents: write` permission, `ubuntu-latest` runner, `actions/checkout@v4`, and `Andrew-Chen-Wang/github-wiki-action@v4` configured with `path: wiki/` and `token: ${{ secrets.GITHUB_TOKEN }}`.
    - No `push:` or `schedule:` triggers present in the workflow.
    - No third-party action pinned to `@main` or `@master`.
    - No other files were modified.
  </done>
</task>

</tasks>

<verification>
After execution, the following must all be true:

1. `wiki/Home.md` exists and starts with `# Flow Language Wiki`.
2. `.github/workflows/wiki-sync.yml` exists and:
   - Has `on: workflow_dispatch:` and no other triggers.
   - Has `permissions: contents: write`.
   - Uses `runs-on: ubuntu-latest`.
   - Uses `actions/checkout@v4`.
   - Uses `Andrew-Chen-Wang/github-wiki-action@v4` (pinned tag).
   - Passes `${{ secrets.GITHUB_TOKEN }}` as `token` input.
   - Passes `wiki/` as `path` input.
3. No previously existing files (including `.github/workflows/publish-extension.yml`) are modified.
4. `git status` shows only the two new files as additions.

Run the verify command from the task. Visual confirmation in the GitHub
Actions UI is deferred to the user (the workflow appears under the Actions
tab and can be run via "Run workflow" — but actual execution requires the
wiki to have been initialized first, which is a user-side prerequisite).
</verification>

<success_criteria>
- Both files exist with the exact specified content.
- The workflow is syntactically valid YAML and parses cleanly (basic shape verified by grep checks).
- No scope creep: no badges, no README edits, no extra workflows, no extra wiki pages.
- The user has been told (via this plan's `user_setup` and the workflow's inline comment) that they must enable + initialize the GitHub wiki via the UI before the first successful run.
</success_criteria>

<output>
After completion, create `.planning/quick/260502-lhm-setup-github-wiki-sync-workflow/260502-lhm-SUMMARY.md` capturing:
- Files added (with paths)
- The pinned action version used
- The `user_setup` reminder about enabling + initializing the wiki via the GitHub UI before the first workflow run
</output>
