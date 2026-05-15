# Phase 33: SFZ Orchestral Sampler — Specification

**Created:** 2026-05-15
**Ambiguity score:** 0.11 (gate: ≤ 0.20)
**Requirements:** 8 locked

## Goal

Flow gains a real SFZ-format orchestral sampler — gated behind `use "@sfz"` —
so composers can load CC-licensed external libraries (blessed: VSCO Community CE)
via `loadSfz #violin` / `loadSfz #cello` style calls, where instrument symbols
resolve through a shipped relative-path dictionary joined to a `sfz_root` key
in the Phase 30 flow config. The parser handles 13 common-subset opcodes plus
`<region>`/`<group>`/`<global>` headers; held sustained notes loop their region
without audible boundary clicks (equal-power crossfade); rendering coexists with
Phase 29's bundled-sample fast path without retrofitting it.

## Background

Today (post-Phase 32 baseline — Phase 32 closed 2026-05-15 at 9b0e69c):

**What exists:**
- Phase 22 `loadWav(path)` / `loadWav(path, semitones)` / `loadWav(path, ratio)` at
  `flow-lang/StandardLibrary/Audio/FileIO.cs:290-329` — linear-interpolation
  varispeed pitch shift, the primitive a sampler needs to play a pitch the
  recorded sample doesn't directly cover.
- Phase 29 `SampledInstrumentRenderer.cs` (224 LOC) + `SampleCache.cs` (244 LOC)
  at `flow-lang/StandardLibrary/Audio/` — nearest-pitch lookup, eager-load on
  `renderSong` entry, velocity-layer crossfade (piano pp/ff), per-FlowEngine
  cache lifetime, deterministic file-load order (two-run byte-identical contract).
- Phase 29 ships 21 CC-BY 4.0 University-of-Iowa WAVs at
  `flow-lang/Samples/{piano,brass,sax,strings,flute,bell}/` (3.05 MB total,
  ≤ 5 MB cap enforced by `RepoSizeTests`). Coverage: single-velocity for most;
  pp + ff for piano; 1–5 pitches per instrument.
- Phase 28 `Articulation` enum + `SynthUtils.GenerateArticulationADSR` shapes
  attack/sustain/release ON TOP of any sample-based render path.
- Phase 26.1 `Symbol` primitive (`#foo` interned literals) + generic `Dict<K, V>`
  with Symbol-keyed lookups — exactly the data shape needed for
  `Dict<Symbol, String>` instrument-name → relative-path mapping.
- Phase 30 XDG config at `~/.config/flow/config.toml` with existing keys
  `install_path` + `default_tempo` + `default_timesig` + `default_audio_device` +
  `stdlib_search_path` (all functional). Adding a new `sfz_root` key follows the
  same shape; readable via the existing `FlowConfig.Load()` path used by the
  flow CLI.
- File-scope `enable <pragma>;` plumbing at `flow-lang/Parsing/Parser.cs`
  (Phase 21 + 23 + 24 + 25 patterns); `use "@name"` stdlib import at
  `flow-lang/Runtime/ModuleLoader.cs`.

**Realism gap that Phase 33 closes:**
- Phase 29's bundled samples are v1.3-tier — single-velocity-per-pitch for most
  instruments, 6 instruments only, 1–5 pitches per instrument. A real orchestral
  library (VSCO-CE: ~400 MB, multi-velocity, multi-articulation, full chromatic
  pitch coverage, sustain looping) outclasses it on every dimension that matters
  for an orchestral symphony.
- Phase 29 v1.5 backlog explicitly seeds the gaps: sampled-drums, sampled
  articulations, three-velocity piano. SFZ libraries already provide all of those
  natively — Phase 33 supersedes the v1.5 backlog for any instrument a composer
  opts into via `use "@sfz"` + `loadSfz #...`.

**What does NOT exist:**
- No SFZ parser. No region-matching by `(pitch, velocity)`. No sustain looping
  (with or without crossfade). No `loadSfz` builtin. No `Sfz` first-class value
  type. No `@sfz` stdlib file. No `sfz_root` config key. No `sampler:NAME`
  instrument-string dispatch in `SongRenderer`.

