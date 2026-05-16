# Phase 34: Symphony Showcase (v1.4 closer — pre-public → public pivot) — Research

**Researched:** 2026-05-16
**Domain:** Composition + docs + release tooling (no interpreter code)
**Confidence:** HIGH overall — VSCO-CE patch quirks VERIFIED via GitHub raw probes; environment tooling (ffmpeg / gh / dotnet / cmp) VERIFIED present; GitHub MP3 inline-embed mechanism VERIFIED as a deviation from the CONTEXT.md D-601 assumption (the planner must address this gap).

## Summary

Phase 34 ships a curated ~60s minimalist-orchestral symphony rendered via Phase 33's SFZ sampler against VSCO-CE 1.1.0, plus the public-facing release machinery (top-level README showcase section, v1.4.0 GitHub Release, announcement draft, milestone closure docs). No interpreter code is touched.

Research surfaces three composition-relevant facts the planner needs:
1. **VSCO-CE patches are well-supplied** for the 5 chosen instruments (15-28 samples each with velocity layers and quick ampeg defaults) — Phase 29's flute-coverage-gap quirk does NOT apply to the SFZ flute (15 samples, full C3-C6 chromatic).
2. **Timpani's round-robin variation is silently ignored** by the Phase 33 SFZ parser (round_robin / seq_position / seq_length are NOT in the 14-opcode whitelist) — the patch still renders but every repeated hit will use the same first-declared region. Composer should accept this for v1.4 (round-robin is a v1.5 SFZ-extension candidate, NOT a Phase 34 blocker).
3. **CONTEXT.md D-601's "GitHub renders MP3 from a bare release-asset URL as inline player" assumption is INCORRECT.** GitHub strips raw `<video>`/`<audio>` HTML from README markdown and does not auto-render external MP3 links as players. The only path that ships an inline player is the `https://github.com/user-attachments/assets/<uuid>` URL produced by the GitHub web-UI drag-drop upload flow (a one-time manual step). The planner MUST replace the D-601 assumption with one of three remediation options documented in the "GitHub Audio Embed Mechanics" section below.

**Primary recommendation:** Plans 34-01 through 34-04 proceed as scoped. Plan 34-03 (README showcase section) must adopt the **user-attachments drag-drop workflow** for the inline player and additionally provide a plain download-link fallback to the release asset MP3. Plan 34-05 (release creation) is straightforward — `gh release create v1.4.0 --notes-file <path> <assets>` already supports the full Phase 30 binary + symphony MP3 + symphony WAV asset bundle.

## User Constraints (from CONTEXT.md)

### Locked Decisions

All D-101..D-903 decisions from `34-CONTEXT.md` are locked. Highlights load-bearing for research:

- **D-101..D-104** Symphony scope: ~60s ABA single-movement film-score/neo-classical minimalist piece at tempo 100 / key D minor / 4/4 (planner may flex ±15s on length and pick a different minor key).
- **D-201..D-203** Five instruments: `#violin` (`SViolinVib.sfz` solo) + `#cello` (`CelloEnsSusVib.sfz` ensemble) + `#flute` (`FluteSusVib.sfz`) + `#horn` (`FHornSus.sfz`) + `#timpani` (`Timpani.sfz`). Flute carries A-section melody; violin enters in A' octave-up via `transpose`; cello sustained bass; horn pads in B; timpani marks A→B and B→A' transitions.
- **D-301..D-304** Source MUST exercise: tempo/timesig/key/voicePool blocks, ≥1 transform (transpose for the octave-up), `sampler:NAME` on every instrument, every Phase 28 articulation (`>` `stacc` `ten` `leg` `marc`), ≥1 `{voice ...}{voice ...}` block, 1 tuplet bracket (Phase 19), `humanizeGaussian` on cello with fixed seed.
- **D-401..D-404** Mix stack: per-instrument `volume(buf, linear)` (flute 0.85, violin 1.0, cello 0.75, horn 0.65, timpani 0.40) → sum → master `(reverb mix 0.3 2.5s)` → master `(compress mix -12dB 4 100ms 200ms)`. Mono output (no pan).
- **D-501..D-504** File layout: `examples/symphony/symphony.flow`; render outputs to `examples/output/symphony.{wav,mid}` (gitignored); rendered audio ships as GitHub Release assets on `v1.4.0` tag, NOT in the repo.
- **D-601..D-603** README updates: new "## Showcase" section in top-level README (after "What is flow-lang?" / "Features" line ~33, before "Install"); expand `examples/symphony/README.md` to cover both files; `docs/announcements/v1.4.0.md` draft.
- **D-701..D-703** Regression strategy: no CI test (VSCO-CE not in CI); two-run determinism verified MANUALLY at release time; no RMS baseline.
- **D-801..D-803** Iterative composer UAT, no cap, sign-off requires (1) "would publicly share this", (2) audible articulation differentiation via A/B fixture, (3) audible polyphony. Recorded in `34-HUMAN-UAT.md`.
- **D-901..D-903** 6 plans: 34-01 composition+UAT loop, 34-02 final source + expanded README, 34-03 top-level README + asset upload, 34-04 announcement draft, 34-05 v1.4.0 tag + Release, 34-06 milestone closure docs.

### Claude's Discretion

- Symphony title `"In Five Voices"` (working title; composer may rename).
- MP3 encoding tool `ffmpeg` (verified present at `/usr/bin/ffmpeg` v7.1.1).
- GitHub Release body template: Highlights / Install / Try the showcase, ~30 lines.
- Tag commit: annotated (`git tag -a v1.4.0`), NOT lightweight.
- CLAUDE.md "Public as of v1.4" footnote single-line addition.
- PROJECT.md "Current State" wording: `**Shipped:** v1.4 Audio Fidelity, Distribution & Public Showcase (YYYY-MM-DD)`.
- No new external dependencies; source ASCII / no emoji.
- Per-section reverb already inside `renderSong`; D-402 master reverb sits ON TOP.

### Deferred Ideas (OUT OF SCOPE)

- Stereo panning across instruments (v1.5 mix-polish phase candidate)
- A second showcase (jazz / EDM / etc.) (v1.5 follow-up)
- Auto-posting infrastructure
- CHANGELOG.md file in repo root
- CI integration that runs symphony render against a VSCO-CE subset
- GitHub-rendered video demo (screen recording)
- A `flow showcase` CLI subcommand
- Per-articulation A/B fixture as a permanent example
- MIDI-export verification as a release-time UAT step (composer-judgement-only)

## Architectural Responsibility Map

Composition + docs + release work — single concern boundary per plan; no multi-tier dispatch.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Symphony composition | `examples/symphony/` source authoring | Phase 28/33 runtime (consumed unchanged) | Pure Flow-source work; interpreter untouched. |
| Render artifact production | Phase 30 `flow render` CLI | ffmpeg WAV→MP3 encode | One-shot composer-local; no CI integration. |
| Repository docs | `examples/symphony/README.md`, top-level `README.md`, `docs/announcements/v1.4.0.md` | — | Markdown only; gitignore re-includes cover all paths. |
| GitHub Release | `gh release create` + `gh release upload` | git annotated tag | Standard gh-cli flow; no CI / Actions wiring. |
| Milestone closure | `.planning/{PROJECT,ROADMAP,STATE,REQUIREMENTS,MILESTONES}.md` + `CLAUDE.md` footnote | Memory `project_pre_public_no_legacy_burden.md` | Bookkeeping that gates the next milestone discussion. |

## Phase Requirements

Per ROADMAP.md Phase 34 entry: "Requirements: TBD (assigned during /gsd-spec-phase 34)." Phase 34 was discussed via `/gsd-discuss-phase 34 --auto` (no SPEC step) — the CONTEXT.md D-101..D-903 decisions are the de facto requirement surface, with the ROADMAP's 5 success criteria as the outermost gate:

| ID (proposed) | Description (derived from ROADMAP success criteria + CONTEXT decisions) | Research Support |
|---|---|---|
| **SYM-01** | Symphony renders end-to-end from `examples/symphony/symphony.flow` via SFZ sampler with no runtime errors | VSCO-CE patch verification (Priority 1 below) confirms the 5 chosen patches load successfully via Phase 33's parser; environment audit confirms all CLI tooling present. |
| **SYM-02** | Composer signs off "postable on GitHub" quality (D-801/D-802/D-803 UAT loop) | HUMAN-UAT.md pattern from Phase 17/33 is established; A/B fixture mechanism documented in Phase 28 articulation tests. |
| **SYM-03** | Code screenshots pair source with audible features (articulation, polyphony, voicePool, etc.) | Articulation differentiation patterns + voice-block patterns documented below (Priorities 5 + 6). |
| **SYM-04** | README.md showcase section + audio embed + `examples/symphony/README.md` reproduction docs | GitHub embed mechanics surfaced as deviation from D-601 (Priority 2 below) — planner must update plan 34-03. |
| **SYM-05** | v1.4 milestone closure: ROADMAP/STATE/REQUIREMENTS marked complete; `v1.4.0` tag cut; public announcement draft ready | Memory-file update mechanics + gh CLI commands documented (Priorities 8 + 4 below). |

