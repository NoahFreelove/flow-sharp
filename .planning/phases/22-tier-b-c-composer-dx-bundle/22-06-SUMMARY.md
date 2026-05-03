---
phase: 22-tier-b-c-composer-dx-bundle
plan: 06
subsystem: transforms
tags: [legato, portamento, articulation, midi-cc, defaulted-parameter, byte-identical, dx-14]

requires:
  - phase: 18-foundation-rational-duration-arithmetic
    provides: defaulted-parameter migration shape (DurationFraction precedent) + byte-identical regression gate
  - phase: 22-tier-b-c-composer-dx-bundle
    provides: 22-05 OnsetOffset migration + With(...) builder helper (rollback-independent composition)
provides:
  - "MusicalNoteData.DurationOverlap defaulted-parameter field (render-time duration extension factor)"
  - "MusicalNoteData.PortamentoMs defaulted-parameter field (MIDI CC5 mapping input)"
  - "MusicalNoteData.With(...) extended with double? durationOverlap + double? portamentoMs nullable optional params (null-coalesce preserves existing values)"
  - "BarRenderer reads DurationOverlap AFTER ToTimeline produces onsets — extends durationBeats by (1 + DurationOverlap) without moving onsets (Pitfall 3)"
  - "MidiExport per-note loop emits CC65=127 + CC5=clamp(round(ms*127/200),0,127) at note start, CC65=0 at note end (bracket); NoteOff lands at extendedBeats (CONTEXT D-03 — overlapping events are valid SMF); barTick advances by ORIGINAL beats (NOT extendedBeats — Pitfall 3)"
  - "TransformFunctions.RegisterArticulationTransforms wires legato(Sequence, Double) + portamento(Sequence, Millisecond)"
  - "Linear ms->CC5 mapping curve: 0->0, 100->64, 200->127 clamped (CONTEXT Claude's Discretion)"
  - "Composition (legato (portamento seq X) Y) preserves both flags via With(...) null-coalesce (Open Question 4 resolved)"
affects: [22-07-closure]

tech-stack:
  added: []
  patterns:
    - "Defaulted-parameter ctor migration extension: 22-05 owned the OnsetOffset slot; 22-06 APPENDS durationOverlap + portamentoMs slots at end of ctor signature. With(...) builder grows naturally with each plan — each plan's transforms name only their owned slot, sibling slots are preserved by the helper's null-coalesce. Rollback of any single plan only removes its slot+field+helper-overload without breaking siblings."
    - "Render-time consumer reads field AFTER onset emission: BarRenderer reads DurationOverlap AFTER bar.ToTimeline() produces onsets, mirroring the existing IsTied pattern at lines 67-72. Default 0.0 makes the addition mathematically dormant for all pre-22-06 callers."
    - "Pitfall 3 (legato extends, never moves): MIDI export's barTick advances by ORIGINAL beats, NOT extendedBeats. This is what makes legato OVERLAP rather than slow the song down. The extension is in the NoteOff tick math only — subsequent NoteOn ticks march forward at the original cadence."
    - "MIDI-only articulation in v1.3: portamento is purely a MidiExport concern in v1.3 — audio renderer ignores PortamentoMs. Documented in NoteType.cs PortamentoMs xmldoc. Audio-side glide is deferred to v1.4."

key-files:
  created:
    - flow-lang.Tests/Unit/Phase22/LegatoFacts.cs
    - flow-lang.Tests/Unit/Phase22/PortamentoMidiFacts.cs
    - tests/test_dx_legato.flow
    - tests/test_dx_portamento.flow
  modified:
    - flow-lang/TypeSystem/SpecialTypes/NoteType.cs
    - flow-lang/StandardLibrary/Audio/BarRenderer.cs
    - flow-lang/StandardLibrary/Audio/MidiExport.cs
    - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/std.flow
    - flow-lang.Tests/FlowScriptData.cs