**Primary deliverable:** `flow-lang/StandardLibrary/Audio/Sfz/` (new subdirectory)
+ `flow-lang/sfz.flow` (new stdlib file) + `flow-lang/TypeSystem/SpecialTypes/SfzType.cs`
(new Sfz value type) + `sfz_root` config key + sampler:NAME dispatch hook in
`SongRenderer.cs`. Phase 33 is purely additive — Phase 29's path stays untouched.

## Requirements

1. **`use "@sfz"` stdlib import gates the SFZ surface (no raw pragma)**: The
   composer opts in via the stdlib import; without it, `loadSfz` and the
   `sampler:NAME` instrument string are undefined.
   - Current: No `@sfz` stdlib module; no gating mechanism for SFZ-only features.
   - Target: A new `flow-lang/sfz.flow` stdlib file. Importing via `use "@sfz";`
     registers the `loadSfz` builtin in the active `ExecutionContext`, exposes
     the shipped `Dict<Symbol, String>` of instrument-name → relative-path,
     reads the `sfz_root` key from `~/.config/flow/config.toml` once on first
     import, and wires the `sampler:NAME` instrument dispatcher into
     `SongRenderer`. Without `use "@sfz";`, calling `(loadSfz #violin)` raises
     `UndefinedFunctionError` pointing at the missing import; rendering with
     `"sampler:violin"` raises `UnknownInstrumentError` likewise. (Aligns with
     the user's directive: pragmas/imports gate pro-library features; mirrors
     `enable justIntonation;`/`enable scaleLint;` opt-in shape but uses the
     stdlib-import surface rather than a raw `enable` pragma since the new
     feature ships executable code, not just a flag.)
   - Acceptance: A `.flow` file containing `(loadSfz #violin)` without `use "@sfz";`
     errors at parse-or-call time with a message containing `use "@sfz"`;
     adding the import resolves the error.

2. **Symbol-keyed instrument lookup via shipped dict + config-resolved root**:
   `loadSfz #violin` joins a shipped relative path with the composer's
   configured library root; absolute string paths (`loadSfz "abs/path.sfz"`)
   also work.
   - Current: No symbol-keyed dict; no `sfz_root` config key; no `loadSfz` builtin.
   - Target: `@sfz` stdlib ships a frozen `Dict<Symbol, String>` keyed by the
     19 General-MIDI orchestral instrument symbols (`#violin`, `#viola`,
     `#cello`, `#contrabass`, `#flute`, `#oboe`, `#clarinet`, `#bassoon`,
     `#trumpet`, `#horn`, `#trombone`, `#tuba`, `#piano`, `#harp`, `#timpani`,
     `#choir`, `#guitar`, `#harpsichord`, `#celeste`) mapped to VSCO-CE
     relative paths. `(loadSfz #violin)` looks up `#violin` → e.g.
     `"Strings/Violin/violin-Sustain.sfz"`, joins with `sfz_root` from
     `~/.config/flow/config.toml`, parses the resulting absolute path, and
     returns an `Sfz` value. `(loadSfz "/abs/or/relative/path.sfz")` (String
     overload) bypasses the dict entirely and loads the literal path.
     Unrecognized symbols (e.g. `#viol`) error with `UnknownInstrumentSymbolError`
     listing the 19 supported symbols. Missing `sfz_root` key errors with a
     message pointing at `~/.config/flow/config.toml` and the install docs.
   - Acceptance: With `sfz_root="$HOME/.flow/samples/VSCO-CE"` in config and a
     VSCO-CE install at that path, `(loadSfz #violin)` returns a non-null `Sfz`
     value; `(loadSfz #viol)` errors with the 19-symbol list; `(loadSfz "...")`
     with a missing absolute path errors `FileNotFoundError`; missing
     `sfz_root` errors point at the config file.

