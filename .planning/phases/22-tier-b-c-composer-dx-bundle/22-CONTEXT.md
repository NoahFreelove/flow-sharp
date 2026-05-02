# Phase 22: Tier B/C Composer DX Bundle - Context

**Gathered:** 2026-05-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Six independently-shippable Tier B/C composer DX features, each closing one REQUIREMENTS row:

- **DX-10** — `arpeggio(chord, rate, direction, pattern)` extends existing `arpeggio` with rate (NoteValue/Fraction), direction (`up`/`down`/`updown`/`downup`/`random`), pattern (`linear`/`chord-tone`/`scale-tone`)
- **DX-11** — `inversion(chord, n)` and `voicing(chord, name)` for `drop2`/`drop3`/`open`/`close`/`spread`
- **DX-12** — `delay(buffer, noteValueRate, feedback, mix)` overload using NoteValue/Fraction synced to active tempo (existing ms-rate overload unchanged)
- **DX-13** — `quantize(seq, resolution, strength, swing)` snaps onsets to a grid; resolution is NoteValue/Fraction; strength 0–1; swing -1..1
- **DX-14** — `legato(seq, overlap)` extends durations by overlap factor; `portamento(seq, glideTime)` emits MIDI CC65+CC5
- **DX-15** — `loadWav(path, semitones)` and `loadWav(path, ratio)` overloads varispeed-pitch-shift via OLA + linear/sinc resample; existing `loadWav(path)` unchanged

Each feature is independently shippable — failure or rollback of one MUST NOT block the others.

</domain>

<decisions>
## Implementation Decisions

### Legato overlap semantics (DX-14)
- **D-01:** `legato(seq, overlap)` extends each note duration to `dur × (1 + overlap)`. So overlap=0.0 = no change, overlap=0.2 = 1.2× duration, overlap=1.0 = 2× duration.
- **D-02:** Notes are allowed to overlap into the next-note onset — true polyphonic legato phrasing, not gap-filling. The audio renderer's existing polyphonic mix pipeline handles overlapping voices automatically; no new voice-allocation work needed.
- **D-03:** MIDI export emits genuinely overlapping note-on/note-off events (note-off of N happens AFTER note-on of N+1 when overlap pushes past the boundary). DryWetMidi handles this correctly per its event-stream model.

### Quantize swing semantic (DX-13)
- **D-04:** Swing magnitude is **linear**: `offset = swing × (subdivision_length / 2)`. swing=0 → no shift; swing=1 → offbeat shifts by exactly half a subdivision (full triplet/dotted-eighth feel). Linear interpolation between. Matches DAW "swing %" sliders where 100% = full triplet swing.
- **D-05:** Swing is **signed**: positive shifts offbeats LATER (drag/jazz feel), negative shifts offbeats EARLIER (push/forward feel). swing=-0.5 and swing=+0.5 produce equal-magnitude shifts in opposite directions. Range is genuinely -1..1.
- **D-06:** Swing applies to every other subdivision at the requested resolution (the "offbeat" of the grid). For a 1/16 quantize, every 2nd 16th note shifts; for a 1/8 quantize, every 2nd 8th note shifts. Resolution determines the swing unit.

### Voicing on incomplete chords (DX-11)
- **D-07:** Per project memory (charitable interpretation): when a chord has fewer notes than the requested voicing requires (drop2/drop3 need ≥4 notes; spread/open need ≥3 notes), `voicing` returns the input chord **unchanged**. No error, no warning, no log spam. Composer can keep iterating. This decision applies symmetrically to all named voicings — no special-casing per voicing name.
- **D-08:** This behavior is **documented in code** — `voicing` function's doc comment (in `flow-lang/StandardLibrary/Harmony/`) explicitly says "Returns input unchanged if the chord lacks enough notes for the named voicing. See Phase 22 CONTEXT D-07." So users who hit the case can grep their way to the explanation.

