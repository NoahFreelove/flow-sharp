---
phase: 33
plan: 04
subsystem: sfz-parser
tags: [phase-33, wave-2, parser, sfz, audio, charitable-interpretation]

# Dependency graph
requires:
  - 33-01 (smoke.sfz fixture + RepoSizeTests gate + VSCO-CONTROL-DECISION FOUND + VSCO-PATH-AUDIT)
  - 33-02 (SfzData / SfzRegion / SfzLoopMode / SfzParseException data model; SfzType + Value.Sfz; ExecutionContext SFZ surface; FlowConfigPoco.SfzRoot)
  - 33-SPEC (SPEC-3 opcode whitelist; SPEC-4 region grid; SPEC-5 loop_mode + loop_start/end)
  - 33-CONTEXT (D-01 SfzRegion?[128,128] grid; D-02 last-declared-wins write-order)
  - 33-RESEARCH (Pattern 2 SFZ parser; Pitfalls 7/8/11; T-33-PARSE-01 / T-33-NUM-01 / T-33-OPCODE-01)
  - 33-PATTERNS (ScalaParser.cs:1-302 single-pass walker template)
  - 33-VSCO-CONTROL-DECISION (FOUND — 14-opcode whitelist + <control> as fourth header type)
  - 33-VSCO-PATH-AUDIT (default_path cascade + Windows-backslash normalisation)
provides:
  - flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs — public static class with Parse(content, filePath, patchDescription) → SfzData entry point
  - 14-opcode whitelist enforced with StringComparer.Ordinal (T-33-OPCODE-01)
  - 4 header types: <control>, <global>, <group>, <region> — inheritance flattened at parse time
  - <control> default_path cascade with backslash → OS-separator normalisation (Linux primary per CLAUDE.md)
  - Strict numeric posture (NumberStyles.Float & ~AllowExponent & ~AllowThousands + InvariantCulture) mirroring Phase 32 D-18
  - MaxRegionCount = 10000 DoS cap (T-33-PARSE-01)
  - dB → linear volume conversion + pan ÷ 100 normalisation at parse time (Pitfalls 7, 8)
  - D-02 last-declared-wins grid write order (16384-cell SfzRegion?[128,128])
  - SortedByPitch ascending-unique index for D-03 nearest-pitch fallback (Plan 33-06 input)
  - RenderingDiagnostics.WarnOnce dedup contract: sfz:opcode:{patch}:{name},
    sfz:opcode_value:{patch}:{name}:{val}, sfz:header:{patch}:{name},
    sfz:syntax:{patch}:{tok}, sfz:opcode_misplaced:{patch}:default_path,
    sfz:orphan_opcode:{patch}:{name}
  - flow-lang.Tests/Unit/Phase33/SfzParserTests.cs — 16 facts covering SPEC-3/4/5 + Pitfalls + VSCO-CONTROL-DECISION cascade