3. **SFZ parser handles 13-opcode common subset + 3 header types**: The parser
   accepts the 13 ROADMAP-listed opcodes and the `<region>`/`<group>`/`<global>`
   headers; unknown opcodes are silently ignored with a one-shot stderr advisory
   per opcode-name per loaded patch.
   - Current: No SFZ parser at all.
   - Target: A new `Sfz` parser at `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs`
     recognizing the headers `<region>` / `<group>` / `<global>` and the opcodes:
     `sample`, `lokey`, `hikey`, `pitch_keycenter`, `lovel`, `hivel`, `loop_mode`
     (with values `no_loop`/`one_shot`/`loop_continuous`/`loop_sustain`),
     `loop_start`, `loop_end`, `ampeg_attack`, `ampeg_release`, `volume`, `pan`.
     Opcodes outside this 13-entry whitelist parse to a no-op + emit a one-shot
     stderr advisory `[sfz] unrecognized opcode '<name>' in '<patch>' — ignoring`,
     deduplicated per `(patch-description, opcode-name)` per loaded patch.
     Aligns with memory[charitable-interpretation] + Phase 32 D-08 advisory
     pattern.
   - Acceptance: A unit test feeds a synthetic SFZ string containing 13 known
     opcodes + 5 unknown opcodes and asserts: (a) all 13 known opcodes appear in
     the parsed region's opcode dictionary; (b) 5 stderr advisory lines emitted
     once each (no duplicates on a re-load with same patch description);
     (c) parser does not throw.

4. **Region matching by `(pitch, velocity)` + nearest-key resample fallback**:
   At render time, the sampler selects the region whose `lokey..hikey` and
   `lovel..hivel` ranges contain the note; if no region covers the requested
   pitch, the nearest-pitched region's sample is varispeed-shifted via the
   existing `loadWav(path, semitones)` infrastructure.
   - Current: No region matching; no SFZ-aware render path.
   - Target: `SfzRenderer` (new class at `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs`)
     implementing `INoteSynthesizer`. For each note: (a) find all regions whose
     `lokey..hikey` covers the note's MIDI pitch AND `lovel..hivel` covers
     `note.Velocity` mapped to 0..127; (b) if multiple regions match, the
     last-declared region in the .sfz file wins (SFZ spec convention); (c) if
     no region matches by pitch, find the nearest pitch-coverage region and
     varispeed-shift its sample by the pitch delta; (d) if no region matches
     even after fallback, emit a one-shot per-(pitch,velocity) stderr advisory
     and render silence for that note (charitable interpretation — don't kill
     the song over one missing region).
   - Acceptance: A test SFZ fixture with 2 regions covering `C4..F4` and `G4..C5`
     correctly routes A4 (G4..C5 region) and D4 (C4..F4 region) to the right
     sample. A test note at B5 (outside both regions) routes to the nearest
     region (G4..C5) varispeed-shifted +12 semitones — verified by spectral
     fingerprint vs the shipped sample shifted by `loadWav(path, 12)`.
     Two regions overlapping at E4 lov 0..63 + lov 64..127 both targeting C4:
     a vel-0.3 note picks the lov 0..63 region, a vel-0.9 note picks the
     lov 64..127 region.

5. **Equal-power loop crossfade prevents audible clicks at loop boundaries**:
   For regions with `loop_mode=loop_continuous` or `loop_sustain`, the renderer
   crossfades the last N samples of `loop_end` with the first N samples of
   `loop_start` using an equal-power (sin/cos) curve.
   - Current: No loop handling exists; `loadWav` returns a fixed-length buffer
     that the existing sample renderer pads with zeros past the sample body.
   - Target: When `loop_mode` is `loop_continuous` / `loop_sustain`, the renderer
     extends the sample's audible body across the entire authored note duration
     by looping from `loop_start` to `loop_end`. At each loop crossover, the
     last `crossfade_frames = 441` samples (10 ms at 44.1 kHz) of the body
     leading up to `loop_end` are crossfaded with the first 441 samples after
     `loop_start` using `out[i] = cos(πi/2N) * a[i] + sin(πi/2N) * b[i]` —
     equal-power blend that preserves perceived loudness across the transition.
     `loop_mode=one_shot` and `loop_mode=no_loop` skip the crossfade and just
     play the sample once. Articulation envelope from Phase 28 applies on top
     of the looped output (REQ 8 below).
   - Acceptance: A test renders a 4-second sustained `C4w` note using a 1-second
     sample with `loop_mode=loop_continuous`, `loop_start=22050`, `loop_end=44100`.
     The output buffer is exactly 4 seconds long. A per-sample discontinuity
     check over the body verifies no inter-sample amplitude jump exceeds 0.05
     (catches the failure mode from Round 4). An equal-power-vs-linear-crossfade
     spectral comparison confirms the equal-power path preserves the loop body's
     spectral centroid within ±2% relative to a stitch-only baseline.