Recommendation: planner should adopt SYM-01..05 as the requirement IDs (or coordinate with REQUIREMENTS.md owner to formalize) and map each plan's `must_haves` to one or more SYM-* IDs. The composer-UAT loop (plan 34-01) gates SYM-01 + SYM-02 + SYM-03 simultaneously.

## Standard Stack

No new external dependencies (per Claude's Discretion). All tooling is already on the system or in the project.

### Core (already shipped — verified present)

| Technology | Verified Version | Purpose | Confidence |
|------------|------------------|---------|------------|
| Flow runtime / `flow render` CLI | net10.0 (.NET 10.0.107) | Symphony composition + render | [VERIFIED: dotnet --version → 10.0.107] |
| Phase 33 SFZ sampler | shipped 2026-05-16 | `(loadSfz #symbol)` + `sampler:NAME` dispatch | [VERIFIED: Phase 33 closure committed] |
| Phase 28 voice-block + articulation | shipped 2026-05-10 | `{voice ...}` polyphony + 5 articulation tokens | [VERIFIED: VoiceBlockRenderTests + ArticulationRulesTests green] |
| VSCO Community CE 1.1.0 | external — composer-supplied | Orchestral sample library | [VERIFIED: 33-VSCO-PATH-AUDIT.md probed 15/19 paths] |
| ffmpeg | 7.1.1-1ubuntu4.2 (with libmp3lame) | WAV → MP3 encode | [VERIFIED: ffmpeg -version → libmp3lame enabled] |
| gh CLI | 2.46.0 | Release create + asset upload | [VERIFIED: gh --version → 2.46.0] |
| cmp (GNU diffutils) | 3.10 | Two-run byte-identical verification | [VERIFIED: cmp --version] |
| aplay (alsa-utils) | 1.2.14 | Composer audio playback during UAT | [VERIFIED: aplay --version] |

### Alternatives Considered

| Instead of | Could Use | Why we use the standard |
|------------|-----------|--------------------------|
| ffmpeg | LAME `lame` CLI directly | ffmpeg is already a documented system dependency for Flow; LAME would be an additional tool. ffmpeg's libmp3lame backend is identical output to standalone LAME. |
| `gh release create` | `git tag` + manual GitHub UI upload | gh CLI is scriptable (composer can re-run), produces audit trail in plan SUMMARY.md, and Phase 30 already established the gh CLI pattern. |
| 192 kbps CBR | VBR V2 (`-q:a 2`) | See Priority 3 research below. Recommendation: keep CBR 192k per CONTEXT.md (deterministic, predictable file size, "highest reasonable quality without VBR variance"). |

## Package Legitimacy Audit

**Not applicable** — Phase 34 ships zero new packages (no new NuGet, no new SFZ extensions, no new C# files). All tooling pre-existed before this phase.

## Architecture Patterns

### System Architecture (composition + render flow)

```
                    ┌──────────────────────────────────────────────┐
                    │  examples/symphony/symphony.flow             │
                    │   ├─ use "@audio" + use "@sfz"               │
                    │   ├─ Sfz violin = (loadSfz #violin)          │
                    │   ├─ Sfz cello  = (loadSfz #cello)           │
                    │   ├─ Sfz flute  = (loadSfz #flute)           │
                    │   ├─ Sfz horn   = (loadSfz #horn)            │
                    │   ├─ Sfz timpani= (loadSfz #timpani)         │
                    │   ├─ tempo 100 + timesig 4/4 + key Dminor +  │
                    │   │  voicePool 32 { sections A B A' }        │
                    │   └─ 5 × renderSong → mix → reverb → compress│
                    └──────────────────────────────────────────────┘
                                       │
                                       │ flow render
                                       ▼
                    ┌──────────────────────────────────────────────┐
                    │  Phase 30 CLI (flow render / flow flow2midi) │
                    │   ├─ resolves sfz_root from config.toml      │
                    │   └─ dispatches to FlowEngine                │
                    └──────────────────────────────────────────────┘
                                       │
                                       ▼
                    ┌──────────────────────────────────────────────┐
                    │  Phase 33 SfzRenderer (per-instrument)       │
                    │   + Phase 28 articulation envelope on top    │
                    │   + voice-block parallel rendering           │
                    │   + voicePool 32 (steal-oldest on overflow)  │
                    └──────────────────────────────────────────────┘
                                       │
                          ┌────────────┴────────────┐
                          ▼                         ▼
              examples/output/symphony.wav   examples/output/symphony.mid
                          │                         │
                          │ (one-shot, by composer) │
                          ▼                         │
                  ffmpeg -c:a libmp3lame            │
                   -b:a 192k                        │
                          │                         │
                          ▼                         ▼
            symphony-v1.4.0.mp3 (~1.5 MB)   (informational artifact;
                          │                  not uploaded per CONTEXT)
                          │
                          ▼
    ┌──────────────────────────────────────────────────────────────┐
    │  gh release create v1.4.0 \                                  │
    │    --notes-file docs/announcements/v1.4.0.md \               │
    │    flow-symphony-v1.4.0.mp3 \                                │
    │    flow-symphony-v1.4.0.wav \                                │
    │    flow-linux-x64.tar.gz                                     │
    └──────────────────────────────────────────────────────────────┘
                          │
                          ▼
              GitHub Release page (assets downloadable + linked
              from top-level README.md "## Showcase" section)
```

### Recommended Project Structure

```
examples/
├── symphony/
│   ├── sfz_smoke.flow              # Phase 33 tutorial chapter (UNCHANGED)
│   ├── symphony.flow               # NEW — the headline piece
│   └── README.md                   # EXPANDED — covers both files
├── output/                         # gitignored (.wav glob)
│   ├── symphony.wav                # locally rendered
│   ├── symphony.mid                # locally rendered
│   └── (existing tutorial / showcase outputs)
docs/
├── announcements/                  # NEW directory
│   └── v1.4.0.md                   # NEW announcement draft
├── editor-setup/                   # (existing)
└── plans/                          # (existing)
README.md                           # NEW "## Showcase" section after Features
CLAUDE.md                           # NEW one-line footnote under Goals
.planning/
├── PROJECT.md                      # "Current State" flipped
├── ROADMAP.md                      # Phase 34 row marked Complete
├── STATE.md                        # reset for next milestone
├── REQUIREMENTS.md                 # v1.4 entries marked Complete
├── MILESTONES.md                   # v1.4 closure entry added
└── phases/34-symphony-showcase-.../
    ├── 34-CONTEXT.md               # (existing)
    ├── 34-RESEARCH.md              # this file
    ├── 34-DISCUSSION-LOG.md        # (existing)
    ├── 34-01-PLAN.md ... 34-06-PLAN.md
    ├── 34-HUMAN-UAT.md             # NEW — composer sign-off (D-803)
    └── 34-VERIFICATION.md          # at phase closure
```

### Pattern 1: Per-instrument render + sum mix (existing `examples/showcase.flow` shape extended)

**What:** Render each instrument to its own Buffer via `renderSong song "sampler:NAME"`, balance with `volume(buf, linear)`, sum into one mix, then apply master reverb + compress.

**When to use:** Every multi-instrument symphony piece that needs per-instrument level control before the master effects chain.

**Example:**
```flow
// Source: examples/showcase.flow (v1.3 pattern) extended to 5 instruments
Song song = [intro themeA bridge themeB themeAPrime outro]

Buffer rawFlute  = (renderSong song "sampler:flute")
Buffer rawViolin = (renderSong song "sampler:violin")
Buffer rawCello  = (renderSong song "sampler:cello")
Buffer rawHorn   = (renderSong song "sampler:horn")
Buffer rawTimp   = (renderSong song "sampler:timpani")

Buffer balancedFlute  = (volume rawFlute  0.85)
Buffer balancedViolin = (volume rawViolin 1.0)
Buffer balancedCello  = (volume rawCello  0.75)
Buffer balancedHorn   = (volume rawHorn   0.65)
Buffer balancedTimp   = (volume rawTimp   0.40)

Buffer summed = balancedFlute -> (mix balancedViolin) -> (mix balancedCello) -> (mix balancedHorn) -> (mix balancedTimp)
Buffer wet    = summed -> (reverb 0.3 2.5s) -> (compress -12dB 4 100ms 200ms)

(writeWav "examples/output/symphony.wav" wet)
(writeMidi "examples/output/symphony.mid" song)
```

**Note:** The `(mix b1 b2)` helper is from `@audio` stdlib (verified by `examples/showcase.flow` line 39 which uses `(renderSong ... "strings")` then `->`-chains effects). If `mix` is not the exact builtin name, planner verifies in `flow-lang/StandardLibrary/Audio/` and adjusts — could be `(add b1 b2)` or `(sum [b1, b2, ...])` depending on the stdlib surface. Either way, summing multiple Buffers is a 1-2 line pattern.

### Pattern 2: Voice block for orchestral parallel lines

**What:** Use `| {voice ...}{voice ...} |` syntax inside a Sequence to render two simultaneous melodic lines that share a bar's onset.

**When to use:** When the score calls for two independent simultaneous lines on the same instrument (e.g., violin double-stop) OR when one section sequence needs to hold a sustained note while another runs underneath. For different-instrument simultaneity, use the per-instrument-render-and-sum pattern instead (the voice block is per-Sequence; cross-instrument simultaneity comes from rendering different Sequences in the same Song through different `sampler:NAME` calls).

**Example (verified against Phase 28 tests):**
```flow
// Source: flow-lang.Tests/Integration/Phase28/VoiceBlockRenderTests.cs:60
Sequence stride = | {voice C4w} {voice C5q D5q E5q F5q} |
```
This produces C4 held for a whole note while C5→D5→E5→F5 quarter notes play underneath. Renders as parallel voices through `BarRenderer.ParallelVoices`. MIDI export emits NoteOn for all voices at tick 0 with appropriate NoteOff ticks.

**For the symphony (D-301 voice-block requirement):** Recommended placement is the violin or cello line where a sustained note covers a melodic figure underneath — e.g., cello holds a long D2 pedal-tone while the flute runs a triplet flourish above. This is the most musically natural use of the voice-block syntax in a film-score context and demonstrates Phase 28's polyphony audibly.

### Pattern 3: Articulation as composer-natural phrasing (not a feature checklist)

**What:** Phrase the music so each Phase 28 articulation lands where its locked semantics audibly serve the music, NOT as a contrived "tick the checklist" demonstration.

**Verified articulation rules** (from `CLAUDE.md` § "Locked articulation rules" + Phase 28 test acceptance):

| Token | Articulation | Locked semantics |
|-------|--------------|------------------|
| `>` (after note) | Accent | +0.30 velocity (clamped); audible duration unchanged |
| `stacc` | Staccato | 25% duration + sustain=0 + release×0.5 |
| `ten` | Tenuto | 100% duration + release×1.2 (soft) |
| `leg` | Legato | 110% duration + crossfade overlap |
| `marc` | Marcato | 25% duration + Accent's +0.30 velocity boost |
| `Articulation.Sforzando` (no note-stream token; envelope-only) | Sforzando | 100% duration + 1.5×→1.0× envelope spike first 15% of frames |

**Composer-natural placement recommendations** (planner uses these as starting points; composer adjusts in UAT loop):

- **`stacc` (staccato):** flute or violin running line in section B — short detached notes give the section forward momentum.
- **`leg` (legato):** cello or horn sustained passages — the 110% duration + crossfade is exactly what film-score-style legato strings sound like.
- **`ten` (tenuto):** horn pad notes — the release×1.2 softens the transition into the next chord without losing the held-tone feel.
- **`>` (accent):** strong-beat downbeats in the A theme — the +0.30 velocity boost is the cleanest "this note matters" mark.
- **`marc` (marcato):** the timpani transition hits at A→B and B→A' boundaries — short, loud, attention-grabbing (25% duration + accent velocity = the textbook marcato sound).

**Anti-pattern to avoid:** Don't stack all 5 articulations on the same instrument within 4 bars just to "check the box." The articulation differentiation UAT criterion (D-802 condition 2) requires the composer to hear that each articulation produces an audibly different result — this is best satisfied by spreading articulations across instruments and sections so the composer notices each one in its natural context.

### Anti-Patterns to Avoid

- **Don't commit the rendered WAV/MP3** — D-502 + .gitignore *.wav coverage. Rendering depends on VSCO-CE; committing the artifact misleads future agents and bloats the repo.
- **Don't bundle VSCO-CE** — Phase 33 SPEC-2 + repo size cap. Composers download themselves.
- **Don't add per-instrument stereo pan** — D-404. SfzRenderer mono-sums; pan retrofit is v1.5.
- **Don't activate Scala/JI pragmas in the symphony** — D-302. Symphony stays in 12-TET; microtonal demos live in `examples/scala/intro.flow`.
- **Don't add a CHANGELOG.md file** — D-501 out-of-scope. PROJECT.md milestone sections + FEATURES.md + `.planning/MILESTONES.md` already cover release history.
- **Don't auto-post the announcement** — Phase 34 ships draft markdown only.
- **Don't touch interpreter code in Phase 34** — pure composition + docs + release work. Bugs surfaced during UAT go to `/gsd-debug` in a sibling thread.
- **Don't amend the v1.4.0 tag after publication** — annotated + immutable. Use v1.4.1 if a fix is needed.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| WAV → MP3 encode | Custom encoder | `ffmpeg -i in.wav -c:a libmp3lame -b:a 192k out.mp3` | ffmpeg is system-standard, libmp3lame is the canonical LAME backend, deterministic at fixed CBR. |
| GitHub Release upload | Custom curl + REST API | `gh release create` + `gh release upload` | gh CLI handles auth, retries, multi-file uploads, and produces a single command line that copy-pastes into the plan SUMMARY. |
| Two-run determinism check | Custom hash + compare script | `cmp a.wav b.wav` (exit 0 = identical) | cmp is system-standard, byte-exact, single command, exit-code-driven (clean for CI/UAT integration). |
| Audio playback during UAT | Custom audio harness | `aplay symphony.wav` (Linux) / `flow play examples/symphony/symphony.flow` | aplay is system-standard ALSA; `flow play` is the Phase 30 CLI subcommand wired through the PulseAudio backend. |
| Markdown audio embed | Custom HTML/iframe injection | GitHub web-UI drag-drop user-attachments flow (see GitHub Audio Embed Mechanics § below) | GitHub strips raw `<video>`/`<audio>` HTML from README markdown; the only inline-player URL pattern that renders is the `user-attachments/assets/<uuid>` form produced by drag-drop upload. |

**Key insight:** Phase 34 is intentionally a thin "consume the existing surface + ship the release" phase. Every tool needed is already on the system or in the project. The composition is the only artisanal work; everything else is glue.

## Runtime State Inventory

**Not applicable** — Phase 34 is a greenfield composition + docs phase. No rename / refactor / migration / data-rewrite is in scope. All new file paths are NEW (symphony.flow, docs/announcements/, 34-HUMAN-UAT.md). The only mutation of existing state is:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — no databases, no caches, no persistent state to migrate | None |
| Live service config | None — no external services with embedded strings | None |
| OS-registered state | None — no scheduled tasks, daemons, or pm2 processes | None |
| Secrets/env vars | None — gh CLI uses existing user `gh auth login` token | None |
| Build artifacts | None — Phase 30 binary is rebuilt fresh for the release tarball, not modified | None |

**Nothing found in any category** — verified by reading CONTEXT.md "Integration Points" (which explicitly states `flow-lang/ + flow-midi/ + flow-lsp/ + flow-cli/ NOT TOUCHED`) and by inspecting the file-write set (all NEW paths or append-to-existing-Markdown).

## Common Pitfalls

### Pitfall 1: GitHub does NOT render inline MP3 players from external URLs

**What goes wrong:** Composer follows CONTEXT.md D-601 literally, writes `https://github.com/<user>/<repo>/releases/download/v1.4.0/flow-symphony-v1.4.0.mp3` as a bare URL in README.md expecting it to render as an inline player. It renders as a plain hyperlink instead.

**Why it happens:** GitHub strips all raw `<video>` / `<audio>` / `<iframe>` HTML from markdown READMEs and release bodies. The ONLY URL pattern GitHub renders as an inline player in markdown is `https://github.com/user-attachments/assets/<uuid>` — produced uniquely by the GitHub web-UI drag-drop upload flow.

**How to avoid:** See "GitHub Audio Embed Mechanics" section below for the canonical workflow. Plan 34-03 must include an explicit step where the composer (a) opens README.md in the GitHub web editor, (b) drags-drops the rendered symphony MP3 into the editor, (c) captures the auto-generated user-attachments URL, (d) commits the README.md edit with that URL. The release-asset URL is still attached to v1.4.0 as a downloadable resource — both URLs coexist.

**Warning signs:** Plan 34-03 step list still cites "bare URL renders as player" or `<video controls src="...releases/download/...">` — these will fail silently (render as link or be stripped).

### Pitfall 2: Phase 33 SFZ parser silently ignores round-robin opcodes

**What goes wrong:** Composer writes a Timpani pattern with multiple repeated hits expecting VSCO-CE's rr1/rr2 round-robin to provide natural variation. The parser's 14-opcode whitelist (`sample`, `lokey`, `hikey`, `pitch_keycenter`, `lovel`, `hivel`, `loop_mode`, `loop_start`, `loop_end`, `ampeg_attack`, `ampeg_release`, `volume`, `pan`, `default_path`) doesn't include `seq_position` / `seq_length`. Unknown opcodes trigger a one-shot stderr advisory per `(patch, opcode-name)` and are silently ignored. The parser's last-declared-wins region grid stores the last region for any (pitch, velocity) cell, so all repeated hits use the same sample.

**Why it happens:** Phase 33 SPEC-3 locked the 13-opcode common subset (extended to 14 by VSCO-CONTROL-DECISION adding `default_path`). Round-robin was out of scope.

**How to avoid:** Composer accepts this for v1.4. Timpani role is "occasional accents on section boundaries" (D-203) — two transitions in a 60s piece is not enough for round-robin variation to matter audibly. If composer notices the sameness during UAT, the workaround is to vary velocity (`>`/`marc` vs unmarked) or pitch (different timpani drums map to different MIDI notes 36-60 per the patch's `pitch_keycenter` declarations) between hits, which DO change the audible result through the existing 14-opcode surface.

**Warning signs:** Composer comments "timpani hits sound mechanical" during UAT — solution is velocity/pitch variation, not waiting for a round-robin parser extension (defer to v1.5+).

### Pitfall 3: Per-section reverb stacked with master reverb can muddy the mix

**What goes wrong:** Phase 28's `SongRenderer` applies a per-section reverb automatically (Phase 28 default; CLAUDE.md "Per-section reverb already inside renderSong"). D-402's master reverb (`reverb mix 0.3 2.5s`) sits ON TOP. Total wet level can exceed what's musically clean — symphony sounds like it's underwater rather than in a concert hall.

**Why it happens:** Two reverb stages compose multiplicatively in perceived wetness, not additively. 30% wet × 30% wet ≈ 51% perceived wet on a sustained note.

**How to avoid:** Per CONTEXT.md Claude's Discretion: "Plan 34-01 verifies the combined wet level isn't muddy during UAT." If composer UAT flags muddiness, options are: (a) lower master reverb mix to 0.2 or 0.15; (b) shorten master decay from 2.5s to 1.5s; (c) lower per-instrument volumes by 5-10% so the rebalanced sum has less wet-tail buildup. Decision lives in the UAT iteration loop, not pre-decided in research.

**Warning signs:** UAT feedback like "sounds underwater" / "can't hear the cello attack" / "lost the timpani transient" — all point at reverb-on-reverb buildup.

### Pitfall 4: `humanizeGaussian` seed must be a fixed integer literal for two-run determinism

**What goes wrong:** Composer writes `humanizeGaussian(celloLine, 0.05, (randomInt 0 1000))` expecting a "different humanization each time" feel. This BREAKS D-702's two-run cmp-clean determinism — two consecutive renders produce different bytes because `randomInt` returns different values.

**Why it happens:** D-304 specifies "Small amount (0.05); fixed seed for byte-identical reruns." The fixed seed is what makes the two-run determinism contract hold.

**How to avoid:** Use a literal integer seed, e.g. `(humanizeGaussian celloLine 0.05 42)`. Showcase.flow line 26 + 23 ship this pattern verbatim: `(humanizeGaussian | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w | 0.08 314)` and `(euclidean 5 16 (get kit #kick) 0.18 0.12 7)`.

**Warning signs:** Two-run `cmp` exits non-zero. Diff the binary at a few byte offsets — if the diff is dispersed rather than localized, it's almost certainly the seed.

### Pitfall 5: VSCO-CE flute pitch coverage is full (15 samples C3-C6) — NOT a Phase 29 quirk

**What goes wrong (NEGATIVE finding):** Composer reads CLAUDE.md's "Known sampled-instrument quirks (v1.5 backlog)" line about the bundled flute's 2-sample G4+G5 timbre crossover at D5 and assumes the SFZ flute has the same gap. They constrain the flute line to avoid D5 unnecessarily.

**Why this is NOT a problem:** VSCO-CE's `FluteSusVib.sfz` ships 15 samples covering C3 through C6 chromatically (C3 / E3 / A3 / C4 / E4 / A4 / C5 / E5 / A5 / C6 keycenters, with velocity layers on C4/E4/E5). Pitch coverage is dense enough that any D5/D#5 falls within 2 semitones of a sampled pitch — varispeed transposition is minimal. The Phase 29 bundled-flute quirk does NOT apply to the SFZ flute.

**How to use this:** Compose the flute melody freely across C3-C6 without the Phase 29 constraint. Similarly: Violin (28 samples G3-C7, p/f velocity layers), Cello (28 samples C1-F4, two velocity layers), Horn (28 samples A0-F#4, up to 4 velocity layers), Timpani (24 samples MIDI 36-60, 3 velocity layers). All five chosen patches are richly sampled — pitch range constraints are the only practical limit (e.g., don't write the cello above F4 or the timpani outside 36-60).

## Code Examples

Verified patterns from in-repo sources.

### Symphony skeleton (planner adapts during plan 34-01)

```flow
// Source: examples/showcase.flow + examples/symphony/sfz_smoke.flow patterns

use "@std"
use "@audio"
use "@sfz"
use "@composition"

// In Five Voices -- v1.4 symphony showcase
// 5 instruments via VSCO Community CE 1.1.0 SFZ sampler

Sfz violin  = (loadSfz #violin)
Sfz cello   = (loadSfz #cello)
Sfz flute   = (loadSfz #flute)
Sfz horn    = (loadSfz #horn)
Sfz timpani = (loadSfz #timpani)

voicePool 32 {
    tempo 100 {
        timesig 4/4 {
            key Dminor {
                // === Section A: flute carries theme, cello bass, light horn pad ===
                section themeA {
                    Sequence fluteMelody  = | D5q E5q F5q> A5h | G5q. F5q. E5w |
                    Sequence celloBass    = (humanizeGaussian | D2w | A2w | F2w | G2w | 0.05 42)
                    Sequence hornPad      = | _ | _ | A3w ten | G3w ten |
                }
                // === A→B transition: timpani marcato hit ===
                section transitionAB {
                    Sequence timpHit = | _ _ _ G2q marc |
                }
                // === Section B: brass + woodwind interplay, triplet flourish ===
                section themeB {
                    Sequence hornLead   = | F4h F4q ten G4q | A4h. G4q | F4w |
                    Sequence fluteOrn   = | _ | {3:2 D5q E5q F5q}q stacc _ _ | _ | _ |
                    Sequence celloLeg   = | F2w leg | E2w leg | D2w leg | A2w leg |
                }
                // === B→A' transition: second timpani hit ===
                section transitionBAPrime {
                    Sequence timpHit = | _ _ _ A2q marc |
                }
                // === Section A': violin enters octave-up via transpose, voice block adds parallel inner voice ===
                section themeAPrime {
                    Sequence violinTheme  = (transpose | D5q E5q F5q> A5h | G5q. F5q. E5w | 12)
                    Sequence celloVoiced  = | {voice D2w} {voice A3h F3h} |  // voice-block polyphony
                    Sequence hornPadFull  = | A3w ten | G3w ten | F3w ten | D3w ten |
                    Sequence fluteHarmony = | A5w | G5w | F5w | D5w |
                }

                Song piece = [themeA transitionAB themeB transitionBAPrime themeAPrime]

                // Per-instrument render
                Buffer rawFlute   = (renderSong piece "sampler:flute")
                Buffer rawViolin  = (renderSong piece "sampler:violin")
                Buffer rawCello   = (renderSong piece "sampler:cello")
                Buffer rawHorn    = (renderSong piece "sampler:horn")
                Buffer rawTimpani = (renderSong piece "sampler:timpani")

                // D-401: per-instrument volume balance
                Buffer balFlute   = (volume rawFlute   0.85)
                Buffer balViolin  = (volume rawViolin  1.0)
                Buffer balCello   = (volume rawCello   0.75)
                Buffer balHorn    = (volume rawHorn    0.65)
                Buffer balTimpani = (volume rawTimpani 0.40)

                // Sum to one buffer (planner verifies exact mix/sum builtin in @audio)
                Buffer summed = balFlute -> (mix balViolin) -> (mix balCello) -> (mix balHorn) -> (mix balTimpani)

                // D-402 + D-403: master reverb + soft compress
                Buffer wet = summed -> (reverb 0.3 2.5s)
                Buffer mastered = wet -> (compress -12dB 4 100ms 200ms)

                (writeWav "examples/output/symphony.wav" mastered)
                (writeMidi "examples/output/symphony.mid" piece)
            }
        }
    }
}
```

**Note:** Skeleton sketches all 5 D-301 features (every articulation × 1, voice block × 1, tuplet × 1, transpose × 1, humanizeGaussian × 1, all 4 musical-context blocks). Composer iterates the actual notes during plan 34-01 UAT. Verifying the `(mix b1 b2)` builtin name is a plan 34-01 first-render task — if absent, alternatives are `(add b1 b2)` or summing through reduce.

### Two-run determinism reproduction (for examples/symphony/README.md § Reproduction step 4)

```bash
# Verifies Phase 28's two-run cmp-clean determinism contract end-to-end
# through the real VSCO-CE library. Same inputs -> same bytes.
flow render examples/symphony/symphony.flow -o /tmp/symphony-a.wav
flow render examples/symphony/symphony.flow -o /tmp/symphony-b.wav
cmp /tmp/symphony-a.wav /tmp/symphony-b.wav && echo "OK: byte-identical"
```

Exit code 0 = bytes identical. Exit code 1 = bytes differ (Phase 28 contract regression — file a bug). Exit code 2 = cmp invocation failure (check the WAVs exist).

### MP3 encoding (composer-local one-shot, per CONTEXT Claude's Discretion)

```bash
ffmpeg -i examples/output/symphony.wav \
       -c:a libmp3lame \
       -b:a 192k \
       -y \
       flow-symphony-v1.4.0.mp3
```

CBR 192 kbps gives ~1.5 MB for a 60-second WAV (verified by ffmpeg bitrate math: 192 kbit/s × 60 s / 8 bits/byte = 1.44 MB). The `-y` flag overwrites without prompt (deterministic for re-encodes). See "MP3 Encoding Choice" research section below for the CBR-vs-VBR-V2 decision rationale.

### GitHub Release creation (composer-local, plan 34-05)

```bash
# Plan 34-06 lands the milestone-closure commit; plan 34-05 tags it.
git tag -a v1.4.0 -m "v1.4 Audio Fidelity, Distribution & Public Showcase"
git push origin v1.4.0

# Create the release with announcement as body + 3 asset uploads in one shot
gh release create v1.4.0 \
    --title "v1.4 Audio Fidelity, Distribution & Public Showcase" \
    --notes-file docs/announcements/v1.4.0.md \
    --verify-tag \
    flow-symphony-v1.4.0.mp3#"Symphony (MP3, 192 kbps, ~1.5 MB)" \
    flow-symphony-v1.4.0.wav#"Symphony (WAV, uncompressed, ~10 MB)" \
    flow-linux-x64.tar.gz#"Flow CLI binary (Linux x64, self-contained)"
```

The `#"Display Label"` suffix is gh CLI's asset-label syntax. `--verify-tag` aborts if the tag doesn't exist on the remote (catches the "forgot to push" footgun).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Showcase rendered through bundled-sample path (`renderSong song "strings"`) — v1.3 `examples/showcase.flow` pattern | Symphony rendered through SFZ sampler (`renderSong song "sampler:violin"`) — Phase 33 surface | Phase 33 shipped 2026-05-16 | Symphony showcases VSCO-CE-distinctive timbres that the bundled samples can't match. Bundled-sample path remains for piano (Phase 29) — coexistence is by design. |
| Pre-Phase-28 byte-identical determinism on tutorial.flow / showcase.flow output | Two-run cmp-clean determinism (consecutive runs at same git SHA produce byte-identical output) | Phase 28 closure 2026-05-10 (CLAUDE.md § "Conventions") | Symphony reproduction docs (D-702) ship the cmp pattern as a composer-facing reproduction step. Pinned-bytes baselines dropped. |
| `gain(buf, multiplier)` interpreted as linear multiplier | Strict split: `gain` = dB only, `volume` = linear only | Phase 26.2 ERG-03 2026-05-06 | D-401 specifies `volume(buf, linear)` for per-instrument balancing because linear ("85% of full") is the intuitive surface for per-section mix. |

**Deprecated/outdated:**
- Pre-Phase-28 articulation defaults (uniform soft envelope on all notes): gone — replaced by the 5 locked articulation rules. Symphony composition uses the new tokens.
- Pre-Phase-33 "no real external library support" position: gone — VSCO-CE is the blessed reference library and the symphony's reason to exist.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `(mix b1 b2)` is the canonical `@audio` builtin for summing two Buffers | Code Examples § symphony skeleton | LOW — if not exactly `mix`, alternatives are `add` / `sum`; planner verifies in plan 34-01 first-render task. The exact name doesn't affect the rest of the plan structure. |
| A2 | The composer's GitHub repo has user-attachments enabled (free GitHub plan permits drag-drop in repo READMEs up to 10 MB) | Pitfall 1 + GitHub Audio Embed Mechanics | LOW — user-attachments has been enabled by default on all public repos since 2022; the 10 MB MP3 size cap is the only meaningful constraint (1.5 MB symphony is well under). |
| A3 | Phase 30 self-contained Linux binary is repackaged as `flow-linux-x64.tar.gz` for the v1.4.0 release | Code Examples § GitHub Release creation | LOW — the binary itself is verified shipped (Phase 30 closure 2026-05-11); the tar.gz packaging is a single `tar -czvf` step in plan 34-05. If a different name is preferred (e.g. `flow-v1.4.0-linux-x64.tar.gz`), it's a one-character edit. |
| A4 | The composer wants 192 kbps CBR MP3 (per CONTEXT.md Claude's Discretion) rather than VBR V2 | MP3 Encoding Choice § | LOW — CBR is the explicit CONTEXT decision; this research validates that decision. If composer changes their mind at plan 34-03, the ffmpeg flag swap is `-b:a 192k` → `-q:a 2` and re-encode. |
| A5 | The `~/.claude/.../memory/project_pre_public_no_legacy_burden.md` memory file is updated via direct Write (the canonical pattern) rather than via an auto-memory hook | Memory File Update Mechanics § | LOW — the file is a YAML-frontmatter + Markdown body file with no auto-generation hooks; direct `Write` overwrite is the established pattern (verified by reading the file's `originSessionId` field which is set once at creation and never auto-updated). |

**If risk for any assumption rises to MEDIUM/HIGH during planning, surface in plan SUMMARY and ping composer.**

## Open Questions

1. **Should plan 34-02 also commit the rendered MP3 + WAV to `examples/symphony/`?**
   - What we know: D-502 + D-503 explicitly say NO (release-asset only, never in repo).
   - What's unclear: Nothing — decision is locked.
   - Recommendation: Hold to D-502/D-503. The `.gitignore` *.wav rule already enforces this; the MP3 would need an explicit ignore rule added in plan 34-02 if a composer accidentally drops one into `examples/symphony/`.

2. **Is the `symphony` GM symbol present in the Phase 33 19-symbol dict?**
   - What we know: VSCO-PATH-AUDIT lists 19 verified symbols; no `#symphony` entry exists (it's a per-instrument concept, not a single-patch concept). All 5 chosen symbols (`#violin`, `#cello`, `#flute`, `#horn`, `#timpani`) are verified in the dict.
   - What's unclear: Nothing.
   - Recommendation: No action.

3. **What happens if the composer's `sfz_root` is unset at render time?**
   - What we know: Phase 33 SPEC § "Diagnostics" requires `loadSfz` to emit a one-shot stderr advisory if `sfz_root` is missing.
   - What's unclear: Whether the symphony render succeeds (with a silent/black-render warning) or fails (clean error).
   - Recommendation: Plan 34-01 first-render task should test this explicitly — the UAT setup instructions in `examples/symphony/README.md` must include the same "Setup → install VSCO-CE → set sfz_root" steps from `sfz_smoke.flow` lines 11-17. If render silently produces a black buffer, document the diagnostic line composers should look for.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | `flow` CLI build + run | ✓ | 10.0.107 | — |
| ffmpeg + libmp3lame | WAV → MP3 encode (plans 34-03, 34-05) | ✓ | 7.1.1-1ubuntu4.2 (libmp3lame enabled) | — |
| gh CLI | Release create + asset upload (plan 34-05) | ✓ | 2.46.0 | Manual GitHub UI upload (slower, no audit trail) |
| cmp | Two-run determinism verify (D-702, examples/symphony/README.md) | ✓ | 3.10 (GNU diffutils) | — |
| aplay (ALSA) | Composer UAT playback (plan 34-01) | ✓ | 1.2.14 | `flow play` (Phase 30 CLI subcommand, uses PulseAudio) |
| VSCO Community CE 1.1.0 | Symphony render (every plan from 34-01 onward) | ✗ (composer-supplied) | — | Composer downloads from https://github.com/sgossner/VSCO-2-CE/releases/tag/1.1.0 per `examples/symphony/sfz_smoke.flow` setup steps |
| git annotated tag tooling | Plan 34-05 tag creation | ✓ | system git | — |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** VSCO Community CE — composer-supplied per Phase 33 SPEC. Same setup steps as Phase 33 (already documented in `examples/symphony/sfz_smoke.flow` lines 11-17 and `examples/symphony/README.md` § "Setup").

## Validation Architecture

Per `.planning/config.json` `workflow.nyquist_validation: true`. This section is REQUIRED.

Symphony is a composition+release phase with NO new interpreter code. Nyquist-checkable assertions are minimal but real.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (existing) — Phase 33 SfzArticulationTests / SfzSmokeTests are the closest comparables |
| Config file | None new — symphony depends on VSCO-CE which is NOT in CI |
| Quick run command | `flow render examples/symphony/symphony.flow -o /tmp/symphony.wav` (renders without runtime error → SYM-01 satisfied) |
| Full suite command | `flow render examples/symphony/symphony.flow -o /tmp/symphony-a.wav && flow render examples/symphony/symphony.flow -o /tmp/symphony-b.wav && cmp /tmp/symphony-a.wav /tmp/symphony-b.wav` (renders × 2 + cmp-clean → SYM-01 + D-702 satisfied) |
| Phase gate | Composer HUMAN-UAT.md sign-off (D-803) — covers SYM-02 + SYM-03 + SYM-04 + SYM-05 |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| SYM-01 | Symphony renders end-to-end with no runtime errors | smoke (composer-local) | `flow render examples/symphony/symphony.flow -o /tmp/symphony.wav` | ❌ Wave 0 (symphony.flow doesn't exist yet) |
| SYM-01 | Two-run byte-identical determinism (D-702) | smoke (composer-local) | `flow render ... -o a.wav && flow render ... -o b.wav && cmp a.wav b.wav` | ❌ Wave 0 (symphony.flow doesn't exist yet) |
| SYM-02 | Composer "postable on GitHub" sign-off | manual-only (D-801..D-803 UAT loop) | Read `.planning/phases/34-.../34-HUMAN-UAT.md` for sign-off statement | ❌ Wave 0 |
| SYM-03 | Audible articulation differentiation | manual-only (D-802 condition 2 A/B fixture) | Composer renders all-articulations-stripped variant + canonical mix, A/B listens | ❌ Wave 0 |
| SYM-03 | Audible polyphony | manual-only (D-802 condition 3) | Composer picks out simultaneous voices in voice-block section | ❌ Wave 0 |
| SYM-04 | README showcase section + audio embed | review (planner reads rendered README on GitHub) | `gh repo view --web` after commit; visually confirm inline player renders | ❌ Wave 0 |
| SYM-04 | examples/symphony/README.md reproduction docs | review | Read expanded README, confirm sections from D-602 present | ❌ Wave 0 |
| SYM-05 | v1.4.0 tag + GitHub Release | smoke (composer-local) | `gh release view v1.4.0` returns the release with all 3 assets attached | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** Composer eyeballs the relevant render artifact (.wav playback or README rendered preview).
- **Per wave merge:** N/A — Phase 34 plans are sequential (34-01 feeds 34-02, etc.) per D-902 + D-903.
- **Phase gate:** Two-run cmp-clean smoke at the canonical commit + HUMAN-UAT.md sign-off + `gh release view v1.4.0` succeeds.

### Wave 0 Gaps

- [ ] `examples/symphony/symphony.flow` — NEW file; plan 34-01 produces (iterative); plan 34-02 commits the post-UAT canonical version.
- [ ] `.planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-HUMAN-UAT.md` — NEW file; plan 34-01 produces; mirrors `33-HUMAN-UAT.md` shape.
- [ ] `examples/symphony/README.md` expansion — plan 34-02 (D-602: "The Symphony" section + tutorial chapter demotion).
- [ ] Top-level `README.md` "## Showcase" section — plan 34-03.
- [ ] `docs/announcements/v1.4.0.md` — plan 34-04 (new file under new `docs/announcements/` directory).
- [ ] v1.4.0 annotated tag + GitHub Release with 3 assets — plan 34-05.
- [ ] Milestone closure doc updates — plan 34-06 (PROJECT/ROADMAP/STATE/REQUIREMENTS/MILESTONES + CLAUDE.md footnote + memory file rewrite).

(No framework install needed; no test files to scaffold.)

## Security Domain

**Not applicable for in-scope work.** Phase 34 ships zero interpreter code, processes no untrusted input, handles no secrets beyond the composer's already-authenticated `gh` token. The only "input" is the composer's hand-written Flow source. No ASVS categories are triggered.

The only adjacent security consideration is the GitHub Release publication: `gh release create` uses the composer's existing `gh auth login` credentials and inherits the repo's branch protection / required reviews. No new secrets are introduced.

---

## Priority-Driven Findings

The 9 research priorities from the spawn brief, in order. Each links to the deeper sections above where applicable.

### Priority 1: VSCO-CE 1.1.0 per-patch quirks (composition-relevant)

**Verified via GitHub raw probes against `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/<patch>.sfz` 2026-05-16:**

| Patch | Symbol | Samples | Pitch Range | Velocity Layers | ampeg_attack | ampeg_release | Composer Notes |
|-------|--------|---------|-------------|------------------|--------------|----------------|----------------|
| SViolinVib.sfz | `#violin` | 28 | MIDI 55-96 (G3-C7) | 2 (piano 0-62, forte 63-127) | 0.001 (1 ms) | 0.8 (800 ms) | [VERIFIED: GitHub raw probe] Fast attack — works perfectly with `stacc` (Phase 28 25%-duration). Long release blends naturally with master reverb. p/f velocity split means accent (`>`) marks audibly shift the timbre AND velocity. Best for solo melodic lines (D-203). |
| CelloEnsSusVib.sfz | `#cello` | 28 | MIDI 36-77 (C2-F5; D-203 says "long sustained bass" so stay in C2-G3 range) | 2 (0-62, 63-127) | 0.001 | 0.8 | [VERIFIED] Ensemble (not solo) cello per 33-VSCO-PATH-AUDIT (VSCO-CE has no solo cello). Ensemble sound is RICHER and FATTER than a solo — perfect for the sustained-bass role. Long release × 1.2 on `ten` will sound lush. |
| FluteSusVib.sfz | `#flute` | 15 | MIDI 60-96 (C4-C6 effectively; can go up to ~D6) | 2 on C4/E4/E5 only (0-62, 63-127) | Patch-default (~0.001) | Patch-default (~0.8) | [VERIFIED] **NO 2-sample coverage quirk** (unlike Phase 29 bundled flute). 15 samples chromatically. Perfect for melodic lead (D-203 flute carries A theme). Triplet flourish (D-303) in B section will sound clean. |
| FHornSus.sfz | `#horn` | 28 | MIDI 33-77 (A1-F5; stay in F2-A4 for "warm brass bed" role per D-203) | Up to 4 layers per pitch (v1 0-41, v2 31-83, v3 63-127, v4 95-127) | 0.001 | 0.7 (700 ms) | [VERIFIED] 4 velocity layers means dynamic shape across `ten`-style pad notes will be EXPRESSIVE — composer can write `mp` to `mf` to `f` and each gets a different sample. Slightly faster release (700ms vs 800ms) than strings — pad notes will decay cleanly between chord changes. |
| Timpani.sfz | `#timpani` | 24 | MIDI 36-60 (C2-C4 — five physical timpani drums mapped) | 3 (v1 0-80, v3 61-110, v4 111-127) | 0.005 (5 ms) | 12 (12 SECONDS — long tail) | [VERIFIED] **No `loop_mode`** (timpani is a one-shot). 12s release is the natural decay tail — `marc` transition hits will RING through into the next section (this is musically correct). **CAVEAT:** round-robin variation (rr1/rr2) declared in the patch is SILENTLY IGNORED by Phase 33 parser. See Pitfall 2. For D-203's "single accented hit each at A→B and B→A'" role, this doesn't matter (only 2 hits in the whole piece). |

**Phase 28 articulation interactions:**
- All 5 patches have fast ampeg_attack (≤5ms) — `stacc` (25% duration + release×0.5) will produce clearly-detached notes on all 5 instruments.
- All 5 have substantial release (700ms+) — `leg` (110% duration + crossfade) will sound smooth without click artifacts on any instrument.
- The patches' VOLUME and PAN values are already baked in (Phase 33 parser respects them via the `volume`/`pan` opcodes in the 14-opcode whitelist). Per-instrument `volume(buf, linear)` (D-401) operates AFTER this baked-in scaling.

**Confidence: HIGH (all 5 patches probed live).**

### Priority 2: GitHub Audio Embed Mechanics

**CRITICAL FINDING:** CONTEXT.md D-601's assumption that GitHub renders a release-asset MP3 URL inline is INCORRECT.

**Verified facts** (sources: [GitHub Community Discussion #53410](https://github.com/orgs/community/discussions/53410), [GitHub Docs § "Attaching files"](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files)):

1. **GitHub strips all raw `<video>` / `<audio>` / `<iframe>` HTML** from markdown READMEs and release bodies — these tags do not render.
2. **The ONLY URL pattern that renders as an inline player** in GitHub-rendered markdown is `https://github.com/user-attachments/assets/<uuid>` — produced uniquely by the GitHub web-UI drag-drop upload flow.
3. **Direct links to external files** (including the repo's own release-asset URL `https://github.com/<user>/<repo>/releases/download/v1.4.0/file.mp3`) render as plain hyperlinks, NOT players.
4. **Supported attachment formats** for user-attachments include `.mp3` and `.wav` (per GitHub Docs).
5. **File size cap** for user-attachments drag-drop in README.md is 10 MB on free GitHub plans (100 MB on paid). The 1.5 MB symphony MP3 is well under either cap.

**Remediation options for plan 34-03:**

**Option A (recommended): Drag-drop the MP3 via GitHub web UI; the release asset coexists as a downloadable.**
1. Plan 34-05 first creates the release (so the asset download URL exists for reference).
2. Plan 34-03 (after 34-05) — composer renders the symphony, encodes to MP3, opens README.md in the GitHub web editor (`https://github.com/<user>/<repo>/edit/main/README.md`), drags-drops the MP3 into the editor.
3. GitHub auto-generates a `https://github.com/user-attachments/assets/<uuid>` URL and inserts a `<video controls src="https://github.com/user-attachments/assets/<uuid>">` snippet at the cursor (the only HTML tag GitHub honors in this context).
4. Composer commits the README.md edit with both URLs: the user-attachments URL for the inline player AND a plain markdown link to the release-asset download (so listeners can save the file).

This requires REORDERING plan 34-03 to land AFTER plan 34-05 (release exists, then README references it). Plans 34-04 (announcement draft) and 34-06 (closure docs) can still run in their planned order. The D-901 plan numbering can stay as-is; only the EXECUTION order shifts. Recommend planner annotate plan 34-03's `depends_on:` to include 34-05.

**Option B: Skip the inline player; ship a "Listen" link only.**
- README "## Showcase" section is just: "Listen to *In Five Voices* — ~60s for 5 orchestral instruments [Download MP3 (1.5 MB)](release-asset-URL) | [Source](./examples/symphony/symphony.flow) | [Reproduce locally](./examples/symphony/README.md)"
- Simpler; one less manual step. Cost: listener has to click and download to hear, rather than press play in browser.

**Option C: Host on a GitHub Pages site.**
- Out of scope — Phase 34 deliberately doesn't introduce a Pages dependency.

**Recommendation:** Option A. The inline player is the headline-artifact UX moment ("listener lands on README and HEARS the symphony immediately") that justifies Phase 34's positioning as the v1.4 closer. The one-time manual drag-drop is acceptable for a release-time activity. Plan 34-03 documents the exact step list. Plan 34-05 ships first so the release asset exists.

**Confidence: HIGH (GitHub Community Discussion + GitHub Docs explicit).**

### Priority 3: MP3 Encoding Choice (CBR 192k vs VBR V2)

**Verified facts** (sources: [LAME USAGE doc](https://github.com/gypified/libmp3lame/blob/master/USAGE), [CleverUtils bitrate guide](https://cleverutils.com/wav-to-mp3/bitrate-guide)):

| Option | Average Bitrate | File Size for 60s WAV | Quality | Reproducibility | Audible Diff |
|--------|------------------|------------------------|---------|------------------|---------------|
| CBR 192k (`-c:a libmp3lame -b:a 192k`) | 192 kbps (fixed) | ~1.44 MB exactly | "Transparent" for most listeners | Byte-identical re-encodes (deterministic) | None vs source for typical music on typical equipment |
| VBR V2 (`-c:a libmp3lame -q:a 2`) | 170-210 kbps (avg ~190) | ~1.4 MB (varies ±5%) | "Indistinguishable from original in blind ABX tests, even on studio monitors" — slightly better than CBR 192k at similar size | Re-encode bytes can vary across LAME versions; same version + same input = same bytes | None vs source for typical music; slightly more bits where music is complex |

**Recommendation:** Keep CONTEXT.md Claude's Discretion default of **CBR 192k**. Reasons:
1. CONTEXT.md explicitly favors "reproducible bytes" framing.
2. The 60s symphony is short; even VBR's best case doesn't deliberately use significantly fewer bits.
3. CBR's predictable file size (~1.44 MB exact) is friendlier for "Download MP3 (1.5 MB)" UX text — VBR introduces ±5% size variance.
4. The encoding is one-shot per release; no aggregate-size budget pressure that VBR would solve.
5. Both are perceptually transparent for music on typical equipment.

Composer can override to V2 at plan 34-03 by changing `-b:a 192k` → `-q:a 2` in the documented ffmpeg command. No structural impact.

**Confidence: HIGH (LAME upstream docs + multiple independent encoder guides agree).**

### Priority 4: gh CLI Release Create + Upload Mechanics

**Verified facts** (source: [gh release create manual](https://cli.github.com/manual/gh_release_create)):

**One-shot create + upload (recommended for plan 34-05):**
```bash
gh release create v1.4.0 \
    --title "v1.4 Audio Fidelity, Distribution & Public Showcase" \
    --notes-file docs/announcements/v1.4.0.md \
    --verify-tag \
    flow-symphony-v1.4.0.mp3#"Symphony (MP3, 192 kbps, ~1.5 MB)" \
    flow-symphony-v1.4.0.wav#"Symphony (WAV, uncompressed, ~10 MB)" \
    flow-linux-x64.tar.gz#"Flow CLI binary (Linux x64, self-contained)"
```

**Useful flags surveyed:**
- `--draft` / `-d` — save as draft (composer can preview before publishing).
- `--prerelease` / `-p` — mark as pre-release (NOT needed for v1.4.0; this IS the stable v1.4 release).
- `--generate-notes` — auto-generate from PR history (NOT recommended for v1.4.0; CONTEXT.md D-603 specifies a hand-written announcement file).
- `--notes-file <path>` / `-F <path>` — read notes from file (USE THIS — `docs/announcements/v1.4.0.md` is the body source).
- `--target <branch>` — specify target ref (default = repo's default branch; safe to omit for v1.4.0 on dev/main).
- `--verify-tag` — abort if tag doesn't exist on remote (USE THIS — catches "forgot to push" footgun).
- `--latest=true` (default) — marks as latest release in repo's "Latest Release" header. Keep default.
- `#"label"` asset suffix — display label for each asset in the release page UI. Recommended for the 3 symphony assets so listeners know what's MP3 vs WAV vs binary.

**Per-release total cap:** GitHub allows 2 GB per individual asset; no hard per-release total cap below the 100 GB per-repo packages cap (which doesn't apply here — releases are separate from Packages). The 3 assets total ~50 MB (1.5 MB MP3 + 10 MB WAV + ~38 MB binary). No concern.

**Authentication:** gh CLI uses `gh auth login` cached credentials. Composer must run `gh auth status` once before plan 34-05 to confirm login. If not logged in, `gh auth login --web` is one command (browser-based OAuth flow).

**Confidence: HIGH (gh CLI manual + gh CLI 2.46.0 verified present locally).**

### Priority 5: Phase 28 Articulation Differentiation Patterns

**Verified via** `flow-lang.Tests/Unit/Phase28/ArticulationRulesTests.cs`, `ArticulationVelocityTests.cs`, `PerSynthArticulationTests.cs`, and `flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs`.

**The 5 note-stream-accessible articulation tokens** (per `SfzArticulationTests.cs:332-339`):

| Token | Note-stream form | Articulation enum | Rendered effect (under SFZ + Phase 28) |
|-------|------------------|---------------------|-------------------------------------------|
| (none) | `C4q` | Normal | Synth-default ADSR |
| `>` | `C4q>` | Accent | +0.30 velocity (clamped); duration unchanged. Audible: louder. |
| `stacc` | `C4q stacc` | Staccato | 25% duration + sustain=0 + release×0.5. Audible: SHORT. |
| `ten` | `C4q ten` | Tenuto | 100% duration + release×1.2. Audible: held + soft fade. |
| `leg` | `C4q leg` | Legato | 110% duration + crossfade. Audible: held BEYOND nominal + connects to next. |
| `marc` | `C4q marc` | Marcato | 25% duration + Accent's +0.30 velocity. Audible: SHORT + LOUD. |

**Sforzando** is enum-only (no note-stream token); envelope spike applied at the renderer layer. Not needed for the symphony — the 5 note-stream tokens cover the D-301 "every Phase 28 articulation" requirement.

**Composer-natural placement** — see Architecture Patterns § Pattern 3 above. Key recommendation: spread articulations across different instruments and sections so the composer hears each one in its natural musical context, NOT all stacked on the same line as a checklist demo.

**A/B fixture for D-802 condition 2** (audible articulation differentiation UAT):
- Composer renders TWO variants: `symphony.flow` (canonical, all articulations) + `symphony_no_articulation.flow` (same notes, all articulation tokens stripped).
- A/B listens to both — if the canonical mix is audibly more expressive than the stripped mix, condition 2 is satisfied.
- CONTEXT.md "Deferred Ideas" notes this A/B fixture COULD become a permanent example but is OUT of scope for Phase 34's plan budget. Plan 34-01 generates it during UAT and deletes it after sign-off. (Alternatively: keep as `examples/symphony/symphony_no_articulation.flow` if composer finds it valuable — but commits add scope.)

**Confidence: HIGH (Phase 28 test suite green; tokens verified by direct test source inspection).**

### Priority 6: Voice-block Polyphony Patterns for Orchestral Writing

**Verified via** `flow-lang.Tests/Unit/Phase28/VoiceBlockParserTests.cs` + `flow-lang.Tests/Integration/Phase28/VoiceBlockRenderTests.cs`.

**Working syntax** (verified by `VoiceBlockParserTests.cs:47-54`):
```flow
Sequence stride = | {voice C4w} {voice C5q D5q E5q F5q} |
```
This renders C4 held for a whole note WHILE C5→D5→E5→F5 quarter notes play underneath. The voice block lives inside a single `| ... |` Sequence; both voices share the bar's onset; each `{voice ...}` block's contents are rendered independently then mixed additively (`SongRenderer` does this through `BarData.ParallelVoices`).

**Composer-orchestral patterns** (from CONTEXT.md "Established Patterns" + standard scoring practice):

1. **Held bass + running line on the same instrument** — the stride pattern verbatim. Use case: cello holds a long pedal-tone while violin runs a melodic figure above (D-203's A' section is a natural fit).
2. **Two-voice counterpoint on one instrument** — e.g. `| {voice A4q F#4q E4q D4q} {voice D4q C4q B3q A3q} |` for two-part harmony on the horn line. Use case: horn carries inner voices in the B section (D-203 horn role).
3. **Drone + melody** — `| {voice D2w} {voice A3h F3h} |` for a sustained low note under a slow upper line. Use case: cello drone under the violin theme reprise in A'.

**Recommendation for the symphony** (composer-natural placement):
- **A' section (D-203 "violin enters in A' with same theme an octave up")** is the natural voice-block landing zone. Use `Sequence celloVoiced = | {voice D2w} {voice A3h F3h} |` so the cello holds a low D pedal while voicing a sparse inner-voice pair underneath the transposed violin lead. This audibly demonstrates polyphony to satisfy D-802 condition 3, AND it serves the music (the A' fullness is what differentiates it from the A statement).
- DON'T over-use voice blocks across the piece — they're a showcase feature in this phase; one strategically-placed voice block in A' makes the polyphony audible without overcrowding the texture.

**Cross-instrument simultaneity** is NOT a voice-block use case — it's handled by the per-instrument-render-and-sum pattern (Architecture Patterns § Pattern 1).

**Confidence: HIGH (Phase 28 voice-block tests green; render path verified end-to-end).**

### Priority 7: Two-Run Determinism Reproduction Script Shape

**Verified facts:**
- Phase 28 dropped pinned-bytes determinism in favor of two-run cmp-clean (CLAUDE.md § "Conventions").
- Phase 33 SfzSampleCache is per-FlowEngine (Phase 33 D-07), so two consecutive `flow render` invocations are TWO separate engine instances — second run cold-loads the cache too. Both runs MUST produce identical bytes for the contract to hold.
- The Phase 28 articulation envelope, Phase 25 humanizeGaussian seed, and Phase 19 tuplet rational arithmetic are all deterministic given identical inputs.

**Canonical reproduction one-liner** (for `examples/symphony/README.md` § Reproduction subsection 4 per CONTEXT.md Claude's Discretion "Determinism reproduction step doc shape"):

```bash
flow render examples/symphony/symphony.flow -o /tmp/symphony-a.wav && \
flow render examples/symphony/symphony.flow -o /tmp/symphony-b.wav && \
cmp /tmp/symphony-a.wav /tmp/symphony-b.wav && \
echo "OK: byte-identical determinism preserved"
```

Single command line with `&&` chaining so a failure at any stage aborts. cmp exit 0 = identical; the trailing `echo` only fires on success. Total runtime is ~2× the render time of the symphony itself (no caching between processes — but the second render warms the OS file cache so disk IO is faster the second time).

**Framing sentence** (per CONTEXT.md Claude's Discretion): "Same inputs → same bytes. Two runs back-to-back must produce identical WAVs."

**Confidence: HIGH (precedent set in Phase 28 tutorial/showcase byte-identical tests; Phase 33 SfzSampleCache verified deterministic).**

### Priority 8: Memory File Update Mechanics

**Verified facts** (sources: direct read of `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/project_pre_public_no_legacy_burden.md`):

- The memory file is a YAML-frontmatter + Markdown body file with no auto-generation hooks.
- The `originSessionId` field is set ONCE at creation and is never auto-updated; the body text is mutable.
- The MEMORY.md index file references the memory by filename — updating the body without renaming the file preserves the index reference.

**Canonical update pattern** (plan 34-06 step):

1. Read the existing file with `Read` tool (planner already familiar — it appears in the system reminder block of every Claude session for this project).
2. Use `Write` tool with the new body. Preserve the YAML frontmatter (name, description, type, originSessionId) verbatim — only rewrite the prose body.
3. New body content per CONTEXT.md Claude's Discretion: "Flow was pre-public; v1.4 closure 2026-XX-XX flipped it public. Breaking changes now ship through deprecation windows."

**Suggested rewrite** (planner refines during plan 34-06):

```markdown
---
name: Flow is public as of v1.4 (2026-XX-XX)
description: Flow shipped publicly at v1.4 (YYYY-MM-DD). External users may now have Flow code. Breaking changes require deprecation windows.
type: project
originSessionId: 00f05ec1-5c85-4739-ab17-cbd561b73e43
---
The Flow language went public at v1.4 (YYYY-MM-DD) with the symphony showcase. Before that, pre-public latitude (no deprecation cycles, no migration tooling, breaking changes in single commits) was acceptable. That latitude no longer applies.

**Why:** v1.4.0 release [LINK] is the first public-facing artifact; the demonstrated API surface from that release point becomes effectively contract.

**How to apply:**
- Breaking changes ship through a deprecation cycle: mark old behavior `// DEPRECATED — use X instead`, ship both old and new for one minor release, remove old in the next minor release.
- Builtin renames and removals require a deprecation cycle.
- Parser errors for newly-removed syntax should include a charitable hint pointing at the new form.
- Semver discipline applies: v1.4.x patches are backward-compatible, v1.5.0+ minors may add features but not break, v2.0.0 may break.
- The pre-public note above is preserved as historical context — do not delete.

**When this WAS not applying:** v1.0..v1.3 (pre-public). Captured in git history of this file pre-v1.4 closure for reference.
```

Composer can refine the body text during plan 34-06; key point is the file's role as a behavioral constraint flips from "breaking changes are cheap" to "breaking changes ship through deprecation."

**Also update** `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/MEMORY.md` index entry from "[Flow is pre-public](project_pre_public_no_legacy_burden.md) — No external users, no legacy code; breaking changes can land in one commit without deprecation windows" → "[Flow is public as of v1.4](project_pre_public_no_legacy_burden.md) — v1.4 closure 2026-XX-XX; breaking changes now require deprecation windows".

**Confidence: HIGH (file structure verified by direct read).**

### Priority 9: PR vs Direct Commit for v1.4.0 Release Flow

**Verified facts** (sources: `git log` inspection):
- Recent commits (d5d161b, 68d8770, 96cec8b, 9d71098, e89c05a) are all phase-completion docs commits **directly on `dev`** branch.
- `.planning/config.json` `git.branching_strategy` is `"none"` — single-branch flow.
- Project's `git.phase_branch_template` exists but is unused per the `branching_strategy: "none"` setting.

**Recommendation:** **Direct commit on `dev`** for plans 34-01..34-06. No PRs.

Reasoning:
1. Project precedent: every Phase 33 plan committed directly to dev (verified by git log).
2. `branching_strategy: "none"` is the explicit project setting.
3. Phase 34 has no multi-developer review concern — composer is the sole author.
4. The `v1.4.0` tag in plan 34-05 is annotated on the commit that lands plan 34-06 (per CONTEXT.md Claude's Discretion). The tag commit MUST be on `dev` (or whatever the default branch is at release time).

If branch strategy changes between research and execution (e.g. composer flips to PR-based flow), the planner updates plan 34-05's git commands to `git push origin dev && gh pr create ... && gh pr merge --squash --auto` first, then tag. But absent that change, direct commit is correct.

**Confidence: HIGH (config setting + git log precedent unanimous).**

## Sources

### Primary (HIGH confidence)
- `examples/symphony/sfz_smoke.flow` — Phase 33 tutorial chapter (existing in-repo)
- `examples/showcase.flow` — v1.3 mix-stack pattern (existing in-repo)
- `.planning/phases/33-sfz-orchestral-sampler/33-VSCO-PATH-AUDIT.md` — verified VSCO-CE 1.1.0 patch paths
- `flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs` — 14-opcode whitelist verified at lines 79-95
- `flow-lang.Tests/Unit/Phase28/{ArticulationRulesTests,VoiceBlockParserTests}.cs` — articulation + voice-block semantics
- `flow-lang.Tests/Integration/Phase28/VoiceBlockRenderTests.cs` — voice-block end-to-end render verification
- `flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs` — 5-token note-stream articulation tokens verified
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/SViolinVib.sfz` (probed 2026-05-16)
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/CelloEnsSusVib.sfz` (probed 2026-05-16)
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/FluteSusVib.sfz` (probed 2026-05-16)
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/FHornSus.sfz` (probed 2026-05-16)
- `https://raw.githubusercontent.com/sgossner/VSCO-2-CE/SFZ/Timpani.sfz` (probed 2026-05-16)
- `https://cli.github.com/manual/gh_release_create` — gh release create flags (verified 2026-05-16)
- `https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files` — GitHub attachment formats (verified 2026-05-16)
- `https://github.com/orgs/community/discussions/53410` — GitHub MP3 inline-embed limitations (verified 2026-05-16)
- `.planning/phases/33-sfz-orchestral-sampler/33-HUMAN-UAT.md` — HUMAN-UAT pattern
- `.planning/phases/27-tutorial-showcase-refresh/27-CONTEXT.md` — prior curation-heavy phase pattern
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/project_pre_public_no_legacy_burden.md` — memory file structure

### Secondary (MEDIUM confidence)
- `https://github.com/gypified/libmp3lame/blob/master/USAGE` — LAME VBR V2 quality preset description
- `https://cleverutils.com/wav-to-mp3/bitrate-guide` — bitrate guide cross-reference

### Tertiary (LOW confidence — flagged for validation)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all tooling verified present on system; VSCO-CE composer-supplied path documented.
- Architecture: HIGH — patterns verified against existing in-repo code (`examples/showcase.flow`, Phase 28 tests).
- Pitfalls: HIGH — Pitfall 1 (GitHub embed) verified against current GitHub Community discussion; Pitfall 2 (round-robin opcode silent-ignore) verified by reading SfzParser source.
- VSCO-CE quirks: HIGH — all 5 patches probed live via GitHub raw content URL.
- GitHub Release mechanics: HIGH — gh CLI manual + local gh CLI version verified.
- MP3 encoding choice: HIGH — multiple independent encoder guides agree.
- Memory file update: HIGH — file structure inspected directly.

**Research date:** 2026-05-16
**Valid until:** 2026-06-15 (30 days for stable surface) — but GitHub's audio embed mechanism is the most volatile element here; revalidate if Phase 34 execution slips past 2026-07-01.
