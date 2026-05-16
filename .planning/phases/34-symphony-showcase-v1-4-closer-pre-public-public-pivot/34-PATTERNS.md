# Phase 34: Symphony Showcase (v1.4 closer) — Pattern Map

**Mapped:** 2026-05-16
**Files analyzed:** 11 (3 NEW + 8 MODIFIED) + 1 release-artifact tool invocation cluster
**Analogs found:** 11 / 11 (100% coverage — every file has a strong in-repo analog)

## Phase character

Phase 34 is **pure composition + docs + release tooling**. The CONTEXT.md "Integration Points" lock is explicit: `flow-lang/ + flow-midi/ + flow-lsp/ + flow-cli/ NOT TOUCHED`. Every file in scope is either Flow source under `examples/`, Markdown under `docs/` / repo root / `.planning/`, or a memory-file rewrite. All 11 files have direct in-repo analogs from prior phase closures (especially Phase 27 tutorial-refresh, Phase 32 Scala tutorial, Phase 33 SFZ sampler closure).

The only "no-analog" pattern is the GitHub user-attachments drag-drop MP3 embed (Pitfall 1 in RESEARCH.md) — that workflow is novel to the repo and Phase 34 documents it for the first time.

## File Classification

| File (relative from repo root) | Action | Role | Data Flow | Closest Analog | Match Quality |
|--------------------------------|--------|------|-----------|----------------|---------------|
| `examples/symphony/symphony.flow` | NEW | composition source | render-pipeline (per-instrument render → mix → master FX → writeWav/writeMidi) | `examples/symphony/sfz_smoke.flow` (single-instrument SFZ pipeline) + `examples/showcase.flow` (per-section render + effect chain + write) | exact-role + role-match (compose) |
| `examples/symphony/README.md` | MODIFY | composer-facing docs (reproduction guide) | static prose + code-fenced bash blocks | current `examples/symphony/README.md` (Phase 33 single-tutorial form) + `examples/scala/README.md` (Phase 32 tutorial-chapter form, if present) | exact (expanding the same file) |
| `README.md` (top-level) | MODIFY | project landing-page prose | static prose + inline link/embed | itself (existing structure preserved; new "## Showcase" section sits between existing `## What is flow-lang?` and `## Install (Linux x64)`) | self-analog (existing structure) |
| `docs/announcements/v1.4.0.md` | NEW | release-announcement Markdown | static prose (3 paragraphs + links) | `docs/editor-setup/README.md` (closest existing docs/ tone — composer-facing, link-heavy, 50-line scope) | role-match (no announcement precedent) |
| `.planning/phases/34-.../34-HUMAN-UAT.md` | NEW | composer sign-off ledger | YAML frontmatter + Tests table + Summary block | `.planning/phases/33-sfz-orchestral-sampler/33-HUMAN-UAT.md` (Phase 33, same pipeline at smoke scope) + `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` (Phase 17, multi-test sign-off precedent) | exact (mirror Phase 33 shape verbatim per D-803) |
| `CLAUDE.md` | MODIFY | project-instructions for Claude Code | structured Markdown with reserved section headers | itself (preserve all sections; append 1-line footnote under § "Goals" + 1-line cross-reference under § "Music-Specific Language Features") | self-analog |
| `.planning/PROJECT.md` | MODIFY | project-state truth-source | structured Markdown ("Current State" + `<details>` collapsed milestones) | itself + the existing v1.2 / v1.1 / v1.0 `<details>` pattern (lines 43-64) | self-analog (extend established pattern) |
| `.planning/ROADMAP.md` | MODIFY | milestone roadmap | structured Markdown (Milestones list + per-phase tables) | itself (Phase 34 row in "Progress" table line 456 flipped from `0/N Spec pending` to `N/N Complete YYYY-MM-DD`; v1.4 milestone line 9 flipped from `🚧 in progress` to `✅ shipped`) | self-analog |
| `.planning/STATE.md` | MODIFY | GSD live state | YAML frontmatter + body | itself + the Phase 33 closure state (last_updated 2026-05-16; stopped_at = "Phase 34 context gathered" → flips to "Phase 34 complete (6/6) — v1.4 shipped") | self-analog |
| `.planning/REQUIREMENTS.md` | MODIFY | v1.x requirement ledger | Markdown with REQ-* checkboxes + Phase traceability table | itself — existing "v1.4 Phase 30" + "v1.4 Phase 33" cross-milestone-insert sections at lines 204-253 are the template for a new "v1.4 Phase 34 — Symphony Showcase" section | self-analog (extend Phase 30/33 cross-insert template) |
| `.planning/MILESTONES.md` | MODIFY | shipped-version history | Markdown with `## v1.X — Shipped YYYY-MM-DD` headers + Stats/Delivered/Key-accomplishments | itself — the v1.2 entry (lines 7-52) and v1.1 entry (lines 55-90) are the template for a new "## v1.4 Audio Fidelity, Distribution & Public Showcase — Shipped" entry inserted ABOVE v1.2 | self-analog (mirror v1.2 entry shape verbatim) |
| `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/project_pre_public_no_legacy_burden.md` | MODIFY | Claude auto-memory | YAML frontmatter + Markdown body | itself (preserve frontmatter `name`/`description`/`type`/`originSessionId` verbatim; rewrite body to "Flow was pre-public; v1.4 closure 2026-XX-XX flipped it public.") | self-analog |

**Release artifact uploads (not file edits — `gh` CLI invocations executed once at release time):**

| Action | Command pattern | Closest analog | Notes |
|--------|-----------------|----------------|-------|
| Annotated v1.4.0 tag | `git tag -a v1.4.0 -m "..."` | prior tags `v1.0.0`, `v1.1.0`, `v1.2.0`, `v1.3.0` (all present in `git tag --list`) | annotated, NOT lightweight, per CONTEXT Claude's Discretion |
| Release create + asset upload | `gh release create v1.4.0 --notes-file docs/announcements/v1.4.0.md --verify-tag <assets>` | No prior `gh release create` invocation in repo history (prior tags were git-tag-only); shape verified via gh CLI manual + RESEARCH.md Priority 4 | 3 assets: `flow-symphony-v1.4.0.mp3` + `flow-symphony-v1.4.0.wav` + `flow-linux-x64.tar.gz` |
| Top-level README MP3 inline embed | GitHub web-UI drag-drop into README → auto-generated `https://github.com/user-attachments/assets/<uuid>` URL | **No in-repo analog** — novel pattern per RESEARCH Pitfall 1. CONTEXT D-601's "bare release-URL renders as player" assumption is incorrect. | Manual one-time step; documented in plan 34-03 |