6. **`Sfz` first-class value type + `sampler:NAME` instrument dispatch**: The
   load path produces a first-class `Sfz` value; binding to a named variable
   registers the patch in the active `ExecutionContext` under that name;
   `renderSong song "sampler:NAME"` dispatches to the sampler renderer for the
   bound patch.
   - Current: No `Sfz` value type; no `sampler:` prefix in SongRenderer; no
     binding-to-renderer registry.
   - Target: New `SfzType` at `flow-lang/TypeSystem/SpecialTypes/SfzType.cs`
     extending `FlowType` (mirrors `TuningType` from Phase 32 — strict, no
     numeric coercion, reference identity). `(loadSfz ...)` returns
     `Value.Sfz(SfzData)`. Assigning to a typed variable (`Sfz violin = ...`)
     registers `(name="violin", patch=value)` in
     `ExecutionContext.SfzPatchRegistry` (new Dictionary). `SongRenderer`
     recognizes instrument strings of the form `sampler:<name>`, looks up
     `<name>` in the registry, and dispatches to `SfzRenderer.Render(note,...)`
     using that patch. Unknown name errors `UnknownSamplerNameError`.
   - Acceptance: A test loads an SFZ patch into `violin`, calls
     `renderSong song "sampler:violin"`, and verifies the output buffer
     has non-zero RMS (the patch actually played). Rendering with
     `"sampler:doesnotexist"` errors with the unknown-name message.
     `(loadSfz #violin) -> renderSong "sampler:violin"` (anonymous binding via
     flow op) is OUT OF SCOPE — explicit binding is the only supported path
     (deferred to v1.5 unless trivial to add at planning time).

7. **CI parser smoke test renders a fixture without errors + non-zero RMS**:
   The acceptance test for "at least one free orchestral library loads + plays
   correctly" is a deterministic smoke test using a tiny self-contained .sfz
   fixture (not a third-party orchestral library), so CI doesn't depend on a
   composer-side download.
   - Current: No SFZ test fixture; no test for SFZ render correctness.
   - Target: Ship a self-contained CI fixture at
     `flow-lang.Tests/fixtures/sfz-smoke/` containing (a) a tiny synthetic
     `smoke.sfz` referencing one or two short `.wav` files (≤ 50 KB total —
     simple sine bursts at known pitches, generated by a helper script and
     committed), (b) a `Phase33SfzSmokeTests` test that loads the fixture,
     renders a 4-bar melody, and asserts: exit code 0; output WAV non-empty;
     RMS > −40 dBFS; per-sample discontinuity check passes on the sustained
     body of a held note. The blessed orchestral library (VSCO-CE) is verified
     manually by the composer per `33-VERIFICATION.md` UAT section — NOT in CI.
   - Acceptance: `dotnet test --filter "FullyQualifiedName~Phase33SfzSmoke"`
     exits 0; the test asserts all four conditions above. `flow-lang.Tests`
     binary stays under the existing 5 MB sample cap (smoke fixture WAVs +
     SFZ < 100 KB).