key-decisions:
  - "DurationOverlap and PortamentoMs are independent defaulted-parameter fields appended to MusicalNoteData ctor at the END (Phase 18 / 22-05 migration shape). 30+ existing positional call sites compile unmodified — byte-identical regression gate stays GREEN by construction. ByteIdentical 6/6 confirms."
  - "MusicalNoteData.With(...) builder helper extended in lockstep: 22-05 introduced the With(double? onsetOffset = null) signature; 22-06 APPENDS double? durationOverlap = null + double? portamentoMs = null. Per CONTEXT line 18, transforms NEVER name fields they don't own — legato calls With(durationOverlap:), portamento calls With(portamentoMs:). The null-coalesce inside With() preserves all sibling fields (OnsetOffset from 22-05, the other 22-06 slot). Rolling back 22-06 only removes its appended slots + fields + helper params; 22-05's call sites stay intact."
  - "Pitfall 3 honored on BOTH the audio AND MIDI paths: BarRenderer extends durationBeats AFTER bar.ToTimeline() emits onsetBeats; MidiExport extends NoteOff's tick AND emits the bracket-close CC65=0 at the extended endpoint, but advances barTick by the ORIGINAL beats. This is the precise mechanism that makes legato OVERLAP rather than slow the song down. Pinned by `LegatoFacts.OnsetsUnchanged` (compares ToTimeline before vs after legato — onsets identical) and by an explicit grep gate in the plan acceptance criteria on the literal `barTick += (long)(beats * ticksPerQuarter)` line."
  - "Linear ms->CC5 curve: byte = clamp(round(ms × 127 / 200), 0, 127). Anchor points: 0->0, 100->64, 200->127, anything beyond -> 127 clamped, anything negative -> 0 clamped. Per CONTEXT Claude's Discretion. Documented in NoteType.cs PortamentoMs xmldoc and pinned by PortamentoMidiFacts.MsToFiveCC_LinearCurve + MsToFiveCC_OutOfRangeIsClamped."
  - "V5 input validation (T-22-V5-22, T-22-V5-23): Math.Clamp on the CC5 byte happens BEFORE the (SevenBitNumber) cast — guards both upper-overflow (PortamentoMs >= 200ms maps to 127, not wraparound) AND negative input (PortamentoMs < 0 maps to 0, not negative SevenBitNumber). Charitable D-07: no exception, no error, just clamp."
  - "Audio renderer ignores PortamentoMs in v1.3 (MIDI-only articulation): explicit per CONTEXT and documented in PortamentoMs xmldoc. Audio-side glide via wave-table re-trigger or pitch-bend interpolation deferred to v1.4 — out of scope for this plan. The (legato (portamento seq) ...) composition Fact verifies both fields stamp through cleanly even though only one of the two has render-time audio consequences in v1.3."
  - "RegisterArticulationTransforms is wired explicitly from BuiltInFunctions.RegisterAllImplementations (one line: `Transforms.TransformFunctions.RegisterArticulationTransforms(registry);` next to the existing `TransformFunctions.Register(registry);` call) — matches the plan's acceptance criterion grep on that file. Stateless registration (no ExecutionContext needed because BarRenderer/MidiExport read the per-note fields directly, not active musical context)."
  - "CONTEXT line 18 rollback-independence guard: `RegisterArticulationTransforms` body contains zero references to `OnsetOffset` (22-05's slot). Verified by an awk-based grep gate in the plan acceptance criteria that returns 0. Each transform names ONLY the field it owns; the With(...) helper takes care of preserving siblings."

patterns-established:
  - "Pattern: extending a Phase 22 With(...) helper across multiple plans — each plan that adds a defaulted field to MusicalNoteData ALSO adds a matching nullable optional parameter to With(...). Transforms call With(named: value) naming only their owned slot. Rollback of any single plan removes its parameter + field + ctor slot but leaves the helper itself intact (modulo the appended slot). Tested empirically by 22-05 OnsetOffset surviving 22-06's With() extension."
  - "Pattern: per-note articulation field consumed at render time, not at sequence-build time — when an articulation transform needs to affect render behavior (audio buffer length, MIDI tick math, CC events), it stamps a defaulted-parameter field onto each note rather than mutating DurationValue/DurationFraction (which would cascade through ToTimeline). The renderer reads the field AFTER onset emission. This is the only way to extend duration WITHOUT moving onsets (Pitfall 3)."
  - "Pattern: MIDI-only articulation that ignores audio path — when an articulation has a MIDI representation but no clean audio implementation in the current language version (e.g. portamento glide requires phase-continuous oscillator state), the field is stamped onto the note but ONLY MidiExport reads it. Audio renderer's BarRenderer skips the field. The xmldoc on the field documents the asymmetry. Composers get the MIDI articulation immediately; audio-side implementation can land in a future phase."

