# Domain Pitfalls: v1.2 Stability & Composer DX

**Domain:** Flow language interpreter bug fixes + composer DX features on an existing DSL
**Researched:** 2026-04-18
**Confidence:** HIGH (audit-verified via file:line inspection of interpreter, lexer, transforms, envelope processor, MIDI export)
**Scope:** Pitfalls specific to the 7 critical audit findings (C1-C7), Tier A DX bundle, and retroactive Nyquist validation

---

## Critical Pitfalls

Mistakes that cause regressions, silent semantic drift, or break user code.

### Pitfall 1: "Frame Leak" Fix That Silently Rebalances the Error Model

**What goes wrong:**
The audit describes C1 as a frame-leak in `ExecuteMusicalContext`. Inspection of `Interpreter.cs:130-290` shows the actual situation is subtler and the wrong fix is worse than the bug:

- The method IS wrapped in `try { PushFrame(); ... } finally { PopFrame(); }` — so the CLR frame is popped even on early `return`.
- The real issue is the body never executes after a validation error (lines 151, 164, 178, 224, 240, 255, 263 all `return` from inside the `try`, skipping the `foreach (var stmt in ctx.Body)` loop).
- The naive fix "replace `return` with `break`" would let the body execute with a partially-initialized `musicalCtx` (e.g., `Tempo` never set after a bad tempo value). The body now runs under the **parent** context instead of erroring loudly.
- The naive fix "convert to `throw`" violates the project's **soft-failure error model** documented in `PROJECT.md` Key Decisions and `CLAUDE.md` ("Soft-failure error model: Programs continue after errors; better REPL experience").

**Why it happens:**
The developer sees "early return inside try/finally = works fine, frame is popped" and concludes the audit is wrong. But the bug the audit *means* is: "after an invalid `tempo -1`, the rest of the program should still render with default tempo, not skip statements entirely." The `return` exits `ExecuteMusicalContext` but does NOT set `_returnValue`, so subsequent top-level statements DO still execute — however, any statements inside the malformed context block's body are silently skipped.

**Consequences:**
- Users get partial renders with no indication which statements were skipped.
- Error messages point at the invalid value but don't say "the 12 notes inside this block were dropped."
- Tests that validate "error reported + rest of program continues" may pass while the block body was silently dropped.

**Prevention:**
1. **Audit the intent of each `return` BEFORE changing.** For each of the 7 early-return sites, decide:
   - Should the block body execute with defaults? (Recommend: YES for tempo/swing/pan/gain — they have defaults in `GetMusicalContext`.)
   - Should the block body execute with partial context? (Recommend: YES for key — unresolved numerals already render as rests; a warning exists path.)
   - Should the block body be skipped? (Recommend: only if the body semantically depends on the invalid value.)
2. **Replace `return` with `continue-with-defaults`**: set `musicalCtx` to valid defaults, emit an error, fall through to the body loop.
3. **Add a regression test** per validation path: `tempo -1 { | C4 D4 | }` should emit the error AND render 2 notes at 120 BPM default.
4. **Do NOT touch the `_returnValue != null` short-circuit in `ExecuteStatement` (C2) in the SAME commit.** These are coupled and confounded regressions will be untraceable.

**Warning signs during implementation:**
- Test output shows "0 errors, 0 notes rendered" — block body was silently dropped.
- A test that previously passed with N statements after an error now fails — you accidentally cascaded `_returnValue`.
- Changing `return` to `throw` — this is always wrong for this codebase.

