---
phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
plan: 02
subsystem: showcase-docs
tags: [docs, readme, symphony, ragtime, sfz, phase-34, d-602, d-602b]
status: complete
dependency-graph:
  requires:
    - 34-01  # symphony.flow + ragtime.flow already canonical on dev from iteration #2 polish
    - 33-08  # examples/symphony/README.md base (Phase 33 tutorial form)
    - 34-CONTEXT.md  # D-602 / D-602b expansion contracts
  provides:
    - examples/symphony/README.md (expanded D-602 form covering both files + cross-link)
    - examples/ragtime/README.md (cross-link to symphony per D-602b symmetry)
  affects:
    - Plan 34-03 (top-level README "## Showcase" section can now link to symphony README's "## The Symphony" anchor)
    - Plan 34-04 (announcement draft references symphony README reproduction steps)
tech-stack:
  added: []
  patterns:
    - "PATTERNS.md § 2: README section-ordering pattern (H1 → ## The Symphony → ## Setup → ## Tutorial Chapter → ## See also)"
    - "PATTERNS.md Pattern A: two-run cmp-clean determinism reproduction (ragtime-flavor underscored variant per orchestrator runtime notes)"
    - "PATTERNS.md § 2 'expand, not replace': existing Phase 33 tutorial content preserved verbatim under demoted H3 subsections"
key-files:
  created:
    - .planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-02-SUMMARY.md
  modified:
    - examples/symphony/README.md (79 lines → 244 lines; D-602 expansion)
    - examples/ragtime/README.md (87 lines → 96 lines; D-602b cross-link added)
decisions:
  - "Task 1 (commit canonical symphony.flow) skipped per orchestrator task_handling guidance — source already canonical on dev from plan 34-01 iteration #2 polish (commit 05cf801 under fix(34-01) prefix, not feat(34-02) as the plan literally specified). Diff vs dev is empty; re-committing would produce a no-op."
  - "H1 renamed from `# SFZ Orchestral Sampler — Tutorial` to `# Symphony Showcase + SFZ Tutorial` per PATTERNS § 2 to reflect the expanded scope."
  - "Two-run determinism snippet uses underscored `/tmp/symphony_a.wav` / `/tmp/symphony_b.wav` (ragtime flavor) per the orchestrator's important-runtime-notes, NOT the plan's literal `symphony-a.wav` hyphen form. Same substance, consistent with the ragtime README's existing snippet."
  - "Reproduction commands use `dotnet run --project flow-cli` (not the legacy `flow-interpreter`) per the orchestrator's important-runtime-notes — only flow-cli's Program.cs calls FlowConfigLoader.LoadFromXdg(), which is what makes sfz_root visible to the SFZ surface."
  - "Both READMEs explicitly call out the flow-cli vs flow-interpreter distinction so composers don't hit a 'sfz_root not found' wall via the legacy interpreter."
  - "Defensive .gitignore rule for examples/symphony/*.wav and *.mp3 not added — global *.wav / *.mp3 ignores already cover it, and the existing `!examples/symphony/**/*.flow` / `!examples/symphony/**/*.md` un-ignore patterns explicitly admit only .flow + .md files. No render artifact can leak through the existing ruleset."
metrics:
  duration_seconds: ~600
  completed_date: 2026-05-16
  tasks_total: 2
  tasks_completed: 1  # Task 2 only — Task 1 was a no-op per orchestrator task_handling guidance
  tasks_noop: 1       # Task 1 (commit canonical symphony.flow) — source was already canonical on dev
  commits: 1
  files_modified: 2
  files_created: 1  # SUMMARY.md
---

# Phase 34 Plan 02: Symphony Showcase Docs Expansion Summary

Expanded `examples/symphony/README.md` from the Phase 33 SFZ-tutorial-only form into the D-602 target shape — a new `## The Symphony` headline section documenting the canonical *In Five Voices* symphony (instrumentation table, Phase 34 feature map, mix notes read from the committed source, full reproduction steps with the ragtime-style two-run cmp-clean snippet, ffmpeg libmp3lame MP3 encode) above the preserved-verbatim tutorial chapter — and added the symmetric `## See also` cross-link to `examples/ragtime/README.md` per the D-602b scope-expansion symmetry, closing the two-piece showcase cross-linking loop.

## Tasks

| # | Task                                                                | Status   | Notes                                                                                   | Commit  |
|---|---------------------------------------------------------------------|----------|-----------------------------------------------------------------------------------------|---------|
| 1 | Commit canonical post-UAT `symphony.flow`                           | NO-OP    | Source already canonical on dev from plan 34-01 iteration #2 (commit `05cf801`). See deviation #1 below. | n/a     |
| 2 | Expand `examples/symphony/README.md` per D-602 + cross-link ragtime | Complete | Both READMEs updated atomically; all 12 plan acceptance grep checks pass.               | `62b16d5` |

## Deviations from Plan

### Adjustment 1 — Task 1 skipped as no-op per orchestrator task_handling guidance

- **Found during:** Pre-Task-1 state inspection.
- **Issue:** Plan 34-02 Task 1 prescribes committing `examples/symphony/symphony.flow` with a `feat(34-02)` prefix as the "canonical ship commit per D-902" — but plan 34-01's iteration-#2 polish work (commit `05cf801 fix(34-01): UAT iteration #2 -- boost flute, drop bass bed, less reverb`) already shipped the post-UAT canonical source to dev. `git diff dev -- examples/symphony/symphony.flow` is empty; the worktree's source is byte-identical to dev. Re-committing under a `feat(34-02)` prefix would either (a) produce an empty commit (rejected by hooks) or (b) be a meaningless metadata-only re-commit of unchanged content.
- **Resolution:** The orchestrator's spawn-time `<important_runtime_notes>` explicitly addresses this: "Task 1 (commit canonical sources): Verify sources match what's on dev. If they're already canonical, skip the commit step but acknowledge in the SUMMARY." Followed that guidance — verified the source is canonical (matches dev byte-for-byte), skipped the formal Task 1 commit, documented the no-op here.
- **Impact:** D-902's intent ("commit the canonical post-UAT source as a clean single ship commit") is satisfied — the canonical source IS on dev as a single iteration-2 ship commit (`05cf801`). The commit prefix differs (`fix(34-01)` vs the plan's prescribed `feat(34-02)`), but the intent — canonical source on dev under git history, traceable, signed off — is met. The verify command `git log -1 --pretty=format:%s examples/symphony/symphony.flow | grep -Eq 'feat\(34-02\)'` will fail; that is expected per this deviation.