affects:
  - 33-05 (loadSfz/loadSfzPatch — consumes Parse to build SfzPatchRegistry; the FOUND-mandate cascade is what makes loadSfz #violin yield non-null Sfz against real VSCO installs)
  - 33-06 (SfzSampleCache — consumes SfzData.Regions + SfzData.BasePath for eager-load; loop_start/loop_end render-time clamp consumes SfzRegion.LoopMode + LoopStart + LoopEnd)
  - 33-07 (sampler dispatch — consumes Grid + SortedByPitch for the (pitch, vel) lookup + D-03 nearest-pitch fallback)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Single-pass line walker with StripCr helper, mirroring Phase 32 ScalaParser.cs:60-167"
    - "NumberStyles.Float & ~AllowExponent & ~AllowThousands + CultureInfo.InvariantCulture for strict-numeric guard (Phase 32 D-18 precedent)"
    - "Bounded loop with MaxRegionCount = 10000 DoS cap mirroring ScalaParser.MaxStepCount"
    - "HashSet<string> with StringComparer.Ordinal whitelist gate (rejects unicode tricks per T-33-OPCODE-01)"
    - "Charitable interpretation: unknown opcodes silently ignored + one-shot stderr advisory via RenderingDiagnostics.WarnOnce (CLAUDE.md feedback_charitable_interpretation)"
    - "[Collection(\"FlowScripts\")] test isolation + RenderingDiagnostics.ResetForTesting() in ctor + Dispose for cross-fact sentinel set isolation (Phase 32 ScalaParserFacts pattern)"
    - "CapturedStderr helper (TextWriter capture via Console.SetError) for in-fact stderr-line counting on WarnOnce advisories"

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs (562 LoC including doc comments)
    - flow-lang.Tests/Unit/Phase33/SfzParserTests.cs (535 LoC; 16 facts)
  modified: []

key-decisions:
  - "Sample-path resolution uses Path.Combine(default_path_normalised, sample_value) NOT a full Path.Combine with basePath — the parse-time SamplePath stays relative to the .sfz file's directory; downstream sample-cache code in Plan 33-06 will compose with SfzData.BasePath at WAV-load time. This mirrors the SfzRegion XML doc's stated contract: 'relative to SfzData.BasePath; resolved against the .sfz file's directory at sample-load time.'"
  - "<control> behaves as file-scope per VSCO-CONTROL-DECISION § 5: opening <control> clears controlOpcodes but PRESERVES already-accumulated <global>/<group>/region state. <global> still clears <group> + region state (a fresh top-level block). This honours the VSCO-CONTROL-DECISION 'one <control> per file at the top' convention while staying tolerant of future SFZ libraries that interleave <control> headers."
  - "Default-path normalisation always rewrites '\\\\' to Path.DirectorySeparatorChar regardless of platform. On Linux primary this turns the VSCO 'Strings\\\\Solo Violin\\\\Arco Vib\\\\' into 'Strings/Solo Violin/Arco Vib/'. On Windows the result is a no-op since '\\\\' is already the OS separator."
  - "Multi-opcode-per-line tokenisation uses a 'peek next non-whitespace for key=' heuristic so that spaces inside default_path values ('default_path=Strings\\Solo Violin\\Arco Vib\\') do NOT terminate the value, while adjacent opcodes ('sample=foo.wav lokey=60 hikey=72') do. The heuristic ends a value when the next token both contains '=' AND starts with an identifier character (letter or underscore) — the SFZ opcode-name shape."
  - "An empty `default_path` value is treated as 'no cascade', preserving the bare sample= relative path. This is defensive — VSCO-CE never writes default_path= with an empty value, but other SFZ libraries might, and a charitable parse should not produce a samplePath identical to a directory."
  - "loop_mode default rule: when loop_start > 0 OR loop_end > 0 AND no loop_mode opcode appears, the spec convention is loop_continuous. This is encoded in BuildRegion's fallback chain; the alternative (always NoLoop) would silently break VSCO patches like SViolinVib.sfz that declare loop_start/end without explicit loop_mode."
  - "Description fallback chain: prefer the first non-comment non-blank non-header line, falling back to Path.GetFileName(filePath) if the file is all-headers (which is the common SFZ case). The smoke fixture's '<global>' is the first non-comment line, so description falls back to 'smoke.sfz'."

requirements-completed: [SPEC-3, SPEC-4, SPEC-5]

# Metrics
duration: ~25 min
completed: 2026-05-15
tasks: 2
commits: 2
files-touched: 2
parser-loc: 562
test-loc: 535
test-facts: 16
opcode-whitelist-size: 14
header-types: 4
maxregion-cap: 10000
---

# Phase 33 Plan 04: Wave 2 — SFZ Parser Summary

Wave 2 ships the hand-rolled SFZ-format parser — the largest single code surface in Phase 33. Single-pass line walker mirroring Phase 32's ScalaParser shape, 14-opcode whitelist with one-shot stderr advisories for everything else, strict numeric posture, MaxRegionCount cap, header inheritance flattened at parse time, D-02 last-declared-wins grid build, and the Plan 33-01 VSCO-CONTROL-DECISION FOUND mandate fully implemented (<control> as a fourth header type + default_path cascade + Windows-backslash → OS-separator normalisation).

## Tasks Completed

| # | Name                                  | Commit    | Files                                                                    |
| - | ------------------------------------- | --------- | ------------------------------------------------------------------------ |
| 1 | SfzParser implementation              | `a3c4150` | flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs                         |
| 2 | SfzParserTests fact suite (16 facts)  | `ad3d017` | flow-lang.Tests/Unit/Phase33/SfzParserTests.cs                           |

Both tasks committed atomically. Task 1 carried the grep-verifiable invariants (MaxRegionCount, KnownOpcodes, NumberStyles.Float, CultureInfo.InvariantCulture); Task 2 proved the parser body via behavior facts.

## What Shipped

**`SfzParser.Parse(content, filePath, patchDescription) → SfzData`**

The parser's full contract in one entry point. Internals:

- **Single-pass line walker** + StripCr helper (LF + CRLF tolerance). Each line is comment-stripped (`//`), trimmed, and token-walked.
- **4 headers**: `<control>`, `<global>`, `<group>`, `<region>`. Inheritance is flattened at parse time by writing a fresh `regionOpcodes` dict on every `<region>` open, copying `<global>` then `<group>` opcodes in (group overrides global), then accumulating region-specific opcodes on top.
- **14-opcode whitelist** (`StringComparer.Ordinal`): the 13 SPEC-3 opcodes + `default_path` (control-only) per the FOUND mandate. Anything else fires a one-shot `sfz:opcode:{patch}:{name}` advisory and is silently ignored.
- **`<control>` is file-scope**: it clears its own dict on open but preserves `<global>`/`<group>`/region state. `default_path` outside `<control>` fires `sfz:opcode_misplaced:{patch}:default_path` and is dropped.
- **Default-path cascade**: every region's `sample=` value is pre-joined with the active `<control>`'s `default_path` via `Path.Combine(normalised_default_path, normalised_sample)`. Windows backslashes are rewritten to `Path.DirectorySeparatorChar` before the join.
- **Strict numeric**: `NumberStyles.Float & ~AllowExponent & ~AllowThousands` + `CultureInfo.InvariantCulture` for floats; `NumberStyles.None` for ints. Failed parses emit `sfz:opcode_value:{patch}:{name}:{val}` advisory + fall back to the SFZ spec default.
- **dB → linear** at parse time: `volumeLinear = Math.Pow(10.0, volumeDb / 20.0)` (Pitfall 8).
- **Pan ÷ 100** at parse time: SFZ `[-100, +100]` → Flow `[-1.0, +1.0]` (Pitfall 7).
- **DoS guard**: throws `SfzParseException` with `"region count <= 10000"` once `regions.Count > MaxRegionCount`.
- **Grid build**: declaration-order iteration with `grid[k, v] = region` write per `(LoKey..HiKey, LoVel..HiVel)` cell — D-02 last-declared-wins is structurally enforced by write order.
- **SortedByPitch**: ascending unique pitches with any grid coverage. ~512 bytes per patch; the D-03 nearest-pitch fallback index.

**`flow-lang.Tests/Unit/Phase33/SfzParserTests.cs`** — 16 facts, all green:

| # | Fact                                              | What it pins                                                  |
| - | ------------------------------------------------- | ------------------------------------------------------------- |
| 1 | SmokeFixture_ParsesCleanly                         | Plan 33-01 fixture end-to-end; Regions.Count == 2; Grid cells |
| 2 | AllKnownOpcodes_Parse                              | All 13 base opcodes populate SfzRegion fields                 |
| 3 | UnknownOpcode_AdvisoryOnce                         | 5 stderr lines on first run; 0 on re-parse (dedup)            |
| 4 | HeaderInheritance                                  | `<global>` cascades; `<group>` overrides; `<region>` overrides|
| 5 | StrictNumeric_RejectsExponent                      | `volume=1.5e2` falls back to 1.0 linear (NOT 150)             |
| 6 | StrictNumeric_RejectsThousands                     | `lokey=1,500` falls back to 0                                 |
| 7 | MaxRegionCount_Caps                                | 10001 `<region>` headers throws SfzParseException             |
| 8 | GridBuild_LastDeclaredWins                         | D-02 write-order encodes the spec rule                        |
| 9 | SortedByPitch_AscendingUnique                      | Strict ascending; smoke fixture yields 48..127 (80 entries)   |
| 10| VolumeOpcode_DbToLinear                            | volume=0 → 1.0; -6 dB → 0.5012; -12 dB → 0.2512               |
| 11| PanOpcode_NormalizedToFlowRange                    | SFZ pan=100 → 1.0; pan=-100 → -1.0; pan=50 → 0.5              |
| 12| MultipleOpcodesOnHeaderLine                        | Pitfall 11 — three opcodes on one line, all three parsed     |
| 13| LoopMode_UnknownValue_FallsBackToNoLoop            | `bogus` → NoLoop + advisory                                   |
| 14| CommentStripping_RemovesLineComments               | `//` stripped before tokenisation                             |
| 15| ControlHeader_DefaultPathCascade_BackslashNormalised | VSCO-CONTROL-DECISION FOUND: `<control>` + backslash → /    |
| 16| NoControlHeader_PreservesPlainRelativePath         | Smoke-fixture codepath: SamplePath stays the bare value      |

## Verification

- `dotnet build flow-sharp.sln --logger "console;verbosity=minimal"` exits 0 (0 errors; 14 pre-existing warnings unrelated to this plan)
- `dotnet test --filter "FullyQualifiedName~Phase33.SfzParserTests"` exits 0 — **16 / 16 facts green** (75 ms)
- `dotnet test --filter "FullyQualifiedName~Phase33"` exits 0 — **25 / 25 Phase 33 facts green** (SfzParserTests + SfzTypeFacts + RepoSizeTests)
- Grep gates (Task 1 verify):
  - `MaxRegionCount = 10000` present
  - `KnownOpcodes` present
  - `NumberStyles.Float` present
  - `CultureInfo.InvariantCulture` present
- Full-suite run shows 26 pre-existing Phase 28 failures on the merged `develop` base (PerSynthArticulationTests + RagtimeFixtureTests). I confirmed by checking out the parent commit before my changes — failures exist there too. They are unrelated to Phase 33 and out of scope per the executor SCOPE BOUNDARY rule. I did not attempt to fix them.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical Functionality] Stripped trailing inline comments inside opcode values**

