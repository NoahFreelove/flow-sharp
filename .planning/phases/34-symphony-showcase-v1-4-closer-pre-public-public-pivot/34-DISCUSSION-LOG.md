# Phase 34: Symphony Showcase (v1.4 closer) — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered + flags which auto-selected decisions the composer may want to review.

**Date:** 2026-05-16
**Phase:** 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
**Mode:** `/gsd-discuss-phase 34 --auto --chain` — fully autonomous; Claude selected the recommended option for every question without an interactive prompt.
**Areas auto-selected:** Symphony scope (length + shape + mood), Instrumentation, Flow features showcased, Mix + post-processing, File layout + commit strategy, README + docs updates, Regression test strategy, Composer UAT + iteration, Plan shape.

---

## Symphony Scope: Length + Shape + Mood

### Q1 — Target rendered duration?

| Option | Selected |
|--------|----------|
| ≈ 30 s — short proof-of-concept | |
| ≈ 60 s — midpoint of 30-90s window (recommended) | ✓ |
| ≈ 90 s — full window | |

**[auto] Selected: ≈ 60 s.** Reason: midpoint of criterion-#1 window; short enough to listen twice without losing attention, long enough to develop a theme. Planner may flex ±15 s during composition. Tracked as D-101.

### Q2 — Movement structure?

| Option | Selected |
|--------|----------|
| Single-movement through-composed | |
| Single-movement ABA (recommended) | ✓ |
| Multi-movement (e.g. ABCBA) | |

**[auto] Selected: Single-movement ABA.** Reason: one coherent arc; no multi-movement complexity that bloats past 90s. Tracked as D-102.

### Q3 — Genre / mood?

| Option | Selected |
|--------|----------|
| Neo-classical minimalist / film-score (recommended) | ✓ |
| Baroque pastiche | |
| Modern dissonant / 12-tone | |
| Romantic-era expressive | |

**[auto] Selected: Neo-classical minimalist / film-score.** Reason: broadly accessible to non-classical listeners; naturally shows off VSCO-CE timbres; doesn't require deep music-theory knowledge to appreciate. Tracked as D-103.

### Q4 — Tempo + key default?

| Option | Selected |
|--------|----------|
| 100 BPM / D minor / 4/4 (recommended) | ✓ |
| 120 BPM / A minor / 4/4 | |
| 80 BPM / C minor / 4/4 | |

**[auto] Selected: 100 BPM / D minor / 4/4.** Reason: sits comfortably under the timpani; gives woodwind lead room to breathe; D minor is a classical orchestral favorite that scales cleanly across the 5 VSCO-CE Sus patches. Planner may pick a different key if a sketch lands cleaner — recommendation, not hard lock. Tracked as D-104.

---

## Instrumentation: Which VSCO-CE Patches

### Q5 — How many instruments?

| Option | Selected |
|--------|----------|
| 3 (minimal) | |
| 4 (balanced) | |
| 5 (recommended — upper-third of 3-6 window) | ✓ |
| 6 (max in spec) | |

**[auto] Selected: 5 instruments.** Reason: enough to feel orchestral; few enough that each line is intelligible in the mix. Tracked as D-201.

### Q6 — Which 5 patches from the 19-symbol GM dict?

| Option | Selected |
|--------|----------|
| Strings-heavy: #violin + #viola + #cello + #flute + #horn | |
| Mixed (recommended): #violin + #cello + #flute + #horn + #timpani | ✓ |
| Brass-forward: #trumpet + #horn + #trombone + #tuba + #timpani | |
| Wind ensemble: #flute + #oboe + #clarinet + #bassoon + #horn | |

**[auto] Selected: Mixed — #violin (solo) + #cello (ensemble) + #flute + #horn + #timpani.** Reason: covers strings + woodwinds + brass + percussion in one piece; intentionally skips piano (Phase 29 territory) and the 4 TBD VSCO-CE-missing symbols. Per 33-VSCO-PATH-AUDIT: violin = `SViolinVib.sfz`, cello = `CelloEnsSusVib.sfz`, flute = `FluteSusVib.sfz`, horn = `FHornSus.sfz`, timpani = `Timpani.sfz` — all 5 verified rows. Tracked as D-202.