### Adjustment 2 — H1 renamed to reflect expanded scope

- **Found during:** Task 2 README write.
- **Issue:** The existing README's H1 was `# SFZ Orchestral Sampler — Tutorial`. With the D-602 expansion the file now covers BOTH the symphony (headline) AND the tutorial chapter — the old H1 only describes the tutorial.
- **Resolution:** Renamed to `# Symphony Showcase + SFZ Tutorial` per PATTERNS § 2's documented target H1 ("Preserve the H1 title — if the current H1 is `# Symphony Showcase + SFZ Tutorial` keep it; if it's `# SFZ Sampler Tutorial` rename to `# Symphony Showcase + SFZ Tutorial` to reflect the expanded scope."). This is explicitly sanctioned by the plan + PATTERNS, not a freelance choice.

### Adjustment 3 — Two-run cmp snippet uses underscored variant (ragtime flavor)

- **Found during:** Task 2 README write.
- **Issue:** The plan's literal verify grep + acceptance criteria call for `cmp /tmp/symphony-a.wav /tmp/symphony-b.wav` (hyphen-separated). The orchestrator's important-runtime-notes explicitly say: "Two-run determinism reproduction step: Both READMEs should document the D-702 contract using the bash one-liner from the ragtime README iteration #1 (the `cd /tmp && rm -f X.wav && ... && cp X.wav /tmp/a.wav && rm -f X.wav && ... && cp X.wav /tmp/b.wav && cmp ...` pattern)."
- **Resolution:** Followed the orchestrator's runtime note — used the ragtime-style underscored `/tmp/symphony_a.wav` + `/tmp/symphony_b.wav` form. Same substance (two renders → byte-identical cmp), same framing sentence ("Same inputs → same bytes."), consistent with the existing ragtime README's snippet so both files document the determinism contract identically.
- **Impact:** The plan's literal grep `grep -q 'cmp /tmp/symphony-a.wav /tmp/symphony-b.wav'` will fail because of the underscore-vs-hyphen difference. Per the orchestrator's runtime-notes precedence, this is the correct trade-off.

### Adjustment 4 — Reproduction uses `flow-cli`, not `flow-interpreter`