## Pattern Assignments

### 1. `examples/symphony/symphony.flow` (NEW — composition source, render pipeline)

**Primary analog:** `examples/symphony/sfz_smoke.flow` (the Phase 33 4-bar SFZ tutorial — same opt-in `@sfz` pipeline, smaller scope).

**Secondary analog:** `examples/showcase.flow` (the v1.3 minimalist-polyrhythmic showcase — proves the per-section render → effect chain → writeWav pattern, but uses Phase 29 bundled-sample "strings" dispatch, NOT the SFZ sampler).

**Tertiary analog:** RESEARCH.md "Symphony skeleton" code block (lines 372-450) — already adapts both analogs to the symphony's 5-instrument shape; planner uses this as the literal starting point.

**Imports/preamble pattern** — copy from `sfz_smoke.flow:21-22`:
```flow
use "@audio"
use "@sfz"
```
**Extend with** (per RESEARCH.md skeleton + CONTEXT D-301):
```flow
use "@std"
use "@composition"
```
The skeleton draws all 4 stdlib modules. `@sfz` is the load-bearing opt-in import per Phase 33 SPEC-1.

**Patch-load pattern** — copy from `sfz_smoke.flow:37`, repeat 5× for the D-202 instrument set:
```flow
Sfz violin  = (loadSfz #violin)
Sfz cello   = (loadSfz #cello)
Sfz flute   = (loadSfz #flute)
Sfz horn    = (loadSfz #horn)
Sfz timpani = (loadSfz #timpani)
```

**Musical-context block nesting pattern** — copy from `sfz_smoke.flow:46-72` (tempo > timesig > key > section) AND extend with the Phase 28 `voicePool 32` block (CONTEXT D-301 requires surfacing the locked default):
```flow
voicePool 32 {
    tempo 100 {
        timesig 4/4 {
            key Dminor {
                section themeA { ... }
                section transitionAB { ... }
                section themeB { ... }
                section transitionBAPrime { ... }
                section themeAPrime { ... }
                Song piece = [themeA transitionAB themeB transitionBAPrime themeAPrime]
                // ... mix/effects/write ...
            }
        }
    }
}
```

**Per-instrument render pattern** — copy from `examples/showcase.flow:39` and replicate per D-202's 5 instruments:
```flow
Buffer rawFlute   = (renderSong piece "sampler:flute")
Buffer rawViolin  = (renderSong piece "sampler:violin")
Buffer rawCello   = (renderSong piece "sampler:cello")
Buffer rawHorn    = (renderSong piece "sampler:horn")
Buffer rawTimpani = (renderSong piece "sampler:timpani")
```
Note: `examples/showcase.flow:39` renders a single Buffer via `(renderSong piece "strings")` (Phase 29 bundled path). Symphony scales the **same shape** to 5 SFZ-sampler calls. The `"sampler:NAME"` prefix routes through Phase 33's SfzRenderer dispatch (D-13).

**Mix-stack pattern** — copy the effect-chain shape from `examples/showcase.flow:42-49` and adapt to D-401..D-403's per-instrument-balance + master-reverb + master-compress:
```flow
// From showcase.flow:46-49 (single-buffer effect chain pattern):
Buffer filtered = rawMix -> (lowpass 1.2kHz)
Buffer delayed = filtered -> (delay 250ms 0.5 0.4)
Buffer reverbed = delayed -> (reverb 0.5 1.8s)
Buffer finalMix = (volume reverbed 0.7)
```
Adapt to symphony shape:
```flow
// D-401: per-instrument volume balance (starting points; composer tunes during UAT)
Buffer balFlute   = (volume rawFlute   0.85)
Buffer balViolin  = (volume rawViolin  1.0)
Buffer balCello   = (volume rawCello   0.75)
Buffer balHorn    = (volume rawHorn    0.65)
Buffer balTimpani = (volume rawTimpani 0.40)

// Sum into one mix (planner verifies `mix` builtin name in @audio — RESEARCH A1 LOW risk)
Buffer summed = balFlute -> (mix balViolin) -> (mix balCello) -> (mix balHorn) -> (mix balTimpani)

// D-402 + D-403: master reverb + soft compress on the summed mix
Buffer wet = summed -> (reverb 0.3 2.5s)
Buffer mastered = wet -> (compress -12dB 4 100ms 200ms)

(writeWav "examples/output/symphony.wav" mastered)
(writeMidi "examples/output/symphony.mid" piece)
```

**HumanizeGaussian pattern** — copy seeded-call shape from `examples/showcase.flow:26`:
```flow
// showcase.flow:26 ships this verbatim — FIXED integer seed for two-run determinism
Sequence melody = (humanizeGaussian | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w | 0.08 314)
```
Apply to symphony's cello line per D-304 + RESEARCH Pitfall 4:
```flow
Sequence celloBass = (humanizeGaussian | D2w | A2w | F2w | G2w | 0.05 42)
```
**Critical:** The seed MUST be a literal integer (per RESEARCH Pitfall 4). DO NOT use `(randomInt ...)` for the seed — that breaks D-702's two-run cmp-clean contract.

**Articulation-token coverage pattern** — per RESEARCH Priority 5 + CONTEXT D-301, the 5 tokens (`>`, `stacc`, `ten`, `leg`, `marc`) must each appear ≥ 1× spread across instruments. RESEARCH Pattern 3 anti-pattern: don't stack all 5 on the same instrument. Recommended placements (composer adjusts in UAT):
- `>` on A-theme flute downbeats
- `stacc` on B-section flute running line
- `ten` on horn pad notes
- `leg` on cello sustained passages
- `marc` on timpani transition hits

**Voice-block pattern** — copy from `flow-lang.Tests/Integration/Phase28/VoiceBlockRenderTests.cs:60` (cited in RESEARCH Pattern 2):
```flow
// Verified-working voice-block syntax — held bass + running line on same instrument:
Sequence stride = | {voice C4w} {voice C5q D5q E5q F5q} |
```
Apply to A' section per RESEARCH Priority 6 recommendation:
```flow
Sequence celloVoiced = | {voice D2w} {voice A3h F3h} |
```

**Tuplet pattern** — single Phase 19 tuplet bracket on the woodwind line in section B per CONTEXT D-303:
```flow
Sequence fluteOrn = | _ | {3:2 D5q E5q F5q}q stacc _ _ | _ | _ |
```
RESEARCH Pitfall 3 (showcase.flow research): NO ties inside tuplet brackets.

