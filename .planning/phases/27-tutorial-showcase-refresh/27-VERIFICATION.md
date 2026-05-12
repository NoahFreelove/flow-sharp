---
phase: 27-tutorial-showcase-refresh
status: passed
phase_name: tutorial-showcase-refresh
shipped: 2026-05-10
nyquist_compliant: true
verification_source: plan-27-05-closure
must_haves_verified: 30
must_haves_total: 30
---

# Phase 27 — Verification (Closure)

**Goal:** `examples/tutorial.flow` and `examples/showcase.flow` demonstrate every v1.3 feature end-to-end with byte-identical determinism preserved; v1.1 + v1.2 chapters preserved; pragma companion files under `examples/pragmas/` cover file-scoped surfaces (`enable hAsB;`, `enable justIntonation;`); CLAUDE.md gains a Music Types Quick Reference table for composer + future-agent reference.

**Outcome:** PASSED. v1.3 milestone closes 12/12 phases.

## Plans (5/5)

| Plan | Wave | Requirement | Commit | Status |
|------|------|-------------|--------|--------|
| 27-01-PLAN.md | 1 | QOL-04 (language-feature weaves) | 995ff67 | ✅ |
| 27-02-PLAN.md | 2 | QOL-04 (chapter 19.5 mega-chapter) | dbffbec | ✅ |
| 27-03-PLAN.md | 3 | QOL-04 (graduation refactor + showcase replace + pragma companions) | eadbd9f | ✅ |
| 27-04-PLAN.md | 4 | QOL-04 (Phase27ByteIdenticalPragmaTests, 4 facts) | e15c5be | ✅ |
| 27-05-PLAN.md | 5 | QOL-04 (closure docs commit + hash substitution) | ace6416 | ✅ |

## Must-Haves Audit

### Plan 27-01 (Language weaves)

- ✅ Chapter 1.5 Symbols inserted with `#foo` interning + strict-separation-from-String demo: `grep -c "^Note: 1\\.5 Symbols" examples/tutorial.flow` → 1
- ✅ Chapter 4.5 Tuples + ~> covering literal, indexing, destructuring, ~>, (unpack): `grep -c "^Note: 4\\.5 Tuples" examples/tutorial.flow` → 1
- ✅ Chapter 4.6 Dict with both constructors + 14-op surface: `grep -c "^Note: 4\\.6 Dict<K, V>" examples/tutorial.flow` → 1
- ✅ Chapter 2 prose teaches no-infix prefix-only rule: `grep -q "There is NO infix" examples/tutorial.flow` → 0
- ✅ Legacy `Operator style: 10 + 25` print removed: `! grep -q "Operator style: 10 + 25" examples/tutorial.flow` → exit 0
- ✅ Chapter 9.5 gain-vs-volume own chapter: `grep -c "^Note: 9\\.5 gain vs volume" examples/tutorial.flow` → 1
- ✅ Chapter 9 Hertz literal `1.2kHz` + Ms literal `250ms` inline: `grep -q "(lowpass 1.2kHz)" examples/tutorial.flow && grep -q "(delay 250ms 0.5 0.4)"` → exit 0
- ✅ Chapter 16 Second-decay reverb appended: `grep -q "(reverb rawSd 0.5 1.8s)" examples/tutorial.flow` → exit 0
- ✅ Tutorial exits 0 with non-empty WAV: `[ -s examples/output/flow_tutorial.wav ]` → exit 0
- ✅ Phase 18 ByteIdenticalTutorialTests stays GREEN: `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalTutorial"` → 2/2

### Plan 27-02 (Chapter 19.5 mega-chapter)

- ✅ Chapter 19.5 with 4 sub-sections: `grep -E "^Note: 19\\.5\\.[ABCD]" examples/tutorial.flow | wc -l` → 4
- ✅ Sub-section A demonstrates 6 tuplet variants: `grep -q "{3:2 C4 D4 E4}q" && grep -q "C4/12" && grep -q "{3:2 C4 D4 E4~}q E4q" && grep -q "{3:2 C4 {3:2 D4 E4 F4}q G4}h" examples/tutorial.flow` → exit 0
- ✅ Sub-section B is print-only prose pointing to companion files: `grep -q "examples/pragmas/h_alias.flow" && grep -q "examples/pragmas/microtonal_ji.flow" examples/tutorial.flow` → exit 0
- ✅ Sub-section C runnable DX-10..14 + createSineTone-Hertz: `grep -q "(arpeggio Cmaj7 QUARTER" && grep -q "(inversion Cmaj7 1)" && grep -q "(delay dxBuf QUARTER" && grep -q "(quantize loose EIGHTH" && grep -q "(legato legSeq 0.9)" && grep -q "(portamento legSeq 50ms)" && grep -q "(createSineTone 0.5 440Hz 0.3)"` → exit 0
- ✅ Sub-section D runnable enharmonic edge + range + neg slice + humanizeGaussian: `grep -q "(range 0 5)" && grep -q "(slice sliceDemo negThree 5)" && grep -q "key Abmajor" && grep -q "(humanizeGaussian rawMel 0.08 314)"` → exit 0
- ✅ Tutorial exits 0 + non-empty WAV+MIDI; Phase 18 byte-identical sentinel GREEN.