### Claude's Discretion
- **Plan decomposition** — User declined to discuss; trust the planner. Recommended baseline: 6 plans, one per DX-1X feature, all in Wave 1 except DX-12/DX-13 which depend on Phase 18 Fraction (already shipped). Planner should optimize for parallelism and clean per-feature reverts. If a feature is too small to justify its own plan (e.g., DX-10 may be a thin extension of existing `arpeggio`), grouping is fine.
- **`loadWav` overload disambiguation (DX-15)** — Existing `OverloadResolver` already handles Int vs Float dispatch by argument type. `loadWav("kick.wav", 12)` → semitones (Int); `loadWav("kick.wav", 1.5)` → ratio (Float). Plan can confirm by reading `flow-lang/TypeSystem/OverloadResolver.cs`.
- **Resampler choice (DX-15)** — "OLA + linear/sinc" is the spec. Recommend **linear** as the default (cheap, deterministic, "good enough" for varispeed). If quality complaints surface in UAT, sinc can be added as a future overload — out of scope for this phase.
- **Portamento CC5 mapping (DX-14)** — Spec says "CC5=64-ish events". Recommend a **linear ms→CC5 mapping** with a documented reference curve (e.g., 0ms→0, 100ms→64, 200ms→127 clamped). Document the curve in CONTEXT and the function doc comment so users can predict the value on the receiving synth.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` lines 64–69 — DX-10 through DX-15 acceptance criteria (canonical contract)
- `.planning/ROADMAP.md` § "Phase 22: Tier B/C Composer DX Bundle" — success criteria and dependency on Phase 18

### Phase 18 dependency (Fraction infrastructure)
- `.planning/phases/18-foundation-rational-duration-arithmetic/18-VERIFICATION.md` — what Fraction guarantees (DX-12 + DX-13 build on this)
- `.planning/phases/18-foundation-rational-duration-arithmetic/18-01-SUMMARY.md` — Fraction API
- `flow-lang/Runtime/Fraction.cs` — Fraction implementation; rate-math helper for tempo/duration conversions

### Existing code to extend / inspect
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — existing `arpeggio` (DX-10 extends), `chordNotes`, `chordRoot`, `chordQuality` (DX-11 builds on)
- `flow-lang/StandardLibrary/Harmony/ChordParser.cs` — chord literal parsing (DX-11 voicings operate on parsed chord notes)
- `flow-lang/StandardLibrary/Audio/DSP/Delay.cs` — existing ms-rate `delay` (DX-12 adds NoteValue overload alongside)
- `flow-lang/StandardLibrary/Audio/FileIO.cs` — existing `loadWav` (DX-15 adds varispeed overloads alongside)
- `flow-lang/Runtime/MusicalContext.cs` — active tempo/timesig (DX-12 reads tempo for delay sync; DX-13 reads timesig for grid alignment)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` — existing sequence transforms (DX-13/14 sequence operations follow this pattern)
- `flow-lang/TypeSystem/OverloadResolver.cs` — Int vs Float dispatch (DX-15 overload disambiguation)

### MIDI export reference
- `flow-midi/Midi/` (DryWetMidi integration) — DX-14 portamento emits CC65/CC5 via this pipeline
- Sweetwater MIDI CC reference (external, cited in REQUIREMENTS.md) — CC65 = portamento on/off, CC5 = portamento time

### Project memory (CLAUDE.md auto-memory, applies to all phases)
- `feedback_charitable_interpretation.md` — silent-and-documented assumptions over errors when musical intent is clear; D-07 (voicing) directly applies this
- `feedback_language_philosophy.md` — functional S-expression style, no infix; acceptance examples already use this