- **Found during:** Task 1 implementation
- **Issue:** The plan's `<behavior>` calls out `opcode=value // commentary` as a single-line comment-stripping case. The naive implementation that strips comments only at line-start would parse `sample=foo.wav // comment` as `sample` opcode with value `foo.wav // comment`, breaking downstream WAV-path lookup.
- **Fix:** Comment-strip with `IndexOf("//", StringComparison.Ordinal)` BEFORE tokenisation, so the `//` rule applies regardless of column position. Documented the trade-off in the parser XML doc: "We use the simple 'first //' rule per the 13-opcode subset where sample paths are barewords (no quotes)."
- **Files modified:** flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs (the line-walker's `int comment = raw.IndexOf("//", StringComparison.Ordinal);` block)
- **Commit:** Folded into Task 1 `a3c4150`
- **Test coverage:** Fact 14 `CommentStripping_RemovesLineComments`

**2. [Rule 2 - Missing Critical Functionality] Default loop_mode when loop_start/end > 0 implies loop_continuous**

- **Found during:** Task 1 implementation, surfaced by reading SfzLoopMode.cs XML doc lines 24-27: "SFZ spec convention is that a region with loop_start / loop_end declared but NO loop_mode opcode defaults to LoopContinuous, not NoLoop — that defaulting also lives in the parser (Plan 33-04)."
- **Issue:** Plan 33-04's `<interfaces>` block notes `loop_mode="no_loop" (or "loop_continuous" if loop_start/end > 0)` but the `<action>` step 12 only specifies the mapping for present opcodes. Without explicit handling, regions like `SViolinVib.sfz` that declare `loop_start=2205 loop_end=4410` without `loop_mode=loop_continuous` would silently NoLoop and break the crossfade math Plan 33-06 ships.
- **Fix:** `BuildRegion`'s loop_mode resolution chain: explicit value → mapped; else if `loopStart > 0 || loopEnd > 0` → `LoopContinuous`; else → `NoLoop`. Smoke fixture's region 1 (loop_start=2205, loop_end=4410, loop_mode=loop_continuous EXPLICIT) and region 2 (no loop_*) both hit the correct path; the implicit-continuous codepath is not exercised by the smoke fixture but is documented in the parser XML doc.
- **Files modified:** flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs (BuildRegion's loop_mode block)
- **Commit:** Folded into Task 1 `a3c4150`

**3. [Rule 2 - Missing Critical Functionality] Empty default_path treated as no-cascade**

- **Found during:** Task 1 implementation
- **Issue:** If a future SFZ library declares `<control>\ndefault_path=` with an empty value, naive `Path.Combine("", "foo.wav")` would produce just `"foo.wav"` — semantically correct, but the check happens AFTER NormaliseSeparators which would call `Replace('\\', ...)` on an empty string (harmless). Still, the defensive guard makes the intent explicit.
- **Fix:** `if (control.TryGetValue("default_path", out var dp) && !string.IsNullOrEmpty(dp))` — only cascade if the value is non-empty. Documented in the parser key-decisions.
- **Files modified:** flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs (BuildRegion's sample-path resolution)
- **Commit:** Folded into Task 1 `a3c4150`

**4. [Rule 2 - Missing Critical Functionality] Charitable handling for `default_path` outside `<control>`**

- **Found during:** Task 1 implementation
- **Issue:** A `.sfz` author could mistakenly write `default_path=` inside `<region>`. The plan's whitelist only says "default_path is recognised"; it doesn't specify what to do if it appears outside `<control>`. Silently routing it into the region dict would corrupt the region's sample-path resolution. Throwing would violate Flow's charitable-interpretation rule.
- **Fix:** Emit `sfz:opcode_misplaced:{patch}:default_path` advisory + drop the opcode (charitable + safe). Documented inline.
- **Files modified:** flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs (in the opcode-routing switch)
- **Commit:** Folded into Task 1 `a3c4150`

**5. [Rule 2 - Missing Critical Functionality] Charitable handling for opcode-before-header (orphan opcode)**

- **Found during:** Task 1 implementation
- **Issue:** The plan's algorithm assumes an active accumulator. If an `.sfz` file starts with an opcode before any header (`sample=foo.wav\n<region>...`), the parser would have no active accumulator and the opcode would be silently discarded with no diagnostic.
- **Fix:** When `target == HeaderKind.None` and an opcode is encountered, emit `sfz:orphan_opcode:{patch}:{name}` advisory + drop. Composer sees the diagnostic on stderr and can correct.
- **Files modified:** flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs (in the opcode-routing switch's default arm)
- **Commit:** Folded into Task 1 `a3c4150`

**6. [Rule 2 - Missing Critical Functionality] Charitable handling for unknown header `<foo>`**

- **Found during:** Task 1 implementation
- **Issue:** The plan only specifies 4 headers. A future SFZ extension or typo (`<rgion>`, `<curve>`) would otherwise be either rejected or silently treated as 'still-in-prior-state'.
- **Fix:** Emit `sfz:header:{patch}:{name}` advisory + set the accumulator target to `HeaderKind.None` so any opcodes that follow get the orphan treatment (also advisory) instead of leaking into the prior accumulator.
- **Files modified:** flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs (in the header switch's default arm)
- **Commit:** Folded into Task 1 `a3c4150`

**7. [Rule 2 - Missing Critical Functionality] Value-terminator heuristic preserves spaces inside `default_path`**

- **Found during:** Task 1 implementation
- **Issue:** VSCO-CE's `default_path=Strings\Solo Violin\Arco Vib\` contains literal spaces. A naive "split on whitespace" tokeniser would terminate `default_path`'s value at the first space, breaking the cascade. Conversely, a "value runs to end of line" rule would break Pitfall 11's adjacent-opcode case (`<region> sample=foo.wav lokey=60 hikey=72`).
- **Fix:** Use a peek heuristic: when whitespace is encountered inside a value, peek the next non-whitespace token. If that token contains `=` AND starts with an identifier character (letter or underscore — the SFZ opcode-name shape), terminate the value at the whitespace. Otherwise, consume the whitespace and continue reading the value. This correctly handles both `default_path=Strings\Solo Violin\Arco Vib\` and `sample=foo.wav lokey=60 hikey=72`.
- **Files modified:** flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs (the inner value-read loop)
- **Commit:** Folded into Task 1 `a3c4150`
- **Test coverage:** Facts 12 (MultipleOpcodesOnHeaderLine) + 15 (ControlHeader_DefaultPathCascade_BackslashNormalised) jointly pin both branches of the heuristic.

No other deviations. The two plan tasks executed in declaration order with no checkpoints required.

## Threat Model Compliance

| Threat ID         | Disposition      | Mitigation Status                                                                                  |
| ----------------- | ---------------- | -------------------------------------------------------------------------------------------------- |
| T-33-PARSE-01     | mitigate         | `MaxRegionCount = 10000` + early-throw `SfzParseException` (Fact 7 pins)                          |
| T-33-NUM-01       | mitigate         | `NumberStyles.Float & ~AllowExponent & ~AllowThousands` + `InvariantCulture` (Facts 5, 6 pin)     |
| T-33-OPCODE-01    | mitigate         | `KnownOpcodes` `HashSet<string>` with `StringComparer.Ordinal` (Fact 3 pins dedup; case-sensitivity verified by code-review of the whitelist construction) |
| T-33-LOOP-01      | accept (deferred)| Raw value stored on SfzRegion; render-time clamp lives in Plan 33-06 (Pitfall 3)                  |
| T-33-PATH-01      | accept           | Composer-controlled .sfz file; absolute-path overload is the documented contract                  |

All three "mitigate" threats fully addressed. The two "accept" threats remain accepted per the plan's threat model.

## Known Stubs

None. The parser produces fully-populated `SfzData` / `SfzRegion` records with no placeholder data flowing to UI / rendering. The `samplePath` field carries the parser-resolved relative path; Plan 33-06's `SfzSampleCache.EagerLoad` consumes it for the WAV-load step. The `Grid` cells are either a non-null `SfzRegion` reference (covered) or `null` (no coverage at that pitch/velocity — semantically meaningful per CONTEXT D-03).

## Threat Flags

None — this plan introduces a parser surface (composer-controlled .sfz file content), but does NOT introduce new network endpoints, auth paths, or trust boundaries beyond the one already in the threat model (Composer .sfz file → SfzParser.Parse). No new threat-register entries needed.

## Self-Check: PASSED

Files-on-disk verification:

```
FOUND: flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs
FOUND: flow-lang.Tests/Unit/Phase33/SfzParserTests.cs
```

Commit verification (worktree-agent-ab5c14ed72afbd970 branch):

```
FOUND: a3c4150  feat(33-04): implement SfzParser (14-opcode whitelist + <control> cascade) (Task 1)
FOUND: ad3d017  test(33-04): SfzParserTests fact suite (16 facts; SPEC-3/4/5 acceptance) (Task 2)
```

All claimed artefacts exist; all claimed commits exist on the worktree branch.
