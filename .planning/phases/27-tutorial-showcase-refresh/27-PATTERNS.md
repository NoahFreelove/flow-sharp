# Phase 27: Tutorial + Showcase Refresh — Pattern Map

**Mapped:** 2026-05-10
**Files analyzed:** 11 (4 to create, 7 to modify)
**Analogs found:** 11 / 11 (every artifact has a direct in-repo precedent)

## File Classification

| File | Action | Role | Data Flow | Closest Analog | Match Quality |
|------|--------|------|-----------|----------------|---------------|
| `examples/tutorial.flow` | MODIFY | educational `.flow` script | batch (chapter-by-chapter print + render) | itself (Phase 16 v1.2 state, lines 21-26 chapter pattern; lines 581-649 graduation-song) | exact — pattern-preserving append/weave |
| `examples/showcase.flow` | REPLACE | composition `.flow` script | render-once → WAV+MIDI batch | itself (lines 1-44 v1.2 ambient piece) for STRUCTURE; new content per D-202 | role-match (structure preserved, content replaced) |
| `examples/pragmas/h_alias.flow` | CREATE | pragma demo `.flow` script | render-once → WAV (no MIDI strictly required, but D-403 facts assume both) | `tests/test_h_alias.flow` (lines 1-21) | exact role-match (pragma at line 1 + chapter-style demo + print) |
| `examples/pragmas/microtonal_ji.flow` | CREATE | pragma demo `.flow` script | render-once → WAV+MIDI | `tests/test_tuning_ji.flow` (lines 1-30) | exact role-match (pragma at line 1 + tempo/timesig/section + writeWav + writeMidi) |
| `flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs` | CREATE | xUnit integration test class (4 facts) | two-run SequenceEqual byte-identity gate | `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` (90 lines) AND `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` (99 lines) | exact — verbatim copy, swap namespace + class name + script paths + run prefix |
| `CLAUDE.md` | MODIFY | project guidance doc | doc append | itself §"Language Features → Music-Specific" (already has gain=dB / volume=linear bullet from 26.2 closure 86bdd15) | role-match (table append after existing section) |
| `.planning/REQUIREMENTS.md` | MODIFY | requirements doc | doc rewrite-at-closure | Phase 26.1 DICT-01/02/03 entries (lines 107-111) AND Phase 26.2 ERG-01..ERG-05 entries (lines 113-123) | exact closure-rewrite precedent |
| `.planning/ROADMAP.md` | MODIFY | roadmap doc | doc closure-marking | Phase 26.2 entry pattern (line 74: `Shipped 2026-05-10 (Waves 0-N: hash + hash + ... + closure)`) AND Phase 16 entry (line 44) | exact precedent |
| `.planning/STATE.md` | MODIFY | session state doc | doc state-advance | Phase 26.2 closure pattern (current-position → "Phase 27 — COMPLETE 2026-05-10 / Plan N of N / v1.3 milestone shipped 12/12") | exact precedent |
| `.planning/phases/27-.../27-VERIFICATION.md` | CREATE | phase closure deliverable | doc closure | Phase 26.2 `26.2-VERIFICATION.md` (149 lines) AND Phase 16 `16-VERIFICATION.md` (grep-table + smoke transcript pattern) | exact — Phase 27 is criterion-grep + smoke + tests format |
| `.planning/phases/27-.../27-SUMMARY.md` | CREATE | phase closure deliverable | doc closure | Phase 26.2 `26.2-06-SUMMARY.md` (closure plan SUMMARY pattern: tasks, files authored/modified, test results, must-haves audit, deviations, self-check, handoff) | exact precedent |

## Pattern Assignments

### `examples/tutorial.flow` (educational `.flow` script — MODIFY)

**Analog:** itself (`examples/tutorial.flow`, current 684 lines, Phase 16 v1.2 state).

**Chapter divider pattern** (verbatim from existing tutorial.flow lines 21-26 + 40-45 — apply to every NEW chapter):

```flow
Note: -----------------------------------------------------------
Note: N. Chapter Title
Note: -----------------------------------------------------------
(print "")
(print "--- N. Chapter Title ---")
(print "")
```

Every existing chapter follows this exact 6-line preamble. Inserting new chapters between existing ones MUST preserve this divider shape and the chapter-number scheme. Half-numbering (`1.5 Symbols`, `4.5 Tuples`, etc.) or full renumbering both acceptable per CONTEXT D-302 + Claude's Discretion clause.

**Inline annotation convention** (from tutorial.flow lines 67, 79, 532-536):

- `Note:` for chapter-header-level multi-line dividers (visually distinct, top-of-block).
- `//` for inline single-line annotations next to a code expression.
- Phase 16 D-09 split — keep both styles. Only refresh comments where a v1.3 feature requires explanation.

**Demo body pattern** (typical chapter — from tutorial.flow lines 28-38, 47-58, 94-101):

```flow
Int sum = (add 10 25)
Double product = (mul 3.0 4.5)
(print $"10 + 25 = {(str sum)}")
(print $"3.0 * 4.5 = {(str product)}")

Note: One-line prose explanation of the rule (S-expression style: (functionName arg1 arg2))
```

Variable declaration → `(print $"label = {(str var)}")` interpolation form. NO render-to-file calls inside non-graduation chapters (Phase 16 REVIEW-FIX anti-pattern; risks breaking byte-identical contract for the graduation song).

**Graduation song pattern** (from tutorial.flow lines 581-649 — D-304 replaces content but preserves structure):