### Plan 27-03 (Graduation refactor + showcase + companions)

- ✅ Tutorial graduation chain: `grep -q "(lowpass 1.2kHz)" && grep -q "(delay 250ms 0.5 0.4)" && grep -q "(reverb 0.5 1.8s)" && grep -q "(volume reverbed 0.85)" examples/tutorial.flow` → exit 0
- ✅ Tutorial.flow has NO active `enable` pragma: `! grep -E "^enable " examples/tutorial.flow` → exit 0 (pragma names appear only inside `Note:` comment prose)
- ✅ Existing `gain 0.6 / 1.0 / reverbTime 2.5` musical-context blocks preserved: `grep -q "gain 0.6 {" && grep -q "gain 1.0 {" && grep -q "reverbTime 2.5 {" examples/tutorial.flow` → exit 0
- ✅ showcase.flow REPLACED with v1.3 polyrhythmic-minimal piece: `grep -q "v1.3 Polyrhythmic Minimal" && grep -q "(dict #kick C2 #snare D2 #hihat F#3)" && grep -q "{3:2 _ C2 _}q" && grep -q "(humanizeGaussian " && grep -q "(lowpass 1.2kHz)" && grep -q "(delay 250ms 0.5 0.4)" && grep -q "(reverb 0.5 1.8s)" && grep -q "(volume reverbed 0.7)" examples/showcase.flow` → exit 0
- ✅ Companion files exist with pragma at line 1: `head -1 examples/pragmas/h_alias.flow` → `enable hAsB;`; `head -1 examples/pragmas/microtonal_ji.flow` → `enable justIntonation;`
- ✅ Companion files match tutorial graduation pattern (Song/Buffer/render/writeWav/writeMidi INSIDE tempo+timesig+key block): `awk '/^tempo 120/,/^}/{print}' examples/pragmas/h_alias.flow | grep -q '(writeWav'` → exit 0; same for microtonal_ji.flow.
- ✅ All 4 .flow scripts smoke clean: each exits 0 + non-empty WAV+MIDI in examples/output/.
- ✅ Phase 18 + Phase 25 byte-identical sentinels stay GREEN: `dotnet test --filter "FullyQualifiedName~Phase18.ByteIdenticalShowcase|FullyQualifiedName~Phase18.ByteIdenticalTutorial|FullyQualifiedName~Phase25.ByteIdenticalShowcaseGaussian"` → 6/6.

### Plan 27-04 (Phase27ByteIdenticalPragmaTests)

- ✅ Test class created at correct path: `[ -f flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs ]` → exit 0
- ✅ namespace + class name: `grep -q "namespace FlowLang.Tests.Integration.Phase27" && grep -q "class Phase27ByteIdenticalPragmaTests"` → exit 0
- ✅ 4 facts present: `grep -c '\[Fact\]'` → 4
- ✅ Helper signature parameterized: `grep -q "RunTwiceAndCompare(string baseName, bool isMidi)"` → exit 0
- ✅ `[Collection("FlowScripts")]` attribute present (Pitfall): `grep -q '\[Collection("FlowScripts")\]'` → exit 0
- ✅ Halt-gate `Assert.NotEqual(source, sourceRun1)` present: `grep -q "Assert.NotEqual(source, sourceRun1)"` → exit 0
- ✅ Two-run SequenceEqual contract: `grep -q "bytes1.SequenceEqual(bytes2)"` → exit 0
- ✅ NO inline byte[] pin literals: `! grep -E 'byte\[\][ ]*pin|new byte\[\][ ]*\{'` → exit 0
- ✅ All 4 facts GREEN: `dotnet test --filter "FullyQualifiedName~Phase27"` → 4/4
- ✅ Full unit suite GREEN: `dotnet test flow-lang.Tests --nologo` → 883/883

### Plan 27-05 (Closure)