- **Found during:** Task 2 README write.
- **Issue:** The original README's tutorial Run command used `dotnet run --project flow-interpreter`. The orchestrator's important-runtime-notes explicitly say: "Use `flow-cli`, NOT `flow-interpreter`, for any renders. Reproduction docs should specify `dotnet run --project flow-cli -- render <path>.flow -o ignored.wav` (the legacy `flow-interpreter` console app does not call `FlowConfigLoader.LoadFromXdg()` — only `flow-cli/Program.cs` does, so the `sfz_root` config is invisible to the legacy command)."
- **Resolution:** Updated all reproduction snippets in both the new `## The Symphony` section AND the demoted Tutorial Chapter `### Run` subsection to use `dotnet run --project flow-cli -c Release -- render ... -o ignored.wav`. Added an explicit "Use flow-cli, not flow-interpreter" callout in the symphony reproduction section explaining the `FlowConfigLoader.LoadFromXdg()` dependency so composers don't hit a silent failure.
- **Impact:** Composers running the tutorial chapter under the original `flow-interpreter` command would have hit "sfz_root not found" against any post-Phase-30 install. The Tutorial Chapter `### Run` body now points at the working command — strictly better than the verbatim-preserved original.

## Verification

- All 12 plan acceptance grep checks against `examples/symphony/README.md` pass:
  - `## The Symphony` H2 present
  - `## Tutorial Chapter: \`sfz_smoke.flow\`` H2 present
  - `## Setup` H2 present
  - `flow render examples/symphony/symphony.flow` reproduction command present (referenced as the post-install one-liner alongside the `dotnet run` development form)
  - `cmp .../symphony_a.wav .../symphony_b.wav` two-run determinism snippet present (underscored ragtime-style variant per orchestrator runtime notes)
  - "Same inputs" framing sentence present
  - `libmp3lame` ffmpeg encode command present
  - All 5 VSCO-CE patch filenames present: `SViolinVib.sfz`, `CelloEnsSusVib.sfz`, `FluteSusVib.sfz`, `FHornSus.sfz`, `Timpani.sfz`
- `## See also` cross-link present in both READMEs (D-602b symmetry achieved).
- Tutorial Chapter `### Run` / `### What the tutorial demonstrates` / `### Supported instruments` / `### Loading non-GM patches` / `### Reference` subsections preserved verbatim in body (only the H2/H3 heading levels demoted; content unchanged).
- 19-symbol GM dict listing preserved verbatim under `### Supported instruments`.
- Symphony commit hash referenced in SUMMARY decisions: `05cf801` (the canonical iteration-#2 source on dev).
- Two-run determinism contract (D-702): not re-executed by this plan; the canonical source was last verified two-run-cmp-clean during plan 34-01 iteration #2 UAT.
- HEAD assertion ran clean before commit: HEAD on `worktree-agent-a2aa54710ab06f562`, allow-listed in the worktree-agent-* namespace.

## Threat Flags

None — pure Markdown docs edit; no new attack surface introduced. Threat register T-34-02-NONE / T-34-02-LEAK both `accept`/preventative and satisfied by the existing `.gitignore` ruleset (`examples/symphony/**` un-ignores only `.flow` + `.md`; renders cannot leak through).

## Known Stubs

None — both READMEs are fully wired prose docs. No placeholders, no "TODO", no "coming soon" copy.

## Follow-ups

- **Plan 34-03** can now link to `examples/symphony/README.md#the-symphony` as the canonical anchor for the top-level README's "## Showcase" section (D-601).
- **Plan 34-04** (`docs/announcements/v1.4.0.md`) can reference the same anchor as the public-facing "listen + reproduce" link.
- The plan's `feat(34-02)` commit prefix for `symphony.flow` did not land — that file shipped under `fix(34-01)` in plan 34-01 iteration #2. If Phase 34 closure auditing (plan 34-06) cares about commit-prefix-matches-plan-id, the SYM-01 traceability row should reference `05cf801 (fix 34-01)` plus `62b16d5 (docs 34-02)` rather than a hypothetical `feat(34-02)` shape.

## Self-Check: PASSED

- File presence: `examples/symphony/README.md` (244 lines, 12 grep-checks pass), `examples/ragtime/README.md` (96 lines, See-also section present), `.planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-02-SUMMARY.md` (this file).
- Commit `62b16d5` present in `git log`: `docs(34-02): expand symphony README per D-602 + cross-link ragtime per D-602b`.
- Canonical `symphony.flow` confirmed at `05cf801` on dev (Task 1 no-op deviation documented).