```flow
tempo BPM {
    timesig 4/4 {
        key Kmajor {
            // Quiet intro — per-section gain musical-context block (NOT volume(buf, linear))
            gain 0.6 {
                section sunriseIntro {
                    Sequence chords = | [C4 E4 G4]h [F4 A4 C5]h |
                    Sequence bass = | C3h F3h |
                }
            }
            // ... more sections (verse, chorus, bridge, outro) ...

            // reverbTime musical-context block on outro — long hall tail
            reverbTime 2.5 {
                section sunriseOutro {
                    Sequence mel = | E4h C4h |
                    Sequence bass = | C3w |
                }
            }

            // Arrange the full song
            Song sunrise = [sunriseIntro sunriseGroove sunriseVerse*2 sunriseChorus ...]
            (print $"Song: {(str sunrise)}")

            // Render → polished effects chain via flow operator
            Buffer rawMix = (renderSong sunrise "piano")
            Double negTwo = (sub 0.0 2.0)
            Buffer finalMix = rawMix -> (reverb 0.25) -> (lowpass 4000.0) -> (gain negTwo)

            // Optional: tempoRamp ritardando tail mixed into WAV (NOT into MIDI)
            Sequence tail = | C4h G3h C3w |
            Buffer ritBuf = (tempoRamp tail 100.0 60.0)
            Buffer finalWithTail = (mix finalMix ritBuf)

            // BOTH writeWav (Buffer) and writeMidi (Song) from same Song value
            // CRITICAL: path strings INLINE in the call (NOT via String var) — Phase 16 IN-02 gotcha
            (writeWav "examples/output/flow_tutorial.wav" finalWithTail)
            (writeMidi "examples/output/flow_tutorial.mid" sunrise)
        }
    }
}
```

**Path-string inline rule** (Phase 16 REVIEW-FIX IN-02 + Phase 27 RESEARCH Pitfall 7):

The `Phase 18 ByteIdenticalTutorialTests.RunTwiceAndCompare` rewrites `examples/output/flow_tutorial.{wav,mid}` to `tests/output/phase18_tutorial_run1.{wav,mid}` via `string.Replace`. The path STRING must be inline in the `(writeWav)` / `(writeMidi)` call — NOT via `String wavPath = "examples/output/..."; (writeWav wavPath buf)`. Variable indirection breaks the Replace match silently.

**Congratulations bullet list pattern** (from tutorial.flow lines 656-676 — D-302/D-303 close with an updated bullet list; one bullet per major topic taught):

```flow
(print "You've learned:")
(print "  - Variables, types, and arithmetic")
(print "  - Functions (procs) and the flow operator ->")
// ... bullet per chapter / topic ...
(print "  - Dual export: `writeWav` (audio) + `writeMidi` (notation) from the same Song")
```

Phase 27 expands this list to include v1.3 topics (prefix-only arithmetic, Symbols, Tuples + `~>`, Dict 14-op surface, tuplets `{3:2 ...}q` + fractional, microtonal pragmas, DX-10..15, Hertz literals, gain-vs-volume, Ms-typed FX, humanizeGaussian, range / negative-slice / multi-letter enharmonics).

**Anti-patterns to avoid** (from Phase 16 REVIEW-FIX, surfaced again in 27-RESEARCH Pitfalls 5 + 6):

1. Don't add new render/export calls inside non-graduation chapters — risks breaking byte-identical contract.
2. Don't claim a feature works in a Congratulations bullet without an executable demo above (Phase 16 IN-02).
3. Don't introduce `dynamics ff { Sequence x = ... }` scoping that traps a reference (Phase 16 IN-05; use inline marker `| ff C4 D4 |`).
4. Don't replace `gain 0.6 { section ... }` (musical-context block) with `(volume sectionBuf 0.6)` (post-render buffer op) — different tier, different audible effect (RESEARCH Pitfall 5).
5. Don't activate `enable justIntonation;` at line 1 of tutorial.flow — pragmas are file-scoped; would re-tune every prior chapter (RESEARCH Pitfall 6). Microtonal demo lives in companion file.

---

### `examples/showcase.flow` (composition `.flow` script — REPLACE)

**Analog:** itself (current 44 lines, Phase 16 v1.2 ambient piece) for STRUCTURE; D-202 polyrhythmic-minimal sketch for content.

**Structural pattern** (current showcase.flow lines 1-44 — preserve verbatim shape, replace content):

```flow
use "@std"
use "@audio"
use "@composition"

(print "Flow Showcase — v1.X <Genre Tagline>")
(print "Generating examples/output/flow_showcase.{wav,mid} ...")

tempo BPM {
    timesig 4/4 {
        key KEYNAME {
            // Named Sequence variables for each layer
            Sequence padBase = | A3w | F3w | D3w | E3w |
            Sequence pad = padBase -> crescendo 0.18 0.6
            // ... pulse, melody, etc. ...

            // Optional outer musical-context wrapper (reverbTime here in v1.2 form)
            reverbTime 3.2 {
                section atmosphere {
                    Sequence layerPad = pad
                    Sequence layerPulse = pulse
                    Sequence layerMelody = melody
                }

                Song showcase = [atmosphere]
                Buffer rendered = (renderSong showcase "strings")
                Double trim = (sub 0.0 4.0)
                Buffer finalMix = rendered -> (lowpass 2800.0) -> (gain trim)

                (writeWav "examples/output/flow_showcase.wav" finalMix)
                (writeMidi "examples/output/flow_showcase.mid" showcase)
            }
        }
    }
}

(print "WAV:  examples/output/flow_showcase.wav")
(print "MIDI: examples/output/flow_showcase.mid")
```

The shape that must be preserved across the replacement:

1. `use "@std" / "@audio" / "@composition"` at file head.
2. Two preamble `(print)` lines — banner + "Generating examples/output/..." status.
3. Outer `tempo X { timesig 4/4 { key Y { ... } } }` musical-context nesting.
4. Named `Sequence` variables for each musical layer (so the test can grep + a maintainer can read top-down).
5. ONE `section sectionName { ... }` block (single-section showcase — D-201 keeps this).
6. `Song = [sectionName]` arrangement.
7. `Buffer rendered = (renderSong piece "instrument")` then a flow-operator effect chain.
8. **Path strings inline** in `(writeWav)` / `(writeMidi)` (Phase 18 + 25 test path-rewrite contract).
9. Closing two `(print)` lines confirming output paths.

**Phase 27 content additions per D-202 + D-103** (from RESEARCH § Code Examples Showcase, lines 612-671):