**Transpose pattern** — composer-natural per CONTEXT D-203 ("violin enters in A' with the same theme an octave up"):
```flow
Sequence violinTheme = (transpose | D5q E5q F5q> A5h | G5q. F5q. E5w | 12)
```

**Comment style** — per CONTEXT Claude's Discretion "Source ASCII / no emoji per CLAUDE.md Conventions": plain ASCII section dividers + inline `//` comments where the WHY is non-obvious. `sfz_smoke.flow:1-19` shows `Note:` style (visible at runtime via `print "Note: ..."`) — symphony.flow can use BOTH `Note:` for tutorial-style narration (visible when run) AND `//` for code-only commentary.

**What NOT to copy from `sfz_smoke.flow`:**
- Its 4-bar single-violin scope — symphony is ~60s, 5-voice (RESEARCH lines 12-13 confirm "richer instance of the same pipeline").
- Its `key Cmajor` choice — symphony locks D minor per D-104 (or planner-chosen minor key).
- Its `(writeWav "sfz_smoke.wav" mix)` cwd-relative output path — symphony uses `examples/output/symphony.wav` per D-504.
- Its absence of effects chain — symphony adds the full D-401..D-403 mix stack.
- The `Note:` tutorial-narration comments inside the file — appropriate for `sfz_smoke.flow` (tutorial scope) but the symphony is a music piece, not a tutorial; brief `//` comments only.

**What NOT to copy from `examples/showcase.flow`:**
- Its bundled-sample `(renderSong piece "strings")` dispatch — symphony uses `sampler:NAME` SFZ dispatch instead.
- Its Phase 27-era v1.3-feature checklist scope (dict + tuplet + euclidean) — symphony is curated music, not a feature-tour. Only Phase 28+ articulation + voice-block + Phase 33 SFZ are required showcases per D-301; tuplet (1×) earns its slot but doesn't dominate.
- Its `(print ...)` lines at top + bottom — symphony is opened by `flow render` not `flow run`; print statements aren't run by `render` but harm nothing if present. Skip for cleanliness.

---

### 2. `examples/symphony/README.md` (MODIFY — expand from 80 lines to ~150 per D-602)

**Primary analog:** The current file itself (lines 1-79). D-602 says "demote existing content to a `## Tutorial Chapter: sfz_smoke.flow` section, add new `## The Symphony` section ABOVE it."

**Secondary analog:** `examples/scala/README.md` (Phase 32 tutorial chapter — if it exists, same composer-facing tone). Worth checking but not strictly necessary.

**Section ordering pattern** (post-expansion):
```markdown
# Symphony Showcase + SFZ Tutorial

## The Symphony  (NEW — per D-602)
  - Brief framing: 60s, ABA, 5 instruments, in D minor at tempo 100
  - Instrumentation list (the 5 VSCO-CE patches with their SFZ-filename mapping)
  - What Phase 34 features it exercises (mapped to audible moments)
  - Expected output file (examples/output/symphony.wav + .mid)
  - Mix notes (per-instrument volume balance, master reverb 2.5s, master compress)
  - Reproduction:
    - Setup (link to Setup section below for VSCO-CE install)
    - Render command (`flow render examples/symphony/symphony.flow -o examples/output/symphony.wav`)
    - Two-run determinism check (the cmp one-liner from RESEARCH lines 454-464)
    - WAV→MP3 encoding command (`ffmpeg -i ... -c:a libmp3lame -b:a 192k`)
    - Listen command (`aplay examples/output/symphony.wav`)

## Setup  (KEEP from current README — verbatim; this is the same VSCO-CE setup for both files)

## Tutorial Chapter: sfz_smoke.flow  (DEMOTE current "## Run" + "## What the tutorial demonstrates" + "## Supported instruments" + "## Loading non-GM patches" + "## Reference" content under this header)
```

**Code-fenced bash patterns** — copy verbatim from the current README's lines 36-42:
```bash
dotnet run --project flow-interpreter examples/symphony/sfz_smoke.flow
```
And add the symphony's render command (NEW per D-504):
```bash
flow render examples/symphony/symphony.flow -o examples/output/symphony.wav
flow flow2midi examples/symphony/symphony.flow -o examples/output/symphony.mid
```
And the two-run determinism block (NEW per D-702 + RESEARCH Priority 7):
```bash
flow render examples/symphony/symphony.flow -o /tmp/symphony-a.wav && \
flow render examples/symphony/symphony.flow -o /tmp/symphony-b.wav && \
cmp /tmp/symphony-a.wav /tmp/symphony-b.wav && \
echo "OK: byte-identical determinism preserved"
```

