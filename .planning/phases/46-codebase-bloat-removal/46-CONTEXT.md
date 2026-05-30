# Phase 46: Codebase Bloat Removal - Context

**Gathered:** 2026-05-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Pay down dead/duplicate/internal cruft accumulated across 40+ phases, acting on
the 2026-05-24 bloat audit (`.planning/research/CODEBASE-BLOAT-AUDIT-2026-05-24.md`).
**Pure removal/redirect — no behavior changes.** Atomic commits per target; one
test-green gate (full `flow-lang.Tests` + every `tests/test_*.flow` + Phase 28
RMS-windowed baselines + two-run cmp-clean determinism).

This discussion materially **re-scoped** the phase vs. the roadmap's locked target
list: the governing removal principle (D-01) keeps composer-callable features that
the audit had flagged purely for low local usage. Net removable is now closer to
~550–650 LOC (down from the audit's ~1,100 upper bound), because Track/Timeline and
bars.flow — the two largest line-count targets — are **kept**.

</domain>

<decisions>
## Implementation Decisions

### Governing Removal Principle
- **D-01 (LOAD-BEARING):** Removal is justified ONLY for (a) genuinely unreachable
  internal plumbing — no caller in `flow-lsp`/`flow-interpreter`/`flow-cli`/tests AND
  not composer-reachable — or (b) pure redundancy where a strictly-better equivalent
  already exists. **Composer-callable features STAY even at zero local example/test
  usage** — absence of usage in this repo ≠ dead code (we can't see all `.flow` code
  users have written). See external memory `feedback_usage_not_removal_signal.md`.
  This OVERRIDES any audit/roadmap "locked removal" label that was justified only by
  low usage.

### Confirmed Removals (pass D-01)
- **D-02:** **TimelineMap editor-highlighting stack** — REMOVE. `TimelineMap.cs` +
  parallel `RenderSongWithTimeline`/`RenderSectionWithTimeline` overloads in
  `SongRenderer.cs:439-540`, `BarRenderer.cs:308-360`, `SequenceRenderer.cs:127-180`
  (~250 LOC). Zero callers anywhere; not composer-reachable (internal render plumbing
  for an editor-highlighting feature never wired to LSP). (audit §1.5/§3.3)
- **D-03:** **NoteSynthesizer private duplicate helpers** — REMOVE/redirect. The 4×
  private `BeatsToSeconds`+`CreateSilence` (11-line blocks) + inline oscillator loops
  in `NoteSynthesizer.cs:24-182` → route through existing `SynthUtils.*` (~80 LOC).
  Oscillator math MUST stay byte-identical to `SynthUtils` (those generators are also
  composer-callable builtins). (§1.1)
- **D-04:** **`Fixtures/` + `fixtures/` case-collision** — MERGE to lowercase
  `fixtures/` (matches larger reference count + Phase 32/33/37 convention). Update the
  6 C# callsite path strings + `git mv`. Highest-priority item — latent
  macOS/Windows case-insensitive-FS breakage. (§1.2)
- **D-05:** **createSineTone/Saw/Square/Triangle dead INTERNAL forward-decls** —
  REMOVE the dead `internal proc` decls in `audio.flow:224-227` ONLY. The
  composer-facing stereo Flow `proc` wrappers (`audio.flow:352-411`) are UNTOUCHED.
  This is internal dead weight (the proc wrapper fully shadows them), NOT a composer
  surface removal. (§1.3, scoped down per D-01)
- **D-06:** **`exportWav` legacy alias** — REMOVE. Pure reversed-arg redundancy of
  canonical `writeWav` (same functionality, different arg order). Migrate ~5 test
  callers to `writeWav`; drop `ExportWav`/`ExportWavWithBitDepth` shims in `FileIO.cs`.
  Qualifies under D-01(b) — strictly-better equivalent exists. *(user-confirmed)* (§2.5)
- **D-07:** **`test.flow` legacy assertion half (lines 30-138)** — REMOVE. Superseded
  by the Phase 35 `@test` module (`assert`/`assertEq`/`assertWithinDb`). Port the one
  consumer `tests/test_test_library.flow` to the `@test` surface. D-01(b). *(user-confirmed)* (§2.4)
- **D-08:** **`ClampSamples` thin-wrapper shims** — INLINE to direct
  `AudioUtils.ClampSamples()` calls (`PulseAudioSimpleBackend.cs`,
  `PlaybackFunctions.cs`). Internal indirection only. (§2.1)
- **D-09:** **Phase35/diagnostics/*.txt orphaned baselines** — REMOVE, but only after
  confirming (cheap grep at execute time) that `DiagnosticRendererGoldenTests.cs` uses
  inline golden assertions and does not read the `.txt` files. (§1.6)

### Confirmed Keeps (composer-callable — D-01)
- **D-10:** **Track/Timeline DAW multitrack layer** — KEEP. Verified a genuinely
  distinct capability: manual `Voice` grouping + per-track gain/pan/offset +
  `renderTrack` mixing. Shares the `Voice` type with Song/Section but is a parallel
  *lower-level* abstraction — NOT integrated into the Song render path, NOT a redundant
  spelling. `test_full_song.flow` uses both side-by-side. **Overrides** the roadmap's
  locked removal (which was usage-justified). Document as legacy (D-16). (§1.4)
- **D-11:** **bars.flow legacy Bar API** — KEEP. Verified orthogonal to Phase 45 Beats
  (zero `beat` references — it's *measure* construction, a different axis). Superseded
  by note-stream literals `| C4 D4 E4 |` but still a usable bar-construction surface.
  `std.flow:6` import stays. **Overrides** roadmap's locked removal. Document as legacy. (§1.7)
- **D-12:** **Progression DSL (`progression | I IV V |`)** — KEEP + INVEST. Add unit
  tests (currently none) + add to a showcase example. A distinct ergonomic
  chord-progression syntax worth keeping alongside the in-key numeral path.
  `ProgressionExpression` + `ProgressionCompiler` + parser/lexer arms stay. *(user-confirmed)* (§2.2)
- **D-13:** **OscillatorState / Envelope low-level synth API** — KEEP as a supported
  composer surface (genre-agnostic value for custom-synth / sound-design composers). (§3.1)
- **D-14:** **audio.flow buffer convenience layer (§2.3)** — KEEP. Composer-callable
  (`createBufferStereoCustom`/`isMono`/`fill`/`sampleAt`/etc.). Reverses the audit's
  remove suggestion per D-01.
- **D-15:** **`preview` builtin (§1.8)** — KEEP. A registered composer-callable
  playback path; low local usage is not removal justification under D-01. Follows from
  the principle (not separately asked); flagged for the planner.

### Keep Treatment
- **D-16:** For kept-but-superseded surfaces (Track/Timeline, bars.flow), add a short
  "legacy / superseded by X — kept as a usable surface" note in source/docs so future
  readers know the canonical path. **No deprecation warnings, no stderr advisories**
  (pre-traction; soft-deprecate rejected as premature). *(user-confirmed)*

### Breadth / Appetite
- **D-17:** Phase scope = audit **§1 (high-priority) + §2 (medium)**, FILTERED by D-01
  — only the internal/redundant items within those sections are removed; composer-facing
  items are kept. The audit's **§3 low-confidence direction calls** (conversion-proc
  unification, etc.) are OUT of this phase to avoid turning a mechanical cleanup into
  product-direction debates. *(user-confirmed breadth = §1 + §2)*

### Process (carried / locked)
- **D-18:** Atomic commit per target (roadmap-locked) for selective revert. One
  test-green gate covering the full suite + RMS baselines + two-run cmp-clean. Pure
  removal/redirect — zero behavior change.
- **D-19:** Pre-traction no-deprecation latitude — removals land in single commits, no
  migrators, callers ported in-place. See `project_pre_public_no_legacy_burden.md`.

### Claude's Discretion
- Ordering of cleanup targets within the phase (suggest: D-04 Fixtures merge first as
  the latent-bug risk reducer).
- Whether D-09's confirm-grep is its own task or folded into the removal task.
- Exact wording/placement of the D-16 legacy doc notes.
- Verification mechanics beyond the locked test-green gate.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The audit (primary input)
- `.planning/research/CODEBASE-BLOAT-AUDIT-2026-05-24.md` — every target (§1 high,
  §2 medium, §3 low-confidence), the §4 quantification table, and the §5 anti-findings.
  Read with D-01 as the filter: usage-justified removals of composer-callable features
  are REVERSED to keeps in this phase.

### Prior-audit boundary
- `.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md` — Phase 42's reflective
  audit; this bloat pass deliberately did NOT re-cover what 42 already addressed.

### Intentional patterns (anti-scope source)
- `CLAUDE.md` — per-synth ≤25-line delegation shells (Phase 29), hand-rolled DSP
  rejections (NWaves/RubberBand/NAudio), music-type singletons (`CentType.cs:24-27`),
  Pidgin referenced-but-unused, flow-lang/flow-interpreter split, CC-BY 4.0 samples.

### Governing principles (external memory)
- `feedback_usage_not_removal_signal.md` — D-01 source (NEW, 2026-05-30).
- `project_pre_public_no_legacy_burden.md` — D-19 single-commit removal latitude.
- `feedback_charitable_interpretation.md` — charitable fallbacks are anti-scope.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SynthUtils` (`GenerateSine/Saw/Square/Triangle`, `CreateSilence`, `BeatsToSeconds`,
  `ToMonoBuffer`) — D-03 redirects NoteSynthesizer here; already used by
  WavetableSynthesizer + every Phase 29 sampled synth.
- `@test` module (`test.flow:1-29`, `assert`/`assertEq`/`assertWithinDb`) — D-07 port
  target for `tests/test_test_library.flow`.
- `writeWav(String, Buffer)` canonical export — D-06 migration target for `exportWav` callers.

### Established Patterns
- Note-stream literal path (`NoteStreamCompiler`, `| C4 D4 E4 |`) is the canonical
  measure-construction surface that made bars.flow legacy — but bars.flow is KEPT (D-11).
- Song/Section render path (`SongRenderer`, sections→sequences→voices→mix) is the
  canonical arrangement path; Track/Timeline is a parallel lower-level mixing layer,
  KEPT (D-10).
- `Voice` is shared by both the Song renderer and `Timeline.CreateVoice` — confirms
  Track is complementary, not redundant.

### Integration Points
- `std.flow:6` imports `@bars` → import STAYS (D-11 keeps bars.flow).
- `exportWav` removal (D-06) → migrate ~5 test files + drop `FileIO.cs` shims.
- TimelineMap removal (D-02) → 4 renderer files lose their parallel TimelineMap overload;
  the primary (non-TimelineMap) render path is unaffected.
- Fixtures merge (D-04) → 6 C# test files have hardcoded `Fixtures/`-cased path strings.

</code_context>

<specifics>
## Specific Ideas

- User's framing, verbatim intent: "Don't remove things just because there aren't
  examples using it. If it's a feature which users can use then keep it. We can't have
  all flow code ever written locally to see what's used or will be used." → D-01.
- User asked two scoping questions answered from code during discussion: bars.flow is
  Beat-orthogonal (measure vs duration axis); Track/Timeline shares `Voice` with
  Song/Section but is a separate manual-mixing abstraction, not integrated.

</specifics>

<deferred>
## Deferred Ideas

- **flow-lsp editor live-highlighting** — the actual feature TimelineMap (D-02) was
  scaffolding for. If v1.6 LSP work wants it, re-add then (cheap under pre-traction).
- **§3.2 conversion-proc unification** (frames/beats/seconds across composition.flow +
  audio.flow + Phase 43 `beatToSec`/`secToBeat`) — product-direction call, future phase.
- **§2.6 FlowFunctionSynthesizer inlining / §2.7 IFunctionInvoker** — audit awareness-only,
  not actionable today; revisit if a future refactor folds ExpressionEvaluator into Interpreter.

</deferred>

---

*Phase: 46-codebase-bloat-removal*
*Context gathered: 2026-05-30*