8. **Phase 28 articulation envelope applies on top of the SFZ render**: The
   articulation rules locked in Phase 28 SPEC-5 (Staccato 25% + sustain=0 +
   release×0.5; Marcato; Tenuto release×1.2; Legato 110% + crossfade; Accent
   +0.30 velocity; Sforzando 1.5×→1.0× envelope spike over first 15% of frames)
   shape the SFZ-rendered buffer the same way they shape every other
   sample-based instrument.
   - Current: Articulation envelope only applies to Phase 29's
     `SampledInstrumentRenderer` and the 9 hand-rolled synthesizers.
   - Target: `SfzRenderer.Render` invokes
     `SynthUtils.GenerateArticulationADSR(note.Articulation, baseAttack=0.005,
     baseDecay=0.05, baseSustain=1.0, baseRelease=0.05, frames, sampleRate,
     isPercussion=false)` after region match + loop expansion + amplitude
     application, and applies the envelope via `SynthUtils.ApplyEnvelope` —
     identical baseline to `SampledInstrumentRenderer` per
     `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs:120-130`.
     The `ampeg_attack` / `ampeg_release` opcodes from the SFZ region act as
     authored hints that override the baseline attack/release for that region
     before the articulation rules layer on top (composer-authored envelope
     respected; articulation modifies it).
   - Acceptance: A piano `C4q` rendered through an SFZ patch under each of the
     6 Phase 28 articulations (Staccato, Tenuto, Legato, Accent, Marcato,
     Sforzando) produces 6 distinct output buffers. RMS-thresholded audible
     duration matches the Phase 28 locked rules within ±5% per articulation.
     A region with `ampeg_attack=0.5` produces a measurably slower attack
     (>200 ms) than a region with `ampeg_attack=0.005` for the same note +
     articulation; Phase 28's articulation rules still shape the tail.

## Boundaries

**In scope:**
- New `flow-lang/sfz.flow` stdlib module gating the SFZ surface
- New `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` for the 13-opcode +
  3-header common subset
- New `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` implementing
  `INoteSynthesizer` (region matching, varispeed fallback, sustain loop with
  equal-power crossfade)
- New `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` first-class value type
  (mirrors Phase 32's `TuningType` shape — strict, reference identity)
- New `(loadSfz Symbol)` + `(loadSfz String)` builtin overloads
- New `sfz_root` key in flow config (`~/.config/flow/config.toml`) wired
  through the Phase 30 `FlowConfig.Load()` path
- Shipped frozen `Dict<Symbol, String>` mapping 19 GM orchestral symbols to
  VSCO-CE relative paths
- New `sampler:NAME` instrument-string dispatcher in `SongRenderer`
- `ExecutionContext.SfzPatchRegistry` for typed-variable binding
- CI smoke test with self-contained synthetic SFZ + WAV fixtures (< 100 KB)
- Composer-facing docs at `examples/symphony/README.md` + a 4-bar
  `examples/symphony/sfz_smoke.flow` runnable example
- Phase 28 articulation envelope hook on top of SFZ render output
- One-shot stderr advisories for: unrecognized opcodes, missing regions,
  missing `sfz_root` config — deduplicated per `(patch, advisory-key)` per
  process

**Out of scope:**
- Full SFZ v2 spec — opcodes outside the 13 listed (`fil_type`, `cutoff`,
  `cutoff_cc*`, `lfo*`, `eq1_freq`, `bend_up`, `xfin_lokey`, etc.) silently
  ignored with stderr advisory. Reason: the symphony showcase needs the
  common subset; the long tail is library-specific polish that doesn't gate
  Phase 34.
- `writeSfz` / SFZ export. Reason: read-only is sufficient for v1.4; nothing
  in Phase 34 needs Flow → SFZ persistence.
- Real-time SFZ editing / hot reload. Reason: composers iterate on Flow
  source, not on .sfz files; FileSystemWatcher integration adds complexity
  without a use case.
- Bundled orchestral library in-repo. Reason: 5 MB cap + CI download time +
  license distribution costs all push against shipping VSCO-CE in tree.
  Composer downloads externally; docs explain how.
- Retrofitting Phase 29's `SampledInstrumentRenderer` to consume SFZ
  underneath. Reason: Phase 29's bundled-sample path stays the zero-config
  default for `piano`/`brass`/`sax`/`strings`/`flute`/`bell`; SFZ is purely
  additive. Composers opting in get richer instruments; everyone else's
  scripts render byte-identically to today.
- Anonymous Sfz value flow (`(loadSfz #violin) -> renderSong "..."` without
  intermediate binding). Reason: the `ExecutionContext.SfzPatchRegistry`
  needs a key; binding to a typed variable provides that key naturally;
  anonymous flow requires a fresh API surface that doesn't pay for itself
  in the v1.4 timeline.
- Adding SFZ patches to MIDI export. Reason: MIDI export uses GM program
  numbers per Phase 28 — a `sampler:violin` patch maps to the same `#violin`
  GM program (40) at MIDI-write time; the SFZ-specific timbre is an audio-
  render concern, not a MIDI concern.
- More than 19 instrument symbols in the shipped dict. Reason: the GM
  orchestral set is well-defined and matches VSCO-CE's coverage; non-GM
  instruments (e.g. `#dulcimer`) can use the absolute-path overload until
  a future phase formalizes them.

## Constraints

- **Linux primary**: matches every prior audio phase; PulseAudio playback and
  WAV file I/O paths unchanged. The SFZ parser is pure C# + filesystem
  reads — cross-platform on its own.
- **External orchestral library is composer-supplied**: nothing > 100 KB
  ships in-repo for SFZ purposes. CI tests use the synthetic smoke fixture
  only. The blessed VSCO Community CE is documented; not vendored.
- **Repo-size CI gate stays at 5 MB**: Phase 29's `RepoSizeTests` budget
  covers `flow-lang/Samples/` — Phase 33 does NOT ship into that directory.
  Phase 33 fixtures live in `flow-lang.Tests/fixtures/sfz-smoke/` and count
  against the test-project budget, target < 100 KB.
- **Determinism preserved**: SFZ parsing + region matching + render path
  must produce two-run byte-identical output to satisfy the Phase 18/25/27
  contract. Region match order is deterministic (last-declared-wins per the
  SFZ spec — already deterministic for any given .sfz file). The shipped
  `Dict<Symbol, String>` is frozen at module load; the `sfz_root` config read
  is one-shot per import. The sample-load order within a render walks regions
  in declaration order.
- **Phase 30 config compatibility**: the new `sfz_root` key follows the
  existing TOML shape; missing key errors clearly without breaking the load
  of unrelated keys. Other keys (`install_path`, `default_tempo`, etc.) work
  unchanged.
- **No new external dependencies**: parser is hand-rolled C# (the SFZ format
  is simple enough — INI-style headers + `key=value` opcodes). DryWetMidi
  stays the only external NuGet dep per the project's "Minimal Dependencies"
  principle.