```flow
// Tuplet groove leading the genre
Sequence drumTriplets = | {3:2 _ C2 _}q C2 {3:2 _ C2 D2}q C2 |

// Dict-driven drum dispatch — Symbol keys, INSERTION-ORDER iteration (DICT-03)
Dict<Symbol, Note> kit = (dict #kick C2 #snare D2 #hihat F#3)

// Euclidean drum with FIXED seed for byte-identical determinism
Sequence drums = (euclidean 5 16 (get kit #kick) 0.18 0.12 7)

// Soft melody humanized via Gaussian — FIXED seed for byte-identical
Sequence melody = (humanizeGaussian | mp _ _ E5q G5q | A5h E5h | 0.08 314)

// Phase 26.2 audible features — chained on the rendered Buffer:
Buffer filtered = rawMix -> (lowpass 1.2kHz)            // Hertz literal filter sweep
Buffer delayed = filtered -> (delay 250ms 0.5 0.4)      // Ms-typed delay
Buffer withReverb = delayed -> (reverb 0.5 1.8s)        // Second-decay reverb
Buffer finalMix = (volume withReverb 0.7)               // volume(buf, linear) — NOT gain(buf, dB)
```

**Determinism contract requirements** (from CONTEXT § Reusable Assets + RESEARCH Pitfalls 1, 3, 4):

- `euclidean` 6-arg form `(euclidean steps hits note swing humanize seed)` with fixed seed (`7` in v1.2).
- `humanizeGaussian seq amount seed` with fixed seed (`314` in v1.2 — preserve).
- NO unseeded `random` / `randomInt` / `choose` / `(? ... )` syntax.
- NO `DateTime.Now` or non-deterministic stdlib calls.
- AVOID tied notes inside tuplet brackets (`{3:2 C4~ D4 E4}q`) — RESEARCH Pitfall 3 edge.
- Document Dict iteration as INSERTION-ORDER in a comment (RESEARCH Pitfall 4).

---

### `examples/pragmas/h_alias.flow` (pragma demo — CREATE)

**Analog:** `tests/test_h_alias.flow` (21 lines) — lines 1-21 verbatim shape.

**Imports + pragma pattern** (lines 1-3 of analog):

```flow
enable hAsB;

use "@std"
```

The `enable hAsB;` MUST be the **first non-comment, non-blank line of the file** (PRAG-01 prefix-region constraint, verified by Phase 21 PragmaScannerFacts:22-49). `use` statements may follow. Add `use "@audio"` and `use "@composition"` if `writeWav` / `writeMidi` / `renderSong` are called.

**Demo body pattern** (lines 5-18 of analog):

```flow
Note: DEFER-02/03 acceptance — H-as-B alias inside note streams (Phase 21)
Note: With `enable hAsB;` declared, every B-shape works with H per D-14.

Note: Basic alias — H4q parses identically to B4q
Sequence basic = | H4q B4q |
(print (str basic))

Note: D-14 full coverage — flats, sharps, dotted, tied, cent offsets
Sequence full = | Hb4q H#4q H4q. H4h~ Hb4+50c |
(print (str full))

Note: D-14 final clause — chord brackets pull inner notes through TryParseNote
Sequence chord = | [H4 D#5 F#5]q |
(print (str chord))
```

Top-of-file `Note:` block explains pragma → demo Sequences → `(print (str ...))` for each.

**Standalone-runnable closure** (line 20 of analog):

```flow
(print "test_h_alias: PASSED")
```

Phase 27 companion file extends this to **render to WAV + MIDI** so `Phase27ByteIdenticalPragmaTests` can pin both extensions:

```flow
// Phase 27 D-402 + D-404 + D-403 + RESEARCH Pitfall 7 contract:
//   Path strings MUST be inline in writeWav/writeMidi calls (test rewrites via string.Replace)
//   Filenames disambiguate companion outputs in examples/output/
tempo 120 {
    timesig 4/4 {
        section h_demo {
            // Sequence layered for audible "H4 == B4 outside German notation"
            | H4q B4q C5q |
        }
    }
}
Song demo = [h_demo]
Buffer audio = (renderSong demo "piano")
(writeWav "examples/output/h_alias.wav" audio)
(writeMidi "examples/output/h_alias.mid" demo)
(print "h_alias: rendered examples/output/h_alias.{wav,mid}")
```

D-402 also requires demonstrating that `H` outside note streams remains a usable identifier (e.g. `Int H = 5; (print H)`).

**Length cap:** ~30 lines per CONTEXT D-402.

---

### `examples/pragmas/microtonal_ji.flow` (pragma demo — CREATE)

**Analog:** `tests/test_tuning_ji.flow` (30 lines) — lines 1-30 verbatim shape.

**Imports + pragma pattern** (lines 1-5 of analog):

```flow
enable justIntonation;

use "@std"
use "@audio"
```

`enable justIntonation;` first non-comment, non-blank line (same PRAG-01 rule).

**Demo body + render pattern** (lines 7-29 of analog — direct mirror):

```flow
Note: MICR-01 acceptance — 5:4 just-intonation third (Phase 23)
Note: With `enable justIntonation;` declared, the C-E interval renders at
Note: ratio 5/4 (= 1.25) instead of 12-TET ~1.2599 (Math.Pow(2, 4/12)).
Note: D-01 tonic = innermost active key; D-02 silent C-major default when no key block.

Note: Build a basic C-major triad sequence under justIntonation
tempo 120 {
    timesig 4/4 {
        section ji_triad {
            | C4q E4q G4q C4w |
        }
    }
}

Song song = [ji_triad]
Buffer audio = (renderSong song "piano")
(writeWav "examples/output/microtonal_ji.wav" audio)
(writeMidi "examples/output/microtonal_ji.mid" song)
(print "JI Cmaj triad rendered — major third at 5:4 ratio")
```

**D-402 specifics: print frequency-ratio comparison** (CONTEXT § specifics):

```flow
// Print the JI vs 12-TET ratio comparison so the file is meaningful even without listening.
(print "JI major third (C-E):  ratio 5/4 = 1.25")
(print "12-TET major third:    ratio 2^(4/12) ≈ 1.2599")
(print "JI fifth (C-G):        ratio 3/2 = 1.5")
(print "12-TET fifth:          ratio 2^(7/12) ≈ 1.4983")
```

