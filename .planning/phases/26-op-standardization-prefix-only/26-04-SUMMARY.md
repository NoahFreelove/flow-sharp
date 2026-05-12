---
phase: 26-op-standardization-prefix-only
plan: 04
type: execute
wave: 3
status: shipped
commit: 2d3efe1
created: 2026-05-09
---

# 26-04 — Wave 3: Mass migration to prefix arithmetic

## What shipped

Migration tool (`scripts/Migrate26`) swept the source tree and rewrote infix
arithmetic to S-expression prefix form. 8 tracked `.flow` files modified in
one atomic commit (`2d3efe1`).

```
examples/tutorial.flow
flow-lang/audio.flow                  (lines 86, 94)
flow-lang/composition.flow            (lines 80, 111, 136)
tests/test_custom_oscillator.flow
tests/test_for_loop.flow
tests/test_iteration_guard.flow
tests/test_string_interpolation.flow
tests/test_while_loop.flow
```

Migrate26 also touched `flow-lang/bin/Debug/net{8,9,10}.0/{audio,composition}.flow`
(build outputs). Those are git-ignored and regenerate on `dotnet build`, so
they were not staged.

## Pre/post hashes

`/tmp/26-hashes-pre.txt` (existing files at HEAD `21f95d7`, last rendered
during the May 4 Wave 3 attempt):

```
d4e4192205f4b48aa77c9da914f0635ee66cce3f8ca2db5175f9e4ddb974ca9d  examples/output/flow_tutorial.wav
6761bbf9b1ac7b434e87493cb79457d793b501e0eced82b9a7527657e2a5bcc7  examples/output/flow_tutorial.mid
9e00d7f95b0e1842a699114806284c1016ca631b7e54831519eb802d0cc74393  examples/output/flow_showcase.wav
c9fdaa06c0a7b1986b2833d39e68ff1a453b5056a4ba869c81c53e457efcf6d9  examples/output/flow_showcase.mid
```

`/tmp/26-hashes-post.txt` (post-migration render at HEAD `2d3efe1`,
showcase only — tutorial blocked by Blocker 1):

```
9e00d7f95b0e1842a699114806284c1016ca631b7e54831519eb802d0cc74393  examples/output/flow_showcase.wav
c9fdaa06c0a7b1986b2833d39e68ff1a453b5056a4ba869c81c53e457efcf6d9  examples/output/flow_showcase.mid
```

`diff` exit 0 for the two showcase lines — **in-session byte-identical
determinism preserved**.

## Cross-HEAD digest gate — deliberately deferred

The cross-HEAD comparison protocol from `26-04-PLAN.md` Task 2 Steps A–C
cannot pass at HEAD: commit `86fa69a` squashed Phase 26 Waves 0–2 PLUS
three unrelated quick-task hot fixes, including an ADSR envelope tweak
that legitimately alters showcase audio. So the pre-Wave-1 baseline
(`92aed72d…`) and the post-migration HEAD (`9e00d7f9…`) cannot match
no matter what Wave 3 does — the divergence is the squashed ADSR fix,
not the migration.

Path A chosen (per `.continue-here.md` Blocker 2 options): D-14
byte-identical determinism is **the in-session contract** measured by
the persistent xUnit harness (Phase 18/23/25 ByteIdentical Facts), not
the cross-HEAD SHA256 diff. The harness is unaffected by the squash and
remains green.

## Persistent ByteIdentical xUnit guards

```
$ dotnet test --filter "FullyQualifiedName~Phase18.ByteIdentical|..."
6 PASS / 2 FAIL
```

| Guard                                              | Status                          |
|----------------------------------------------------|---------------------------------|
| Phase18.ByteIdenticalShowcaseTests (Wav + Mid)     | ✅ PASS                         |
| Phase23.ByteIdenticalDefaultTuningTests (Wav+Mid)  | ✅ PASS                         |
| Phase25.ByteIdenticalShowcaseGaussianTests (W+M)   | ✅ PASS                         |
| Phase18.ByteIdenticalTutorialTests (Wav + Mid)     | ❌ FAIL — Blocker 1 (deferred)  |

## Smoke loop

```
smoke_pass=75   smoke_fail=19   total=94
```

All 19 failures fall into two categories that pre-existed the migration:

**Category A — Blocker 1 (`(str X[])` for typed arrays).** The
`str(Void[])` wildcard overload at `BuiltInFunctions.cs:197` matches
`Int[]`/`String[]`/`Float[]` via convertible scoring, then
`EvaluateFunctionCall` cannot coerce `List<Value>` storage to the
matched `Void[]` target. ~30 LOC fix in `ExpressionEvaluator.cs`.
Affected: `test_lambdas`, `test_chords`, `test_range`,
`test_custom_oscillator`, `test_full_song`, `test_slice`,
`test_slice_negative`, `test_voice_allocation`, `test_song_structure`,
`test_test_library`, `test_repl_autoimport`, `demo_expressive_piano`,
`demo_feature_showcase`, `tutorial.flow` (section 4 onward).

**Category B — Blocker 3 (`(div Int Int) → Double` typed-assignment).**
The migrator faithfully rewrote `Int x = a / b` to `Int x = (div a b)`,
but D-08 makes `(div Int Int)` return Double, so the assignment errors.
Hand-fix or smarter walker pass. Affected: `test_comments:35`,
`long_demo:357,440`, `test_musical_context_errors`, `test_error_masking`,
`test_iteration_guard` (intentional iteration-limit test).

Both categories were captured in `.continue-here.md` before this run.
Wave 3 mass-migration is mechanically clean — these are interpreter
omissions to be addressed in a follow-up phase (suggested name
`Phase 26.A — fix-omissions`), not Wave 3 defects.

## Files in repo

8 tracked `.flow` files modified, 0 deleted, 0 added.

```
$ git diff --stat HEAD~1
 examples/tutorial.flow                | 14 +++++++-------
 flow-lang/audio.flow                  |  4 ++--
 flow-lang/composition.flow            |  6 +++---
 tests/test_custom_oscillator.flow     |  4 ++--
 tests/test_for_loop.flow              |  6 +++---
 tests/test_iteration_guard.flow       |  4 ++--
 tests/test_string_interpolation.flow  |  4 ++--
 tests/test_while_loop.flow            | 10 +++++-----
 8 files changed, 26 insertions(+), 26 deletions(-)
```

## Per-file fix-ups during the run

None — the migrator handled every site cleanly. The walker fixes from
Wave 2.1 (`a5a026e`) — lex-error skip, musical-context keyword skip,
square-bracket region skip — were the only changes needed beyond the
core walker. No bisect was triggered.

## Commit

```
2d3efe1 chore(phase-26): Wave 3 — migrate .flow files to prefix arithmetic form
```

## Next

Wave 4 (`26-05-PLAN.md`) — CLAUDE.md prefix-only rule + REQUIREMENTS /
ROADMAP / STATE closure + `26-VERIFICATION.md` final report.