requirements-completed: [DX-14]

duration: 8min
completed: 2026-05-02
---

# Phase 22 Plan 06: DX-14 Legato + Portamento Summary

**`legato(Sequence, Double)` extends note durations by overlap factor without moving onsets; `portamento(Sequence, Millisecond)` emits MIDI CC65/CC5 bracket per Sweetwater spec; both compose via independent defaulted-parameter fields and a shared With(...) builder helper. ByteIdentical 6/6 GREEN confirms the dormant-default contract.**

## Performance

- **Duration:** ~8 min wall clock (full task cycle including build + tests)
- **Started:** 2026-05-02T20:01:27Z
- **Completed:** 2026-05-02T20:09:30Z
- **Tasks:** 3 (RED + GREEN + verify)
- **Files modified:** 11 (4 created, 7 modified)

## Accomplishments

- DX-14 closed: `legato(Sequence, Double)` and `portamento(Sequence, Millisecond)` both registered as Flow built-ins
- Two new defaulted-parameter fields on `MusicalNoteData` — `DurationOverlap` (legato) and `PortamentoMs` (portamento) — both default to 0.0 at end of ctor (Phase 18 / 22-05 migration shape, 30+ existing positional call sites unmodified)
- `MusicalNoteData.With(...)` builder helper extended with two new nullable optional params (`double? durationOverlap = null`, `double? portamentoMs = null`) atop 22-05's `onsetOffset` slot. Each transform names only its owned slot; null-coalesce preserves siblings (rollback-independent per CONTEXT line 18)
- `BarRenderer.RenderBarToVoices` extends `durationBeats` by `(1 + DurationOverlap)` AFTER `bar.ToTimeline()` produces onsets — onsets are NOT moved (Pitfall 3 honored on the audio path)
- `MidiExport` per-note loop:
  - emits `CC65=127` + `CC5=clamp(round(ms*127/200),0,127)` at note start when `PortamentoMs > 0` (V5 clamp guards SevenBitNumber cast)
  - emits NoteOff at `barTick + extendedBeats × ticksPerQuarter` (CONTEXT D-03 — overlapping events are valid SMF)
  - emits `CC65=0` at `barTick + extendedBeats × ticksPerQuarter` (bracket-close)
  - advances `barTick` by ORIGINAL beats (NOT extendedBeats) — Pitfall 3 honored on the MIDI path