**Path-string inline rule** (RESEARCH Pitfall 7): `examples/output/microtonal_ji.wav` and `examples/output/microtonal_ji.mid` strings appear LITERALLY in `(writeWav)` / `(writeMidi)` — NOT via a String var. The Phase 27 test rewrites these strings via `string.Replace`.

**Length cap:** ~40 lines per CONTEXT D-402.

---

### `flow-lang.Tests/Integration/Phase27/Phase27ByteIdenticalPragmaTests.cs` (xUnit fact class — CREATE)

**Analog:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` (90 lines) — verbatim copy with parameter swap. Phase 25's `ByteIdenticalShowcaseGaussianTests.cs` documents this verbatim-mirror pattern explicitly in its doc comment ("Mirrors Phase18/ByteIdenticalShowcaseTests.cs:1-90 verbatim — the only changes are the namespace, the class name, and the run-file basenames.")

**Imports pattern** (analog lines 1-6 — copy verbatim):

```csharp
using System;
using System.IO;
using System.Linq;
using FlowLang.Tests;
using FlowLang.Tests.Fixtures;
using Xunit;
```

**Namespace + class header pattern** (analog lines 8-18 — adjust namespace, class name, doc comment):

```csharp
namespace FlowLang.Tests.Integration.Phase27;

/// <summary>
/// Phase 27 D-403 byte-identical determinism gate for examples/pragmas/h_alias.flow
/// + examples/pragmas/microtonal_ji.flow companion files.
///
/// Mirrors Phase18/ByteIdenticalShowcaseTests.cs:1-90 verbatim — the only changes are
/// the namespace, the class name, the script paths (examples/pragmas/{baseName}.flow),
/// and the run-file basenames (phase27_{baseName}_run1.{ext}).
/// </summary>
[Collection("FlowScripts")]
public class Phase27ByteIdenticalPragmaTests
{
```

**Four-fact pattern** (D-403: 4 facts = 2 files × 2 extensions):

```csharp
[Fact] public void HAlias_TwoRunsProduceIdenticalWav()       => RunTwiceAndCompare("h_alias",       isMidi: false);
[Fact] public void HAlias_TwoRunsProduceIdenticalMidi()      => RunTwiceAndCompare("h_alias",       isMidi: true);
[Fact] public void MicrotonalJi_TwoRunsProduceIdenticalWav() => RunTwiceAndCompare("microtonal_ji", isMidi: false);
[Fact] public void MicrotonalJi_TwoRunsProduceIdenticalMidi()=> RunTwiceAndCompare("microtonal_ji", isMidi: true);
```

This is a **parameter extension** beyond the analog's 2-fact pattern — Phase 18 hardcodes `flow_showcase` so its 2 facts dispatch on `isMidi` only. Phase 27 has TWO scripts, so the helper takes both `baseName` and `isMidi`.

**RunTwiceAndCompare pattern** (analog lines 32-89 — verbatim copy with the additional `baseName` parameter threaded through):

```csharp
private static void RunTwiceAndCompare(string baseName, bool isMidi)
{
    string testsRoot = FlowScriptData.FindTestsRoot();
    string repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."));
    // CHANGE: examples/pragmas/{baseName}.flow instead of examples/showcase.flow
    string scriptPath = Path.Combine(repoRoot, "examples", "pragmas", $"{baseName}.flow");
    Assert.True(File.Exists(scriptPath), $"{baseName}.flow missing at {scriptPath}");

    string ext = isMidi ? "mid" : "wav";
    // CHANGE: outputs disambiguated by baseName (was hardcoded "flow_showcase")
    string outDir = Path.Combine(repoRoot, "tests", "output");
    string path1 = Path.Combine(outDir, $"phase27_{baseName}_run1.{ext}");
    string path2 = Path.Combine(outDir, $"phase27_{baseName}_run2.{ext}");

    if (File.Exists(path1)) File.Delete(path1);
    if (File.Exists(path2)) File.Delete(path2);
    Directory.CreateDirectory(outDir);

    string source = File.ReadAllText(scriptPath);
    // CHANGE: defaultRel uses baseName instead of "flow_showcase"
    string defaultRel = $"examples/output/{baseName}.{ext}";
    string sourceRun1 = source.Replace(defaultRel, $"tests/output/phase27_{baseName}_run1.{ext}");
    string sourceRun2 = source.Replace(defaultRel, $"tests/output/phase27_{baseName}_run2.{ext}");

    Assert.NotEqual(source, sourceRun1); // substitution must have actually replaced

    string originalCwd = Environment.CurrentDirectory;
    try
    {
        Environment.CurrentDirectory = repoRoot;

        using (var runner1 = new FlowEngineRunner())
        {
            var (success1, _, stderr1, errorCount1) = runner1.RunSource(sourceRun1);
            Assert.True(success1, $"run1 failed: stderr={stderr1}");
            Assert.Equal(0, errorCount1);
        }

        using (var runner2 = new FlowEngineRunner())
        {
            var (success2, _, stderr2, errorCount2) = runner2.RunSource(sourceRun2);
            Assert.True(success2, $"run2 failed: stderr={stderr2}");
            Assert.Equal(0, errorCount2);
        }

        Assert.True(File.Exists(path1), $"output not written: {path1}");
        Assert.True(File.Exists(path2), $"output not written: {path2}");

        byte[] bytes1 = File.ReadAllBytes(path1);
        byte[] bytes2 = File.ReadAllBytes(path2);

        Assert.True(bytes1.Length > 0, $"empty output: {path1}");
        Assert.True(bytes1.SequenceEqual(bytes2),
            $"{ext} bytes differ: run1 len={bytes1.Length}, run2 len={bytes2.Length}");
    }
    finally
    {
        Environment.CurrentDirectory = originalCwd;
    }
}
```

**Critical preservation points** (RESEARCH §Critical Note + Pitfall 1):

1. NO inline `byte[]` literal — pin-byte arrays exist ONLY in `Phase15/EuclideanByteIdenticalTests.cs` for compact MIDI velocity sequences. Phase 27 uses two-run SequenceEqual (verbatim Phase 18 pattern).
2. `[Collection("FlowScripts")]` attribute — required for serialized execution; collisions on `Console.SetOut/SetError` between parallel `FlowEngineRunner` instances would corrupt assertions.
3. `Environment.CurrentDirectory = repoRoot` inside `try/finally` — required because companion `.flow` scripts use relative `examples/output/...` paths in `writeWav`/`writeMidi`.
4. `Assert.NotEqual(source, sourceRun1)` halt-gate — verifies that the test's `string.Replace` actually engaged. If a future maintainer changes the pragma file's path string (Pitfall 7), this fires immediately rather than silently passing.

**Test infrastructure dependency:**

- `FlowLang.Tests.Fixtures.FlowEngineRunner` (existing — 50+ lines at `flow-lang.Tests/Fixtures/FlowEngineRunner.cs`). Constructor hijacks `Console.Out` / `Console.Error`; `RunSource(string source)` returns `(Success, Stdout, Stderr, ErrorCount)`. Phase 27 uses verbatim.
- `FlowLang.Tests.FlowScriptData.FindTestsRoot()` (existing). Resolves the `flow-lang.Tests/` directory absolute path; Phase 18 + 25 + 27 derive `repoRoot = Path.GetFullPath(Path.Combine(testsRoot, ".."))` from this.

---

### `CLAUDE.md` (project guidance — MODIFY)

**Analog:** itself, "Language Features → Music-Specific" section. Phase 26.2 closure (commit `86bdd15`) added the `gain` vs `volume` distinction bullet AND the `Hertz` type bullet to the Core Language Features list (lines that are already present).

**Pattern from existing CLAUDE.md** (D-104 says append AFTER existing Music-Specific section; existing Music-Specific bullets show feature → brief explanation → "(Phase 26.2)" tag):

```markdown
- **`gain` vs `volume` distinction:** `gain(Buffer, Double|Decibel)` interprets its 2nd arg as decibels (negative = attenuate, positive = amplify); `volume(Buffer, Double)` interprets its 2nd arg as a linear multiplier (0.5 = half-amplitude, 2.0 = double-amplitude). Composer chooses by semantic intent — function name documents the unit. Negative `volume` rejected (use `gain` for dB attenuation); both emit clipping warnings to stderr when post-multiplication samples exceed 1.0 (Phase 26.2)
- **Hertz type + literal syntax:** `Hertz` first-class music type for audio frequency parameters; `800Hz` and `1.5kHz` literals (kHz canonical-Hz at lex time: 1.5kHz → 1500.0). Used by filters (`lowpass`/`highpass`/`bandpass`) + signal generators (`createSineTone`/`createSawTone`/`createSquareTone`/`createTriangleTone`); coexists with bare-Double overloads via OverloadResolver exact-match scoring (Phase 26.2)
```

**Music Types Quick Reference table pattern** (NEW per D-104 — append after the existing Music-Specific bullet list, ~20 lines):

```markdown
### Music Types Quick Reference

| Literal      | Type        | IsCompatibleWith       | Accepted at                                                     |
|--------------|-------------|------------------------|------------------------------------------------------------------|
| `-12dB`      | `Decibel`   | `Double`, `Float`      | `gain`, `compress` threshold, `sidechain` threshold, anywhere `Double` |
| `100ms`      | `Millisecond` | `Double`, `Float`    | `delay`, `compress` attack/release, `sidechain` attack/release, `CanConvertTo Second` |
| `2.5s`       | `Second`    | `Double`, `Float`      | `reverb` decay, `CanConvertTo Millisecond`                       |
| `+50c`       | `Cent`      | `Double`, `Float`      | `transpose` cent-precision                                       |
| `+2st`       | `Semitone`  | `Int`                  | `transpose` semitone-precision                                   |
| `1.5` (Beat-tagged) | `Beat` | `Double`, `Float`      | beat-position arithmetic                                         |
| `440Hz` / `1.5kHz` | `Hertz` | `Double`, `Float`     | `lowpass`/`highpass`/`bandpass`, `createSineTone`/etc.            |
| `#foo`       | `Symbol`    | strict (no Double/Float) | `Dict<Symbol, V>` keys, identity-equality usage                |
```

Appended directly to the Music-Specific bullet block. Single source of truth for the music-type surface (per CONTEXT § specifics sketch).

---

### `.planning/REQUIREMENTS.md` QOL-04 (MODIFY — closure rewrite)

**Analog:** Phase 26.2 ERG-01..ERG-05 entries (lines 113-123) AND Phase 26.1 DICT-01/02/03 entries (lines 107-111). Both authored at closure to reflect actual landed scope. CONTEXT D-101 explicitly cites this pattern.

**Current QOL-04 entry** (line 127, status `[ ]`):

```markdown
- [ ] **QOL-04**: `examples/tutorial.flow` and `examples/showcase.flow` refreshed to demonstrate every v1.3 feature end-to-end: tuplets `{3:2 ...}q`, fractional `C4/12`, range, multi-letter enharmonics (E↔Fb, F↔E#, B↔Cb, C↔B#), negative slice, `enable hAsB;` pragma, arpeggio/voicings/delay-sync/quantize/legato/portamento/varispeed-loadWav, named-tuning microtonal, scale-lint pragma, `humanizeGaussian`, **dict** `(dict "k" v)` + `(get d "k")`. Both scripts run to completion (exit 0) producing non-empty WAV + MIDI; byte-identical determinism contract holds across two consecutive runs (cmp-clean). Existing v1.1 + v1.2 chapters preserved.
```

**Closure-rewrite pattern** (mirror Phase 26.1 DICT-01 verbosity AND Phase 26.2 ERG-04 5-hash chain shipped-marker):

```markdown
- [x] **QOL-04**: `examples/tutorial.flow` and `examples/showcase.flow` refreshed to demonstrate every v1.3 feature end-to-end. Language additions: prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`/`(idiv)`/`(neg)`/`(concat)` (Phase 26); Symbol primitive `#foo` (Phase 26.1 SYM-01); Tuple `<<a, b, c>>` literal + `tup@N` indexing + destructuring + `~>` flow op + `(unpack)` runtime (Phase 26.1 TUP-09/10/11); generic `Dict<K, V>` 14-op surface (Phase 26.1 DICT-01/02/03). Music features: tuplets `{3:2 ...}q` + fractional `C4/12` + nested tuplets (Phase 19 TUP-01..08); `range(Int, Int)` / `range(Int, Int, Int)` (Phase 20 DEFER-01); multi-letter enharmonics E↔Fb / F↔E# / B↔Cb / C↔B# (Phase 20 DEFER-04); negative slice `arr@-1` / `slice(arr, -3, _)` (Phase 20 DEFER-05); `enable hAsB;` pragma (Phase 21 PRAG-01/02); DX-10..15 bundle — arpeggio rate/direction/pattern, chord inversions+voicings, NoteValue-rate delay, quantize, legato/portamento, varispeed-loadWav (Phase 22); microtonal pragmas `enable justIntonation;` / `pythagorean;` / `equalTemperament;` (Phase 23 MICR-01..03); scale-lint pragma `enable scaleLint;` print-only mention (Phase 24 LINT-01..03 — flow-lsp owns surface); `humanizeGaussian(seq, amount, seed)` (Phase 25 DEFER-06). Phase 26.2 surface: `volume(Buffer, Double)` linear-multiplier alongside `gain` dB-only split (ERG-03); Hertz literals `440Hz` / `1.5kHz` with kHz canonical-Hz lex (ERG-04); Ms-typed FX overloads on `delay`/`compress`/`sidechain` (ERG-02); Second-decay `(reverb buf mix 1.5s)` (ERG-02); `Hertz` overloads on `lowpass`/`highpass`/`bandpass` filters + `createSineTone`/`createSawTone`/`createSquareTone`/`createTriangleTone` (ERG-04); `(gain buf -12dB)` literal at expression-start (ERG-05). Companion files under `examples/pragmas/`: `h_alias.flow` (~30 lines, `enable hAsB;`) + `microtonal_ji.flow` (~40 lines, `enable justIntonation;`). Both tutorial + showcase scripts run to completion (exit 0) producing non-empty WAV + MIDI; byte-identical determinism contract holds across two consecutive runs (cmp-clean) — `Phase18ByteIdenticalTutorialTests` + `Phase18ByteIdenticalShowcaseTests` + `Phase25ByteIdenticalShowcaseGaussianTests` + new `Phase27ByteIdenticalPragmaTests` (4 facts pinning h_alias.flow + microtonal_ji.flow run-twice identity). CLAUDE.md Music Types Quick Reference table appended for composer + future-agent reference. v1.1 + v1.2 chapters preserved. — Shipped <COMMIT_HASH>
```

**Traceability table row** (line 200 — flip from `Pending` to `Shipped <hash>`):

```markdown
| QOL-04 | Phase 27 | Shipped <COMMIT_HASH> |
```

The pattern: feature list grouped by "Language additions" / "Music features" / "Phase 26.2 surface" sub-clauses; checkbox `[ ] → [x]`; trailing `— Shipped <hash>` marker matches the Phase 26.1/26.2 entries verbatim.

---

### `.planning/ROADMAP.md` Phase 27 (MODIFY — closure marking)

**Analog:** Phase 26.2 entry pattern at line 74. ROADMAP closure rewrites occur in two places:

**Phase Summary line** (line 75 — flip `[ ] → [x]`, add `Shipped 2026-MM-DD (Waves 0-N: hash + hash + ... + closure)` suffix):

```markdown
- [x] **Phase 27: Tutorial + Showcase Refresh** — `examples/tutorial.flow` + `examples/showcase.flow` exercise every v1.3 feature end-to-end (including prefix-only arithmetic, symbols, tuples, dicts); byte-identical determinism re-pinned — Shipped 2026-MM-DD (Waves 0-N: <hash> + <hash> + ... + closure)
```

**Detail entry** (lines 266+ "### Phase 27: Tutorial + Showcase Refresh"): plan list with shipped commit hashes per wave. Pattern from Phase 26.2 detail entry (line 247+).

**Progress table row:** flip `Phase 27 | v1.3 | 0/N | Not started | -` → `N/N | Complete | 2026-MM-DD`.

---

### `.planning/STATE.md` (MODIFY — state-advance)

**Analog:** Phase 26.2 closure pattern (current STATE.md lines 5-6, 11, 13, 24, 31, 38, 535, 544).

**Frontmatter advances** (lines 5-13):

```yaml
status: "Phase 27 fully shipped 2026-MM-DD. v1.3 milestone shipped (12/12 phases). ..."
stopped_at: Phase 27 fully shipped
last_activity: 2026-MM-DD - Phase 27 closure (plan 27-NN)
progress:
  completed_phases: 12  # was 11
  completed_plans: <prev + N>
```

**Current Position section** (line 24): `Phase 27 — tutorial-showcase-refresh (v1.3 milestone closer)` → `v1.3 milestone shipped 12/12; ready for /gsd-complete-milestone v1.3 OR v1.4 planning`.

**Resume Instructions** (top + bottom — lines 31-38, 535-548): rewrite to "v1.3 milestone shipped" forward-pointing instruction.

**Performance Metrics table:** add rows for each Phase 27 plan with duration extracted from each plan SUMMARY.

---

### `.planning/phases/27-.../27-VERIFICATION.md` (CREATE — closure deliverable)

**Primary analog:** `.planning/phases/26.2-music-type-ergonomics-fx-overloads-inserted/26.2-VERIFICATION.md` (149 lines) — most recent closure precedent.
**Secondary analog:** `.planning/phases/16-tutorial-refresh/16-VERIFICATION.md` (Phase 16 was the previous tutorial-refresh phase; uses grep-table + smoke-transcript pattern that fits Phase 27 directly).

**Frontmatter pattern** (26.2-VERIFICATION.md lines 1-6):

```yaml
---
phase: 27-tutorial-showcase-refresh
status: passed
phase_name: tutorial-showcase-refresh
shipped: 2026-MM-DD
nyquist_compliant: true
verification_source: plan-27-NN-closure
must_haves_verified: <count>
must_haves_total: <count>
---
```

**Section structure** (mirror 26.2-VERIFICATION.md):

1. `# Phase 27 — Verification (Closure)` heading + Goal restatement.
2. `## Plans (N/N)` table — plan number, wave, requirement, commit, status.
3. `## Must-Haves Audit` per plan — bulleted ✅ + commit ref evidence (Phase 26.2 has Plan 26.2-01 through 26.2-06 sections).
4. `## Regression Gates` table — Phase 18 ByteIdenticalShowcase, Phase 18 ByteIdenticalTutorial, Phase 25 ByteIdenticalShowcaseGaussian, NEW Phase 27 ByteIdenticalPragma, full unit suite, FlowScriptTests, examples scripts exit 0, companion files exit 0, all GREEN.
5. **Phase 16 grep-table addition** — Phase 16-VERIFICATION lines 41-59 pattern: `Feature | Required by ROADMAP #1 | Tutorial chapter | Grep verification`. For Phase 27, expand to cover every v1.3 feature listed in QOL-04 rewrite (prefix-only, Symbols, Tuples + ~>, Dict, tuplets, range, multi-letter enharmonics, negative slice, hAsB, DX-10..15, microtonal, scaleLint print-only, humanizeGaussian, volume, gain-vs-volume, Hertz, Ms-FX, Second-reverb).
6. **Phase 16 smoke transcript addition** — pinned `dotnet run --project flow-interpreter examples/tutorial.flow` output excerpt + `examples/showcase.flow` + each `examples/pragmas/*.flow` exit-0 confirmation.
7. `## Open Questions Resolved` (RESEARCH § Open Questions answered).
8. `## Deviations Summary` (any auto-fixed deviations from per-plan SUMMARYs).
9. `## Acceptance Idioms` cross-referenced against ROADMAP § Phase 27 success criteria #1-#4.
10. `## Sign-Off` block.

---

### `.planning/phases/27-.../27-SUMMARY.md` (CREATE — closure deliverable)

**Analog:** `.planning/phases/26.2-music-type-ergonomics-fx-overloads-inserted/26.2-06-SUMMARY.md` (190 lines) — closure plan SUMMARY pattern.

**Frontmatter pattern** (26.2-06-SUMMARY.md lines 1-39):

```yaml
---
phase: 27-tutorial-showcase-refresh
plan: NN
subsystem: docs-only-closure   # or examples-and-tests-and-docs depending on scope split
tags: [closure, requirements-authoring, roadmap-marking, state-advance, claude-md-update, verification-doc]
requires: [phase27-01, phase27-02, ...]
provides: [phase27-shipped, qol-04-traceability, v1.3-milestone-complete]
affects:
  - .planning/REQUIREMENTS.md
  - .planning/ROADMAP.md
  - .planning/STATE.md
  - .planning/phases/27-tutorial-showcase-refresh/27-VERIFICATION.md
  - CLAUDE.md
tech-stack:
  added: []
  patterns: [phase-26.2-closure-pattern-mirror, single-atomic-docs-commit]
key-files:
  created:
    - .planning/phases/27-tutorial-showcase-refresh/27-VERIFICATION.md
    - .planning/phases/27-tutorial-showcase-refresh/27-SUMMARY.md
  modified:
    - .planning/REQUIREMENTS.md
    - .planning/ROADMAP.md
    - .planning/STATE.md
    - CLAUDE.md
decisions:
  - "Closure commit prefix `docs(27):` matches Phase 26.2 closure precedent (commit 86bdd15)"
  - "QOL-04 rewrite uses full canonical text mirroring Phase 26.1 DICT-01/02/03 + Phase 26.2 ERG-01..ERG-05 verbosity (CONTEXT D-101)"
  - ...
metrics:
  duration: ~N minutes
  tasks: N
  files-touched: N
  completed: 2026-MM-DD
---
```

**Body structure** (mirror 26.2-06-SUMMARY.md):

1. `## One-liner` — single-paragraph closure summary.
2. `## Tasks Completed` table.
3. `## Files Authored / Modified` per file with bullet-list of specific changes.
4. `## Test Results` — Phase 27 filter, regression sentinels, full suite numbers.
5. `## Must-haves Audit` table — every plan must_have with `✓` + grep/file evidence command.
6. `## Deviations from Plan`.
7. `## Decisions Made` numbered list.
8. `## Threat Surface Scan`.
9. `## Self-Check: PASSED` bullet list.
10. `## v1.4 / Milestone Handoff` (Phase 26.2 had "## Phase 27 Handoff" — Phase 27 closes the milestone, so this becomes a v1.4 / release-tag handoff).

---

## Shared Patterns

### Pattern S1: Two-Run Byte-Identical SequenceEqual (xUnit)

**Source:** `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs:32-89` (mirrored verbatim by `Phase25/ByteIdenticalShowcaseGaussianTests.cs:40-97`).

**Apply to:** All new byte-identical regression tests for `.flow` scripts that produce WAV+MIDI to `examples/output/`.

**Concrete excerpt** (the load-bearing assertion block at lines 78-83):

```csharp
byte[] bytes1 = File.ReadAllBytes(path1);
byte[] bytes2 = File.ReadAllBytes(path2);

Assert.True(bytes1.Length > 0, $"empty output: {path1}");
Assert.True(bytes1.SequenceEqual(bytes2),
    $"{ext} bytes differ: run1 len={bytes1.Length}, run2 len={bytes2.Length}");
```

NEVER add an inline `byte[]` literal pinning specific bytes — that is reserved for compact MIDI velocity sequences in `Phase15/EuclideanByteIdenticalTests.cs`. RESEARCH Pitfall 1 documents the misreading of CONTEXT D-204 as "pin bytes." The actual closure work for D-204 is "verify Phase 18/25 stay GREEN," NOT "encode hex literals."

### Pattern S2: Path-string-inline writeWav/writeMidi

**Source:** `examples/showcase.flow:36-37`, `examples/tutorial.flow:643-644`.

**Apply to:** Every `.flow` script that participates in a byte-identical determinism gate (tutorial, showcase, every companion file).

**Concrete excerpt:**

```flow
(writeWav "examples/output/<basename>.wav" finalBuffer)
(writeMidi "examples/output/<basename>.mid" songValue)
```

The path STRING is literal in the call. NEVER `String wavPath = "..."; (writeWav wavPath buf)`. The `Phase 18/25/27ByteIdenticalTests` rewrite paths via `source.Replace(...)`; variable indirection breaks the rewrite silently. Phase 16 REVIEW-FIX IN-02 surfaced this once already. Phase 27 RESEARCH Pitfall 7 reiterates.

### Pattern S3: Pragma-First-Line PRAG-01

**Source:** `tests/test_h_alias.flow:1`, `tests/test_tuning_ji.flow:1`. Verified by `Phase21/PragmaScannerFacts:22-49` — pragmas must appear in the prefix region (comments + blank lines + pragmas only) before any non-pragma statement.

**Apply to:** Both `examples/pragmas/h_alias.flow` and `examples/pragmas/microtonal_ji.flow`.

**Concrete excerpt:**

```flow
enable hAsB;          # OR: enable justIntonation; — must be line 1 (or after only comments + blanks)

use "@std"
use "@audio"
# ... rest of file ...
```

Pragmas DO NOT propagate via `use` (Phase 21 PragmaIsolationFacts:24-69). Tutorial.flow CANNOT pull `enable hAsB;` from a companion file via `use`. Demonstration is by prose + pointer per CONTEXT D-401.

### Pattern S4: `(print "")` blank-line + `(print "--- N. Title ---")` chapter divider

**Source:** `examples/tutorial.flow:21-26, 40-45, 60-65, 86-91, 124-129, …`. Every existing chapter follows this 6-line preamble:

```flow
Note: -----------------------------------------------------------
Note: N. Chapter Title
Note: -----------------------------------------------------------
(print "")
(print "--- N. Chapter Title ---")
(print "")
```

**Apply to:** Every NEW chapter inserted into tutorial.flow (D-302 Symbols / Tuples+~> / Dict; D-303 v1.3 Music Capabilities batch chapter + sub-sections; D-102 gain-vs-volume own chapter).

### Pattern S5: Fixed-seed determinism for euclidean + humanizeGaussian

**Source:** `examples/showcase.flow:17` (`(euclidean 5 16 A2 0.18 0.12 7)` — seed `7`), `examples/showcase.flow:20` (`humanizeGaussian ... 0.08 314` — seed `314`), `examples/tutorial.flow:546, 594` (seed `42` everywhere).

**Apply to:** New showcase.flow + new graduation song + companion files. Use SOME fixed seed; the specific numeric value is composer-discretion (Phase 16 used 42, v1.2 showcase used 7+314).

**Concrete excerpt:**

```flow
Sequence drums = (euclidean 5 16 (get kit #kick) 0.18 0.12 7)
Sequence melody = (humanizeGaussian | mp _ _ E5q G5q | A5h E5h | 0.08 314)
```

NO unseeded `random` / `randomInt` / `choose` / `(? ... )` syntax. NO `DateTime.Now`. RESEARCH Pitfall 1 + A1 verify byte-identical contract is two-run SequenceEqual, content-agnostic — works automatically as long as the script is deterministic.

### Pattern S6: Closure single-atomic docs commit

**Source:** Phase 26.2 closure commit `86bdd15` (Wave 5 SUMMARY 26.2-06 line 28: "Closure commit prefix `docs(26.2):` matches Phase 26.1 closure precedent — single atomic commit for all closure docs"); Phase 26.1 closure `41fd4ab`; Phase 23-05 / 22-07 / 21-03 / 20-04 / 24-05 closure plans.

**Apply to:** Phase 27 final closure plan (likely 27-05 per RESEARCH § Phase 16 Plan Skeleton).

**Concrete excerpt:**

```bash
git commit -m "$(cat <<'EOF'
docs(27): closure — REQUIREMENTS/ROADMAP/STATE/VERIFICATION/SUMMARY/CLAUDE.md

[Body explaining QOL-04 rewrite, ROADMAP marking, etc.]
EOF
)"
```

ONE commit covering all closure docs (REQUIREMENTS.md + ROADMAP.md + STATE.md + 27-VERIFICATION.md + 27-SUMMARY.md + CLAUDE.md), NOT per-file commits.

---

## No Analog Found

| File | Role | Reason |
|------|------|--------|
| _(none)_ | _(none)_ | Every Phase 27 artifact has a direct in-repo precedent. The phase is documentation-shaped + test-mirror-shaped; there is zero greenfield surface. |

---

## Metadata

**Analog search scope:**

- `examples/` (tutorial.flow, showcase.flow, output/.gitignore)
- `tests/test_h_alias.flow` + `tests/test_tuning_ji.flow` (pragma demos)
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` + `Phase18/ByteIdenticalTutorialTests.cs` + `Phase25/ByteIdenticalShowcaseGaussianTests.cs`
- `flow-lang.Tests/Fixtures/FlowEngineRunner.cs`
- `.planning/phases/16-tutorial-refresh/{16-VERIFICATION.md, 16-SUMMARY.md, 16-CONTEXT.md}` (previous tutorial-refresh phase)
- `.planning/phases/26.1-symbols-tuples-dicts/26.1-VERIFICATION.md` + `.planning/phases/26.2-.../{26.2-VERIFICATION.md, 26.2-06-SUMMARY.md}` (recent closure precedents)
- `.planning/REQUIREMENTS.md` (DICT-01/02/03 + ERG-01..ERG-05 + QOL-04 entries)
- `.planning/ROADMAP.md` (Phase 26.2 entry + Phase 27 entry)
- `.planning/STATE.md` (current state + Phase 26.2 closure trail)
- `CLAUDE.md` (Music-Specific section + Special Types list)

**Files scanned:** 18 (analogs) + 2 (CONTEXT + RESEARCH) = 20.
**Pattern extraction date:** 2026-05-10.