### Q7 — Default role assignment?

| Option | Selected |
|--------|----------|
| Flute melody, violin doubles octave-up in A', cello sustained bass, horn pads in B, timpani transition accents (recommended) | ✓ |
| Violin lead throughout, others accompany | |
| Rotating melody across instruments | |

**[auto] Selected: Flute melody, violin doubles octave-up in A', cello sustained bass, horn pads in B, timpani transition accents.** Reason: gives every instrument distinct musical role; the octave-up violin in A' is the natural Phase 28 `transpose` transform showcase; planner adjusts during composition iteration. Tracked as D-203.

---

## Flow Features Showcased in Source

### Q8 — Criterion-#3 feature coverage?

| Option | Selected |
|--------|----------|
| Every criterion-#3 item explicitly demonstrated (recommended) | ✓ |
| Subset — pick best 2-3 | |

**[auto] Selected: Every criterion-#3 item.** Reason: criterion #3 is part of the ROADMAP success contract — full coverage is the safest path to UAT sign-off. Mandatory list: musical-context blocks, note streams, transform (transpose/invert/retrograde/repeat), sampler:NAME dispatch, every Phase 28 articulation mark (`>`, `stacc`, `ten`, `leg`, `marc`), at least one voice block. Tracked as D-301.

### Q9 — Include microtonal / Scala tuning?

| Option | Selected |
|--------|----------|
| Skip microtonal — film-score sound (recommended) | ✓ |
| Activate `enable justIntonation;` for color | |
| Use Scala `.scl` via `tuning t { ... }` block | |

**[auto] Selected: Skip microtonal.** Reason: out of place in a film-score / neo-classical showcase; `examples/scala/intro.flow` (shipped Phase 32) is the canonical microtonal demo. The symphony's job is "musical first, feature-showcase second" — explicit microtonal pragmas distract from the mood. Tracked as D-302.

### Q10 — Include tuplets?

| Option | Selected |
|--------|----------|
| One tuplet bracket in the woodwind line (recommended) | ✓ |
| Multiple tuplets scattered through the piece | |
| Skip tuplets entirely | |

**[auto] Selected: One tuplet bracket — triplet flourish on the flute in section B.** Reason: earns its showcase slot without forcing the genre off-target; `{3:2 ...}q` is the most musically natural tuplet for film-score writing. Tracked as D-303.

### Q11 — Include humanize?

| Option | Selected |
|--------|----------|
| `humanizeGaussian(seq, 0.05, seed)` on the cello (recommended) | ✓ |
| Apply humanize on all sequences | |
| Skip humanize | |

**[auto] Selected: humanizeGaussian on the cello with 0.05 amount + fixed seed.** Reason: orchestral strings benefit audibly from micro-timing humanization; cello is the most rhythmically exposed sustained line; fixed seed preserves byte-identical reruns. Tracked as D-304.

---

## Mix and Post-Processing

### Q12 — Per-instrument balancing?

| Option | Selected |
|--------|----------|
| `volume(buf, linear)` per instrument (recommended) | ✓ |
| `gain(buf, dB)` per instrument | |
| Flat sum, no per-instrument balance | |

**[auto] Selected: `volume(buf, linear)` per instrument.** Reason: per-instrument levels are intuitively linear ("flute is 85% of full"), not dB; aligns with Phase 26.2 ERG-03 composer-ergonomic surface. Default starting balance: flute 0.85, violin 1.0, cello 0.75, horn 0.65, timpani 0.40 — adjusted during UAT iteration. Tracked as D-401.

### Q13 — Master reverb?

| Option | Selected |
|--------|----------|
| Second-decay reverb `(reverb mix 0.3 2.5s)` (recommended) | ✓ |
| ms-decay reverb | |
| No master reverb (rely on per-section reverb only) | |