- ✅ REQUIREMENTS.md QOL-04 flipped + Phase 26.2 surface: `grep -q '^- \[x\] \*\*QOL-04\*\*' && grep -q "Phase 26.2 surface" && grep -q "Phase27ByteIdenticalPragmaTests" .planning/REQUIREMENTS.md` → exit 0
- ✅ REQUIREMENTS.md Traceability row flipped: `grep -q "^| QOL-04 | Phase 27 | Shipped" && ! grep -q "^| QOL-04 | Phase 27 | Pending |"` → exit 0
- ✅ ROADMAP.md Phase 27 marked [x] + 5/5 Complete: `grep -q "^- \[x\] \*\*Phase 27" && grep -q "5/5 | Complete | 2026-05-10"` → exit 0
- ✅ STATE.md frontmatter advanced: `grep -q "completed_phases: 12" && grep -q "v1.3 milestone shipped"` → exit 0
- ✅ CLAUDE.md gains Music Types Quick Reference table (8 rows): `grep -q "### Music Types Quick Reference" && grep -q '\`-12dB\`' && grep -q '\`100ms\`' && grep -q '\`2.5s\`' && grep -q '\`440Hz\`' && grep -q '\`#foo\`'` → exit 0
- ✅ 27-RESEARCH.md Open Questions flipped to (RESOLVED) with 4 inline `**RESOLVED:**` markers: `grep -q "^## Open Questions (RESOLVED)" && [ "$(grep -c '\*\*RESOLVED:\*\*' .planning/phases/27-tutorial-showcase-refresh/27-RESEARCH.md)" -ge 4 ]` → exit 0

## v1.3 Feature Coverage Grep Audit

Note: `(invertChord ...)` / `(delaySync ...)` / `(quantize loose 0.125)` / `(portamento C4 G4 0.5)` / `(varispeed src 1.5)` cited in the original plan are stale plan strings — the actual registered builtins are `(inversion Chord Int)`, `(delay Buffer NoteValue Double Double)`, `(quantize Sequence NoteValue Double Double)`, `(portamento Sequence Millisecond)`, and there is NO `(varispeed Buffer Double)` (varispeed lives in `(loadWav path Int|Double)`). Tutorial uses the actual signatures; this audit documents the truth.