**Phase to address:** Stability Phase 1 (C1 + C2 together in one commit, since C2 is the short-circuit that C1's early-returns avoid triggering).

---

### Pitfall 2: Fixing C2 (statement short-circuit) Without Distinguishing Returns from Errors

**What goes wrong:**
`ExecuteStatement` at `Interpreter.cs:73-74` reads `if (_returnValue != null) return;`. The audit flags this as masking errors. The naive fix — "remove the check" — breaks legitimate proc early-return semantics. The naive fix — "only check inside procs" — breaks implicit returns.

**Why it happens:**
`_returnValue` is overloaded: it signals (a) explicit `return` from a proc, (b) implicit return of the last expression in a proc body, (c) *possibly* is being set spuriously on some error paths (the audit is not explicit about which paths). The fix must preserve (a) and (b) while ensuring error paths never set it.

**Prevention:**
1. **Grep for every `_returnValue =` assignment in the interpreter.** There should only be two legitimate sites: `ExecuteReturn` and the implicit-return collector at end of `ExecuteProc`.
2. **Any other assignment is the bug.** Fix at the source, not at the check site.
3. **If all `_returnValue =` sites are legitimate**, the C2 audit finding is about the scope of the check: it should only short-circuit *within the current proc body*, not across top-level statements. Move the check into proc-body iteration, out of `ExecuteStatement`.
4. **Preserve the soft-failure contract:** errors accumulate in `ErrorReporter`, execution continues. A clear rule: "errors never set `_returnValue`; only explicit/implicit returns do."

**Warning signs:**
- Tests with multiple `proc` definitions start failing (you broke implicit returns).
- Tests that define a proc and then call it at the top level now produce no output (you broke explicit returns).
- `test_error_masking.flow` now reports fewer errors than before (you accidentally suppressed accumulation).

**Phase to address:** Stability Phase 1 (coupled with C1; see Pitfall 1).

---

### Pitfall 3: C5 (`augment`/`diminish` Swap) — BREAKING CHANGE Communication Failure

**What goes wrong:**
`TransformFunctions.cs:247,268` confirms the audit: `Augment` subtracts 1 from `NoteValueType` (WHOLE=0 to THIRTYSECOND=5), turning quarter into eighth (shorter). Musically, "augment" means *lengthen* (double duration) and "diminish" means *shorten* (halve duration). The code is inverted.

The fix is one line per function — swap `-1` and `+1`, swap the clamp directions. The **hard problem is communication**:

- Any user who relied on the existing (wrong) behavior gets silently flipped output.
- v1.1 and earlier example scripts / user compositions that "worked" used the wrong semantic — fixing the code breaks their audio.
- MIDI exports using `augment` now have different durations — byte-level diffs for any user with regression tests.

**Why it happens:**
Semantics bugs in a published DSL are high-blast-radius. Once users learn the (wrong) behavior, "fixing" it is indistinguishable from "breaking" it from their perspective. This is the ground-truth of every language versioning post-mortem (Python 2→3 `print`, Ruby `Array#flatten`, Haskell `Prelude.catch`).

**Prevention:**
1. **Version-bump the breaking change loudly.** v1.2 release notes MUST have a top-level "BREAKING CHANGES" section naming `augment` and `diminish`, showing before/after.
2. **Keep both names available during migration:**
   - `augment` and `diminish` take the correct (swapped) semantics.
   - Add `augmentV1` / `diminishV1` (or `shorten` / `lengthen` with the old semantics) as explicit aliases so users can mechanically search-replace to preserve old behavior.
   - Remove the aliases in v1.3 or later.
3. **Deprecation detection is impossible here** because the names aren't changing — only the semantics. Compensate by adding a one-time startup notice if env var `FLOW_WARN_TRANSFORM_SEMANTICS=1` is set, or a new `--strict-v1.2` flag that prints a warning on first use of `augment`/`diminish`.
4. **Grep the repo's `examples/` and `tests/`** for every call to `augment`/`diminish` and update them. Visually audit each: was the original intent "double duration" (now correct) or "halve duration" (now needs rename to `diminish`)?
5. **Update tutorial + docs in the SAME commit as the fix.** A user reading v1.1 docs after v1.2 ships will be misled.
6. **Add a regression test that asserts duration NUMERICALLY**, not behaviorally: `| C4q | -> augment` must produce a half note (NoteValue 1), not just "a different duration than before."

**Warning signs:**
- PR review comment "this is just a one-line swap, ship it" — reject. The comms + migration are the whole work.
- No test that numerically pins the duration before/after — you can't verify the fix.
- Existing example scripts still have `augment` and sound "wrong" after the fix — you didn't audit calls.

**Phase to address:** Stability Phase 1 (code fix + test), but **communication artifacts (release notes, tutorial) must ship with the same release**. Consider a separate "Migration Phase" commit that updates all `examples/*.flow` and documents each change.

---

### Pitfall 4: Thunk Exception Caching (C7) — Silent Corruption Pattern

**What goes wrong:**
`Thunk.cs:35` sets `_cachedValue = _evaluator!.Evaluate(...)` and then `_isEvaluated = true` on line 36. If `Evaluate` throws, execution unwinds — but the next `Force()` call re-enters the `if (!_isEvaluated)` guard, gets `_isEvaluated = false` still (because line 36 never ran), and tries to evaluate again. Wait — actually, inspecting more carefully, the exception DOES leave `_isEvaluated = false`, so it re-evaluates. The audit claim that `_isEvaluated` is set is INCORRECT as written.

**BUT** — the real bug is different: the evaluator-dropping cleanup (lines 39-40) is inside the same `if`, so after a failed evaluation, `_expression` and `_evaluator` are still held. On retry, evaluation happens again. If the error was non-deterministic (e.g., depends on a variable value at a prior point), results may differ between retries. More critically, if `_errorReporter` accumulated errors during the failed first call and the soft-failure model let execution continue, subsequent `Force()` calls will re-report the same errors.

**Why it happens:**
`lazy` expressions are rare in the test suite — there are no failing-thunk tests. The thunk was designed for "succeed once, cache forever" and the failure mode wasn't considered.

**Prevention:**
1. **Before changing Thunk**, write a failing test: `lazy (1 / 0)` forced twice — must not double-report errors.
2. **Explicit cached-exception pattern:**
   ```csharp
   private Exception? _cachedException;
   public Value Force() {
       if (_isEvaluated) {
           if (_cachedException != null) throw _cachedException;
           return _cachedValue!;
       }
       lock (_lock) {
           if (_isEvaluated) { /* re-check */ }
           try {
               _cachedValue = _evaluator!.Evaluate(_expression!);
               _isEvaluated = true;
           } catch (Exception ex) {
               _cachedException = ex;
               _isEvaluated = true;
               throw;
           } finally {
               _expression = null;
               _evaluator = null;
           }
       }
       return _cachedValue!;
   }
   ```
3. **Decide the semantics explicitly:** does Flow's soft-failure model apply to thunks? If `lazy` expressions are supposed to fail silently and return null, say so in the type system (`Lazy<T>` should probably be `Lazy<Option<T>>`). For v1.2, preserve current semantics (caller handles exceptions) but stop the silent-null re-evaluation.

**Warning signs:**
- After fix, any test using `lazy` with side effects (like `lazy (print "x")`) prints twice — cache isn't working.
- The `_lock` changes semantics between calls — consider whether the lock is even needed (the interpreter is single-threaded in current use).

**Phase to address:** Stability Phase 1 (C7 is isolated; can ship independently of C1/C2).

---

### Pitfall 5: Envelope/Fade Div-by-Zero Fix (C3/C4) Masks Real Bugs

**What goes wrong:**
The obvious fix for `EnvelopeProcessor.cs:108,120,150,156,169` and `BufferHelpers.cs:130,159` is `Math.Max(1, frames)`. This silences the crash but:

- An envelope with `attack = 0` should produce an **instantaneous** ramp (1 sample at 1.0, or just the sustain level from frame 0). Clamping `attackFrames` to 1 produces a ramp of `[0.0]` (single sample at 0) because the loop is `for (int i = 0; i < attackFrames; i++)` and `curve[0] = (float)0 / 1 = 0`. The note is silent for its first sample.
- An envelope with `release = 0` produces `curve[lastFrame] = 1.0 - 0/1 = 1.0` — a sudden cutoff with no release. Same click problem as before.
- At very low sample rates (8kHz testing), attackSec = 0.01 rounds to 0 frames, and the Max-clamp silently re-adds 10ms of attack to every note. Now the test "very short attack at low sample rate" measures 1ms instead of 0ms.

**Why it happens:**
Div-by-zero is a crash; `Math.Max(1, n)` is a one-character fix. Both are true. But the *correct* behavior for zero-length segments is not "one frame" — it's "skip the segment and start at the correct level for the next segment."

**Prevention:**
1. **Skip zero-length segments entirely:**
   ```csharp
   if (attackFrames > 0) {
       for (int i = 0; i < attackFrames; i++, frame++)
           curve[frame] = (float)i / attackFrames;
   }
   // (no else — if zero frames, just don't advance frame counter)
   ```
2. **Clamp the final sample of each non-zero segment to its target value** to avoid the single-sample step documented in the audit's Minor Issues section: "Final attack-frame value is (n-1)/n, then sustain jumps to 1.0."
3. **Add a test matrix:** attack ∈ {0, 1ms, 10ms, 100ms} × sampleRate ∈ {8000, 44100, 48000}. Every cell must produce a finite, non-NaN envelope curve with the correct total length.
4. **Check for NaN in the final buffer** in the test — silent NaN propagation through subsequent DSP stages is harder to detect than crashes.

**Warning signs:**
- Envelope tests pass but audio has clicks at low sample rates.
- `dotnet run` produces silent output for a note that used to have a short attack — you clamped away the whole attack segment.

**Phase to address:** Stability Phase 1 (alongside other Critical fixes; isolated to 2 files).

---

### Pitfall 6: Adding `reverbTime` Context Block — Identifier Namespace Collision

**What goes wrong:**
Adding `reverbTime` as a keyword requires:
- A `TokenType.ReverbTime` enum entry.
- A string match in `ScanIdentifierOrKeyword` (`SimpleLexer.cs:571-608`) — currently `"tempo"`, `"swing"`, `"key"`, `"pan"`, `"gain"`, etc. are keywords, and `Identifier` is the default fallback.
- Parser logic in `ExecuteMusicalContext` / `Parser.cs` to recognize the new form.

The collision risk: **any existing user script with a variable or proc named `reverbTime`** stops lexing as an identifier and becomes a keyword. Scripts that compiled and ran under v1.1 now produce parse errors under v1.2.

Inspecting the lexer keyword list confirms none of the current keywords overlap with common variable names (`tempo`, `key`, `pan`, `gain` do — but those shipped in v1.0, so this risk was eaten then). Each new keyword is a new collision surface.

**Why it happens:**
Keyword-vs-identifier decisions are made at the lexer level with no backward-compatibility hook. There is no "context-sensitive keyword" pattern in the current lexer.

**Prevention:**
1. **Grep the stdlib, examples, and tests** for the exact identifier before committing: `grep -rn "reverbTime" flow-lang/*.flow examples/ tests/`. Zero hits is required.
2. **Check `MusicalContext.cs`** to see if `ReverbTime` is already a property name — if so, users might expect it as a keyword already (no conflict); if not, nobody is depending on it.
3. **Announce new keywords in release notes** with an explicit "if your code uses this identifier, rename it to X before upgrading."
4. **Use context-sensitive parsing if feasible:** only treat `reverbTime` as a keyword when followed by a `{` or a numeric literal in a position where an identifier wouldn't make sense. The existing lexer doesn't support this, but the parser can — keep `reverbTime` as `TokenType.Identifier` and have the parser match it as a keyword-in-context. This is how some languages handle `async`/`await` addition.
5. **Prefer two-word names** that are less likely to collide: `reverb_time`, `reverbSecs`, `rvb_time`. Flow's existing style favors single-word keywords, but a new two-word keyword is cheaper to add than to retract.

**Warning signs:**
- Any existing user report / issue mentioning `reverbTime` as a variable name.
- Stdlib `.flow` files that use the identifier in a proc definition.
- Release notes that don't mention the new keyword.

**Phase to address:** Composer DX Phase 2 (reverbTime feature) — with identifier audit as a pre-condition gate.

---

### Pitfall 7: Enharmonic Helpers — `H` Note Name vs Identifier Conflict

**What goes wrong:**
Adding `H = B` (German notation) to note parsing creates a conflict: every variable, proc, or section named `H` now lexes as a note literal. The conflict extends to any letter-starting identifier whose first character is H followed by a digit (`H1`, `H2`, etc.) — these would lex as note literals (H1 = B1 = B at octave 1).

Inspection of `SimpleLexer.cs:548` shows the current rule: `if (firstChar >= 'A' && firstChar <= 'G' && char.IsDigit(text[1]))` — notes are A-G followed by a digit. Extending to include `H` means `H1` becomes a note. Users with `Int H1 = 5;` break.

**Why it happens:**
Note-name lexing is ambiguity-driven. Flow already has the A-G+digit rule; adding H seems incremental. But the existing rule only covers 7 letters × reasonable octaves (0-9), while identifiers starting with H are much more common in normal code (`Height`, `Hz`, `High`, `HP`).

**Prevention:**
1. **Do NOT lex `H` as a note at the lexer level.** Keep it an identifier. Transform `H` → `B` at a later pass (e.g., in `NoteType.Parse` when parsing a note literal, or in a pre-parse identifier rewrite).
2. **Alternative: require explicit opt-in.** A `use "@notation/german"` module that adds `H` → `B` aliasing only inside note-stream expressions, not in general identifier space.
3. **Scope aliases to note-stream contexts only:** `| H4 |` should parse as B4, but `Int H = 5;` should be a valid identifier. The parser knows when it's inside `|...|` — apply the alias there only.
4. **Enharmonic pairs (`Db` ↔ `C#`)** are less risky because `Db` is already not a valid identifier (lowercase-starting is fine but the pattern has `D` capital + `b` lowercase which could be a variable named `Db`). Verify: does `String Db = "flat"` parse today? If yes, adding `Db` as a note-name alias breaks it. Same rule: apply aliasing only inside note-stream context.
5. **Test matrix:** `Int H = 5;`, `proc H () { ... }`, `Int Db = 5;`, `section H { | C4 | }`, `| H4 D4 |`, `| Db4 C#4 |` — all must have consistent, documented behavior.

**Warning signs:**
- A new test that uses `H` as a variable name starts failing the lexer.
- A user script in `examples/` with a proc named `Hello` starts parsing differently (the lookahead after `H` sees `e`, not a digit, so should be safe — verify).

**Phase to address:** Composer DX Phase 3 (enharmonic helpers) — with note-stream-scoped aliasing from day one, NOT global.

---

### Pitfall 8: MIDI Velocity from Dynamics — Envelope Sampling & Quantization

**What goes wrong:**
`MidiExport.cs:192` currently uses `note.Velocity` directly: `byte velocity = (byte)Math.Clamp((int)(note.Velocity * 127), 1, 127);`. The audit proposes "preserve `crescendo`/`decrescendo`/`swell` envelope into MIDI velocities" — the naive implementation samples the envelope at the note's *start* position. This drops time-varying dynamics within a long note.

Pitfalls:
1. **Envelope-sampling mistake:** A whole note under a crescendo should get a single velocity value (MIDI has no per-sample velocity). What value? Start? Middle? Peak? Integral? Each choice is defensible; users will have different intuitions. Pick one (recommend: velocity at note START) and document it.
2. **Velocity = 0 silently becomes rest:** The current clamp floor is 1 (MIDI velocity 0 is note-off in the standard). A crescendo from `pp` (velocity 0.1) starting with velocity 0 would produce `velocity = 1` — a near-silent note. The audit's Minor Issues (`MidiExport.cs:195`) flags this. For dynamics-driven velocity, **consider a rest threshold**: below some amplitude, emit no note event at all. But this changes note count, which breaks any user comparing MIDI byte-diffs.
3. **Quantization loss:** Flow's velocity is `double 0.0-1.0`; MIDI is `byte 1-127`. Linear scaling (`(int)(v * 127)`) loses precision. For rendered audio, this doesn't matter. For MIDI export, a crescendo from 0.5 to 0.6 (velocity 63→76, 13 steps) is different from 0.5 to 0.51 (63→64, 1 step). Users editing the exported MIDI expect smooth crescendos.
4. **Timing:** The crescendo transform already modifies `note.Velocity` on each note in the sequence BEFORE rendering. If that's true, MIDI export just uses the already-computed velocity per note — no new sampling logic needed. **Verify this before building an envelope-sampling layer on top.** The simplest correct fix may be "the MIDI path was never wrong — it was the dynamics transform that wasn't writing per-note velocity."

**Why it happens:**
Dynamics-to-MIDI is a known-hard problem in DAWs. Flow's interpreter-at-render-time architecture means the transform can fully expand dynamics to per-note velocities BEFORE MIDI export runs — this is simpler than most DAW pipelines. But if a dev assumes "MIDI needs envelope sampling" without first checking how dynamics are expanded in `TransformFunctions.cs`, they'll build the wrong abstraction.

**Prevention:**
1. **Investigate the existing dynamics implementation first.** Does `crescendo(seq, start, end)` already modify `note.Velocity` on each note in the sequence? If so, no MIDI changes needed — the bug is just that some dynamic transforms don't write velocity.
2. **Document the sampling choice:** "Flow samples dynamic velocity at note onset. Long notes do not change velocity mid-note." One sentence in the tutorial.
3. **Preserve the `velocity >= 1` clamp** to maintain audio/MIDI parity (the audit's suggestion to add a rest threshold is a separate change; don't bundle it).
4. **Regression test:** Render a sequence with crescendo to MIDI, dump the bytes, compare to a pinned reference file. Any change to velocity computation will fail this test loudly.
5. **Do not change `Math.Clamp(..., 1, 127)` to `Math.Clamp(..., 0, 127)` without a separate, explicit decision.** This would change the MIDI byte output for every quiet note.

**Warning signs:**
- Velocity output changes for notes with no dynamics applied — you broke the default path.
- MIDI files produced by v1.2 are byte-identical to v1.1 for non-dynamics sequences — good.
- A new "envelope sampler" class in the MIDI export pipeline — likely overengineering; check if transforms already handled it.

**Phase to address:** Composer DX Phase 4 (MIDI velocity from dynamics) — start by reading `TransformFunctions.cs` dynamics implementations.

---

### Pitfall 9: Euclidean Swing/Humanize — Determinism & Seed Handling

**What goes wrong:**
`BuiltInFunctions.cs:1016` registers `euclidean(hits, steps, note)` as deterministic (Bjorklund algorithm, no randomness). Adding swing (delay even hits by a percent of a beat) and humanize (jitter timing and velocity by a small random amount) introduces non-determinism. Pitfalls:

1. **Humanize without seed → non-reproducible compositions.** User renders a track today, shares the .flow file, runs it tomorrow — different humanization. This violates Flow's "code is the score" philosophy.
2. **Seed source ambiguity:** The existing `(??  C4 E4 G4)` random-choice syntax uses seeded random. Does `humanize(0.1)` use the same seed stream, a new one, or a parameter? Different choices produce different compositions for the same code.
3. **Swing overlap with existing `swing` context block:** Flow already has `swing 0.6 { ... }` as a musical context block. `euclidean(3, 8, C4, swing: 0.6)` could mean the same thing or something different (euclidean-local swing vs. section-level swing). Users will expect both to work identically. Producing different audio for "same-named" parameters is a trust-breaking bug.
4. **Taste:** "Humanize by 10%" is meaningless without units. 10% of a beat? 10% of the note duration? 10% in milliseconds? Each is defensible; users will expect whatever their previous tool used (DAW users: usually ms; tracker users: usually ticks).
5. **Determinism across platforms:** `Random(seed)` in .NET is documented as NOT stable across .NET versions. A seed that produces output X on .NET 9.0.1 may produce X' on .NET 9.0.2. For reproducible rendering, implement a pinned PRNG (xorshift, splitmix64) explicitly.

**Why it happens:**
Adding randomness "just" uses `Random`. The discipline of "seeded, pinned, documented" is separate work that often gets skipped because the feature looks done once it produces varied output.

**Prevention:**
1. **Default to the active musical-context swing** when `euclidean` sees no explicit swing parameter. No overlap — one source of truth (the context block).
2. **Require explicit seed for humanize:** `euclidean(3, 8, C4, humanize: 0.1, seed: 42)`. If no seed given, use 0 (fully reproducible) and document that "no seed = no randomness across runs."
3. **Use a pinned PRNG** (copy a 20-line xorshift64* into the codebase; do NOT use `System.Random`).
4. **Document units explicitly:** "humanize: fraction of a sixteenth note timing jitter, ±velocity jitter 0-0.1." Match a commonly-understood reference (DAWs typically use "Humanize %" as % of step duration).
5. **Match the existing swing semantics exactly:** read `MusicalContext.Swing` usage in `NoteStreamCompiler`, apply the same formula (typically: delay even-numbered 8ths by swing * 50% of an 8th).
6. **Reproducibility test:** Same .flow file rendered twice = byte-identical MIDI and byte-identical WAV when seed is specified.

**Warning signs:**
- `new Random()` appears anywhere in the new code — use pinned PRNG instead.
- No seed parameter on humanize — non-reproducible by default, violates DSL philosophy.
- Swing audio differs between `swing 0.6 { euclidean(3,8,C4) }` and `euclidean(3,8,C4, swing: 0.6)` — same name should mean the same thing.

**Phase to address:** Composer DX Phase 5 (euclidean swing/humanize) — specify seed/PRNG/units BEFORE writing code.

---

### Pitfall 10: Retroactive Nyquist Validation — Confirmation Bias

**What goes wrong:**
Retroactively adding validation tests for v1.1 phases 6-9 has a fundamental problem: the code already exists and (mostly) works, so tests written to "validate" it will unconsciously be shaped to pass. This is the textbook definition of confirmation bias in testing.

Specific failure modes:
1. **Tests that duplicate the implementation logic:** "Does `tempoRamp(seq, 60, 120)` produce the right BPM midway?" If the test uses the same midpoint-averaging the implementation uses (Key Decision: "Bar-midpoint BPM interpolation"), the test is a tautology.
2. **Missing edge cases because the dev already knows which ones were designed for:** phases 6-9 tests will cover the paths the dev-at-v1.1-close thought about, missing the ones they didn't (e.g., tempo ramps that span multiple sections, mix() with 3+ buffers, REPL auto-import precedence).
3. **"Feature is documented, therefore tested" fallacy:** The tutorial showing `writeWav` doesn't mean `writeWav` with bad paths / read-only filesystems / non-44100 sample rates was ever exercised.
4. **Tests that pass today but don't pin behavior:** "Does sing() produce a non-empty buffer?" passes if the buffer has 1 sample at 0.0. Tests must pin **specific observable behavior**, not existence.

**Why it happens:**
Retroactive validation is boring work and feels like it's "catching up" rather than "moving forward." The bar is often set at "tests exist" rather than "tests would have caught the bugs that would have shipped."

**Prevention:**
1. **Write tests against the requirements doc, not the code.** Open `.planning/milestones/v1.1-*.md`, read the requirement as stated, write the test before reading the implementation. If the test disagrees with the implementation, investigate which is right.
2. **Grep for features with code-but-no-test:** `PolyrhythmFunctions.cs`, `createADSR`, `createAR`, `applyEnvelope`, `LiveReloadManager`. Each of these is a retroactive-validation gap identified by the audit.
3. **Test the error paths.** v1.1 added "honest error reporting" — pin specific error messages, not just "some error was reported." Test cases where errors SHOULDN'T be reported (false positives) and where they MUST (false negatives).
4. **Use golden-file testing** for audio: render v1.1 example + pin the WAV byte-hash. Any semantic change to renderers (including C1/C2/C3/C4/C5 fixes) will change the hash — that's information.
5. **One reviewer for retroactive tests must NOT be the author of the v1.1 feature.** Different eyes catch different holes.
6. **Nyquist-specific:** Sample rates below 2× the highest note frequency produce aliasing. For Flow's default 44100 Hz, any note above ~22kHz aliases. Test that high notes (C8 = 4186 Hz fundamental, but harmonics of piano synth go to 20kHz+) don't produce visible aliasing. If they do, document the limitation.

**Warning signs:**
- Every retroactive test passes on first run — suspicious; retroactive testing should find at least one bug.
- Tests all assert "buffer is not null" / "no exception thrown" rather than specific values.
- Test author and feature author are the same person.

**Phase to address:** Retroactive Validation Phase (near end of v1.2, after Stability phase fixes the bugs being validated against).

---

## Technical Debt Patterns

Shortcuts that will tempt the implementer. Categorized by long-term cost.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Change `return` to `throw` in `ExecuteMusicalContext` | One-line C1 fix | Violates soft-failure model; regressions in REPL; breaks tests that expect accumulation | **Never** in this codebase |
| Remove the `_returnValue != null` check entirely | "Fixes" C2 in isolation | Breaks all proc return semantics; breaks implicit returns | **Never** |
| `Math.Max(1, frames)` envelope fix | C3/C4 stop crashing | Zero-length segments produce 1-sample ramps; click at low sample rates; hidden sample-rate dependence | Only as last-resort crash guard with TODO + regression test |
| Swap `augment`/`diminish` without release-note comms | 1-line commit | Silent breaking change to user compositions; MIDI byte-level diffs; trust loss | **Never** — always bundle with migration docs |
| Lex `H` as a note globally | Enharmonic helper "just works" | Breaks every user variable named H, H1, H2, etc. | **Never** — always scope to note-stream contexts |
| Use `System.Random` for humanize | Feature ships sooner | Non-reproducible across .NET patch versions; violates DSL reproducibility; seed has undefined semantics | Only if reproducibility is explicitly dropped as a requirement (it shouldn't be) |
| Skip tests for code you changed "just to unblock CI" | Get the red bar green | Unverified regressions compound; each audit reveals more | Only with an explicit follow-up issue filed BEFORE merge |
| Register both `augment` and `augmentV1` as permanent aliases | Backwards-compat forever | Two correct-sounding names forever; documentation rot; new users confused | Until v1.3+, then remove |
| Fix C1, C2, C3, C4, C5, C6, C7 in one commit | Fewer commits to review | Confounded regressions; no bisection possible; reviewer can't track blast radius per fix | **Never** — one commit per critical fix |

## Integration Gotchas

Flow doesn't integrate with external services much (Linux PulseAudio + optional TTS). Gotchas are internal.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| `MusicalContext` stack | Reading `Tempo` directly from `CurrentFrame.MusicalContext` instead of `GetMusicalContext()` (which walks the stack) | Always use `_context.GetMusicalContext()` for reads; only mutate `CurrentFrame.MusicalContext` on writes |
| `ErrorReporter` | Calling `ReportError` and continuing into code that assumes success | Return early or guard every subsequent code path with `if (_errorReporter.HasErrors) return;` |
| `Thunk.Force` under soft-failure | Forcing a thunk whose first evaluation threw; re-force re-evaluates | Cache exception or value; never re-evaluate after first force |
| MIDI export vs WAV rendering | Assuming both pipelines use the same velocity computation | Verify by running the same sequence through both and comparing: WAV peak amplitude should correlate with MIDI velocity |
| Lexer keyword additions | Forgetting that existing user scripts may use the identifier | Grep `examples/`, `tests/`, stdlib `.flow` files before adding any new keyword |
| Note-literal lexing | Assuming A-G+digit uniquely identifies a note (H1 counter-example) | Apply new note aliases ONLY inside `| ... |` stream context |
| `PushFrame`/`PopFrame` balance | Early `return` inside a `try` with `PopFrame` in `finally` — balanced, but body was skipped | Audit intent: should block body execute with defaults? If yes, use `continue-with-defaults` pattern, not `return` |

## Performance Traps

Flow is a tool for offline composition + real-time playback. Performance concerns are narrow.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Per-sample user-function callback for custom oscillator | CPU pegged; audio dropouts; `test_custom_oscillator.flow` slow even with small sequences | Support per-block callbacks (N samples at a time) as an opt-in mode; document the tradeoff | Custom oscillators at sample rates ≥ 44100 Hz on sequences longer than a few seconds |
| Comb filter denormal accumulation (Reverb) | CPU pin after long silence; `play` followed by silence exhibits it | Flush denormals with `x < 1e-15 ? 0 : x` in feedback paths | Any reverb with feedback > 0.5 during silence gaps |
| Filter bandpass with tight Q | Audible ringing, numerical instability | Clamp Q ≤ 50 in `Filter.cs` bandpass derivation | Bandpass with bandwidth < centerHz/50 |
| Humanize with `System.Random` in a hot loop | Unpredictable GC pauses from random seed expansion | Pinned PRNG with stack-allocated state | Not really a perf issue for reasonable sequence sizes; more of a correctness one |

## Security Mistakes

Flow is a local CLI tool. Security surface is small. The two concerns:

| Mistake | Risk | Prevention |
|---------|------|------------|
| `writeWav` / MIDI export writing to arbitrary paths | User scripts overwriting system files when run from untrusted .flow files | Document that Flow does NOT sandbox file I/O; users should not `dotnet run` untrusted scripts (matches Python, Node, Ruby norm) |
| External TTS process invocation | Shell injection via synthesized text | Verified in v1.1 as using `ProcessStartInfo` with `UseShellExecute=false` — preserve this invariant |

## UX Pitfalls

Composer DX is the whole second half of v1.2. These matter.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Breaking change (`augment`/`diminish`) without release notes | User's existing compositions sound wrong; no clear migration path | Top-level BREAKING CHANGE section in release notes; migration example; both-names-work overlap period |
| Silent behavior change in MIDI velocity | User MIDI tests regress byte-level; hard to bisect | Pin the sampling policy in docs; include a "diff from v1.1" snippet in release notes |
| Tutorial that doesn't exercise v1.1 features (math stdlib, mix, presets) | New users don't discover features; features atrophy unused | Tutorial refresh must include a runnable example per v1.1+v1.2 feature; measure: "tutorial mentions feature X" for each V1.1 requirement |
| Error message that says "token error" without line context | User can't locate the bug; gives up | Every `ReportError` call site must pass `ctx.Location` or equivalent; `--verbose` should show full token context |
| `reverbTime { }` accepts negative or zero values silently | Silent DSP numerical instability | Validation with specific error message (mirror `tempo` / `pan` / `gain` validation patterns already in `ExecuteMusicalContext`) |

## "Looks Done But Isn't" Checklist

Verification checklist for the v1.2 milestone. Each item is a place "it works" is easy to claim prematurely.

- [ ] **C1 (context frame fix):** Verify block body still executes after error with correct defaults — not that the stack is balanced (which it already is).
- [ ] **C2 (statement short-circuit):** Verify tests with explicit `return` in procs still work, AND error accumulation continues across top-level statements.
- [ ] **C5 (augment/diminish swap):** Verify release notes mention the break; all `examples/*.flow` audited; a migration alias (`augmentV1`, etc.) is registered.
- [ ] **C7 (Thunk exception):** Verify `lazy` with a side-effecting expression that throws is not re-executed on second `Force()`.
- [ ] **`range(Int, Int)` implementation:** Verify `range(5, 10)` returns `[5,6,7,8,9]` or `[5,6,7,8,9,10]` — pick one, document it, test both endpoints.
- [ ] **`break` / `continue` implementation:** Verify `test_while_loop.flow:37-54` passes; nested loops break only the innermost (look at existing `BreakSignal` / `ContinueSignal` in `Interpreter.cs:17-22` — the exception-based approach is the designed pattern).
- [ ] **`bpm()` / `createStereoTrack` / `renderBars`:** Verify `test_full_song.flow` passes; if removed from test instead of implemented, the decision is documented.
- [ ] **`reverbTime` context block:** Verify no existing identifier collision; validation rejects negative/zero; nested inside other context blocks works (mirrors `pan`/`gain`).
- [ ] **Enharmonic helpers (`H`, `Db↔C#`):** Verify `Int H = 5; section test { | H4 | }` both parse correctly and don't conflict.
- [ ] **MIDI velocity from dynamics:** Verify transforms write to `note.Velocity`; MIDI export just reads it; no byte-level change for dynamics-free sequences.
- [ ] **Euclidean swing/humanize:** Verify same seed produces byte-identical output across two runs; humanize without seed is fully deterministic (0.0 jitter).
- [ ] **Retroactive Nyquist validation:** Verify each v1.1 phase (6-9) has at least one test that fails if its feature is removed — not just a smoke test.
- [ ] **Tutorial refresh:** Verify every v1.1 Validated requirement appears in at least one tutorial snippet.

## Recovery Strategies

When pitfalls ship despite prevention.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| C1 fix skipped block body silently | MEDIUM | 1. Add a test that exposes the skip. 2. Change early-return sites to set defaults + continue to body. 3. Re-release as patch (v1.2.1). |
| C5 semantic swap shipped without comms | HIGH | 1. Urgent release-note amendment + blog post. 2. Add `augmentV1`/`diminishV1` aliases with deprecation warning in next patch. 3. Offer migration script that rewrites .flow files. |
| Thunk re-evaluation causes duplicate side effects | LOW | 1. Add exception-caching pattern (Pitfall 4 prevention). 2. Patch release. 3. Document in release notes that lazy expressions with side effects now cache exceptions. |
| `reverbTime` keyword breaks a user script | MEDIUM | 1. Accept identifier form as a grammar fallback in the parser. 2. Emit a warning on first use. 3. Plan context-sensitive lexing for v1.3. |
| `H` note breaks user variable | HIGH | 1. Immediately scope aliases to note-stream context (the correct fix). 2. Patch release. 3. Document the v1.2→v1.2.1 narrowing. |
| MIDI velocity changes broke a user's MIDI pipeline | LOW | 1. Document the new velocity policy. 2. Provide opt-out flag (`--legacy-midi-velocity`) if demand is high. |
| Euclidean humanize non-reproducible | MEDIUM | 1. Add explicit seed parameter (default 0 = no jitter). 2. Switch to pinned PRNG. 3. Patch release with reproducibility note. |
| Retroactive tests miss a real regression | LOW | 1. Add the test that would have caught it. 2. Continue — retroactive testing is asymptotic. |

## Pitfall-to-Phase Mapping

How v1.2 roadmap phases should address these pitfalls. **Ordering matters** — some fixes confound others if bundled.

| Pitfall | Prevention Phase | Verification | Ordering Constraint |
|---------|------------------|--------------|---------------------|
| P1: C1 context frame / skipped body | Stability Phase 1a | Block body still executes with defaults after error | Must ship BEFORE Composer DX to avoid confounded regressions; commit BEFORE `reverbTime` |
| P2: C2 statement short-circuit | Stability Phase 1a (same commit as P1) | Proc returns still work; error accumulation works across top-level | Paired with P1 |
| P3: C5 augment/diminish swap | Stability Phase 1b (separate commit) | Numeric duration pinned; migration aliases live; release notes updated | Independent of P1/P2; **must ship with comms artifacts** |
| P4: C7 Thunk exception | Stability Phase 1c | Failing `lazy` doesn't re-execute side effects | Independent; can parallelize with P3 |
| P5: C3/C4 envelope/fade div-by-zero | Stability Phase 1d | Zero-length segments produce correct curves, not 1-frame clamps | Independent |
| P6: reverbTime keyword collision | Composer DX Phase 2 | No identifier collision in stdlib/examples/tests | Depends on stability fixes (don't confound test regressions) |
| P7: Enharmonic `H` / `Db` collision | Composer DX Phase 3 | `Int H = 5` still compiles; `\| H4 \|` renders as B4 | Note-stream scoping from day 1 |
| P8: MIDI velocity sampling | Composer DX Phase 4 | MIDI bytes identical for non-dynamics; clean velocity propagation for dynamics | Verify existing transform behavior FIRST |
| P9: Euclidean swing/humanize determinism | Composer DX Phase 5 | Same seed → byte-identical output across runs | Pinned PRNG + seed contract specified before coding |
| P10: Retroactive Nyquist confirmation bias | Validation Phase (end of milestone) | At least one test per v1.1 phase fails if feature is removed | **Different person than v1.1 author** writes tests where feasible |

## Communication & Migration Notes (C5-specific)

The `augment`/`diminish` swap deserves dedicated communication planning because it's the only **semantic-change** bug in the critical list (all others are fixes for objectively-wrong behavior).

**Required artifacts shipping with the fix commit:**
1. **Release notes section (top of v1.2 notes):**
   ```
   BREAKING CHANGE: `augment` and `diminish` semantics corrected
   
   Prior to v1.2, `augment(seq)` halved note durations (quarter→eighth)
   and `diminish(seq)` doubled them — inverted from standard musical
   meaning. As of v1.2:
   
   - `augment(seq)` now lengthens durations (quarter → half)
   - `diminish(seq)` now shortens durations (quarter → eighth)
   
   If your code depended on v1.1 behavior, use the transitional aliases
   `augmentV1` / `diminishV1` (deprecated, will be removed in v1.3).
   ```
2. **Tutorial update:** Every mention of augment/diminish must be audited. The fix lands with tutorial changes in the same commit.
3. **Migration script** (optional, high-value): A .sh or .flow script that rewrites `.flow` files, replacing `augment` → `augmentV1` and `diminish` → `diminishV1` to preserve old behavior.
4. **Example audit:** Every `examples/*.flow` file is audited and updated. For each change, a comment notes whether the original intent was "lengthen" (now `augment`) or "shorten" (now `diminish`).
5. **One-line startup warning** (optional): If `FLOW_WARN_V1_2_TRANSFORMS=1` env var, print on first use: "Note: augment/diminish semantics changed in v1.2 from v1.1. See release notes."

**Communication rule for v1.2:** No Critical bug fix ships without determining whether it's user-visible. For bugs that ARE user-visible (C5 definitively; C1, C2 possibly depending on tests), release notes MUST explicitly describe the change.

## Sources

- Codebase inspection (verified file:line): `Interpreter/Interpreter.cs:73-128`, `Interpreter/Interpreter.cs:130-290`, `Runtime/Thunk.cs:27-46`, `Runtime/ExecutionContext.cs:186-212`, `StandardLibrary/Transforms/TransformFunctions.cs:239-279`, `StandardLibrary/Audio/EnvelopeProcessor.cs:95-172`, `StandardLibrary/Audio/MidiExport.cs:180-210`, `StandardLibrary/BuiltInFunctions.cs:1011-1075`, `Lexing/SimpleLexer.cs:540-610`
- Audit: `.planning/CODEBASE-AUDIT-2026-04-18.md` sections 1-3 (Critical/Major/Minor bugs)
- Project decisions: `PROJECT.md` Key Decisions (soft-failure error model, scoped musical context, hand-written parser)
- Developer conventions: `CLAUDE.md` (error accumulation model, musical context scoping, built-in registration pattern)
- Language versioning references: Python PEP-11 deprecation policy, Rust RFC-1105 (breaking change communication), Haskell `base` library CHANGELOG conventions

---
*Pitfalls research for: Flow language v1.2 Stability & Composer DX*
*Researched: 2026-04-18*
*Downstream consumer: roadmap phase planning — use Pitfall-to-Phase Mapping and ordering constraints to structure phases*