**[auto] Selected: Second-decay reverb on summed mix — 30% wet, 2.5s tail.** Reason: generous concert-hall feel matches film-score mood; Phase 26.2 Second-decay literal demonstrates the ergonomic surface in the showcase. Sits ON TOP of per-section reverb already inside `renderSong`; planner verifies combined wet stack isn't muddy during UAT. Tracked as D-402.

### Q14 — Master compression?

| Option | Selected |
|--------|----------|
| Soft glue: 4:1 above -12dB, 100ms/200ms (recommended) | ✓ |
| Aggressive limiting | |
| No master compression | |

**[auto] Selected: Soft 4:1 compressor with 100ms attack / 200ms release.** Reason: gentle glue, NOT a brick-wall limiter; preserves dynamic range. Tracked as D-403.

### Q15 — Stereo panning?

| Option | Selected |
|--------|----------|
| Skip pan — mono output, defer to v1.5 mix-polish (recommended) | ✓ |
| Per-instrument hand-pan via dual-render | |
| Wait for SfzRenderer stereo retrofit | |

**[auto] Selected: Skip pan — mono output.** Reason: SfzRenderer's stereo behavior at v1.4 is sum-only; adding per-instrument pan would require either a Phase 33.x SfzRenderer stereo retrofit or hand-stereo via dual-render-and-pan. Out of scope for v1.4 closer. Captured as deferred idea for v1.5 mix-polish phase. Tracked as D-404.

---

## File Layout and Commit Strategy

### Q16 — Symphony source location?

| Option | Selected |
|--------|----------|
| `examples/symphony/symphony.flow` (sibling to sfz_smoke.flow) (recommended) | ✓ |
| `examples/showcase-symphony.flow` (top-level) | |
| `examples/symphony/movement_1.flow` (versioned filename) | |

**[auto] Selected: `examples/symphony/symphony.flow` — sibling to existing `sfz_smoke.flow`.** Reason: keeps SFZ-related composer surface in one directory; `sfz_smoke.flow` remains the SFZ tutorial chapter, `symphony.flow` becomes the headline piece. Single directory holds both files + the shared README. Tracked as D-501.

### Q17 — Commit the rendered WAV/MP3 to the repo?

| Option | Selected |
|--------|----------|
| No — release-asset only (recommended) | ✓ |
| Yes — commit a downsampled / lossy version | |
| Yes — commit canonical WAV | |

**[auto] Selected: No — rendered output as GitHub Release asset only.** Reason: ~10 MB WAV is wasteful for the repo; rendered output requires VSCO-CE installed at `sfz_root` which is not a fresh-clone-deterministic input — committing an artifact that can't be reproduced from the repo alone misleads future agents. Tracked as D-502.

### Q18 — How to ship the rendered audio?

| Option | Selected |
|--------|----------|
| GitHub Release asset (MP3 + WAV on v1.4.0 tag) (recommended) | ✓ |
| Issue attachment + raw URL embed | |
| External CDN / S3 upload | |

**[auto] Selected: GitHub Release asset.** Reason: ties the audio to the version tag; `gh release upload` is the canonical asset-attach mechanism. MP3 ≈ 1.5 MB at 192 kbps + WAV ≈ 10 MB for archival. Tracked as D-503.

### Q19 — Local-render output filenames?

| Option | Selected |
|--------|----------|
| `examples/output/symphony.{wav,mid}` (Phase 27 D-404 pattern) (recommended) | ✓ |
| `examples/output/flow_symphony.{wav,mid}` | |
| `examples/symphony/symphony.{wav,mid}` (sibling to source) | |

**[auto] Selected: `examples/output/symphony.{wav,mid}`.** Reason: same directory as existing `flow_tutorial.{wav,mid}` / `flow_showcase.{wav,mid}`; already covered by existing `.gitignore`. Tracked as D-504.

---

## README + Documentation Updates

### Q20 — Top-level README showcase section?

| Option | Selected |
|--------|----------|
| New "## Showcase" section after "What is flow-lang?" before "Install" (recommended) | ✓ |
| Append showcase to end of README | |
| Replace existing intro with showcase | |