| Feature | Canonical Site | Grep Verification |
|---------|----------------|-------------------|
| Prefix-only arithmetic | tutorial.flow ch. 2 | `grep -q "There is NO infix" examples/tutorial.flow` |
| Symbol #foo | tutorial.flow ch. 1.5 | `grep -q "Symbol kick = #kick" examples/tutorial.flow` |
| Tuple <<a, b>> literal | tutorial.flow ch. 4.5 | `grep -q "Tuple<<Int, Int>> pair = <<10, 20>>" examples/tutorial.flow` |
| Tuple destructuring | tutorial.flow ch. 4.5 | `grep -q "<<Int a, Int b>> = pair" examples/tutorial.flow` |
| ~> flow op | tutorial.flow ch. 4.5 | `grep -q "<<1, 2, 3>> ~> add3" examples/tutorial.flow` |
| (unpack) runtime | tutorial.flow ch. 4.5 | `grep -q "(unpack <<3, 4, 5>> add3)" examples/tutorial.flow` |
| Dict (dict) constructor | tutorial.flow ch. 4.6 | `grep -q "(dict #kick 90 #snare 70 #hihat 50)" examples/tutorial.flow` |
| Dict (dictTuple) constructor | tutorial.flow ch. 4.6 | `grep -q "(dictTuple <<#kick, 90>>" examples/tutorial.flow` |
| Tuplets {3:2 ...}q | tutorial.flow ch. 19.5.A | `grep -q "{3:2 C4 D4 E4}q" examples/tutorial.flow` |
| Fractional C4/12 | tutorial.flow ch. 19.5.A | `grep -q "C4/12 D4/12 E4/12" examples/tutorial.flow` |
| Nested tuplets | tutorial.flow ch. 19.5.A | `grep -q "{3:2 C4 {3:2 D4 E4 F4}q G4}h" examples/tutorial.flow` |
| Tied tuplet (safe form) | tutorial.flow ch. 19.5.A | `grep -q "{3:2 C4 D4 E4~}q E4q" examples/tutorial.flow` |
| Microtonal pragma docs | tutorial.flow ch. 19.5.B | `grep -q "examples/pragmas/microtonal_ji.flow" examples/tutorial.flow` |
| enable hAsB; docs | tutorial.flow ch. 19.5.B | `grep -q "examples/pragmas/h_alias.flow" examples/tutorial.flow` |
| Scale-lint pragma docs | tutorial.flow ch. 19.5.B | `grep -q "enable scaleLint;" examples/tutorial.flow` |
| Arpeggio (DX-10) | tutorial.flow ch. 19.5.C | `grep -q "(arpeggio Cmaj7 QUARTER" examples/tutorial.flow` |
| Chord inversions (DX-11) | tutorial.flow ch. 19.5.C | `grep -q "(inversion Cmaj7 1)" examples/tutorial.flow` |
| NoteValue-rate delay (DX-12) | tutorial.flow ch. 19.5.C | `grep -q "(delay dxBuf QUARTER" examples/tutorial.flow` |
| quantize (DX-13) | tutorial.flow ch. 19.5.C | `grep -q "(quantize loose EIGHTH" examples/tutorial.flow` |
| legato (DX-14a) | tutorial.flow ch. 19.5.C | `grep -q "(legato legSeq 0.9)" examples/tutorial.flow` |
| portamento (DX-14b) | tutorial.flow ch. 19.5.C | `grep -q "(portamento legSeq 50ms)" examples/tutorial.flow` |
| varispeed (DX-15) docs | tutorial.flow ch. 19.5.C | `grep -q "(loadWav .*samples" examples/tutorial.flow` (prose-only) |
| createSineTone Hertz overload | tutorial.flow ch. 19.5.C | `grep -q "(createSineTone 0.5 440Hz 0.3)" examples/tutorial.flow` |
| (range) | tutorial.flow ch. 19.5.D | `grep -q "(range 0 5)" examples/tutorial.flow` |
| Negative slice | tutorial.flow ch. 19.5.D | `grep -q "sliceDemo@negOneIdx" examples/tutorial.flow` |
| Multi-letter enharmonic edge (Cb) | tutorial.flow ch. 19.5.D | `grep -q "(enharmonic B3)" && grep -q "key Abmajor" examples/tutorial.flow` |
| humanizeGaussian | tutorial.flow ch. 19.5.D + showcase.flow | `grep -q "(humanizeGaussian " examples/tutorial.flow examples/showcase.flow` |
| Hertz literal 1.2kHz | tutorial.flow + showcase.flow | `grep -q "1.2kHz" examples/tutorial.flow examples/showcase.flow` |
| Ms-typed delay | tutorial.flow + showcase.flow | `grep -q "250ms" examples/tutorial.flow examples/showcase.flow` |
| Second-decay reverb | tutorial.flow + showcase.flow | `grep -q "1.8s" examples/tutorial.flow examples/showcase.flow` |
| volume(buf, linear) | tutorial.flow + showcase.flow | `grep -q "(volume " examples/tutorial.flow examples/showcase.flow` |
| gain dB-only | tutorial.flow ch. 9.5 | `grep -q "(gain signal -6dB)" examples/tutorial.flow` |
| enable hAsB; companion runs | examples/pragmas/h_alias.flow | `dotnet run --project flow-interpreter examples/pragmas/h_alias.flow` → exit 0 + non-empty WAV |
| enable justIntonation; companion runs | examples/pragmas/microtonal_ji.flow | `dotnet run --project flow-interpreter examples/pragmas/microtonal_ji.flow` → exit 0 + non-empty WAV |

## Regression Gates

| Gate | Pre-Phase | Post-Phase | Status |
|------|-----------|------------|--------|
| Phase 18 ByteIdenticalTutorialTests | 2/2 | 2/2 | ✅ GREEN |
| Phase 18 ByteIdenticalShowcaseTests | 2/2 | 2/2 | ✅ GREEN |
| Phase 25 ByteIdenticalShowcaseGaussianTests | 2/2 | 2/2 | ✅ GREEN |
| Phase 27 ByteIdenticalPragmaTests (NEW) | n/a | 4/4 | ✅ GREEN |
| Full unit suite | 879/879 | 883/883 | ✅ GREEN (zero new failures; +4 tests = Phase 27 facts) |
| tutorial.flow smoke | exit 0 | exit 0 | ✅ |
| showcase.flow smoke | exit 0 | exit 0 | ✅ |
| h_alias.flow smoke | n/a | exit 0 | ✅ NEW |
| microtonal_ji.flow smoke | n/a | exit 0 | ✅ NEW |

## Smoke Transcripts