- **Phase 29 bundled-sample path stays byte-identical**: every existing
  `renderSong song "piano"`/`"brass"`/etc. call continues to produce
  identical output. SFZ dispatch only fires for `sampler:NAME` strings.

## Acceptance Criteria

- [ ] Without `use "@sfz";`, `(loadSfz #violin)` raises `UndefinedFunctionError`
      mentioning `use "@sfz"`; `renderSong song "sampler:violin"` raises
      `UnknownInstrumentError`.
- [ ] With `use "@sfz";` and a configured `sfz_root`, `(loadSfz #violin)`
      returns a non-null `Sfz` value pointing at the resolved file.
- [ ] `(loadSfz #unknownSymbol)` errors with `UnknownInstrumentSymbolError`
      listing the 19 supported symbols.
- [ ] Missing `sfz_root` config key errors with a message naming
      `~/.config/flow/config.toml`.
- [ ] SFZ parser recognizes 13 listed opcodes + 3 header types; unknown
      opcodes silently ignored with one-shot stderr advisory per
      `(patch, opcode-name)`.
- [ ] Region matching by `(pitch, velocity)` selects the correct region in
      the 2-region overlap test; last-declared-wins on conflicts.
- [ ] Nearest-pitch fallback varispeed-shifts the closest region's sample
      for out-of-range pitches.
- [ ] Sustained note (`C4w`, 4 seconds) with `loop_mode=loop_continuous` +
      441-frame equal-power crossfade has no per-sample amplitude jump
      exceeding 0.05 across the body.
- [ ] `Sfz violin = (loadSfz #violin)` + `renderSong song "sampler:violin"`
      produces a non-empty WAV with RMS > −40 dBFS.