**[auto] Selected: New "## Showcase" section between intro and Install.** Reason: positions playable audio as the first interaction for new users; install instructions follow naturally. Tracked as D-601.

### Q21 — examples/symphony/README.md update?

| Option | Selected |
|--------|----------|
| Expand existing README to cover BOTH files (recommended) | ✓ |
| Create separate `symphony.README.md` | |
| Leave smoke-fixture README alone, add inline comments in symphony.flow | |

**[auto] Selected: Expand existing README.** Reason: single source of truth for the `examples/symphony/` directory; tutorial chapter sits below the headline piece (composers learn the surface on the 4-bar fixture, then tackle the symphony). Tracked as D-602.

### Q22 — Public announcement draft location?

| Option | Selected |
|--------|----------|
| `docs/announcements/v1.4.0.md` (new directory under existing docs) (recommended) | ✓ |
| `.planning/announcements/v1.4.0.md` | |
| Top-level `ANNOUNCEMENT.md` | |

**[auto] Selected: `docs/announcements/v1.4.0.md`.** Reason: under existing `docs/` directory; future v1.X announcements land alongside; one source for the composer to adapt per platform. Tracked as D-603.

---

## Regression Test Strategy

### Q23 — CI test for the symphony render?

| Option | Selected |
|--------|----------|
| No CI test — rely on Phase 33 synthetic fixture for SFZ coverage (recommended) | ✓ |
| Add CI test that runs only when VSCO-CE is detected | |
| Add CI test against a synthetic mini-VSCO-CE | |

**[auto] Selected: No CI test.** Reason: symphony render requires VSCO-CE which is not in CI; Phase 33 synthetic SFZ smoke fixture already exercises SfzParser + SfzRenderer + SfzSampleCache + crossfade paths on CI. Tracked as D-701.

### Q24 — Two-run determinism verification?

| Option | Selected |
|--------|----------|
| Manual at release time, documented in README (recommended) | ✓ |
| Automated as a release-script step | |
| Skip determinism check for symphony | |

**[auto] Selected: Manual at release time, documented in `examples/symphony/README.md`.** Reason: composer runs render twice + cmp; confirms Phase 28 "two-run cmp-clean" contract holds end-to-end on the real library. Release-time activity, not CI-time. Tracked as D-702.

### Q25 — RMS-windowed baseline for the symphony WAV?

| Option | Selected |
|--------|----------|
| No RMS baseline (recommended) | ✓ |
| Generate baseline + run on every commit | |
| Generate baseline + skip on CI | |

**[auto] Selected: No RMS baseline.** Reason: symphony WAV depends on VSCO-CE samples not in CI; RMS baseline would either need the library checked into CI (rejected per Phase 33 SPEC-2 + repo size) or be skipped on CI (worse than no test — false signal of coverage). Tracked as D-703.

---

## Composer UAT and Iteration

### Q26 — UAT iteration model?

| Option | Selected |
|--------|----------|
| Iterative, no cap, until composer signs off (recommended) | ✓ |
| Single-shot UAT, accept first render | |
| 3-iteration cap | |

**[auto] Selected: Iterative, no cap.** Reason: this is the headline artifact; quality matters more than schedule. Phase doesn't close until composer signs off. Tracked as D-801.

### Q27 — Sign-off criteria?

| Option | Selected |
|--------|----------|
| 3 conditions: subjective + articulation A/B + polyphony intelligibility (recommended) | ✓ |
| 1 condition: subjective only | |
| 5+ conditions including measurable mix-bus metrics | |

**[auto] Selected: 3 conditions.** Reason: (1) "I would publicly share this" matches ROADMAP success criterion #2 verbatim; (2) Phase 28 articulation differentiation must be audible; (3) Phase 28 polyphony intelligibility must be audible. Strikes balance between rigor + composer ergonomics. Tracked as D-802.

### Q28 — Sign-off evidence shape?