### Test patterns to follow
- `flow-lang.Tests/Unit/Phase18/` — Fraction Facts (template for new arithmetic Facts in DX-12/13)
- `flow-lang.Tests/Unit/Phase21/` — recent xUnit Facts patterns (PragmaScannerFacts uses `FlowScriptData.FindTestsRoot()` for cwd portability)
- `flow-lang.Tests/Integration/` — sequence-level integration patterns
- `tests/test_*.flow` — `.flow` script integration loop (each feature should ship at least one `tests/test_dx_NN.flow` smoke script)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Fraction` (Phase 18)** — DX-12 delay sync and DX-13 quantize grid both use `Fraction` for exact tempo/duration math. No new arithmetic primitives needed.
- **Existing `arpeggio` (HarmonyFunctions.cs)** — DX-10 extends in place; do not create a parallel function. Add the 4-arg overload alongside the existing signature.
- **Polyphonic audio mix pipeline** — DX-14 legato relies on the renderer already mixing overlapping voices (no new voice allocator needed). Confirm in `flow-lang/Audio/AudioPlaybackManager.cs` and `SongRenderer.cs` during planning.
- **`MusicalContext` stack** — DX-12 reads active tempo; DX-13 reads active timesig. Both use the existing `MusicalContext.Current` accessor.
- **`OverloadResolver` Int/Float dispatch** — DX-15 disambiguates `loadWav(path, semitones:Int)` from `loadWav(path, ratio:Float)` via existing type-based dispatch.

### Established Patterns
- **Test scaffolding (Wave 0)** — Phase 18-21 all wrote failing xUnit Facts BEFORE production code (RED → GREEN). Phase 22 plans should follow this TDD pattern.
- **Atomic commits per task** — Phase 18-21 committed each task individually with conventional-commit messages prefixed by plan ID (`feat(22-NN): ...` / `test(22-NN): ...`). Phase 22 follows.
- **`tests/test_*.flow` smoke scripts** — Each new feature ships at least one `.flow` script demonstrating it; the integration loop runs all such scripts and any non-zero exit is a regression.
- **Phase 18 byte-identical regression gate** — Every Phase 19/20/21 closure verified `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` 19/19 GREEN. Phase 22 must too — DX-12 (NoteValue delay) and DX-13 (quantize) touch tempo/timing math; regressions here would change rendered audio bytes.

### Integration Points
- **DX-12 delay overload** integrates with `MusicalContext.Current.Tempo` to compute ms-time from NoteValue rate
- **DX-13 quantize** integrates with `MusicalContext.Current.TimeSignature` for grid alignment
- **DX-14 portamento** integrates with the MIDI export pipeline (`flow-midi/`) — this is the first feature outside `flow-lang/` proper to ship MIDI CC events
- **DX-15 loadWav variants** integrate with the existing WAV reader in `FileIO.cs`; resample step happens after RIFF parse, before returning the buffer

</code_context>

<specifics>
## Specific Ideas

- **Acceptance examples are S-expression style** — `(arpeggio Cmaj7 q "up" "linear")` per REQUIREMENTS DX-10. All Phase 22 documentation, test scripts, and tutorial snippets should mirror this style. No infix operators introduced.
- **`portamento(seq, 100ms)`** — REQUIREMENTS uses `100ms` literal — confirms `Millisecond` type literal works in user-facing examples (already shipped). Documentation should use `100ms` not `(milliseconds 100)`.
- **`loadWav("kick.wav", 12)` returns one octave higher** — REQUIREMENTS spec is explicit: 12 semitones = 1 octave = 2× frequency = 0.5× sample count. Smoke test must assert sample-count exactly halves (within ±1 for OLA window edge).

</specifics>

<deferred>
## Deferred Ideas

- **Phase-vocoder time-preserving pitch shift for `loadWav`** — explicit anti-feature for v1.3 per REQUIREMENTS.md line 104 (no clean single-file pure-C# implementation; varispeed-only ships in DX-15). Future v1.4 phase candidate.
- **Auto-derived chord-tone / scale-tone arpeggio sequencing beyond the basic `pattern` enum** — REQUIREMENTS line 105 explicitly defers richer pattern logic. Future phase candidate if composer feedback demands.
- **Sinc resampler quality option for `loadWav`** — DX-15 ships linear-only by default per Claude's discretion D-15. Adding a sinc overload (`loadWav(path, semitones, "sinc")`) is a clean future extension if quality complaints surface.
- **Configurable portamento mapping curve** — D-15 picks a linear ms→CC5 default. If composers want exponential or per-synth-table mapping, that's a v1.4+ extension.
- **Strict mode for `voicing`** — D-07 picks charitable-only. If a power user wants `voicing(chord, name, "strict")` to error on incomplete chords, that's a clean future extension that doesn't break existing scripts.

</deferred>

---

*Phase: 22-tier-b-c-composer-dx-bundle*
*Context gathered: 2026-05-01*
