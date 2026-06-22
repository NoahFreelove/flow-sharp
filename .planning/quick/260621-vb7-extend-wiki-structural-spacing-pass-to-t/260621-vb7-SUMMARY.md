---
quick_id: 260621-vb7
title: Extend wiki structural/spacing pass to the remaining 25 pages
date: 2026-06-22
status: complete
---

# Quick Task 260621-vb7 — Summary

## Goal
Extend the Home.md structural/spacing rework (task 260621-na4) to the other 25 wiki pages.

## Key finding — the premise didn't hold
A read-only survey (Explore agent) + independent mechanical checks (Python/awk scans for
double-blank-runs and mixed loose/tight list spacing) found the **other 25 pages were already
clean and consistent**:

- **Tight lists everywhere** — `loose=0` on all 25 pages (verified via per-file scan). Home.md
  was the only page with the mixed loose/tight defect.
- **Zero double-blank-line runs** on any of the 25.
- The large "lists" that looked like flat-dump candidates are actually **markdown tables**
  (synth/chord-quality/module/transform reference tables), **ToC link lists** (Examples.md's
  48-item table of contents — correctly left flat), or already broken into `###` subsections
  (Tips-and-Tricks "Common Pitfalls"). None resembled Home.md's old 40-item flat bullet dump.

So there was no structural defect to repair on those pages, and the Phase-na4 CSS fix already
restored list/paragraph spacing + bullet markers for all of them on the flow-site.

## What changed
The only real inconsistency was one I introduced in na4: **Home.md was loose-spaced** (blank
line between bullets) while the other 25 are tight. Per owner decision (2026-06-22), reconciled
by making **Home.md tight** to match the rest (smallest, cleanest change; the CSS fix already
gives every page breathing room on the site):

- Removed 22 inter-bullet blank lines in Home.md's Key Features section (loose → tight),
  **keeping** the 5 thematic `###` subsections from na4.
- Removed 1 pre-existing double-blank-line before `## Wiki Pages`.
- Content integrity: 27 Key Features bullets retained verbatim; diff is whitespace-only
  (23 blank-line deletions).

Net wiki state: all 26 pages now use consistent tight lists, no double blanks.

## Verification
- `double-blank-runs=0`, `loose-list-transitions=0` on Home.md post-edit.
- Key Features bullet count = 27 (unchanged).
- `git diff --stat`: `wiki/Home.md | 23 deletions` (no insertions → no content altered).

## Commits
- (code) make Home.md lists tight for wiki-wide consistency

## Notes / no-ops
- No edits to the other 25 wiki pages — verified already clean (evidence above).
- No worktree isolation (stale-base issue from na4); ran on the clean `dev` tree.