| Option | Selected |
|--------|----------|
| `34-HUMAN-UAT.md` file in phase directory (Phase 17 / 33 pattern) (recommended) | ✓ |
| Inline in commit message | |
| External shared doc | |

**[auto] Selected: 34-HUMAN-UAT.md.** Reason: mirrors Phase 17 + Phase 33 HUMAN-UAT pattern; read by planner during 34-VERIFICATION. Tracked as D-803.

---

## Plan Shape

### Q29 — How many plans?

| Option | Selected |
|--------|----------|
| 6 plans — composition / source-commit / README / announcement / release / closure (recommended) | ✓ |
| 3 plans — bundled composition + ship + close | |
| 8+ plans — finer-grained split | |

**[auto] Selected: 6 plans.** Reason: each plan does one coherent thing; 34-01 (the long pole) is isolated from the rest so its UAT iteration doesn't block downstream doc work. Tracked as D-901.

### Q30 — Split composition from source commit?

| Option | Selected |
|--------|----------|
| Yes — 34-01 = iteration loop, 34-02 = final commit (recommended) | ✓ |
| No — single plan does both | |

**[auto] Selected: Yes — split.** Reason: keeps the long composition-iteration loop's working commits separate from the final ship commit; cleaner git history; easier to revert the source if needed without unwinding README work. Tracked as D-902.

### Q31 — Release tag before or after milestone closure docs?

| Option | Selected |
|--------|----------|
| Tag first (34-05), closure docs after (34-06) (recommended) | ✓ |
| Closure docs first, tag last | |

**[auto] Selected: Tag first.** Reason: closure docs reference the release URL — 34-05 must land before 34-06 can write the canonical "v1.4 shipped" pointer. Tracked as D-903.

---

## Open for composer review

Auto-selected decisions the composer may want to override BEFORE plan-phase begins. Edit `34-CONTEXT.md` directly and re-run `/gsd-plan-phase 34` to pick up the change.

- **D-101 (length ≈ 60s):** Composer may want 30s for "social-clip-friendly" or 90s for "full demo" instead. Affects D-102 ABA balance.
- **D-104 (D minor / 100 BPM):** Composer may prefer a different key or tempo. D minor is recommendation, not lock.
- **D-202 (5 instruments + which 5):** Composer may swap one out (e.g. add `#trumpet` instead of `#timpani` for brass-forward mood). 4 TBD VSCO-CE-missing symbols stay off-limits unless composer wants to source SFZ files manually.
- **D-302 (skip microtonal):** Composer may want a Scala-tuning section as a "this is what v1.4's other capabilities can do" segue — currently skipped to keep the film-score mood pure.
- **D-401 default volume balance:** Starting point only; iterated to taste during 34-01 UAT.
- **D-503 (MP3 + WAV release assets):** Composer may prefer FLAC instead of WAV for archival (better compression, lossless).
- **D-603 (announcement file location):** Composer may prefer `.planning/announcements/` to keep all planning artifacts together vs `docs/announcements/` to expose them publicly.
- **Symphony title `"In Five Voices"`:** Working title; composer renames during 34-01 if a better one emerges.

---

## Deferred for v1.5+

Captured during analysis but explicitly out of scope for Phase 34 (full list in CONTEXT.md § Deferred Ideas):

- Stereo panning across instruments — v1.5 mix-polish phase.
- A second showcase (jazz / EDM / etc.) — v1.5+ genre-coverage phase.
- Auto-posting infrastructure for announcements — v1.5+ if friction surfaces.
- CHANGELOG.md file — v1.5+ if a contributor asks.
- CI integration for symphony render against mini-VSCO-CE — v1.5+ when contributor base grows.
- GitHub-rendered video demo — v1.5+ marketing-polish.
- `flow showcase` CLI subcommand — v1.5+ if friction surfaces.
- Permanent A/B no-articulation fixture — v1.5+ docs-polish.
- DAW-import release-time UAT step — composer-judgement-only, not a hard requirement.

---

*This log preserves the auto-selection trail for audit + post-hoc review.*
*Canonical decisions: `34-CONTEXT.md`.*