- [ ] All 6 Phase 28 articulations applied to a sampler-rendered C4q produce
      6 distinct buffers with RMS-thresholded audible duration matching the
      locked Phase 28 rules within ±5%.
- [ ] `dotnet test --filter "FullyQualifiedName~Phase33SfzSmoke"` exits 0.
- [ ] Full unit suite (`dotnet test flow-sharp.sln`) stays GREEN.
- [ ] Two-run byte-identical determinism: two consecutive renders of the
      sampler smoke fixture produce `cmp -clean` identical WAVs.
- [ ] Existing `renderSong song "piano"` (Phase 29 path) produces
      byte-identical output before vs after Phase 33.
- [ ] Phase 33 in-repo artifacts (`flow-lang/sfz.flow` + new
      `Audio/Sfz/` subdirectory + new `SfzType.cs` + test fixture) total
      < 100 KB on disk.

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                       |
|--------------------|-------|------|--------|-------------------------------------------------------------|
| Goal Clarity       | 0.92  | 0.75 | ✓      | 8 falsifiable requirements; composer surface fully locked   |
| Boundary Clarity   | 0.92  | 0.70 | ✓      | Explicit 8-item out-of-scope list with reasoning            |
| Constraint Clarity | 0.85  | 0.65 | ✓      | Linux primary; 100 KB in-repo cap; determinism preserved    |
| Acceptance Criteria| 0.85  | 0.70 | ✓      | 15 pass/fail checkboxes including 4-second sustained test   |
| **Ambiguity**      | 0.11  | ≤0.20| ✓      |                                                             |

## Interview Log

| Round | Perspective             | Question summary                                            | Decision locked                                                                                       |
|-------|-------------------------|-------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|
| 1     | Researcher              | Coexist with Phase 29's SampledInstrumentRenderer?          | Hybrid — opportunistic upgrade; Phase 29 path stays default; SFZ is purely additive                   |
| 1     | Researcher              | Blessed orchestral library for symphony showcase?           | VSCO Community CE (CC-BY 4.0)                                                                         |
| 1     | Researcher              | Distribution model for the orchestral library?              | External download, doc-only; nothing > 100 KB in repo for SFZ purposes                                |
| 2     | Researcher + Simplifier | Policy on SFZ opcodes outside the common subset?            | Silently ignore + one-shot stderr advisory per `(patch, opcode-name)`                                 |
| 2     | Simplifier              | Loop boundary smoothing method?                             | Equal-power (sin/cos) crossfade, 441-frame window (10 ms at 44.1 kHz)                                 |
| 2     | Simplifier              | Falsifiable acceptance test for "plays correctly"?          | Self-contained synthetic smoke fixture in CI; non-zero RMS + discontinuity check                      |
| 3     | Boundary Keeper         | Pragma vs stdlib import for gating?                         | `use "@sfz"` stdlib import — leverages Phase 26.1 symbols + Phase 30 config; not a raw enable pragma  |
| 3     | Boundary Keeper         | Composer surface for invoking SFZ patches?                  | `Sfz violin = (loadSfz ...)` + `renderSong song "sampler:violin"` (named binding + sampler: prefix)  |
| 3     | Boundary Keeper         | Out-of-scope items (multi-select)?                          | No hot reload; rest deferred to Round 4                                                               |
| 4     | Boundary Keeper         | Library root + name→path mapping resolution?                | Symbol-keyed lookup: `loadSfz #violin` → shipped dict relative path + `sfz_root` flow-config key      |
| 4     | Boundary Keeper         | Out-of-scope confirmation (single)?                         | Read-only, common subset only, no bundle, no rewrite                                                  |
| 4     | Failure Analyst         | Worst failure invalidating Phase 34?                        | Loop crossfade clicks under sustained notes — acceptance test must include 4-second sustained check   |

---

*Phase: 33-sfz-orchestral-sampler*
*Spec created: 2026-05-15*
*Next step: /gsd-discuss-phase 33 — implementation decisions (parser line discipline, region storage shape, varispeed reuse vs duplication, etc.)*