- Linear ms→CC5 mapping curve: 0→0, 100→64, 200→127, beyond→127 clamped, negative→0 clamped (CONTEXT Claude's Discretion)
- Composition `(legato (portamento seq X) Y)` preserves both flags on every note (RESEARCH Open Question 4 resolved)
- 17 Facts GREEN (8 LegatoFacts + 9 PortamentoMidiFacts including DryWetMidi `MidiFile.Read` round-trip asserting CC65=127, CC5=64, CC65=0 events present in the generated `.mid`); both smoke scripts exit 0 with sentinels (`DX-14 legato: PASSED` and `DX-14 portamento: PASSED`)
- ByteIdentical regression gate **6/6** GREEN (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI) — confirms `DurationOverlap=0` and `PortamentoMs=0` are dormant defaults
- flow-lang test suite **499/499** GREEN — zero regressions (was 479/479 at 22-05 close + 17 new LegatoFacts/PortamentoMidiFacts + 2 new sentinel theory rows + 1 — all incremental)

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 RED — Failing LegatoFacts + PortamentoMidiFacts + smokes + ctor stubs** — `2f860f8` (test)
   - 8 LegatoFacts (3 ctor/With direct + 4 engine-eval through legato + 1 sibling-composition with portamento)
   - 9 PortamentoMidiFacts (3 ctor/With direct + 2 ms→CC5 curve + 3 DryWetMidi read-back + 1 sibling-composition with legato)
   - `tests/test_dx_legato.flow` and `tests/test_dx_portamento.flow` smoke scripts with PASSED sentinels
   - `flow-lang.Tests/FlowScriptData.cs` — two new RequiredSentinels theory rows
   - `MusicalNoteData.DurationOverlap` + `MusicalNoteData.PortamentoMs` properties + ctor parameters appended at end of signature (after 22-05's `onsetOffset`)
   - `MusicalNoteData.With(...)` extended with `double? durationOverlap = null` + `double? portamentoMs = null` parameters; null-coalesce preserves existing values
   - State at end of Task 1: build clean; 9/17 facts GREEN immediately (ctor migration + With() preservation + ms→CC5 reference helper math + V5 clamp), 8/17 RED (engine-eval through legato/portamento overloads — not yet registered)

2. **Task 2: Wave 5 GREEN — Wire BarRenderer + MidiExport + register transforms** — `d2bde5d` (feat)
   - `BarRenderer.cs` — DurationOverlap branch added immediately after the existing IsTied block (mirrors the analog at lines 67-72)
   - `MidiExport.cs` — per-note loop extended with: `extendedBeats` calc, CC65/CC5 bracket-open before NoteOn, NoteOff at `barTick + durationTicks` where `durationTicks = (long)(extendedBeats * ticksPerQuarter)`, CC65=0 bracket-close at the same extended endpoint, and `barTick += (long)(beats * ticksPerQuarter)` advancing by ORIGINAL beats (Pitfall 3 critical line)
   - `TransformFunctions.RegisterArticulationTransforms` — registers both `legato(Sequence, Double)` and `portamento(Sequence, Millisecond)`. Both call `note.With(...)` naming ONLY their owned slot. Body contains zero references to `OnsetOffset` (22-05's slot) — pinned by an awk-based acceptance gate.
   - `BuiltInFunctions.cs` — explicit one-line addition: `Transforms.TransformFunctions.RegisterArticulationTransforms(registry);` immediately after the existing `TransformFunctions.Register(registry);` call
   - `std.flow` — `internal proc legato (Sequence: seq, Double: overlap)` and `internal proc portamento (Sequence: seq, Millisecond: glideTime)` declarations next to existing transforms
   - In-task fix: `LegatoFacts.Legato_OnEmptySequence_ReturnsEmpty` was renamed to `Legato_OnSingleNoteSequence_PropagatesField` because bare `| |` is not valid Flow syntax (lexer requires at least one note in a stream). One-note input still pins the no-crash + field-stamp invariant.
   - All 17 Facts flipped GREEN

3. **Task 3: Wave 5 — Smoke run + ByteIdentical regression gate** — `332154c` (chore, verification-only empty commit)
   - `dotnet run --project flow-interpreter tests/test_dx_legato.flow` → exit 0, sentinel printed; `tests/output/dx_legato.wav` (352844 bytes)
   - `dotnet run --project flow-interpreter tests/test_dx_portamento.flow` → exit 0, sentinel printed; `tests/output/dx_portamento.mid` (119 bytes, valid SMF)
   - LegatoFacts + PortamentoMidiFacts: 17/17 GREEN
   - **ByteIdentical 6/6 GREEN** — strict regression on tutorial.flow + showcase.flow + euclidean WAV/MIDI. Confirms DurationOverlap=0 and PortamentoMs=0 are dormant defaults: BarRenderer's `if (DurationOverlap > 0.0)` short-circuits, MidiExport's `extendedBeats == beats` and `if (PortamentoMs > 0.0)` short-circuits.
   - flow-lang.Tests full suite **499/499 GREEN**

## Files Created/Modified

- `flow-lang.Tests/Unit/Phase22/LegatoFacts.cs` (created) — 8 xUnit Facts: `DurationOverlap_DefaultsTo0`, `DurationOverlap_OptionalCtorParam_AcceptedAtEndOfSignature`, `With_DurationOverlap_PreservesOtherFields` (ctor + builder helper); `OverlapHalf_PropagatesDurationOverlapField`, `OnsetsUnchanged` (Pitfall 3 — compares ToTimeline before vs after legato), `Legato_OnSingleNoteSequence_PropagatesField`, `Legato_OverlapZero_IsIdentityOfDurationOverlapField` (engine-eval); `Legato_AndPortamento_Compose` (sibling composition Open Question 4)
- `flow-lang.Tests/Unit/Phase22/PortamentoMidiFacts.cs` (created) — 9 xUnit Facts: `PortamentoMs_DefaultsTo0`, `PortamentoMs_OptionalCtorParam_AcceptedAtEndOfSignature`, `With_PortamentoMs_PreservesOtherFields` (ctor + builder helper); `MsToFiveCC_LinearCurve` (anchor points 0/100/200), `MsToFiveCC_OutOfRangeIsClamped` (V5 input validation); `WriteMidi_ContainsCC65AndCC5`, `Portamento_BracketCloseEmitsCC65Zero`, `WriteMidi_NoPortamento_EmitsNoCC` (DryWetMidi `MidiFile.Read` round-trip — confirms CC events present when PortamentoMs > 0 AND absent when PortamentoMs == 0); `Portamento_AndLegato_Compose` (sibling composition Open Question 4)
- `tests/test_dx_legato.flow` (created) — Smoke: `(legato seq 0.5)` → renderSong → writeWav (352844 bytes), prints `DX-14 legato: PASSED`
- `tests/test_dx_portamento.flow` (created) — Smoke: `(portamento seq 100ms)` → writeMidi (119 bytes valid SMF), prints `DX-14 portamento: PASSED`
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` (modified) — Added `DurationOverlap` and `PortamentoMs` properties + ctor parameters appended at end of signature; extended `With(...)` builder with two new nullable optional params + null-coalesce preservation logic
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` (modified) — Added DurationOverlap branch immediately after the existing IsTied block (analog at lines 67-72)
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` (modified) — Extended per-note loop with extendedBeats + CC65/CC5 bracket-open + NoteOff at extended endpoint + CC65=0 bracket-close + Pitfall 3 critical line `barTick += (long)(beats * ticksPerQuarter)`
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (modified) — Added `RegisterArticulationTransforms` static method registering both transforms. Both call `note.With(...)` naming only their owned slot.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modified) — One-line addition: `Transforms.TransformFunctions.RegisterArticulationTransforms(registry);` immediately after `TransformFunctions.Register(registry);`
- `flow-lang/std.flow` (modified) — Two new `internal proc` declarations alongside existing transforms with explanatory comments
- `flow-lang.Tests/FlowScriptData.cs` (modified) — Two new `RequiredSentinels` entries pinning `DX-14 legato: PASSED` and `DX-14 portamento: PASSED`

## Decisions Made

- **Defaulted-parameter migration over visitor pattern (continuity from 22-05)**: `DurationOverlap` and `PortamentoMs` are new fields on `MusicalNoteData` rather than parallel data structures. Matches the Phase 18 `DurationFraction` shape and the 22-05 `OnsetOffset` shape — 30+ existing positional call sites compile unchanged, byte-identity preserved by construction. ByteIdentical 6/6 confirms.
- **Builder helper extended in lockstep with each plan's field**: 22-05 introduced `With(double? onsetOffset = null)`. 22-06 extends it to `With(double? onsetOffset = null, double? durationOverlap = null, double? portamentoMs = null)`. Each transform names only its owned slot — `note.With(durationOverlap: x)` for legato, `note.With(portamentoMs: x)` for portamento. The null-coalesce inside the helper preserves all sibling fields automatically. Rolling back 22-06 only removes its appended slots + fields + helper params; 22-05's `note.With(onsetOffset:)` call sites stay compiling.
- **BarRenderer reads DurationOverlap AFTER ToTimeline produces onsets, mirroring the IsTied analog at lines 67-72**: Per CONTEXT D-02 + Pitfall 3, legato extends duration without moving onsets. The mechanism is structurally identical to the existing `IsTied` overlap pattern: read the per-note flag, extend `durationBeats`, render. Onset position came from `bar.ToTimeline()` which ran BEFORE the render loop and is not affected by DurationOverlap. Pinned by `LegatoFacts.OnsetsUnchanged` which compares ToTimeline output before vs after legato.
- **MidiExport extends NoteOff but NOT barTick (Pitfall 3 critical)**: The MIDI version of legato is the genuine SMF construct — overlapping NoteOn/NoteOff events are valid and the receiving DAW or synth handles them via voice allocation. NoteOff lands at `barTick + extendedBeats × ticksPerQuarter`, but `barTick += (long)(beats × ticksPerQuarter)` (ORIGINAL beats). This is the precise mechanism that distinguishes legato from a tempo slowdown. The plan's acceptance criteria include an explicit grep gate on the literal advance line to catch any future accidental swap to `extendedBeats`.
- **CC5 mapping is linear `byte = clamp(round(ms × 127 / 200), 0, 127)`**: per CONTEXT Claude's Discretion. Anchor points: 0→0, 100→64, 200→127. The clamp guards both directions: PortamentoMs ≥ 200 wraps to 127 (no SevenBitNumber overflow), PortamentoMs < 0 clamps to 0 (no negative SevenBitNumber). Charitable D-07: out-of-range PortamentoMs is silently corrected, no exception. Documented in the `PortamentoMs` xmldoc.
- **CC65=0 bracket-close at note end**: Per CONTEXT, MIDI emits a per-note bracket: `CC65=127` at start (portamento on), `CC5=value` at start (portamento time), `CC65=0` at end (portamento off). The bracket-close ensures the next note doesn't inherit portamento by accident — receivers that hold CC state across notes will reset to non-portamento mode at the bar end. Pinned by `PortamentoMidiFacts.Portamento_BracketCloseEmitsCC65Zero`.
- **Audio renderer ignores PortamentoMs in v1.3 — MIDI-only articulation**: Per CONTEXT, audio-side glide via wave-table re-trigger or pitch-bend interpolation is non-trivial and deferred. The xmldoc on `PortamentoMs` documents this asymmetry. The composition fact `Portamento_AndLegato_Compose` verifies that even though only `legato` has audio consequences in v1.3, both fields stamp through cleanly and survive composition.
- **`RegisterArticulationTransforms` is wired explicitly from BuiltInFunctions, not from inside `TransformFunctions.Register`**: The plan's acceptance criteria require `grep -F 'TransformFunctions.RegisterArticulationTransforms' flow-lang/StandardLibrary/BuiltInFunctions.cs >= 1`. Inline-from-Register would not satisfy the grep, and the explicit wiring matches the convention established by 22-04 (`EffectsFunctions.RegisterContextDependent`) and 22-05 (`TransformFunctions.RegisterContextDependent`). Stateless registration (no ExecutionContext needed) because BarRenderer/MidiExport read the per-note fields directly.
- **CONTEXT line 18 rollback-independence guard pinned by an awk-based grep**: The plan acceptance criteria include `awk '/RegisterArticulationTransforms/,/^    }$/' flow-lang/StandardLibrary/Transforms/TransformFunctions.cs | grep -c 'OnsetOffset'` returning 0. This proves the articulation transforms never enumerate 22-05's slot — rolling back 22-05 would only remove its slot from the shared With(...) helper, not break 22-06's transforms.

## Deviations from Plan

**Total deviations:** 2 (1 Rule 1 test bug, 1 documentation count discrepancy)
**Impact on plan:** None — verification still GREEN; both deviations follow established Phase 22 conventions.

### 1. [Rule 1 - Test bug] `Legato_OnEmptySequence_ReturnsEmpty` used invalid `| |` syntax

- **Found during:** Task 2 (Facts run after GREEN implementation)
- **Issue:** Plan's Task 1 spec for Test 5 said `(legato emptySeq 0.5) → empty`, written as `Sequence src = | |` in Flow source. Flow's lexer requires at least one note in a note stream — `| |` produces a parse error and `errorCount == 1`, breaking the `Assert.Equal(0, errorCount)` precondition.
- **Fix:** Renamed the Fact to `Legato_OnSingleNoteSequence_PropagatesField` and changed the input to `| C4q |` (one-note sequence). The Fact still pins the same invariants (no crash, no error, DurationOverlap stamped on every note) but using a syntactically valid input. Same charitable-smoke evidence; documented in the Fact body comment.
- **Files modified:** `flow-lang.Tests/Unit/Phase22/LegatoFacts.cs`
- **Verification:** All 17 LegatoFacts + PortamentoMidiFacts GREEN after the fix.
- **Committed in:** `d2bde5d` (Task 2 commit) — the renamed Fact shipped together with the GREEN body since the original Fact would have failed even before the GREEN registration landed.

### 2. [Documentation] Plan referenced "ByteIdentical 19/19" but actual count is 6

- **Found during:** Task 3 (verification gate)
- **Issue:** Plan's `<verification>`, `<success_criteria>`, and acceptance criteria all reference `ByteIdenticalTutorialTests + ByteIdenticalShowcaseTests stay 19/19 GREEN`. The actual byte-identical regression gate consists of 6 tests across 3 classes: `ByteIdenticalTutorialTests` (2: WAV + MIDI), `ByteIdenticalShowcaseTests` (2: WAV + MIDI), `EuclideanByteIdenticalTests` (2: WAV + MIDI). Same documentation lag observed in 22-01 through 22-05.
- **Fix:** Documented actual count (6/6) in Task 3 commit message and this summary. No code change required.
- **Verification:** `dotnet test --filter ByteIdentical` enumerates and runs 6 tests; all 6 GREEN.

### Echo deviations from prior plans (informational only, not adjudicated again)

- **`tests/` directory is gitignored**: `git add -f` used for `tests/test_dx_legato.flow` and `tests/test_dx_portamento.flow`, matching the convention from 22-01 through 22-05.

## Threat Surface — STRIDE Compliance

The plan's `<threat_model>` lists five dispositions. All five are honored by the GREEN implementation:

| Threat ID | Disposition | Mitigation |
|-----------|-------------|------------|
| T-22-V5-22 (Tampering: CC5 overflow when PortamentoMs is very large) | mitigate | `Math.Clamp((int)Math.Round(ms * 127.0 / 200.0), 0, 127)` BEFORE SevenBitNumber cast in MidiExport.cs. `PortamentoMidiFacts.MsToFiveCC_OutOfRangeIsClamped` pins the upper-bound clamp (`PortamentoToCC5(99999.0) == 127`). |
| T-22-V5-23 (Tampering: negative PortamentoMs produces negative CC5) | mitigate | Same `Math.Clamp(...0, 127)` lower bound. `PortamentoMidiFacts.MsToFiveCC_OutOfRangeIsClamped` pins (`PortamentoToCC5(-50.0) == 0`). |
| T-22-V5-24 (DoS: extreme DurationOverlap produces huge audio buffer) | accept | Same threat surface as user setting `Duration=WHOLE` with extreme tempo. BarRenderer uses durationBeats which feeds the existing audio buffer allocator. No new attack surface. |
| T-22-V5-25 (Repudiation: byte-identity at default) | mitigate (CRITICAL) | Both DurationOverlap and PortamentoMs default to 0.0 at the END of the constructor signature. BarRenderer's `if (DurationOverlap > 0.0)` and MidiExport's `if (PortamentoMs > 0.0)` guards short-circuit when the fields are dormant. ByteIdentical 6/6 GREEN confirms empirically. |
| T-22-V5-26 (Repudiation: MidiExport tick advance accidentally uses extendedBeats) | mitigate | Explicit comment + literal grep gate in plan acceptance criteria on `barTick += (long)(beats * ticksPerQuarter)` (NOT extendedBeats). Plus the implicit ByteIdentical 6/6 contract — any swap to extendedBeats would shift NoteOn ticks for every legato note and break MIDI byte-identity. |

## Issues Encountered

- **`tests/` directory is gitignored**: First `git add tests/test_dx_legato.flow` would have been blocked by `.gitignore` line 7. Resolved with `git add -f` — same convention as 22-01 through 22-05.
- **`Legato_OnEmptySequence_ReturnsEmpty` invalid Flow syntax**: covered in Deviation 1 above. Caught at Task 2 GREEN run; corrected mid-task.

## Next Phase Readiness

- **DX-14 is the sixth of seven Phase 22 plans (22-01 DX-10 + 22-02 DX-15 + 22-03 DX-11 + 22-04 DX-12 + 22-05 DX-13 + 22-06 DX-14 shipped)**. 22-07 (closure) is the only remaining plan.
- **`MusicalNoteData` migration tally at 22-06 close**: three Phase 22 defaulted-parameter fields (OnsetOffset, DurationOverlap, PortamentoMs) appended in three separate plans; ByteIdentical 6/6 GREEN throughout. The migration shape generalizes cleanly.
- **`MusicalNoteData.With(...)` is now a fully exercised builder helper** — three slots, three independent transforms (quantize from 22-05; legato + portamento from 22-06), null-coalesce preserves siblings on every call. Future plans extending MusicalNoteData (e.g. groove templates, per-note pan, microshift, articulation envelopes) follow the same shape: append a defaulted-parameter at end of ctor + matching nullable optional param to With(...).
- **MIDI CC emission pattern is now established** for any future plan that needs to ship articulation control changes (Sweetwater MIDI CC reference). The pattern is: stamp a per-note field in the transform; emit `ControlChangeEvent` events from the per-note loop in `MidiExport`; clamp before SevenBitNumber cast (V5 input validation); use the bracket-open/bracket-close pair pattern when the CC has on/off semantics. Future plans for aftertouch (CC65 family), modulation wheel (CC1), expression (CC11), or sustain pedal (CC64) can reuse this shape.
- **Pitfall 3 (extend duration without moving onsets) pattern is now ratified** for any future articulation that affects render-time duration without affecting sequential timing. The pattern is: stamp a defaulted-parameter on the note; renderer reads it AFTER `bar.ToTimeline()` produces onsets; renderer extends durationBeats locally without advancing the sequential cursor. Future plans for staccato density variation, accent notes with auto-extension, or breath marks with mid-phrase pause can reuse this shape.
- **Byte-identical regression gate proven robust under three consecutive MusicalNoteData ctor migrations** (22-05 OnsetOffset + 22-06 DurationOverlap + 22-06 PortamentoMs) — ByteIdentical 6/6 GREEN at every milestone. Confirms the Phase 18 migration shape generalizes to any Phase 22+ plan that needs to add a defaulted field to MusicalNoteData. The pattern is now de-risked for 22-07 closure if it touches the type.

## Self-Check

Files verified:
- FOUND: `flow-lang.Tests/Unit/Phase22/LegatoFacts.cs`
- FOUND: `flow-lang.Tests/Unit/Phase22/PortamentoMidiFacts.cs`
- FOUND: `tests/test_dx_legato.flow`
- FOUND: `tests/test_dx_portamento.flow`
- FOUND: `.planning/phases/22-tier-b-c-composer-dx-bundle/22-06-SUMMARY.md`

Commits verified:
- FOUND: `2f860f8` (Task 1 RED)
- FOUND: `d2bde5d` (Task 2 GREEN)
- FOUND: `332154c` (Task 3 verification)

Final acceptance run:
- LegatoFacts: **8/8 GREEN**
- PortamentoMidiFacts: **9/9 GREEN**
- ByteIdentical: **6/6 GREEN**
- flow-lang.Tests full suite: **499/499 GREEN**

## Self-Check: PASSED

---
*Phase: 22-tier-b-c-composer-dx-bundle*
*Completed: 2026-05-02*