```
$ dotnet run --project flow-interpreter examples/tutorial.flow
... 1.5 Symbols ...  4.5 Tuples and ~> ...  4.6 Dict<K, V> ...
... 9.5 gain vs volume ...  19.5.A Tuplets ...  19.5.B Pragmas ...
... 19.5.C Composer DX ...  19.5.D Misc Small Wins ...
... Graduation Piece: Sunrise ...  After Phase 26.2 fx chain: <N> frames
WAV:  examples/output/flow_tutorial.wav
MIDI: examples/output/flow_tutorial.mid
EXIT 0

$ dotnet run --project flow-interpreter examples/showcase.flow
Flow Showcase -- v1.3 Polyrhythmic Minimal
Generating examples/output/flow_showcase.{wav,mid} ...
WAV:  examples/output/flow_showcase.wav
MIDI: examples/output/flow_showcase.mid
EXIT 0

$ dotnet run --project flow-interpreter examples/pragmas/h_alias.flow
Flow Pragma Demo -- enable hAsB;
basic sequence | H4q B4q C5q | parses identically to | B4q B4q C5q |: ...
outside note streams: Int H = 5
WAV:  examples/output/h_alias.wav
MIDI: examples/output/h_alias.mid
EXIT 0

$ dotnet run --project flow-interpreter examples/pragmas/microtonal_ji.flow
Flow Pragma Demo -- enable justIntonation;
JI major third (C-E):  ratio 5/4 = 1.25
JI fifth (C-G):        ratio 3/2 = 1.5
[midi] tuning != equalTemperament; MIDI export emits 12-TET pitches without pitch-bend (faithful microtonal MIDI deferred to v1.4)
WAV:  examples/output/microtonal_ji.wav
MIDI: examples/output/microtonal_ji.mid
EXIT 0
```

## Open Questions Resolved

27-RESEARCH.md `## Open Questions` heading flipped to `## Open Questions (RESOLVED)` (W8 fix). The 4 inline `**RESOLVED:**` markers under each question:

1. **Q1 Tuplet ties:** Yes, only in safe form — last tuplet member crossing back to straight time. Demoed as `{3:2 C4 D4 E4~}q E4q` in tutorial 19.5.A. Ties INSIDE the bracket avoided per RESEARCH Pitfall 3.
2. **Q2 Tutorial graduation pragma:** NO. tutorial.flow stays 12-TET. Microtonal demo lives exclusively in examples/pragmas/microtonal_ji.flow per D-401/D-402.
3. **Q3 Scale-lint chapter:** Yes, prose-only inside 19.5.B. flow-interpreter does not surface lint diagnostics — flow-lsp owns the surface. Tutorial documents this distinction explicitly.
4. **Q4 Showcase tied tuplet edges:** NO. Showcase.flow tuplet groove uses no ties INSIDE the {3:2 ...}q brackets per RESEARCH Pitfall 3.

## Deviations Summary

Per-plan deviations are documented in 27-{01,02,03,04}-SUMMARY.md "Deviations from Plan" sections. Aggregate count: ~10 Rule-1 (bug) auto-fixes, mostly stale plan signatures or syntactic edge cases (negative literal lex, key-name spelling, missing imports, gitignore exception). No Rule-4 architectural escalations. All deviations resolved without changing user-facing intent of any deliverable.

## Acceptance Idioms

| ROADMAP Phase 27 Success Criterion | Verified ✅ |
|------------------------------------|-------------|
| 1. Tutorial demonstrates EVERY v1.3 feature end-to-end | ✅ — see grep audit table above (32 rows) |
| 2. Both scripts exit 0 producing non-empty WAV+MIDI | ✅ — 4 .flow scripts smoke clean |
| 3. Byte-identical determinism (cmp-clean two runs) | ✅ — Phase 18 + 25 + 27 sentinels all GREEN |
| 4. Existing v1.1 + v1.2 chapters preserved | ✅ — chapters 1-20 intact; 19.5 inserted between 19 + 20; half-numbered 1.5 / 4.5 / 4.6 / 9.5 inserted without renumbering existing chapters |

## Sign-Off

Phase 27 closes v1.3 milestone (12/12 phases). Tutorial.flow + showcase.flow are the canonical surfaces a new composer encounters; they exercise the entire v1.3 language + audio surface end-to-end with byte-identical determinism preserved. Pragma companion files under examples/pragmas/ ship `enable hAsB;` and `enable justIntonation;` demos at file-scope without contaminating the tutorial. CLAUDE.md gains a Music Types Quick Reference table for fast composer + agent lookup. The new `Phase27ByteIdenticalPragmaTests` (4 facts) joins Phase 18 + 25 sentinels, completing the byte-identical regression-gate coverage for every shipped .flow exemplar in `examples/`. Ready for `/gsd-complete-milestone v1.3` (release tag + retrospective) OR v1.4 planning.
