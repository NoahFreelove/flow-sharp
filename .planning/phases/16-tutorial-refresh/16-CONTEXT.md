# Phase 16: Tutorial Refresh — Context

**Gathered:** 2026-04-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Refresh `examples/tutorial.flow` so a new user running it once experiences every
v1.1 + v1.2 composer-visible feature end-to-end, producing audible WAV +
non-empty MIDI on first run. ROADMAP success criteria #1-#3 (and the now-moot
#4) gate the phase.

**In scope (the 12 features that must be demonstrated per ROADMAP criterion #1):**
- v1.1: `//` line comments, `writeWav`, `mix`, per-section `gain`,
  `strings`/`organ`/`bell` synth presets, `tempoRamp`, `sing`/`tts`
- v1.2: `slice`, enharmonic helpers (incl. flats), `reverbTime`, MIDI velocity
  export via dynamics, `euclidean` swing/humanize

**Out of scope:**
- New language features, new built-ins, new synths, new effects
- Tutorial framework / scaffolding generators
- Documentation outside tutorial.flow + showcase.flow (CLAUDE.md, wiki, etc.)
- C5 augment/diminish migration notes — C5 was dismissed in Phase 11; ROADMAP
  criterion #4 is moot and stays unchecked

</domain>

<decisions>
## Implementation Decisions

### Structure & narrative arc
- **D-01:** Tutorial structure = **expand the current narrative**. Keep the
  existing build-up arc (Variables → Arithmetic → Functions → Collections →
  Note Streams → Musical Context → Song). Weave new v1.1 + v1.2 features into
  their natural place. Add a final v1.2 Capabilities chapter for features that
  don't fit organically (`euclidean`, `reverbTime`, `sing`/`tts`).
- **D-02:** Length cap = **none**. Tutorial may grow naturally beyond 600
  lines. Concision is preferred but not enforced. If a feature deserves more
  space, take it.
- **D-03:** `examples/showcase.flow` is **also refreshed** in parallel as an
  ambient mood piece. Distinct role from tutorial — showcase = "wow, listen to
  this" compact piece using `reverbTime` + `euclidean` humanize +
  dynamics-driven MIDI. No teaching, no per-feature comments. Tutorial keeps
  the educational role; showcase keeps the "quick demo" role. **This is an
  intentional scope expansion** — both files ship under Phase 16.

### Output strategy
- **D-04:** Output strategy = **single graduation piece at the end**. The
  final song renders to `examples/output/flow_tutorial.wav` AND
  `examples/output/flow_tutorial.mid` via `writeMidi` from the same source
  song. Demonstrates `writeWav`/`writeMidi` together.
- **D-05:** Output path = **`examples/output/`** (new directory). Add a
  `.gitignore` entry so generated artifacts are not committed. Discoverable:
  user finishing the tutorial sees outputs sitting next to the source they
  ran. Cross-platform; survives reboot (unlike `/tmp/`).
- **D-06:** `showcase.flow` writes to the same `examples/output/` directory
  with a distinct filename (`flow_showcase.wav` / `.mid`). Both files share
  the same `.gitignore` rule.

### Graduation piece feature integration
- **D-07:** Features that the graduation song must integrate **audibly** (not
  just print-and-show):
  - `reverbTime` — wrap a section in `reverbTime { ... }` for hearable RT60
    decay
  - `euclidean` swing + humanize — drum or bass groove inside the song using
    a 6-arg `euclidean(steps, hits, note, swing, humanize, seed)` call with a
    fixed seed for reproducibility
  - per-section `gain` — vary section gain to demonstrate dynamic structure
    (intro quiet, chorus loud, etc.)
  - `tempoRamp` — ritardando or accelerando moment somewhere in the song
- **D-08:** Other features (`slice`, enharmonic helpers, `mix`,
  `sing`/`tts`, `strings`/`organ`/`bell` synth presets, MIDI velocity export
  via dynamics, `//` line comments) get demonstrated in their own chapters
  with print-and-show + a small audible artifact where natural. They do NOT
  need to appear in the graduation song.

### Comment style
- **D-09:** Comment style = **stylistic split**. Use `Note:` for chapter
  headers and multi-line big-picture explanations (visually distinct dividers).
  Use `//` for short inline annotations on or near specific lines.
  Naturally demonstrates BOTH styles in their respective strengths.
- **D-10:** `//` introduction = **organic, no dedicated callout**. The first
  `//` comment appears wherever it's natural — the reader infers the syntax.
  No "Comments come in two forms" preamble. Aligns with charitable
  interpretation philosophy (don't over-explain).

### Feature traceability
- **D-11:** Traceability format = **prose-only inline**. Each feature
  demonstration has a `Note:` chapter header (prose) and inline `//`
  annotations with feature names in plain language (e.g.,
  `// Try euclidean rhythms with swing accents`). NO REQ-ID tags
  (`// covers DX-09`) — beginner-friendly readability wins.
- **D-12:** Traceability location = **inline at each demonstration**. NO
  central index at top or bottom of the file. The reader sees feature
  references right next to the snippet that demonstrates them. ROADMAP
  criterion #3 is satisfied by the inline prose mentions — every required
  feature name must appear in at least one comment.
- **D-13:** Deferred features = **not mentioned in tutorial**. No "coming
  soon" callouts, no inline DEFER-NN references. Tutorial demonstrates only
  shipped reality. Deferred items live in `deferred-items.md` and roadmap
  discussions.

### ROADMAP criterion #4 (migration notes)
- **D-14:** Criterion #4 (C5 augment/diminish migration notes) = **moot**.
  C5 was dismissed in Phase 11; no breaking change shipped. The criterion
  stays unchecked in ROADMAP for audit trail; Phase 16 does NOT need to
  produce migration content. Phase 15-08 verification can confirm this.

### Pre-decided (from prior phases / project memory)
- **D-15 (carryover):** Functional S-expression style only — no infix
  operators. Tutorial body uses `(add x y)`, not `x + y`. (Memory:
  `feedback_language_philosophy.md`)
- **D-16 (carryover):** Charitable interpretation philosophy — silent
  assumptions over errors; music > rigid correctness. Tutorial tone reflects
  this: encourage exploration, downplay edge-case error handling. (Memory:
  `feedback_charitable_interpretation.md`)
- **D-17 (carryover):** `(sub 0.0 N)` idiom for negative doubles — parser
  collides bare `-N` with binary subtraction. Tutorial uses this idiom
  wherever a negative double literal is needed (e.g., negative swing).
  (Phase 14 D-19, Phase 12 D-19)

### Claude's Discretion
- Order of feature introduction within the v1.2 capabilities chapter
  (alphabetical, by complexity, or by audible impact)
- `sing`/`tts` placement — separate chapter, or part of the graduation song
  (tradeoff: sing/tts is voice synthesis with limited expressiveness; could
  feel out of place in an instrumental graduation song; planner picks)
- Exact musical content of the graduation piece (key, tempo, structure,
  number of sections) as long as the 4 audible features (D-07) are integrated
- Exact musical content of the showcase ambient piece — planner/executor's
  artistic call
- Whether to keep all 348 existing tutorial lines verbatim or trim
  redundant prints. Spirit: minimal churn on existing content (D-01) but if
  a chapter has obvious dead weight, trim
- Whether `examples/output/` gets a separate `.gitignore` file or a top-level
  `.gitignore` entry — either is fine

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope
- `.planning/ROADMAP.md` § "Phase 16: Tutorial Refresh" — goal + 4 success
  criteria
- `.planning/REQUIREMENTS.md` — QOL-03 row (the 12-feature list is the
  authoritative source of what must be demonstrated)

### Shipped features the tutorial must demonstrate
- `.planning/REQUIREMENTS.md` § "Composer DX — Tier A" — DX-05 (slice),
  DX-06 (enharmonic helpers, flat literals), DX-07 (reverbTime), DX-08
  (MIDI velocity), DX-09 (euclidean swing/humanize)
- `.planning/phases/14-composer-dx-part-1/14-VERIFICATION.md` — DX-05/06/08
  shipped state
- `.planning/phases/15-composer-dx-part-2/15-VERIFICATION.md` — DX-07/09
  shipped state
- `.planning/phases/15-composer-dx-part-2/15-SUMMARY.md` — Phase 15
  rollup including the byte-identical determinism contract (relevant if
  graduation piece uses a fixed seed)

### Project conventions
- `CLAUDE.md` § "Language Features" — full v1.0+v1.1+v1.2 feature surface
- `CLAUDE.md` § "Music-Specific" — `tempo`, `timesig`, `key`, `swing`
  context blocks; note streams; chord literals; roman numerals
- `flow-lang/std.flow`, `flow-lang/audio.flow`, `flow-lang/composition.flow`,
  `flow-lang/notation.flow`, `flow-lang/collections.flow`, `flow-lang/bars.flow`
  — stdlib procs the tutorial imports

### Memory (decisions that carry across phases)
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_language_philosophy.md`
  — functional S-expression style only
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_charitable_interpretation.md`
  — silent assumptions, music > rigid correctness

### Existing files modified
- `examples/tutorial.flow` (348 lines, current state — narrative arc Variables→Song)
- `examples/showcase.flow` (84 lines, current state — Phase 1-4 ambient demo)

### Past tutorial-as-regression-pin precedent
- `.planning/phases/13-nyquist-validation-backfill/13-04-SUMMARY.md` —
  Phase 13 Plan 04 confirmed `examples/tutorial.flow` runs GREEN under HEAD;
  Phase 16 must preserve that property after refresh

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `examples/tutorial.flow` (current 348 lines) — narrative arc Variables→Song
  is sound; just needs new chapters woven in. Most existing chapters stay.
- `examples/showcase.flow` (current 84 lines) — Phase 1-4 ambient piece;
  decision D-03 says rewrite this in parallel as a v1.2 ambient piece, NOT
  retire it.
- `tests/output/.gitignore` (Phase 15 Plan 01) — precedent for a directory
  with a co-located `.gitignore` that ignores `*.wav` and `*.mid`. Apply
  same pattern to `examples/output/`.
- `tests/test_reverb_time.flow`, `tests/test_euclidean_swing.flow`,
  `tests/test_euclidean_humanize.flow`, `tests/test_dynamics_midi_velocity.flow`,
  `tests/test_enharmonic.flow`, `tests/test_flat_literals.flow`,
  `tests/test_slice.flow` — Phase 14/15 test scripts that exercise each
  feature standalone. Useful as reference snippets when the planner is
  drafting tutorial chapters; copy idioms, not whole bodies.

### Established Patterns
- Tutorial uses `(print $"...{(str x)}...")` interpolation pattern
  throughout — keep this idiom.
- Tutorial uses `Note: --- N. Section name ---` with `(print "--- N. Section name ---")`
  paired below (visible chapter dividers in stdout AND source). Keep this.
- Tutorial ends with a "Congratulations!" block listing what the user learned.
  Refresh this list to include v1.1 + v1.2 features taught.
- Existing graduation piece uses `tempo`, `timesig`, `key` context blocks +
  `section` declarations + `Song` arrangement — extend with `reverbTime` +
  per-section `gain` per D-07.

### Integration Points
- Tutorial's existing graduation piece is at the end of the file (after the
  Pattern Transforms chapter). The new v1.2 capabilities chapter goes
  between Pattern Transforms and the graduation piece, OR the graduation
  piece itself absorbs the v1.2 features (D-07 leans this way).
- `writeWav` / `writeMidi` calls go in the same final block; both consume
  the same `Song` value rendered by `renderSong`.

</code_context>

<specifics>
## Specific Ideas

- "Wow, listen to this" tone for showcase.flow (D-03) — the user's word for
  it. Showcase is decoration; tutorial is education.
- Graduation piece should be **musical**, not a feature dump. The 4
  integrated features (reverbTime, euclidean, gain, tempoRamp) must serve a
  composition, not show up as a checklist.
- `Note:` block-comment chapter headers are visually distinct dividers; `//`
  inline annotations are short prose nudges. Don't blur the two.
- New `//` comments appear naturally — beginner sees them in context and
  infers. No callout.
- Outputs go to `examples/output/` because a learner who just finished the
  tutorial should see the WAV sitting next to the source they ran (D-05).
- ROADMAP criterion #4 (migration) stays unchecked permanently — audit-trail
  signal that C5 was dismissed, not forgotten.

</specifics>

<deferred>
## Deferred Ideas

- **REQ-ID tags inline (`// covers DX-09`)** — rejected per D-11 in favor of
  prose-only. Could be revisited if a future audit phase wants
  machine-checkable traceability; would require adding REQ-IDs without
  disrupting the prose teaching flow.
- **Central traceability index** — rejected per D-12. Could be added in a
  future doc-hygiene phase if the wiki/README needs a separate
  feature-coverage map.
- **"What's next" / coming-soon callouts** — rejected per D-13. H-alias
  (DEFER-02), pragma system (DEFER-03), Gaussian humanize (DEFER-03 within),
  double-sharp respelling (DEFER-04), and similar deferred items stay out of
  the tutorial. They live in `.planning/phases/14-composer-dx-part-1/deferred-items.md`.
- **Splitting tutorial into multiple files** — rejected by structural
  decision D-01 (single file, expand current narrative). Could be revisited
  if the file genuinely becomes hard to read; D-02 imposes no length cap so
  this is unlikely to bind.
- **`augment`/`diminish` migration notes** — moot per D-14 (C5 dismissed in
  Phase 11). If a future v1.3 ships a real breaking change, that phase's
  closure plan adds migration notes.

</deferred>

---

*Phase: 16-tutorial-refresh*
*Context gathered: 2026-04-25*
