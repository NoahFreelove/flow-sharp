# Portable project audio package — 2026-10-04

P8-11 adds project-directory packaging for saved state and referenced audio.
The overall backend-readiness goal remains active.

## Implemented

- `ProjectPackage.Create` prepares a new sibling staging directory, copies every
  referenced external WAVE through the bounded managed importer, and verifies its
  hash, frame count and rate against the saved reference. Changed or missing files
  fail packaging instead of silently replacing the intended sound.
- Copies use content-addressed relative paths and deduplicate identical files.
  Stable asset IDs remain unchanged, so clip references require no rewriting.
  Inline generated/sampler audio stays in the saved project contents.
- A saved `project.flowproject` plus `audio-assets` directory is published with a
  same-parent directory rename only after preparation succeeds. Existing destination
  files/directories are never overwritten. Failures clean owned staging files.
- The working snapshot/document is not mutated or marked saved. Package paths are
  adjusted only in the exported snapshot. Moving the resulting directory preserves
  relative asset resolution.
- Default published-package budget is 4 GiB, with existing 512 MiB encoded-file and
  256 MiB decoded-asset import limits. Staging/import temporary memory/disk usage may
  exceed final retained sizes. Cancellation is checked during copying/hashing and
  before publication. This is a directory package, not an archive format.

## Verification

The initial packaging/import run passed **13 tests**, including package relocation
with original files removed, verified deduplication, unchanged source references,
changed/missing input rejection, cancellation, byte-budget failure, staging cleanup
and protection of an existing destination. Log: `/tmp/flow-project-package.log`.

A final two-test packaging run additionally verified actual prepared playback from
the moved package after the original source directory was deleted. Log:
`/tmp/flow-project-package-playback.log`. Existing analyzer warnings remain;
`git diff --check` passed. No full-solution run or Web publish was performed.

## Remaining

Script/module dependency collection and version pins, compressed audio formats,
archive export, crash-recovery discovery and broader large-project qualification
remain open. Packaging accepted audio is not a guarantee that regeneration can run
on another machine without its code dependencies. Expanded score construction and
final live/offline/export qualification still need work. Native frontend and
historical callback-gap diagnosis remain deferred; no hardware capture occurred.
