---
phase: 34
slug: symphony-showcase-v1-4-closer-pre-public-public-pivot
type: discuss
gathered: 2026-05-16
status: ready-for-planning
mode: auto
---

# Phase 34: Symphony Showcase (v1.4 closer — pre-public → public pivot) — Context

**Gathered:** 2026-05-16
**Status:** Plan 34-01 closed; scope expanded by composer to include a 2nd showcase piece (ragtime)
**Mode:** `/gsd-discuss-phase 34 --auto --chain` — Claude auto-selected every gray area at the recommended option; this CONTEXT.md is the audit trail of those decisions. The composer is the visionary; planner / researcher / executor downstream must treat these decisions as locked unless flagged in `34-DISCUSSION-LOG.md` § "Open for composer review".

**Scope expansion (2026-05-16, after Plan 34-01 symphony sign-off):**

Composer chose Option B from the post-approval recommendation: scope-expand Phase 34 to ship TWO showcase pieces in v1.4.0 — the orchestral symphony (already approved, iteration #2) AND a contrasting upbeat solo-piano ragtime piece. The deferred-to-v1.5 "jazz / EDM follow-up" idea is pulled forward; v1.4 now demonstrates Flow's genre-agnostic claim (orchestral + ragtime) inside a single release. Both pieces share the v1.4.0 release tag, the top-level README `## Showcase` section, and the announcement draft. Downstream plan / file impacts:

- **D-202b [ragtime instrument]:** `#piano` only — solo piano (UprightPiano.sfz at VSCO-CE root). Ragtime is intrinsically piano-forward; one instrument keeps the showcase focused. NOT a 5-instrument arrangement.
- **D-104b [ragtime tempo + key]:** ~100 BPM (Joplin "not too fast" tradition), F major (warm and bluesy), 4/4. Tempo intentionally matches the symphony to keep the two pieces relatable as a pair (different mood, same time-base).
- **D-501b [ragtime source location]:** `examples/ragtime/ragtime.flow` (NEW sibling directory to `examples/symphony/`). Self-contained — does NOT reorganize the existing `examples/symphony/` directory or rename `symphony.flow`. Sibling layout keeps each piece's docs scoped to its own directory.
- **D-602b [ragtime README]:** NEW `examples/ragtime/README.md` (short — composition notes, reproduction step, the single-instrument SFZ pipeline). The existing `examples/symphony/README.md` stays unchanged from plan 34-02's expansion.
- **D-503b [release assets +2]:** v1.4.0 GitHub Release now ships 5 assets — symphony.mp3 + symphony.wav + ragtime.mp3 + ragtime.wav + flow-linux-x64.tar.gz.
- **D-601b [top-level README showcase]:** `## Showcase` section embeds BOTH pieces as inline `<video>` players (each via the GitHub user-attachments drag-drop URL per RESEARCH Pitfall 1).
- **D-901b [plan shape]:** Plan 34-01 now produces TWO pieces — symphony (approved) + ragtime (composing iteration in progress). The remaining plans 34-02..34-06 expand to cover both. No new plan IDs inserted; scope absorbed into the existing plan slots.
- **No new SPEC requirements:** SYM-01..SYM-05 already cover both pieces (criteria worded as "a short symphony" → reinterpret as "the showcase artifacts"; criterion 2 "composer signs off" applies per-piece; criterion 4 "README updated with a prominent showcase link + clip embed" → plural clips).

<domain>
## Phase Boundary

Phase 34 produces the **v1.4 headline artifact** — a curated ~60-second symphony for ~5 orchestral instruments, rendered end-to-end from Flow source through the Phase 33 SFZ sampler against VSCO Community CE 1.1.0, plus the public-facing release machinery that flips Flow from pre-public to public.

The deliverable set:

1. **`examples/symphony/symphony.flow`** — the canonical Flow source for the symphony. Renders to `examples/output/symphony.wav` + `examples/output/symphony.mid` via the existing `flow render` / `flow flow2midi` Phase 30 CLI surface.
2. **Rendered audio asset** — canonical MP3 (~1.5 MB) + canonical WAV (~10 MB) uploaded as **GitHub Release assets** attached to the `v1.4.0` tag. NOT committed to the repo (size + non-reproducible-from-fresh-clone-without-VSCO-CE).
3. **`examples/symphony/README.md`** — expanded from the Phase 33 smoke-fixture-only doc to cover BOTH `sfz_smoke.flow` (the 4-bar tutorial chapter, unchanged) AND `symphony.flow` (the new headline piece, with reproduction instructions, mix notes, and a per-instrument VSCO-CE patch checklist).
4. **Top-level `README.md` showcase section** — a new "Showcase" section near the top with the GitHub-rendered audio embed, the .flow source link, and the v1.4.0 release link.
5. **`docs/announcements/v1.4.0.md`** — a draft public announcement (~3 paragraphs + showcase link) the composer can adapt for Reddit / HN / X / Discord. Staged, not auto-posted.
6. **`v1.4.0` git tag + GitHub Release** — release body summarizes Phase 28-34 user-facing features; assets include the Phase 30 self-contained Linux binary + symphony MP3 + symphony WAV.
7. **v1.4 milestone closure docs** — `PROJECT.md`, `ROADMAP.md`, `STATE.md`, `REQUIREMENTS.md`, `.planning/MILESTONES.md` marked complete; PROJECT.md "Current State" flipped to "Shipped: v1.4 (YYYY-MM-DD)"; CLAUDE.md gains a single-line "Public as of v1.4" footnote under § "Goals".

**In scope:**

- Composition of a ~60 s minimalist-orchestral piece for **5 instruments** — `#violin` (solo) + `#cello` (ensemble) + `#flute` + `#horn` + `#timpani` (D-201/D-202).
- An **ABA single-movement** shape in a minor key (planner picks key based on what reads cleanly across the 5 VSCO-CE Sus patches — recommended D minor at 100 BPM) — see D-201.
- Source MUST exercise **every Phase 34 criterion-#3 feature**: musical-context blocks (`tempo`/`timesig`/`key`/`voicePool`), note streams (`| ... |`), at least one transform (`transpose`/`invert`/`retrograde`/`repeat`), `sampler:NAME` dispatch on every instrument, every Phase 28 articulation mark used at least once (`>`, `stacc`, `ten`, `leg`, `marc`), AND at least one voice block (`{voice ...}{voice ...}`) demonstrating Phase 28 polyphony.
- One **tuplet bracket** (Phase 19) for rhythmic interest (e.g. a triplet flourish in the woodwind line) — earns its place; doesn't force the showcase off the genre.
- Explicit `voicePool 32 { ... }` block (Phase 28 SPEC-7 locked default) declared at file scope — surface the locked default in composer-facing source for documentation value.
- **Mix processing on the rendered Buffer:** per-instrument `volume(buf, linear)` balancing → mixed sum → master Second-decay reverb (`reverb buf 0.3 2.5s`) → soft master `compress` (gentle 4:1 above -12dB threshold). All four steps are PHASE 28+ surface, demonstrate composer-pipeline ergonomics.
- **Two-run determinism manually verified** at release time: composer runs `flow render examples/symphony/symphony.flow -o a.wav && flow render examples/symphony/symphony.flow -o b.wav && cmp a.wav b.wav` — expects byte-identical (Phase 28's preserved-in-shape contract; the Phase 33 SFZ path inherits it via SfzSampleCache + deterministic eager-load order). Documented as a reproduction step in `examples/symphony/README.md`.
- **Iterative composer UAT** until the composer signs off "I would publicly share this" — no iteration cap; quality matters more than schedule (D-801).
- **6-plan wave structure** (D-901): plan 34-01 = symphony composition + render-iteration loop; 34-02 = `examples/symphony/symphony.flow` + expanded `examples/symphony/README.md`; 34-03 = top-level README.md showcase section + audio asset upload mechanics; 34-04 = `docs/announcements/v1.4.0.md` draft; 34-05 = `v1.4.0` tag + GitHub Release artifact bundle; 34-06 = v1.4 milestone closure docs (PROJECT/ROADMAP/STATE/REQUIREMENTS/MILESTONES/CLAUDE.md).

**Out of scope (capture for a later phase):**

- Stereo panning across instruments — added complexity for marginal showcase value; SfzRenderer mono-sums today, kept that way for v1.4. Could land in a v1.5 mix-polish phase if composer demand surfaces.
- Microtonal tuning (Scala `.scl` from Phase 32) on the symphony — out of place in a film-score / neo-classical showcase. `examples/scala/intro.flow` (shipped Phase 32) is the canonical microtonal demo; the symphony stays in 12-TET. (D-302)
- The 4 TBD VSCO-CE-missing GM symbols (`#choir`, `#guitar`, `#harpsichord`, `#celeste`) — none of them appear in the symphony, so no composer setup churn for absolute-path overloads.
- Piano (`#piano`) — Phase 29 already covers piano via bundled samples; symphony showcase intentionally features VSCO-CE-distinctive timbres (strings + winds + brass + percussion). Composers wanting piano have the Phase 29 path. (D-202)
- A CHANGELOG.md file in the repo root — Flow has FEATURES.md + PROJECT.md milestone sections + .planning/MILESTONES.md; per-release notes live in the GitHub Release body, not a tracked CHANGELOG. (D-501)
- Auto-posting the v1.4.0 announcement to any external platform — Phase 34 ships the draft markdown only; composer chooses platform + timing. (D-602)
- CI regression test for the symphony render — symphony render requires VSCO-CE which is not present in CI. The Phase 33 synthetic SFZ smoke fixture already exercises the SfzRenderer + parser + loader paths on CI. Symphony render is release-time activity. (D-701)
- A v1.4.1 / v1.5 roadmap — Phase 34 closes v1.4; the next milestone discussion is a separate `/gsd-new-milestone` invocation.
- "Postable on GitHub quality" being a strict measurable criterion — UAT is subjective per the ROADMAP success criterion; D-801 codifies the iteration loop instead.

</domain>

<decisions>
## Implementation Decisions

Decisions captured under `--auto`. Each was Claude's recommended option at the time of writing; composer can flag any for review in `34-DISCUSSION-LOG.md` § "Open for composer review" before plan-phase starts.

### 1. Symphony Scope: Length + Shape + Mood

- **D-101 [length-60s]:** Target rendered duration ≈ **60 seconds** — midpoint of the criterion-#1 window (30-90 s). Short enough to listen twice without losing attention, long enough to develop a theme. Planner may flex ±15 s during composition if the piece needs the room.
- **D-102 [shape-ABA-single-movement]:** Single-movement **ABA** structure. A (introduces theme on woodwind/strings) → B (contrasting middle — brass + timpani enter, key shifts up a fourth or relative-major) → A' (return to original theme with fuller orchestration). One coherent arc; no multi-movement complexity that bloats past 90 s.
- **D-103 [genre-neo-classical-minimalist]:** **Film-score / neo-classical minimalist** mood — broadly accessible to non-classical listeners, naturally shows off the sampler timbres, doesn't require deep music-theory knowledge to appreciate. NOT pastiche-Mozart, NOT death-metal, NOT EDM. The "Yann Tiersen / Ólafur Arnalds / Max Richter" sound bracket.
- **D-104 [tempo-100bpm-d-minor]:** Default to **tempo 100 / key D minor / timesig 4/4**. Planner may pick a different key if a sketch lands cleaner in (say) A minor or C minor — D minor is the recommendation, not a hard lock. Tempo 100 sits comfortably under the timpani and gives the woodwind lead room to breathe.

### 2. Instrumentation: Which VSCO-CE Patches

- **D-201 [count-5]:** **5 instruments** — the upper third of the criterion-#1 "3-6" window. Enough to feel orchestral, few enough that each line is intelligible in the mix.
- **D-202 [instruments-strings-woodwinds-brass-percussion]:** Locked instrument set: `#violin` (solo — `SViolinVib.sfz`, the most distinctive single VSCO-CE timbre), `#cello` (ensemble — `CelloEnsSusVib.sfz`, ensemble is the canonical VSCO-CE cello per 33-VSCO-PATH-AUDIT), `#flute` (`FluteSusVib.sfz` — woodwind lead), `#horn` (`FHornSus.sfz` — warm brass bed), `#timpani` (`Timpani.sfz` — occasional accents on section boundaries). Covers strings + woodwinds + brass + percussion in one piece; skips piano (Phase 29 territory) and the 4 TBD VSCO-CE-missing symbols (no composer setup churn).
- **D-203 [role-assignment]:** Default role assignment — flute carries the A-section melody; violin enters in A' with the same theme an octave up; cello holds a long sustained bass throughout; horn provides chord pads in B; timpani marks the A→B and B→A' transitions with a single accented hit each. Planner may adjust during composition iteration.

### 3. Flow Features Showcased in Source

- **D-301 [every-criterion-3-feature]:** The .flow source MUST demonstrate every criterion-#3 item explicitly: `tempo` + `timesig` + `key` context blocks (D-104), `voicePool 32 { ... }` block (Phase 28 SPEC-7 locked default — surface it for documentation value), at least one `transpose` / `invert` / `retrograde` / `repeat` transform (D-203 already implies `transpose` for the violin's A' octave-up entrance), `sampler:NAME` dispatch on every instrument (5 calls minimum), at least one of every Phase 28 articulation mark (`>` accent, `stacc` staccato, `ten` tenuto, `leg` legato, `marc` marcato), at least one `{voice ...}{voice ...}` voice block exercising Phase 28 polyphony.
- **D-302 [skip-microtonal-and-scala]:** **Do NOT** activate `enable justIntonation;` / `enable pythagorean;` (Phase 23) or `tuning t { ... }` (Phase 32) in the symphony. Microtonal showcases live in `examples/scala/intro.flow` (shipped Phase 32). The symphony's job is "musical first, feature-showcase second" — explicit microtonal pragmas distract from the film-score mood.
- **D-303 [include-one-tuplet]:** **One** `{3:2 ...}q` tuplet bracket (Phase 19) for rhythmic interest, placed in the woodwind line in section B (a triplet flourish on the flute, the place where it reads most naturally). Earns its showcase slot without forcing the genre off-target.
- **D-304 [include-humanize]:** `humanizeGaussian(seq, 0.05, seed)` (Phase 25) on the cello line for organic feel. Small amount (0.05); fixed seed for byte-identical reruns. Earns its slot because orchestral strings benefit audibly from micro-timing humanization.

### 4. Mix and Post-Processing

- **D-401 [per-instrument-volume-balance]:** Per-instrument balancing via `volume(buf, linear)` (Phase 26.2 ERG-03) before summing. Default starting balance — flute 0.85, violin 1.0, cello 0.75, horn 0.65, timpani 0.40 — adjusted during composer-UAT iteration. `volume()` not `gain()` because per-instrument levels are intuitively linear ("flute is 85% of full"), not dB.
- **D-402 [master-reverb-second-decay]:** Master Second-decay reverb on the summed mix: `(reverb mix 0.3 2.5s)` — 30% wet, 2.5 s tail (generous concert-hall feel). Phase 26.2 Second-decay literal demonstrates the ergonomic surface in the showcase.
- **D-403 [soft-master-compress]:** Soft master compressor on top of the reverb: `(compress mix -12dB 4 100ms 200ms)` — 4:1 ratio above -12 dB, 100 ms attack / 200 ms release. Gentle glue, NOT a brick-wall limiter; preserves dynamic range.
- **D-404 [no-stereo-pan]:** **Mono output** (or stereo-summed via existing renderSong path). NO `pan(...)` calls. SfzRenderer's stereo behavior at v1.4 is sum-only; adding per-instrument pan would require either a Phase 33.x SfzRenderer stereo retrofit or hand-stereo via dual-render-and-pan. Out of scope for v1.4 closer. Captured as v1.5 mix-polish deferred idea.

### 5. File Layout and Commit Strategy

- **D-501 [symphony-source-location]:** Symphony source lives at **`examples/symphony/symphony.flow`** — sibling to the existing `examples/symphony/sfz_smoke.flow`. `sfz_smoke.flow` keeps its role as the SFZ tutorial chapter (4-bar single-violin smoke fixture); `symphony.flow` becomes the headline piece. Single directory holds both files + the single shared `README.md`.
- **D-502 [no-rendered-wav-in-repo]:** Do **NOT** commit the rendered symphony WAV or MP3 to the repo. Reasons: (a) ~10 MB WAV exceeds the Phase 29 SPEC-2 ≤ 5 MB sample-bundle cap and there's no sister cap for output assets, but committing a 10 MB binary just to ship a showcase is wasteful; (b) the rendered output requires VSCO-CE installed at `sfz_root`, which is not a fresh-clone-deterministic input — committing an artifact that can't be reproduced from the repo alone misleads future agents.
- **D-503 [rendered-output-via-github-release]:** Rendered audio ships as **GitHub Release assets** on the `v1.4.0` tag (`flow-symphony-v1.4.0.mp3` ≈ 1.5 MB at 192 kbps + `flow-symphony-v1.4.0.wav` ≈ 10 MB uncompressed for archival). Both rendered locally by the composer, uploaded via `gh release upload v1.4.0` (Phase 30 ships gh-CLI usage pattern in scripts/). Top-level README.md embeds the MP3 via the GitHub-native `<video>`-tag-style asset URL.
- **D-504 [output-naming-aligned-with-existing]:** Local render outputs to `examples/output/symphony.wav` + `examples/output/symphony.mid` — same directory as existing `flow_tutorial.wav` / `flow_showcase.wav` (Phase 27 D-404 pattern). Repo `.gitignore` already covers `examples/output/`.

### 6. README + Documentation Updates

- **D-601 [top-level-readme-showcase-section]:** Add a NEW "## Showcase" section to top-level `README.md`, positioned **after "What is flow-lang?" and before "Install (Linux x64)"** (i.e. line ~30 ish). Contents:
  - 1-2 sentence framing: "Listen to *[Symphony Title]* — ~60 s for 5 orchestral instruments, rendered entirely from `examples/symphony/symphony.flow` via the v1.4 SFZ sampler against VSCO Community CE."
  - GitHub-native audio embed via the MP3 release-asset URL — GitHub renders `https://github.com/<user>/<repo>/releases/download/v1.4.0/flow-symphony-v1.4.0.mp3` inline as a player when written as a bare URL or wrapped in `<video controls src="...">`.
  - Source link: `[Source: examples/symphony/symphony.flow](./examples/symphony/symphony.flow)`.
  - Reproduction link: `[How to reproduce locally](./examples/symphony/README.md)`.
  - Release link: `[v1.4.0 release](https://github.com/<user>/<repo>/releases/tag/v1.4.0)`.
- **D-602 [expand-symphony-readme]:** Expand existing `examples/symphony/README.md` to cover both files:
  - NEW section at top: **"## The Symphony"** — describes `symphony.flow`'s ~60 s arc, ABA structure, 5-instrument lineup, what each Phase 34 Flow feature it exercises maps to which audible moment in the piece, expected output file, mix notes (length, key, BPM, sections), two-run determinism reproduction step.
  - Existing tutorial content **demoted but preserved** to a "## Tutorial Chapter: `sfz_smoke.flow`" section below — composers learn the surface on the 4-bar fixture before tackling the symphony.
- **D-603 [announcement-draft-location]:** Public announcement draft lives at **`docs/announcements/v1.4.0.md`** (new directory under existing `docs/`). Format: ~3 paragraphs Markdown.
  - Paragraph 1: "Flow is a music-production language…" (the elevator pitch).
  - Paragraph 2: "v1.4 ships…" (highlight summary: SFZ sampler + Scala tuning + sampled tonal instruments + articulation + polyphony + CLI binary + LSP polish).
  - Paragraph 3: "Listen to the showcase: [link]. Try it yourself: [install link]. Source: [repo link]."
  - Composer adapts the per-platform variant (Reddit / HN / X / Discord) from this single source. Phase 34 ships the draft; composer chooses platform + timing.

### 7. Regression Test Strategy

- **D-701 [no-ci-test-for-symphony-render]:** **No CI test** for the rendered symphony WAV. Symphony render requires VSCO-CE installed at `sfz_root`; CI has no library. The Phase 33 synthetic SFZ smoke fixture (`flow-lang.Tests/fixtures/sfz-smoke/`) already exercises the SfzParser + SfzRenderer + SfzSampleCache + crossfade paths on CI; that's the load-bearing CI surface for the SFZ subsystem.
- **D-702 [two-run-determinism-manual-at-release]:** Two-run byte-identical determinism is **manually verified at release time** by the composer running the render twice and `cmp`-ing the two WAVs. Documented as a reproduction step in `examples/symphony/README.md`. Confirms the Phase 28 "two-run cmp-clean" contract holds end-to-end on the real VSCO-CE library — preserves the Phase 18/25/27 contract shape.
- **D-703 [no-rms-baseline-for-symphony]:** **No RMS-windowed regression baseline** for the symphony WAV. Phase 28 RMS baselines live under `flow-lang.Tests/baselines/Phase28/` for the tutorial / showcase / graduation song output — those run on CI's deterministic synth-only path. Symphony WAV depends on VSCO-CE samples not in CI, so an RMS baseline would either need the library checked into CI (rejected per Phase 33 SPEC-2 + repo size budget) or be skipped on CI (worse than no test at all — false signal of coverage). Leave it out.

### 8. Composer UAT and Iteration

- **D-801 [iterative-uat-no-cap]:** **Iterative UAT** with no arbitrary iteration cap. Composer listens, gives feedback in plain English (e.g. "violin too loud", "B section transition feels abrupt", "timpani hit lands a beat too early"), planner / executor adjust the `symphony.flow` source + re-render, composer re-listens. Loop continues until composer signs off "I would publicly share this". Tracked in plan 34-01 as an explicit UAT-iteration checkpoint, not a one-shot. Phase 34 doesn't close until sign-off lands.
- **D-802 [uat-sign-off-criteria]:** Sign-off requires THREE conditions:
  1. Composer subjective: "I would publicly share this" / "postable on GitHub quality" — matches the ROADMAP success criterion #2 verbatim.
  2. Audible Phase 28 articulation differentiation: composer can hear that the staccato note is shorter than the legato note, the accent is louder than the unmarked note, etc. (At minimum, ONE A/B side-by-side fixture rendered with all-articulations-stripped vs the canonical mix — composer listens to both and confirms the articulated mix is audibly more expressive.)
  3. Audible polyphony: composer can pick out the simultaneous voices in the section where the voice block fires (e.g. violin + cello held over flute melodic line).
- **D-803 [uat-evidence-recorded]:** Sign-off is recorded as a HUMAN-UAT.md file in the phase directory — `.planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-HUMAN-UAT.md` — capturing date, mix iteration number, composer sign-off statement, the 3 D-802 criteria checked. Mirrors the Phase 17 / Phase 33 HUMAN-UAT pattern. Read by planner during 34-VERIFICATION.

### 9. Plan Shape

- **D-901 [6-plans]:** Phase 34 ships **6 plans** organized as a small wave:
  - **34-01** — Symphony composition + render-iteration loop (the long pole; D-801 iterative UAT lives here).
  - **34-02** — `examples/symphony/symphony.flow` (final canonical source post-iteration) + expanded `examples/symphony/README.md` per D-602.
  - **34-03** — Top-level `README.md` showcase section per D-601 + audio asset upload mechanics (gh-release-upload commands; D-503).
  - **34-04** — `docs/announcements/v1.4.0.md` draft per D-603.
  - **34-05** — `v1.4.0` git tag creation + GitHub Release artifact bundle (release body summarizing Phase 28-34 user-facing features; Phase 30 binary repackaged as `flow-linux-x64.tar.gz`; symphony MP3 + WAV assets).
  - **34-06** — v1.4 milestone closure docs: `PROJECT.md` "Current State" flip, `ROADMAP.md` Phase 34 row marked Complete, `STATE.md` reset for next milestone, `REQUIREMENTS.md` v1.4 entries marked Complete, `.planning/MILESTONES.md` v1.4 entry added, `CLAUDE.md` "Goals" gets the "Public as of v1.4" footnote.
- **D-902 [34-01-feeds-34-02]:** Plan 34-01 produces the iterated symphony source as a working artifact; plan 34-02 commits the final post-UAT source + README expansion as the canonical deliverable. Splitting them keeps the long composition-iteration loop's commits separate from the final ship commit (cleaner git history; easier to revert the source if needed without unwinding README work).
- **D-903 [34-05-blocks-34-06]:** Plan 34-05 (release tag) MUST land before plan 34-06 (milestone closure docs) — closure docs reference the release URL.

### Claude's Discretion

Decisions that follow from the above without separate user input. Planner may refine if research surfaces a better shape.

- **Symphony title.** Working title `"In Five Voices"` (5 instruments + the underlying voice-allocation pun) — composer renames during 34-01 if a better one emerges from the finished piece. Title appears in the .flow source as a top comment + in the announcement draft.
- **MP3 encoding tool.** `ffmpeg -i symphony.wav -c:a libmp3lame -b:a 192k symphony.mp3` — standard system tool; no new dependency. Plan 34-03 includes the exact command in scripts/ as a copy-pasteable snippet.
- **GitHub Release body template.** Markdown — 3 sections: "Highlights" (Phase 28-34 user-facing summary, one bullet per phase), "Install" (one-line `bash scripts/install.sh` + link to README), "Try the showcase" (download the MP3 asset + link to the .flow source). ~30 lines.
- **`v1.4.0` tag commit.** Tag the commit that lands plan 34-06 (milestone-closure docs). Annotated tag (`git tag -a v1.4.0 -m "..."`), NOT lightweight — annotated tags ship release-note metadata to `git describe`.
- **CLAUDE.md "Public as of v1.4" footnote text.** Single line under § "Goals": `> **Note:** Flow is public as of v1.4 (2026-XX-XX). The pre-public scope-creep-without-deprecation latitude (`project_pre_public_no_legacy_burden`) no longer applies; breaking changes now go through a deprecation cycle.` Composer-facing instructions stay otherwise unchanged. Memory `project_pre_public_no_legacy_burden.md` flagged for update at milestone closure.
- **PROJECT.md "Current State" wording.** `**Shipped:** v1.4 Audio Fidelity, Distribution & Public Showcase (YYYY-MM-DD)` replacing the current "**Shipped:** v1.2 / **In progress:** v1.3" lines. Add a single-line "Next milestone: TBD — see `.planning/MILESTONES.md`" pointer. Existing v1.2 / v1.1 / v1.0 collapsed-summary `<details>` blocks remain unchanged.
- **Composer per-instrument volume tuning.** Default starting balance from D-401 is a STARTING POINT — composer overrides during 34-01 UAT iteration. Final levels land in the committed `symphony.flow` in 34-02.
- **Determinism reproduction step doc shape.** `examples/symphony/README.md` § "Reproduction" subsection 4: `flow render examples/symphony/symphony.flow -o a.wav` + `flow render examples/symphony/symphony.flow -o b.wav` + `cmp a.wav b.wav` (exits 0 = byte-identical determinism preserved). One-sentence framing: "Same inputs → same bytes. Two runs back-to-back must produce identical WAVs."
- **No new external dependencies.** Plan 34 ships pure-composition + docs + release-tooling work; no new NuGet packages, no new SFZ extensions, no new C# files in `flow-lang/` or `flow-midi/`. The build of the phase doesn't touch the interpreter at all (only `examples/` and `docs/` and top-level Markdown).
- **Source ASCII / no emoji per CLAUDE.md "Conventions".** Composer comments in `symphony.flow` use plain ASCII; rendering-pipeline + mix notes inline only where the WHY is non-obvious (e.g. "// timpani hit -- accent marks the A→B transition; volume=0.40 because the patch is loud").
- **Per-section reverb already inside `renderSong`.** SongRenderer's per-section reverb (Phase 28 default) fires automatically — D-402's master reverb sits ON TOP of that. Composer doesn't need to disable section reverb; the combo of section-reverb-then-master-reverb is the canonical film-score wet-stack. Plan 34-01 verifies the combined wet level isn't muddy during UAT.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 34 anchors (this phase)

- `.planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-CONTEXT.md` — this file. Decisions D-101..D-903 + Claude's-Discretion items.
- `.planning/ROADMAP.md` § "Phase 34: Symphony Showcase (v1.4 closer — pre-public → public pivot)" — original phase entry with 5 success criteria.
- `.planning/REQUIREMENTS.md` — v1.4 requirements ledger (Phase 34 closure rewrites the milestone-completion row).
- `.planning/PROJECT.md` — flipped to "Shipped: v1.4" at phase closure.
- `.planning/STATE.md` — current GSD state (last-updated by Phase 33 closure; reset by plan 34-06).
- `.planning/MILESTONES.md` — v1.4 closing entry added by plan 34-06.

### Phase 33 anchors (the SFZ surface the symphony consumes)

- `.planning/phases/33-sfz-orchestral-sampler/33-SPEC.md` — **LOCKED REQUIREMENTS for the SFZ surface.** Read before composing the symphony source.
- `.planning/phases/33-sfz-orchestral-sampler/33-CONTEXT.md` — Phase 33 implementation decisions D-01..D-20; the composer surface (`use "@sfz"`, `(loadSfz #symbol)`, `sampler:NAME`) lives here.
- `.planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md` — the 19-symbol GM dict + verified VSCO-CE paths; symphony instrument choices (D-202) draw the 5 patches from the "verified" rows of this table.
- `.planning/phases/33-sfz-orchestral-sampler/33-VSCO-CONTROL-DECISION.md` — `<control> default_path=` convention; relevant if symphony composition surfaces a parser issue.
- `.planning/phases/33-sfz-orchestral-sampler/33-HUMAN-UAT.md` — Phase 33 UAT shape; Phase 34's `34-HUMAN-UAT.md` (D-803) mirrors it.

### Existing symphony directory (the foundation)

- `examples/symphony/sfz_smoke.flow` — Phase 33 tutorial chapter; symphony.flow lives sibling to it (D-501).
- `examples/symphony/README.md` — current Phase 33 SFZ tutorial doc; expanded per D-602 to cover both files.

### Phase 28 anchors (articulation + polyphony — features the symphony showcases)

- `.planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-CONTEXT.md` — articulation rule locked-shape (staccato 25% / legato 110% / accent +0.30 vel / marcato accent+25% / tenuto 100%); voice-block syntax; `voicePool` block; D-301 + D-303 of the symphony source must exercise these.
- `.planning/phases/28-midi-audio-polyphony-articulation-rewrite/28-SPEC.md` — voicePool default = 32, range 1..256.
- `CLAUDE.md` § "Locked articulation rules" — the surface CLAUDE references; matches the 28-CONTEXT canonical surface.

### Phase 29 anchors (sampled-instrument render path — preserved coexistence)

- `flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs` — Phase 29 sample-based render path; Phase 33 SFZ renderer is a parallel branch BEFORE this in SongRenderer's instrument-string dispatch. Symphony does NOT exercise Phase 29 (skips piano — D-202).
- `.planning/phases/29-instrument-realism/29-VERIFICATION.md` — Phase 29 closure; relevant only to confirm Phase 29 stays green when symphony doesn't touch it.

### Phase 30 anchors (CLI + install — composer-facing reproduction path)

- `scripts/install.sh` — per-user install; symphony reproduction steps in `examples/symphony/README.md` assume `flow` is on `$PATH` via this script.
- `flow-cli/Config/FlowConfigPoco.cs` — `sfz_root` key; symphony reproduction requires composer to populate this.
- `README.md` § "Install (Linux x64)" — the install section the new "Showcase" section sits ABOVE (D-601).

### Phase 27 anchors (the prior big-curation phase — pattern reference for plan shape)

- `.planning/phases/27-tutorial-showcase-refresh/27-CONTEXT.md` — pattern reference: how the prior tutorial/showcase phase organized its plans, output naming (D-404: `flow_showcase.{wav,mid}`), file-replacement-not-versioning (D-201).
- `.planning/phases/27-tutorial-showcase-refresh/27-DISCUSSION-LOG.md` — discussion-log shape Phase 34's mirrors.
- `examples/showcase.flow` — v1.3 showcase; the SHAPE of the per-section render → effect chain → writeWav pipeline mirrors what `symphony.flow` will use (with `renderSong` per instrument instead of strings-only).

### Project-wide convention anchors

- `CLAUDE.md` § "Music Types Quick Reference" — table updated by plan 34-06 if a v1.4 row needs adding (no new music type from Phase 34 itself; Phase 32 Tuning + Phase 33 Sfz rows already landed).
- `CLAUDE.md` § "Goals" — gains the "Public as of v1.4" footnote (D-901 plan 34-06).
- `CLAUDE.md` § "Music-Specific Language Features" — Phase 33 SFZ paragraph confirms the surface the symphony uses.
- `CLAUDE.md` § "Conventions" — pre-Phase-28 byte-identical contract dropped in favor of two-run cmp-clean; symphony reproduction docs (D-702) reflect this.
- `~/.claude/.../memory/project_pre_public_no_legacy_burden.md` — flagged for memory update at v1.4 closure (D-901 plan 34-06 also rewrites this memory file's body to "Flow was pre-public; v1.4 closure 2026-XX-XX flipped it public. Breaking changes now ship through deprecation windows.").

### External references (for plan-phase researcher — not read by planner directly)

- VSCO Community CE 1.1.0 release: https://github.com/sgossner/VSCO-2-CE/releases/tag/1.1.0 — the orchestral library the symphony renders against.
- GitHub Release asset audio embed convention: https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository — for D-503 release upload mechanics.
- `gh release upload` man page (https://cli.github.com/manual/gh_release_upload) — for D-503 + 34-05 release-asset upload commands.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`examples/symphony/sfz_smoke.flow`** — proves the full `use "@audio" + use "@sfz" + Sfz violin = (loadSfz #violin) + Song song = [section] + Buffer mix = (renderSong song "sampler:violin") + writeWav` pipeline works end-to-end against VSCO-CE. `symphony.flow` is a richer instance of the same pipeline.
- **`examples/showcase.flow`** — v1.3 showcase, demonstrates the per-section-render → effect-chain → writeWav pipeline with the Phase 26.2 surface (`volume`, `lowpass` with Hertz, `delay` with Ms, `reverb` with Second). The symphony's mix-stack (D-401..D-404) parallels this; planner copies the shape.
- **`flow render` / `flow flow2midi` CLI subcommands (Phase 30)** — `flow render examples/symphony/symphony.flow -o examples/output/symphony.wav` and `flow flow2midi examples/symphony/symphony.flow -o examples/output/symphony.mid` produce the canonical render outputs. No new CLI work needed.
- **`SfzSampleCache` (Phase 33 D-06/D-07)** — eager-load + per-FlowEngine lifetime means rendering the symphony twice in a row hits the cache on the second run. Two-run determinism (D-702) is fast because of this.
- **`SongRenderer` instrument-string dispatch (Phase 33 D-13)** — `sampler:violin` / `sampler:cello` / etc. branch dispatches to SfzRenderer. Symphony issues 5 such calls; each gets its own per-instrument Buffer back, summed at the mix stage.
- **`writeWav` + `writeMidi` Phase 28 multi-track export** — multi-track MIDI emits one track per unique sequence name + the conductor track (CLAUDE.md § "Multi-track MIDI export"). Symphony's 5 sequences → 5 MIDI tracks + conductor track in the exported .mid.
- **`scripts/install.sh` + Phase 30 self-contained binary** — release asset shape for plan 34-05 derives from these (the Phase 30 binary is what gets uploaded as `flow-linux-x64.tar.gz`).
- **`ffmpeg` (system dependency)** — for WAV → MP3 conversion in plan 34-03 + 34-05. Not a Flow dependency; standard system tool. Plan should NOT bundle it; documents the encoding command as a one-liner.

### Established Patterns

- **Per-section reverb inside `renderSong`** — Phase 28 SongRenderer's default. Symphony's master reverb (D-402) sits ON TOP. Combined wet stack is film-score-canonical; planner verifies it doesn't go muddy during UAT.
- **Per-instrument Buffer + sum-mix pattern** — `examples/showcase.flow` renders one Buffer via `renderSong piece "strings"`. Symphony pattern extends this: render 5 separate Buffers (one per instrument via `renderSong piece "sampler:NAME"`), apply per-instrument `volume(buf, linear)` (D-401), sum, then apply master reverb + compress (D-402 + D-403). The "render-per-instrument-then-sum" shape is the natural surface for the SFZ sampler + Phase 28 voice-block mix model.
- **HUMAN-UAT.md pattern** — Phase 17 + Phase 33 ship `*-HUMAN-UAT.md` files capturing date + sign-off statement + criteria. Phase 34 mirrors this verbatim (D-803).
- **Determinism verification via `cmp`** — Phase 28 dropped pinned-bytes determinism in favor of two-run cmp-clean. Symphony reproduction (D-702) ships this exact pattern as a composer-facing reproduction step.
- **Output naming convention** — `examples/output/{name}.{wav,mid}` (Phase 27 D-404). Symphony adopts: `examples/output/symphony.{wav,mid}` (D-504).
- **`gh release upload` for binary release assets** — Phase 30 plans use `gh` for tag pushes; symphony MP3 + WAV upload uses the same `gh release upload v1.4.0 <files>` pattern.

### Integration Points

- **`examples/symphony/`** — new file `symphony.flow` lives here; existing `sfz_smoke.flow` unchanged. README expanded per D-602.
- **`examples/output/`** — new render targets `symphony.wav` + `symphony.mid`; already covered by existing `.gitignore`.
- **`README.md` (top-level)** — new "## Showcase" section inserted at line ~30 per D-601. Does NOT touch existing sections.
- **`docs/announcements/`** — NEW directory under existing `docs/`; first file `v1.4.0.md` per D-603.
- **`.planning/PROJECT.md`** — "Current State" lines updated per Claude's-Discretion. v1.3 details preserved in the existing `<details>` summary block.
- **`.planning/ROADMAP.md`** — Phase 34 row marked Complete; v1.4 milestone progress row updated.
- **`.planning/STATE.md`** — reset by plan 34-06 ("stopped_at: Phase 34 complete (6/6) — v1.4 shipped"); next-milestone field set to "TBD pending /gsd-new-milestone".
- **`.planning/REQUIREMENTS.md`** — v1.4 entries marked Complete; new file or appended section closes the milestone ledger.
- **`.planning/MILESTONES.md`** — gains a v1.4 closure entry mirroring the v1.0..v1.3 pattern already in the file.
- **`CLAUDE.md`** — gains the "Public as of v1.4" footnote under § "Goals" per Claude's-Discretion. No new music-type rows (Tuning + Sfz already shipped Phase 32 + 33). No § "Conventions" changes; § "Music-Specific Language Features" gets a single-line "Symphony showcase: `examples/symphony/symphony.flow` — see README.md § Showcase" reference paragraph appended after the Phase 33 SFZ block.
- **`flow-lang/` + `flow-midi/` + `flow-lsp/` + `flow-cli/`** — **NOT TOUCHED.** Phase 34 ships zero interpreter code changes; pure composition + docs + release work.

### Anti-Patterns to Avoid

- **DO NOT commit the rendered symphony WAV or MP3** (D-502). Size + non-fresh-clone-deterministic.
- **DO NOT bundle VSCO-CE into the repo** (Phase 33 SPEC-2 + repo size cap). Composers download the library themselves.
- **DO NOT add per-instrument stereo pan** (D-404). SfzRenderer mono-sums; pan retrofit is out of scope.
- **DO NOT add a CHANGELOG.md file** (D-501 out-of-scope). Per-release notes live in the GitHub Release body; PROJECT.md milestone sections + FEATURES.md + .planning/MILESTONES.md already cover the history.
- **DO NOT activate microtonal pragmas or Scala tuning in the symphony** (D-302). Out of place in a film-score showcase; `examples/scala/intro.flow` is the canonical microtonal demo.
- **DO NOT auto-post the announcement** (out-of-scope). Phase 34 ships the draft markdown only; composer chooses platform + timing.
- **DO NOT create a CI regression test for the symphony render** (D-701). VSCO-CE not in CI; synthetic Phase 33 fixture is the load-bearing CI surface for the SFZ subsystem.
- **DO NOT amend the v1.4.0 tag after publication** — annotated, signed via composer's normal git workflow, immutable. If a fix is needed, ship v1.4.1.
- **DO NOT touch interpreter code in Phase 34** — pure composition + docs + release work. Any interpreter bug surfaced during composition iteration goes to `/gsd-debug` in a sibling thread, lands as its own commit, and Phase 34 picks up the fix in the next render iteration.

</code_context>

<specifics>
## Specific Ideas

- **The "v1.4 closer" framing.** ROADMAP positions Phase 34 as "the moment Flow stops being pre-public — once the clip is public, the demonstrated API surface becomes effectively frozen". Translates directly: after `v1.4.0` ships, breaking changes need deprecation windows (overriding memory `project_pre_public_no_legacy_burden`). Plan 34-06 rewrites that memory file at closure.
- **The "headline artifact" framing.** Phase 34's deliverable is what new users SEE FIRST when they land on the repo. The README "## Showcase" section is positioned above "Install" (D-601) so the playable audio is the first interaction; install instructions follow.
- **The "iterative UAT loop" framing.** D-801 + D-802 explicitly model the composer-as-final-arbiter pattern. The phase doesn't close until the composer signs off. Mirrors film-score mix engineering — the composer renders, listens, adjusts, re-renders. Plan 34-01 budgets time for multiple rounds; no schedule pressure overrides quality.
- **The "VSCO-CE-distinctive timbres" framing.** D-202 chose 5 instruments that VSCO-CE renders distinctively (violin solo + cello ensemble + flute + horn + timpani). Piano deliberately excluded because Phase 29 already covers piano via bundled samples — the symphony showcase intentionally features VSCO-CE-distinctive sounds, not "piano you could have rendered without VSCO-CE".
- **The "5 instruments fit 5 voices" pun.** Working title `"In Five Voices"` (Claude's-Discretion) lands a play on both the orchestral count and the underlying Phase 28 `voicePool` voice-allocation model. Composer renames during 34-01 if a better title surfaces.

</specifics>

<deferred>
## Deferred Ideas

Captured during analysis but belong outside Phase 34:

- **Stereo panning across instruments.** Per-instrument `pan(buf, position)` calls would let the violin sit slightly left + flute slightly right + horn centre + cello slightly left + timpani back-centre — natural orchestral seating. Requires SfzRenderer stereo retrofit OR hand-stereo via dual-render-and-pan. v1.5 mix-polish phase.
- **A second showcase: jazz piece.** "Genre-agnostic" is a Flow goal; one orchestral showcase serves v1.4 well, but a v1.5 follow-up could ship a contrasting jazz/EDM/etc. piece to round out the showcase set. Track in `.planning/MILESTONES.md` v1.5+ slot.
- **Auto-posting infrastructure.** `gh issue create` + Reddit API + Discord webhook integration for one-button "ship the announcement everywhere". Out of scope; composer prefers manual launch control for the first public release.
- **CHANGELOG.md file in repo root.** Standard convention but currently redundant against PROJECT.md milestone sections + FEATURES.md + .planning/MILESTONES.md. Could land in v1.5+ if a contributor asks for it.
- **A CI integration that runs the symphony render against a small VSCO-CE subset.** Would require either (a) negotiating with VSCO-CE maintainer for a redistributable subset, (b) building a synthetic mini-VSCO-CE for CI, (c) running symphony render only on local releases. Not worth the complexity at v1.4; revisit when contributor base grows.
- **GitHub-rendered video demo.** A short screen recording of `flow watch examples/symphony/symphony.flow` + audio playback. Stronger marketing artifact than audio alone; out of scope because video tooling adds dependencies + iteration time. v1.5+ marketing-polish phase.
- **A "Showcase" CLI subcommand.** `flow showcase` could `git clone` VSCO-CE on demand, populate `sfz_root`, render `symphony.flow`, play the result — one-command demo for new users. Out of scope; symphony.flow + `examples/symphony/README.md` instructions are clean enough at v1.4.
- **Per-articulation A/B fixture as a permanent example.** D-802's UAT requires an all-articulations-stripped vs canonical mix A/B render — generated during UAT iteration. Could be saved as a permanent `examples/symphony/symphony_no_articulation.flow` (with stripped articulation marks) so new users hear the audible difference. Out of scope for Phase 34's plan budget; pin as a v1.5 docs-polish item.
- **MIDI-export verification as a release-time UAT step.** D-701 leaves MIDI export untested for the symphony (CI does test Phase 28 multi-track export on synthetic fixtures). Could add a release-time step: import the symphony.mid into MuseScore / a DAW, confirm it opens + plays. Composer-judgement-only; not a hard requirement.

</deferred>

---

*Phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot*
*Context gathered: 2026-05-16 via `/gsd-discuss-phase 34 --auto --chain`*
*Mode: auto — every decision auto-selected at the recommended option; composer reviews `34-DISCUSSION-LOG.md` § "Open for composer review" before plan-phase begins.*