**Framing sentence** (from CONTEXT Claude's Discretion "Determinism reproduction step doc shape"): "Same inputs → same bytes. Two runs back-to-back must produce identical WAVs."

**What NOT to copy from current README:**
- DO NOT delete any current content — D-602 is "expand, not replace." All current sections (Setup, Run, What the tutorial demonstrates, Supported instruments, Loading non-GM patches, Reference) move UNDER the "Tutorial Chapter" header.
- DO NOT change the "Supported instruments" section's GM dict listing — it's still accurate (Phase 33 19-symbol dict).
- DO NOT use emoji or fancy formatting — current file is plain Markdown, keep it that way.

---

### 3. `README.md` (top-level, MODIFY — insert "## Showcase" section per D-601)

**Primary analog:** The current file's structure (lines 1-85). Position: NEW "## Showcase" section between current `## What is flow-lang?` (line 9-29 — extends through "Bugs?") and `## Install (Linux x64)` (line 35).

Actually re-reading lines 31-33: there is a `## Features` section header at line 31 with one line of content `See [FEATURES.md](./FEATURES.md) for a complete list of features.` at line 32. So insertion point is AFTER line 33 (after `## Features`) and BEFORE line 35 (`## Install (Linux x64)`).

**Insertion shape** (per D-601 + RESEARCH Priority 2 Option A recommendation):
```markdown
## Showcase

Listen to *In Five Voices* — ~60 seconds for 5 orchestral instruments
(violin, cello, flute, horn, timpani), rendered entirely from
`examples/symphony/symphony.flow` via the v1.4 SFZ sampler against
VSCO Community CE 1.1.0.

<!-- Inline player via user-attachments URL (drag-dropped MP3 via GitHub web UI) -->
<video controls src="https://github.com/user-attachments/assets/<uuid>"></video>

- [Source: `examples/symphony/symphony.flow`](./examples/symphony/symphony.flow)
- [How to reproduce locally](./examples/symphony/README.md)
- [Download MP3 (1.5 MB)](https://github.com/noah-freelove/flow-sharp/releases/download/v1.4.0/flow-symphony-v1.4.0.mp3)
- [v1.4.0 release](https://github.com/noah-freelove/flow-sharp/releases/tag/v1.4.0)
```

**CRITICAL per RESEARCH Pitfall 1:** The `<video>` tag is the ONLY GitHub-honored HTML form, and ONLY when the `src` is a `user-attachments/assets/<uuid>` URL produced via the GitHub web-UI drag-drop flow. CONTEXT D-601's assumption that a bare release-asset URL renders inline is incorrect. Plan 34-03 must include the manual drag-drop step (open `https://github.com/<user>/<repo>/edit/main/README.md` in browser, drag-drop the MP3 into the editor, capture the auto-generated user-attachments URL, commit). RESEARCH Option A is recommended; this also implies plan 34-03 must run AFTER plan 34-05 (release exists, so the download-link target URL is real).

**What NOT to change in `README.md`:**
- DO NOT touch the AI Disclaimer (lines 4-7), What is flow-lang? (lines 9-29), or any section after Install.
- DO NOT replace the bare release-asset URL form (CONTEXT D-601's literal text) — it'll render as a plain link, defeating the purpose. RESEARCH explicitly flags this as the planner's gap to address.
- DO NOT add emoji — repo convention (CLAUDE.md Conventions) is no emoji.

---

### 4. `docs/announcements/v1.4.0.md` (NEW directory + first file)

**Primary analog:** `docs/editor-setup/README.md` (closest existing `docs/` tone — composer-facing, link-heavy, ~80 lines).

**Shape pattern** per CONTEXT D-603 (3 paragraphs):
```markdown
# Flow v1.4 — Audio Fidelity, Distribution & Public Showcase

Flow is an interpreted, statically-typed music-production language.
Composers write musical ideas as code — note streams, musical-context
blocks (tempo / timesig / key), chord literals, transforms — and the
interpreter renders them to WAV and MIDI via a full audio pipeline.
[Elevator pitch — adapted from PROJECT.md lines 5-9.]

v1.4 ships the audio-fidelity rewrite: per-voice polyphony, Phase 28
articulation envelopes (staccato / legato / accent / marcato / tenuto),
Phase 29 sampled tonal instruments, a Phase 30 self-contained `flow` CLI
binary + install script + XDG config, Phase 31 LSP polish + JetBrains
plugin, Phase 32 full Scala (`.scl`) microtonal tuning loader, and
Phase 33 SFZ orchestral sampler (blessed library: VSCO Community CE 1.1.0).
[Highlight summary — one bullet per phase.]

Listen to the showcase: [In Five Voices](https://github.com/<user>/<repo>#showcase)
— ~60 seconds for 5 orchestral instruments, rendered from
`examples/symphony/symphony.flow`. Try it yourself:
[Install (Linux x64)](https://github.com/<user>/<repo>#install-linux-x64).
Source: [github.com/<user>/<repo>](https://github.com/<user>/<repo>).
```

**Composer adapts per-platform.** Same single source feeds Reddit / HN / X / Discord with platform-specific edits (e.g., HN gets the top-line elevator pitch only; Reddit gets the full 3 paragraphs; X gets the 280-char compression).

**What NOT to copy from `docs/editor-setup/README.md`:**
- Its setup-instructions structure — announcement is prose-first, not steps-first.
- Its multiple sections — announcement is 3 paragraphs, single H1, no H2s.
- Its tone-of-voice for marketplace / OpenVSX details — announcement is for non-editor-setup audiences.

---

### 5. `.planning/phases/34-.../34-HUMAN-UAT.md` (NEW — composer sign-off)

**Primary analog:** `.planning/phases/33-sfz-orchestral-sampler/33-HUMAN-UAT.md` (Phase 33 SFZ smoke UAT — same pipeline, smaller scope).

**Secondary analog:** `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` (Phase 17 LSP smoke UAT — multi-test sign-off shape).

**Frontmatter pattern** — copy from `33-HUMAN-UAT.md:1-7` verbatim, change phase + dates:
```yaml
---
status: partial   # flips to "closed" on composer sign-off
phase: 34-symphony-showcase-v1-4-closer-pre-public-public-pivot
source: [34-VERIFICATION.md, 34-CONTEXT.md D-801..D-803]
started: 2026-MM-DDTHH:MM:SSZ
updated: 2026-MM-DDTHH:MM:SSZ
---
```

**Test-row pattern** — copy from `33-HUMAN-UAT.md:13-31` (single end-to-end test) or `17-HUMAN-UAT.md:16-75` (multi-row pattern). For Phase 34, three rows per D-802 conditions:
```markdown
## Tests

### 1. Composer "would publicly share this" sign-off (D-802 condition 1)
expected: composer listens to rendered symphony.wav end-to-end (~60s)
  and signs off with the verbatim ROADMAP success criterion phrasing:
  "postable on GitHub quality" / "I would publicly share this".
setup: render via `flow render examples/symphony/symphony.flow -o examples/output/symphony.wav`,
  play via `aplay examples/output/symphony.wav`.
expected_outcome: composer affirms in plain English.
why_human: subjective quality judgement — no automated proxy possible.
result: [pending]

### 2. Audible articulation differentiation (D-802 condition 2)
expected: composer A/B-listens canonical mix vs all-articulations-stripped
  variant; canonical is audibly more expressive.
setup: render canonical symphony.wav + symphony_no_articulation.wav (latter
  with `>`/`stacc`/`ten`/`leg`/`marc` tokens stripped); A/B with `aplay`.
expected_outcome: composer can hear staccato shorter than legato, accent
  louder than unmarked, etc.
why_human: perceptual judgement — RMS / spectral checks don't capture
  "is it audibly more expressive".
result: [pending]

### 3. Audible polyphony (D-802 condition 3)
expected: composer picks out simultaneous voices in the A' section voice-block.
setup: listen to bars containing `| {voice D2w} {voice A3h F3h} |` cello
  voicing under transposed violin theme.
expected_outcome: composer can hear the inner voices as distinct lines, not
  a single muddied chord.
why_human: perceptual judgement — needs trained ears.
result: [pending]
```

**Summary block pattern** — copy from `33-HUMAN-UAT.md:32-40`:
```markdown
## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps
```

**What NOT to copy:**
- DO NOT copy `33-HUMAN-UAT.md`'s single test row — Phase 34 has 3 distinct UAT conditions per D-802.
- DO NOT copy `17-HUMAN-UAT.md`'s "Note on deferred items (rows 4–5)" footer — Phase 34 has no deferred-to-future-release-tag rows; the symphony IS the release-tag activity.
- DO NOT mark any row passed until composer signs off in plain English. Frontmatter `status` stays `partial` until all 3 rows resolve.

---

### 6. `CLAUDE.md` (MODIFY — append 1-line footnote + 1-line cross-ref)

**Primary analog:** itself.

**Insertion 1** (per CONTEXT Claude's Discretion "CLAUDE.md 'Public as of v1.4' footnote text"):

Location: under § "Goals" (CLAUDE.md:11-19; immediately after the 3rd "Make the easy cases fast." bullet at line 19, before the blank line preceding "**Non-Goals**" at line 21).

```markdown
> **Note:** Flow is public as of v1.4 (2026-XX-XX). The pre-public scope-creep-without-deprecation latitude (`project_pre_public_no_legacy_burden`) no longer applies; breaking changes now go through a deprecation cycle.
```

**Insertion 2** (per `pattern_mapping_context` brief + CONTEXT § Integration Points):

Location: under § "Music-Specific Language Features", appended as a one-line paragraph at the end of the Phase 33 SFZ block (after CLAUDE.md's existing line `**SFZ orchestral sampler (opt-in):** ... See `examples/symphony/sfz_smoke.flow` ... v1.4 Phase 34 symphony showcase is the downstream consumer (Phase 33).`).

```markdown
- **Symphony showcase:** `examples/symphony/symphony.flow` — see `README.md` § "Showcase". The v1.4 headline artifact rendering 5 VSCO-CE instruments through the Phase 33 SFZ surface; ~60 s ABA single-movement piece in D minor (Phase 34).
```

**What NOT to change:**
- DO NOT touch § "Music Types Quick Reference" — Phase 34 ships no new music type (Tuning + Sfz rows already landed Phase 32 + 33).
- DO NOT touch § "Conventions" — pre-Phase-28 byte-identical contract already updated to "two-run cmp-clean" in Phase 28.
- DO NOT touch § "Goals & Non-Goals" structure — only append the single footnote line under Goals.
- DO NOT touch the existing "Locked articulation rules" or "Multi-track MIDI export" or any other Phase 28 paragraphs.

---

### 7. `.planning/PROJECT.md` (MODIFY — flip "Current State" + add v1.4 collapsed block)

**Primary analog:** itself — the existing v1.2 `<details>` block (lines 43-54) and v1.1 `<details>` block (lines 56-64) are the template for the new v1.4 entry.

**Current state update** — replace lines 11-16:
```markdown
## Current State

**Shipped:** v1.4 Audio Fidelity, Distribution & Public Showcase (2026-XX-XX)

**Next milestone:** TBD — see `.planning/MILESTONES.md`
```

**Add v1.4 collapsed `<details>` block** ABOVE the v1.2 `<details>` block (per the established pattern at PROJECT.md:43-54):
```markdown
<details>
<summary>v1.4 Audio Fidelity, Distribution & Public Showcase (shipped 2026-XX-XX)</summary>

Delivered: per-voice polyphony + Phase 28 articulation envelopes (staccato/legato/accent/marcato/tenuto), Phase 29 sampled tonal instruments (piano/brass/sax/strings/flute/bell), Phase 30 self-contained `flow` CLI binary + install + XDG config, Phase 31 LSP polish + JetBrains plugin scaffolding, Phase 32 full Scala (`.scl`) microtonal tuning loader, Phase 33 SFZ orchestral sampler (blessed: VSCO Community CE 1.1.0), Phase 34 curated symphony showcase ("In Five Voices") as the v1.4 closer + pre-public → public pivot.

- N of N requirements Complete (across SPEC-1..SPEC-N + REQ-1..REQ-8 + SYM-01..05)
- ~M plans across Phases 28-34
- See: `.planning/MILESTONES.md` and `.planning/milestones/v1.4-*.md`

</details>
```

**Move the existing "v1.2 Stability & Composer DX" entry's wording** — keep PROJECT.md:44-54 unchanged but ensure the v1.4 block sits above it chronologically. Also flip "Current Milestone: v1.3" (line 17) — since v1.3 already shipped per ROADMAP (`v1.3.0` git tag exists), this section is already stale; planner replaces it with "Next milestone: TBD".

**What NOT to change:**
- DO NOT touch § "Requirements" Validated list (lines 67-126) — only append new validated bullets if Phase 34 adds requirements with SYM-* IDs. RESEARCH proposes SYM-01..05 — those rows append at the end of the Validated list per planner discretion.
- DO NOT touch § "Out of Scope" — no Phase 34 items invalidate it.
- DO NOT touch § "Constraints" or § "Key Decisions" — both stay accurate.

---

### 8. `.planning/ROADMAP.md` (MODIFY — flip Phase 34 row + v1.4 milestone marker)

**Primary analog:** itself.

**Two specific edits:**

1. **Line 9 (Milestones list):** `🚧 **v1.4 Audio Fidelity, Distribution & Public Showcase** — Phases 28-34 (in progress; ...)` → flip to `✅ **v1.4 Audio Fidelity, Distribution & Public Showcase** — Phases 28-34 (shipped 2026-XX-XX) — see `milestones/v1.4-ROADMAP.md`` (mirroring v1.2's line 7 pattern).

2. **Line 456 (Progress table — Phase 34 row):** `| 34. Symphony Showcase (v1.4 closer) | v1.4 | 0/N | Spec pending | - |` → flip to `| 34. Symphony Showcase (v1.4 closer) | v1.4 | 6/6 | Complete | 2026-XX-XX |` (mirroring Phase 33's line 455 pattern).

3. **Line 415 (Phase 34 section "Plans: TBD"):** replace `TBD` with the 6-plan listing per D-901 (`34-01-PLAN.md` through `34-06-PLAN.md`), mirroring Phase 33's lines 397-403 plan-list pattern.

4. **Lines 412-414 (Phase 34 Success Criteria):** mark each criterion's status. Mirror Phase 33's lines 391-396 inline-status pattern (`Shipped <commit-hash>`) if/when commits land.

5. **Add a new "## v1.4 Phase 34 — Symphony Showcase (cross-milestone insert)" section** at line ~254 (mirroring lines 204-253's Phase 30/33 cross-insert sections), with a `| SPEC | Phase | Status |` table for SYM-01..05.

**What NOT to change:**
- DO NOT touch the v1.0 / v1.1 / v1.2 collapsed `<details>` sections.
- DO NOT touch the v1.3 progress table rows (Phases 18-27 — already Complete).
- DO NOT touch Phase 28-33 progress table rows except to confirm they remain Complete.

---

### 9. `.planning/STATE.md` (MODIFY — reset for next milestone)

**Primary analog:** itself. The frontmatter (STATE.md:1-15) gets four fields flipped:

```yaml
---
gsd_state_version: 1.0
milestone: v1.4
milestone_name: Audio Fidelity, Distribution & Public Showcase
status: shipped                            # was: ready_to_plan
stopped_at: Phase 34 complete (6/6) — v1.4 shipped 2026-XX-XX   # was: Phase 34 context gathered (auto mode)
last_updated: "2026-XX-XXTHH:MM:SS.SSSZ"
last_activity: 2026-XX-XX
progress:
  total_phases: 7
  completed_phases: 7                      # was: 5
  total_plans: 52                          # was: 46 (+ 6 Phase 34 plans)
  completed_plans: 52                      # was: 45
  percent: 100                             # was: 71
---
```

**Body updates** — copy the Phase 33 closure note pattern (STATE.md:40-50 "Phase 30 highlights" + lines 51-55 "PHASE 29 STILL GATED..."):
- Add a "Phase 34 highlights" block summarizing the symphony render + release + announcement.
- Update "Current Position" (lines 27-32) to reflect "milestone closed; next milestone TBD pending /gsd-new-milestone".
- Update "Resume Instructions" body to a brief "v1.4 shipped; next session begins next-milestone discussion."

**What NOT to change:**
- DO NOT touch § "Performance Metrics" (lines 56-141) — those are historical accumulations.
- DO NOT touch the Phase 17 HUMAN-UAT mention if it's still open (rows 4-5 — per ROADMAP.md they're explicitly deferred to first release tag, which IS v1.4.0; planner judges whether to close those rows here or keep them open).

---

### 10. `.planning/REQUIREMENTS.md` (MODIFY — add v1.4 Phase 34 cross-insert section)

**Primary analog:** itself — the existing "v1.4 Phase 30" cross-insert section (REQUIREMENTS.md:204-222) and "v1.4 Phase 33" cross-insert section (REQUIREMENTS.md:226-253) are the exact templates.

**Insertion shape** — add after the Phase 33 section (after REQUIREMENTS.md:253), before the "## Notes" section. Mirror the Phase 33 entry verbatim in structure:

```markdown
---

## v1.4 Phase 34 — Symphony Showcase (v1.4 closer — pre-public → public pivot)

Phase 34 ships the v1.4 headline artifact — a curated ~60 s minimalist-
orchestral symphony for 5 VSCO-CE instruments rendered through the Phase 33
SFZ surface — plus the public-facing release machinery (v1.4.0 tag,
GitHub Release with MP3 + WAV + Linux binary, top-level README showcase
section, announcement draft) and v1.4 milestone closure docs.

REQ-IDs map 1:1 to the 5 ROADMAP Phase 34 success criteria, formalized as
SYM-01..05 per RESEARCH § Phase Requirements.

| SPEC | Phase | Status |
|------|-------|--------|
| SYM-01 (Symphony renders end-to-end via SFZ sampler, two-run cmp-clean) | Phase 34 | Shipped <plan-34-01 + 34-02 hashes> |
| SYM-02 (Composer "postable on GitHub" sign-off recorded in 34-HUMAN-UAT.md) | Phase 34 | Shipped <plan-34-01 hash> |
| SYM-03 (Code paired with audible features: articulation, polyphony, voicePool) | Phase 34 | Shipped <plan-34-02 hash> |
| SYM-04 (README.md showcase + audio embed + examples/symphony/README.md reproduction) | Phase 34 | Shipped <plan-34-03 hash> |
| SYM-05 (v1.4.0 tag + GitHub Release + announcement draft + milestone closure) | Phase 34 | Shipped <plan-34-04 + 34-05 + 34-06 hashes> |

Two-run byte-identical determinism contract (Phase 18/25/27/33 inheritance)
preserved end-to-end through the real VSCO-CE library — verified manually
by composer at release time per D-702.

---
```

**What NOT to change:**
- DO NOT touch the v1.3 "## Active Requirements" section (lines 21-128).
- DO NOT touch the existing Phase 30 (lines 204-222) or Phase 33 (lines 226-253) cross-insert sections.
- DO NOT touch the "## Future Requirements (deferred)" or "## Out of Scope (for v1.3)" sections.
- DO NOT touch the "## Traceability" table (lines 152-201).

---

### 11. `.planning/MILESTONES.md` (MODIFY — add v1.4 entry above v1.2)

**Primary analog:** itself — the v1.2 entry (MILESTONES.md:7-52) is the template.

**Insertion shape** — add as the NEW topmost milestone entry (above the current v1.2 entry at line 7), preserving the existing v1.2 / v1.1 / v1.0 entries unchanged below:

```markdown
## v1.4 Audio Fidelity, Distribution & Public Showcase — Shipped 2026-XX-XX

**Goal:** Ship the v1.4 audio-fidelity rewrite (per-voice polyphony + articulation envelopes + sampled tonal instruments), the distribution wedge (self-contained `flow` CLI + install + XDG config + MIDI↔Flow round-trip), LSP polish + JetBrains plugin scaffolding, full Scala (`.scl`) microtonal tuning loader, SFZ orchestral sampler (blessed library: VSCO Community CE 1.1.0), and the curated symphony showcase as the milestone closer — flipping Flow from pre-public to public.

**Delivered:** [body — mirror v1.2 entry's "Delivered" shape at MILESTONES.md:11].

**Stats:**
- Phases: 7 (Phase 28 – Phase 34)
- Plans: ~M (all complete)
- Requirements: ~N total — SPEC-1..SPEC-N + REQ-1..REQ-8 + SYM-01..05 — all Complete
- Git range: post-`v1.3` tag → `v1.4.0` tag (~5 weeks, 2026-04-10 → 2026-05-XX)
- Source files at close: <to-fill>

**Key accomplishments:**
1. Phase 28 — articulation system (5 tokens: staccato / legato / accent / marcato / tenuto, locked envelope rules) + per-voice polyphony (voicePool default 32) + multi-track MIDI export
2. Phase 29 — sampled tonal instruments (piano + brass + sax + strings + flute + bell via SampledInstrumentRenderer, ≤5 MB CC-BY 4.0 University-of-Iowa MIS bundle)
3. Phase 30 — `flow` self-contained Linux x64 binary (38 MB) + 11-subcommand CLI + install.sh + XDG config + MIDI↔Flow round-trip ±1 tick
4. Phase 31 — LSP polish (4 closed gaps) + JetBrains plugin scaffolding (stretch goal MET)
5. Phase 32 — full Scala (`.scl`) tuning loader + `tuning t { ... }` musical-context block, ±0.1¢ Carlos Alpha / Bohlen-Pierce, 5 canonical fixtures
6. Phase 33 — SFZ orchestral sampler (`use "@sfz"` gated; 19-entry GM dict; 14-opcode parser; 441-frame equal-power crossfade)
7. Phase 34 — symphony showcase ("In Five Voices") + v1.4.0 release + public announcement + pre-public → public pivot

**Patterns established:**
- Two-run cmp-clean determinism contract (replacing pre-Phase-28 pinned-bytes)
- HUMAN-UAT.md for subjective composer sign-off (Phase 17 + Phase 33 + Phase 34 precedent)
- Per-instrument render + sum-mix pattern for orchestral pieces (Phase 33 + Phase 34)
- GitHub user-attachments drag-drop for inline audio player in README (Phase 34)

**Known deferred items at close:** [as discovered during plan 34-06]

**Forward-deferred items:** [v1.5 candidates — stereo pan, second showcase, jazz piece, sampled drums, MIDI live output, sampler round-robin]

**Archives:**
- `.planning/milestones/v1.4-ROADMAP.md` (to be created at closure)
- `.planning/milestones/v1.4-REQUIREMENTS.md` (to be created at closure)
```

**What NOT to change:**
- DO NOT touch the v1.2 entry (lines 7-52), v1.1 entry (lines 55-90), or v1.0 entry (lines 94-100).

---

### 12. Memory file `project_pre_public_no_legacy_burden.md`

**Primary analog:** itself.

**Frontmatter** — preserve verbatim per RESEARCH Priority 8 (the `originSessionId` is set once and never auto-updated; `name`/`description`/`type` stay):
```yaml
---
name: Flow is pre-public — no legacy compatibility burden
description: No external users have written code in Flow yet. Breaking changes can land cleanly without deprecation windows or migration tooling for end users.
type: project
originSessionId: 00f05ec1-5c85-4739-ab17-cbd561b73e43
---
```

**Body rewrite** per CONTEXT D-901 plan 34-06 Claude's Discretion + RESEARCH Priority 8:

```markdown
The Flow language went **public** with v1.4.0 (2026-XX-XX). This memory file's
original premise — "no external users, no legacy burden" — no longer applies.

**Why this changed:** v1.4 closure shipped:
- A self-contained `flow` CLI binary on GitHub Releases (Phase 30)
- A public announcement at `docs/announcements/v1.4.0.md` (Phase 34)
- The headline symphony showcase at `examples/symphony/symphony.flow` (Phase 34)
- A `v1.4.0` git tag with attached MP3 + WAV + binary release assets

Once external listeners hear the symphony and external composers download the
binary, the surface they observe (note streams, musical-context blocks, the
`@sfz` import, the `loadSfz` builtin, the `volume`/`gain` split, etc.) is
effectively a public contract.

**How to apply now (post-v1.4):**
- **Breaking changes go through a deprecation cycle.** When a phase changes
  syntax/semantics, ship a deprecation advisory at least one minor version
  before removal. Dual-parse paths and transitional grace periods are
  appropriate for any user-visible surface (parsers, builtins, music types,
  CLI flags).
- **Migration tools cover external `.flow` files.** Any future migration
  script (e.g. `scripts/migrate-XX.cs`) must run cleanly on `.flow` files
  outside the repo. Test against a synthetic external-fixture set.
- **Parser errors should hint at migrations.** When syntax is removed,
  the error message points at the replacement (e.g. "infix `+` removed in
  v1.4 — use `(add)` builtin instead").
- **Builtin renames / removals get `// DEPRECATED` markers** for one minor
  version before the underlying symbol disappears.
- **Semver discipline applies.** v1.4 → v1.5 is now a real public minor
  bump; v2.0 carries the "we will break things" license. v1.4.1 is for
  bug fixes only.

**Original pre-public latitude (preserved for historical reference):** Before
v1.4 closure, no external users had written code in Flow yet. Breaking changes
could land cleanly without deprecation windows or migration tooling for end
users. The trigger for revisiting was always defined as "the first public
release tag" — which v1.4.0 satisfies.
```

**What NOT to change:**
- DO NOT touch the YAML frontmatter (4 fields: name / description / type / originSessionId).
- DO NOT rename the file or delete it — the MEMORY.md index entry references it by filename.
- DO NOT change the MEMORY.md index entry's wording (it still says "Flow is pre-public — Breaking changes can land in one commit without deprecation windows"); planner judges whether to update the index entry's description in a follow-up edit. The body rewrite is the primary deliverable.

---

## Shared Patterns

### Pattern A: Two-run cmp-clean determinism (applies to symphony.flow + symphony/README.md)

**Source:** Phase 28 closure (CLAUDE.md § "Conventions") + Phase 33 D-702 + Phase 18/25/27 inheritance.

**Apply to:** `examples/symphony/symphony.flow` (composer MUST use fixed integer seeds in any randomization call: humanizeGaussian seed = 42, NOT randomInt). `examples/symphony/README.md` (composer ships the cmp one-liner as a reproduction step).

**Canonical pattern** (from RESEARCH Priority 7 + Code Examples):
```bash
flow render examples/symphony/symphony.flow -o /tmp/symphony-a.wav && \
flow render examples/symphony/symphony.flow -o /tmp/symphony-b.wav && \
cmp /tmp/symphony-a.wav /tmp/symphony-b.wav && \
echo "OK: byte-identical determinism preserved"
```

### Pattern B: HUMAN-UAT.md sign-off for subjective quality (applies to 34-HUMAN-UAT.md)

**Source:** Phase 17 HUMAN-UAT (multi-row), Phase 33 HUMAN-UAT (single-row).

**Apply to:** `.planning/phases/34-.../34-HUMAN-UAT.md` — 3 rows per D-802 conditions.

**Canonical frontmatter shape** (from `33-HUMAN-UAT.md:1-7`):
```yaml
---
status: partial   # flips to "closed" on sign-off
phase: 34-symphony-showcase-...
source: [34-VERIFICATION.md, 34-CONTEXT.md D-801..D-803]
started: <ISO timestamp>
updated: <ISO timestamp>
---
```

### Pattern C: Per-instrument render + sum-mix (applies to symphony.flow)

**Source:** `examples/showcase.flow:39-49` (single-buffer render + effect chain).

**Apply to:** `examples/symphony/symphony.flow` — extend to 5 buffers, sum, then master FX.

**Canonical extension** (from RESEARCH Pattern 1):
```flow
Buffer rawX = (renderSong piece "sampler:X")    // × 5 instruments
Buffer balX = (volume rawX 0.XX)                // × 5, D-401 starting points
Buffer summed = balA -> (mix balB) -> (mix balC) -> (mix balD) -> (mix balE)
Buffer wet = summed -> (reverb 0.3 2.5s) -> (compress -12dB 4 100ms 200ms)
```

### Pattern D: Cross-milestone REQ insert (applies to REQUIREMENTS.md)

**Source:** `REQUIREMENTS.md:204-253` (existing v1.4 Phase 30 + Phase 33 cross-insert sections).

**Apply to:** Phase 34 entry added at line 254ish, before "## Notes".

**Canonical shape:** `---` separator + `## v1.4 Phase N — <Name> (cross-milestone insert)` header + 2-paragraph framing + `| SPEC | Phase | Status |` table + closing `---`.

### Pattern E: Collapsed `<details>` milestone summary (applies to PROJECT.md)

**Source:** `PROJECT.md:43-54` (v1.2 entry) + `PROJECT.md:56-64` (v1.1 entry).

**Apply to:** New v1.4 `<details>` block inserted ABOVE the v1.2 block.

**Canonical shape:** `<details><summary>vX.Y NAME (shipped YYYY-MM-DD)</summary>` + 1-paragraph delivered summary + 3-line stats (requirements / plans / archives) + `</details>`.

### Pattern F: Milestone-history entry (applies to MILESTONES.md)

**Source:** `MILESTONES.md:7-52` (v1.2 entry, full shape).

**Apply to:** New v1.4 entry inserted as NEW topmost milestone.

**Canonical shape:** `## vX.Y NAME — Shipped YYYY-MM-DD` + Goal paragraph + Delivered paragraph + Stats block + Key accomplishments numbered list + Patterns established list + Known deferred + Forward-deferred + Archives. ~50 lines.

### Pattern G: Annotated git tag + gh release with assets (applies to plan 34-05 tool invocations)

**Source:** Prior tags `v1.0.0`, `v1.1.0`, `v1.2.0`, `v1.3.0` exist as annotated tags (verified via `git tag --list`); no prior `gh release create` invocation in repo history (this is the first GitHub Release).

**Apply to:** Plan 34-05 release-creation step.

**Canonical shape** (from RESEARCH Code Examples + Priority 4):
```bash
git tag -a v1.4.0 -m "v1.4 Audio Fidelity, Distribution & Public Showcase"
git push origin v1.4.0

gh release create v1.4.0 \
    --title "v1.4 Audio Fidelity, Distribution & Public Showcase" \
    --notes-file docs/announcements/v1.4.0.md \
    --verify-tag \
    flow-symphony-v1.4.0.mp3#"Symphony (MP3, 192 kbps, ~1.5 MB)" \
    flow-symphony-v1.4.0.wav#"Symphony (WAV, uncompressed, ~10 MB)" \
    flow-linux-x64.tar.gz#"Flow CLI binary (Linux x64, self-contained)"
```

## No Analog Found

| Pattern | Why no analog | What planner uses instead |
|---------|---------------|---------------------------|
| GitHub user-attachments drag-drop MP3 inline player URL | Novel to repo — no prior README has an inline audio player. RESEARCH Pitfall 1 surfaces this as a CONTEXT D-601 gap. | RESEARCH Priority 2 Option A: manual GitHub web-UI drag-drop step in plan 34-03, executed AFTER plan 34-05 lands the release. Plan documents the manual step explicitly. |
| `docs/announcements/` directory + first announcement file | New directory; no prior `docs/announcements/` exists. | RESEARCH § Recommended Project Structure positions it as a sibling to existing `docs/editor-setup/` and `docs/plans/`. Planner creates the directory + the first file in one commit (plan 34-04). |
| `gh release create` first-ever invocation | All prior tags (v1.0.0..v1.3.0) are git-tag-only — no GitHub Release has been published yet. | RESEARCH Priority 4 supplies the verified one-shot command with `--verify-tag` + `--notes-file` + `#"label"` asset-label syntax. Composer must run `gh auth status` once before invoking. |
| Memory file body-rewrite at milestone closure | First time the project's pre-public latitude has expired. | RESEARCH Priority 8: direct Write overwrite preserving YAML frontmatter verbatim, rewriting only the body prose. |

## Metadata

**Analog search scope:**
- `examples/` (all .flow files for composition analogs)
- `examples/symphony/` (Phase 33 + Phase 34 sibling files)
- `.planning/phases/*/HUMAN-UAT.md` (Phase 17 + 33 sign-off patterns)
- `.planning/` (PROJECT/ROADMAP/STATE/REQUIREMENTS/MILESTONES live-state docs)
- `docs/` (announcement-tone analog)
- `~/.claude/projects/.../memory/` (memory-file shape)
- `git tag --list` (prior tag conventions)

**Files Read for pattern extraction:**
- `examples/symphony/sfz_smoke.flow` (73 lines — full read)
- `examples/symphony/README.md` (79 lines — full read)
- `examples/showcase.flow` (58 lines — full read)
- `README.md` (85 lines — full read)
- `CLAUDE.md` (lines 1-50 — § Goals + early structure)
- `.planning/PROJECT.md` (222 lines — full read)
- `.planning/MILESTONES.md` (101 lines — full read)
- `.planning/STATE.md` (lines 1-140 — frontmatter + body shape)
- `.planning/REQUIREMENTS.md` (lines 1-265 — full read)
- `.planning/ROADMAP.md` (lines 1-100 + 320-456 — Phase 34 section + progress table)
- `.planning/phases/33-sfz-orchestral-sampler/33-HUMAN-UAT.md` (42 lines — full read)
- `.planning/phases/17-flow-language-server/17-HUMAN-UAT.md` (130 lines — full read)
- `~/.claude/projects/.../memory/project_pre_public_no_legacy_burden.md` (19 lines — full read)
- `34-CONTEXT.md` (308 lines — full read)
- `34-RESEARCH.md` (lines 1-800 — through Priority 8)

**Pattern extraction date:** 2026-05-16

**Coverage assessment:** 11 / 11 files have strong in-repo analogs. The only "novel" pattern (GitHub user-attachments drag-drop) is documented in RESEARCH.md Priority 2 with the exact remediation steps for plan 34-03. Planner has concrete, line-numbered references for every file in scope.
