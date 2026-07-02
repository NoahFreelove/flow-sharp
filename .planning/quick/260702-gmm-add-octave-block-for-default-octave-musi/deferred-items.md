# Deferred Items — quick-260702-gmm

## Pre-existing xUnit failure (NOT introduced by this task — out of scope)

**Test:** `FlowLang.Tests.Unit.Phase10.FormantDataTests.GetFormants_UnknownVowel_ThrowsArgumentException`
**Status:** FAIL (`Assert.Throws() Failure: No exception was thrown`)

**Root cause:** commit `44da382` (quick task 260701-vx4, "charitable vocal fixes") changed
`FormantData.GetFormants` to charitably degrade an unknown phoneme to `"ah"` + a one-shot
`[vocal] unknown phoneme '<x>' — using 'ah'` advisory instead of throwing
`ArgumentException`. That charitable-interpretation change (aligned with project house style)
left this unit test pinning the OLD throwing behavior — the test was not updated in the same
commit, and the vx4 verification note only re-ran `test_vocalization` + `test_type_ergonomics`,
not the full xUnit suite, so the stale test slipped through.

**Why not fixed here:** entirely in the vocal/formant subsystem
(`flow-lang/StandardLibrary/Audio/Vocalization/FormantData.cs`) — zero overlap with this task's
octave-block change (lexer/parser/interpreter/note-stream/NoteType). Per the executor SCOPE
BOUNDARY rule, only issues directly caused by this task's changes are auto-fixed.

**Suggested fix (future task):** update the test to assert the charitable path — no throw,
formants equal the `"ah"` formant set, and the `[vocal] unknown phoneme` advisory fires — OR
rename/repurpose it to `GetFormants_UnknownVowel_FallsBackToAh`.

**This task's xUnit delta:** 2738 passed / 1 failed (this pre-existing one) / 19 skipped.
Zero NEW failures introduced by quick-260702-gmm.
