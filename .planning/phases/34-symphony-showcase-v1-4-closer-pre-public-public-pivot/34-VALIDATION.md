---
phase: 34
slug: symphony-showcase-v1-4-closer-pre-public-public-pivot
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-16
---

# Phase 34 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Mirrors `34-RESEARCH.md` § "Validation Architecture" (the source of truth for SYM-* mappings).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing) — no new framework; Phase 33 SfzArticulationTests / SfzSmokeTests are the closest comparables |
| **Config file** | None new — symphony render depends on VSCO-CE 1.1.0 which is NOT in CI |
| **Quick run command** | `flow render examples/symphony/symphony.flow -o /tmp/symphony.wav` (renders without runtime error → SYM-01 satisfied) |
| **Full suite command** | `flow render examples/symphony/symphony.flow -o /tmp/symphony-a.wav && flow render examples/symphony/symphony.flow -o /tmp/symphony-b.wav && cmp /tmp/symphony-a.wav /tmp/symphony-b.wav` (two-run cmp-clean → SYM-01 + D-702 satisfied) |
| **Estimated runtime** | ~15 s per render (two-run = ~30 s + instant cmp) on a fully-warmed `SfzSampleCache`; first render eager-loads ~50 MB samples per patch (~3-5 s overhead) |

---

## Sampling Rate

- **After every task commit:** Composer eyeballs the relevant render artifact (.wav playback or README rendered preview) — Phase 34 produces composer-judgement artifacts, not unit-testable code.
- **After every plan wave:** N/A — Phase 34 plans are sequential per D-902 + D-903 (no parallel waves).
- **Before `/gsd:verify-work`:** Full suite (two-run cmp-clean) must be green AND `34-HUMAN-UAT.md` must contain composer sign-off.
- **Max feedback latency:** ~30 seconds (two-run cmp) once `symphony.flow` exists; before then it's "iteration cycle = composer-listening latency".

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 34-01-* | 01 | 1 | SYM-01 | — | N/A — composition only | smoke (composer-local) | `flow render examples/symphony/symphony.flow -o /tmp/symphony.wav` | ❌ Wave 0 (symphony.flow doesn't exist yet) | ⬜ pending |
| 34-01-uat | 01 | 1 | SYM-02, SYM-03 | — | N/A | manual-only | Read `34-HUMAN-UAT.md` for sign-off statement | ❌ Wave 0 | ⬜ pending |
| 34-02-* | 02 | 1 | SYM-04 (reproduction docs) | — | N/A | review | Read `examples/symphony/README.md`, confirm D-602 sections present | ❌ Wave 0 | ⬜ pending |
| 34-03-* | 03 | 2 | SYM-04 (top-level README) | — | N/A | review (visual) | `gh repo view --web` after commit; visually confirm inline `<video>` player renders from user-attachments URL | ❌ Wave 0 | ⬜ pending |
| 34-04-* | 04 | 1 | SYM-05 (announcement draft) | — | N/A | review | Read `docs/announcements/v1.4.0.md`, confirm 3-paragraph shape per D-603 | ❌ Wave 0 | ⬜ pending |
| 34-05-* | 05 | 2 | SYM-05 (release tag + assets) | — | N/A — uses composer's authenticated `gh` token | smoke (composer-local) | `gh release view v1.4.0 --json assets --jq '.assets | length' | grep -q '^3$'` (3 assets: MP3 + WAV + Linux binary) | ❌ Wave 0 | ⬜ pending |
| 34-06-* | 06 | 3 | SYM-05 (closure docs) | — | N/A | review | `grep -q 'Shipped: v1.4' .planning/PROJECT.md && grep -q '34. Symphony Showcase.*Complete' .planning/ROADMAP.md` | ❌ Wave 0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `examples/symphony/symphony.flow` — NEW file; plan 34-01 produces iteratively; plan 34-02 commits the post-UAT canonical version
- [ ] `.planning/phases/34-symphony-showcase-v1-4-closer-pre-public-public-pivot/34-HUMAN-UAT.md` — NEW file; plan 34-01 produces; mirrors `33-HUMAN-UAT.md` shape
- [ ] `examples/symphony/README.md` expansion — plan 34-02 (D-602: "## The Symphony" section + "## Tutorial Chapter: sfz_smoke.flow" demotion)
- [ ] Top-level `README.md` "## Showcase" section — plan 34-03 (after 34-05 per Research Pitfall 1)
- [ ] `docs/announcements/v1.4.0.md` — plan 34-04 (new file under NEW `docs/announcements/` directory)
- [ ] `v1.4.0` annotated git tag + GitHub Release with 3 assets (MP3 + WAV + `flow-linux-x64.tar.gz`) — plan 34-05
- [ ] Milestone closure doc updates — plan 34-06 (PROJECT.md / ROADMAP.md / STATE.md / REQUIREMENTS.md / .planning/MILESTONES.md + CLAUDE.md "Public as of v1.4" footnote + memory file `project_pre_public_no_legacy_burden.md` rewrite)

(No framework install needed; no test files to scaffold. Phase 34 ships zero interpreter code.)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Composer "postable on GitHub" sign-off | SYM-02 | Subjective audio judgement — the headline UAT criterion per ROADMAP success criterion #2 | Composer listens to canonical mix, confirms "I would publicly share this" in `34-HUMAN-UAT.md` |
| Audible Phase 28 articulation differentiation | SYM-03 | Subjective auditory differentiation across staccato/legato/accent/marcato/tenuto | Composer renders all-articulations-stripped variant + canonical mix per D-802 condition 2, A/B listens, confirms canonical mix is audibly more expressive |
| Audible Phase 28 polyphony | SYM-03 | Subjective auditory pickout of simultaneous voices in voice-block section | Composer plays the section containing the `{voice ...}{voice ...}` block, confirms simultaneous voices are intelligible per D-802 condition 3 |
| GitHub-rendered audio player visible in top-level README | SYM-04 | Only visible AFTER commit lands on GitHub; user-attachments URL only resolves on GitHub-rendered markdown, not in editor preview | `gh repo view --web` opens browser; composer scrolls README "## Showcase" section + presses play on inline player |
| First-public-facing announcement reads naturally | SYM-05 | Subjective copywriting judgement | Composer reads `docs/announcements/v1.4.0.md` end-to-end, confirms 3-paragraph shape + accessible framing per D-603 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies — composer-local smoke + per-task review commands documented above
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify — N/A for composition phase; per-task review by composer
- [ ] Wave 0 covers all MISSING references — 7 Wave 0 deliverables listed above; every PLAN.md task should declare one of these in its `<read_first>` or output
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s once `symphony.flow` exists
- [ ] `nyquist_compliant: true` set in frontmatter — pending after Phase 34 closure proves the smoke loop works end-to-end

**Approval:** pending

---

*Source of truth for SYM-01..SYM-05 mappings: `34-RESEARCH.md` § "Validation Architecture" lines 554-599.*
*Manual-only verifications drawn from CONTEXT.md D-801..D-803 (composer UAT loop).*
