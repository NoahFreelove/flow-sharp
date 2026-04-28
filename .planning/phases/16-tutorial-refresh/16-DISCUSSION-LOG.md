# Phase 16: Tutorial Refresh — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in `16-CONTEXT.md` — this log preserves the alternatives considered.

**Date:** 2026-04-25
**Phase:** 16-tutorial-refresh
**Areas discussed:** Structure & narrative arc, Comment style transition, Output strategy, Feature traceability format

---

## Gray area selection

| Option | Description | Selected |
|--------|-------------|----------|
| Structure & narrative arc | Tutorial organization, narrative vs catalog vs hybrid; showcase.flow relationship | ✓ |
| Comment style transition | How `Note:` block comments and `//` line comments coexist | ✓ |
| Output strategy | WAV/MIDI output count, paths, integration with graduation piece | ✓ |
| Feature traceability format | How required features map to comments per ROADMAP criterion #3 | ✓ |

**User selected all 4 areas.**

---

## Structure & narrative arc

### Q1: Overall tutorial structure approach?

| Option | Description | Selected |
|--------|-------------|----------|
| Expand the current narrative | Keep the existing build-up arc; weave new features into the path; add a final v1.2 chapter for features that don't fit organically. Lowest churn. | ✓ |
| Rewrite as feature catalog | Each feature gets a self-contained section. No narrative arc. Easier to grep, harder to learn linearly. | |
| Hybrid: current basics + v1.2 reference chapter | Keep current 348 lines for v1.0 basics; append a new v1.2 chapter at the end. | |

**User's choice:** Expand the current narrative (Recommended)

---

### Q2: Audio/MIDI output strategy across the tutorial?

| Option | Description | Selected |
|--------|-------------|----------|
| Single graduation piece at the end | One final song renders to WAV + MIDI. Total: 1 WAV + 1 MIDI. | ✓ |
| Per-feature mini-outputs | Each chapter writes its own demo file. ~10-12 WAVs + 2-3 MIDIs. | |
| Hybrid: chapter-mini outputs + final graduation | Some chapters write their own mini WAV; final song ties multiple features. | |

**User's choice:** Single graduation piece at the end (Recommended)

---

### Q3: How should examples/showcase.flow relate to the refreshed tutorial?

| Option | Description | Selected |
|--------|-------------|----------|
| Leave showcase.flow untouched | Phase 1-4 ambient demo stays. Add a one-liner pointer in tutorial.flow. Zero churn. | |
| Retire showcase.flow | Delete entirely. Tutorial becomes single canonical entry. | |
| Expand showcase.flow to v1.2 in parallel | Keep both files, refresh showcase with v1.2 in a separate compact piece. | ✓ |

**User's choice:** Expand showcase.flow to v1.2 in parallel
**Notes:** Intentional scope expansion. User wants both files refreshed under Phase 16.

---

### Q4 (follow-up): How should showcase.flow's v1.2 expansion split scope from tutorial.flow?

| Option | Description | Selected |
|--------|-------------|----------|
| Showcase = ambient mood piece using new effects | Fresh ambient piece using reverbTime + euclidean humanize + dynamics-driven MIDI. No teaching, no per-feature comments. | ✓ |
| Showcase = all-feature compact catalog | Dense ~150-line catalog touching every v1.0→v1.2 feature. Reference card. | |
| Showcase = v1.1+v1.2 only (skip v1.0 basics) | Demonstrates only the 12 v1.1+v1.2 features. | |

**User's choice:** Showcase = ambient mood piece using new effects (Recommended)

---

### Q5 (follow-up): Length cap on tutorial.flow after refresh?

| Option | Description | Selected |
|--------|-------------|----------|
| Soft cap ~600 lines | Current 348 + ~250 for new features. Forces concision. | |
| No cap — let it grow naturally | Whatever it takes. Could be 800-1000 lines. | ✓ |
| Hard cap 500 lines | Each feature gets ~12 lines max. Forces minimalism. | |

**User's choice:** No cap — let it grow naturally

---

## Comment style transition

### Q1: How should `Note:` block comments and `//` line comments coexist?

| Option | Description | Selected |
|--------|-------------|----------|
| Note: for chapter headers; // for inline annotations | Stylistic split. Naturally demonstrates BOTH styles. | ✓ |
| Replace all Note: with // | Convert everything. Demonstrates // exhaustively. | |
| Keep all Note:, add // only in v1.1+v1.2 sections | Minimal churn but inconsistent across the file. | |

**User's choice:** Note: for chapter headers; // for inline annotations (Recommended)

---

### Q2: When should `//` line comments first appear in the tutorial?

| Option | Description | Selected |
|--------|-------------|----------|
| Introduce explicitly with a short callout | Add a "Comments come in two forms" callout near the top. | |
| Appear naturally without explanation | // shows up where used; reader infers. | ✓ |
| Demo // and Note: side-by-side in dedicated 'Syntax' chapter | New chapter with explicit demo and rationale. | |

**User's choice:** Appear naturally without explanation
**Notes:** Aligns with charitable interpretation philosophy — don't over-explain.

---

## Output strategy

### Q1: Where should the graduation WAV + MIDI files write to?

| Option | Description | Selected |
|--------|-------------|----------|
| examples/output/ | New directory colocated with the tutorial. Cleaner than /tmp/, survives reboot. | ✓ |
| tests/output/ (existing convention) | Reuse Phase 15's tests/output/ directory. | |
| /tmp/ (current pattern) | Keep existing /tmp/flow_tutorial_output.wav. Linux-only. | |

**User's choice:** examples/output/ (Recommended)

---

### Q2: How should MIDI export integrate with the graduation piece?

| Option | Description | Selected |
|--------|-------------|----------|
| Same graduation piece exported to both WAV and MIDI | Final song renders to WAV AND writeMidi'd to MIDI. | ✓ |
| Separate MIDI-focused mini-snippet | Dedicated chapter writes a smaller piece to both formats. | |
| MIDI only (skip WAV for that snippet) | One chapter writes only MIDI. | |

**User's choice:** Same graduation piece exported to both WAV and MIDI (Recommended)

---

### Q3: Which features should the graduation piece audibly integrate? (multiSelect)

| Option | Description | Selected |
|--------|-------------|----------|
| reverbTime | Wrap a section in reverbTime { ... } for hearable RT60. | ✓ |
| euclidean swing/humanize | Use a euclidean rhythm with swing+humanize+seed. | ✓ |
| Per-section gain | Vary section gain for dynamic structure. | ✓ |
| tempoRamp | Use tempoRamp for ritardando/accelerando. | ✓ |

**User's choice:** All four selected.
**Notes:** Graduation piece is ambitious — must integrate four v1.2 features into a coherent musical piece, not a checklist.

---

## Feature traceability format

### Q1: How should each demonstrated feature be traceable to a requirement?

| Option | Description | Selected |
|--------|-------------|----------|
| Both — REQ-ID tag in // + prose explanation in Note: | Machine-checkable + human-friendly. Slight verbosity. | |
| REQ-ID tags only (audit-friendly) | // covers REQ-ID style. Easy grep -c. Beginner-unfriendly. | |
| Prose-only (beginner-friendly) | Plain language only. ROADMAP criterion #3 satisfied via feature name in prose. | ✓ |

**User's choice:** Prose-only (beginner-friendly)
**Notes:** ROADMAP criterion #3 still satisfied — every feature name appears in at least one comment, just not as a REQ-ID.

---

### Q2: Where should the traceability live?

| Option | Description | Selected |
|--------|-------------|----------|
| Inline at each demonstration | Tag sits next to the snippet. Localized. | ✓ |
| Central index at the top + minimal inline | A 'Features Demonstrated' table at the top. Drift risk. | |
| Central index at the bottom (audit-trail style) | Index after 'Congratulations!'. | |

**User's choice:** Inline at each demonstration (Recommended)

---

### Q3: Should the tutorial include forward-looking notes about deferred features?

| Option | Description | Selected |
|--------|-------------|----------|
| No — keep tutorial focused on shipped features | Demonstrates only what works in v1.2. | ✓ |
| Yes — brief 'coming soon' callout at the bottom | Lists 1-2 most user-visible deferred items. | |
| Yes — inline notes where features are demonstrated | // note: H-alias is a candidate; see deferred-items.md. | |

**User's choice:** No — keep tutorial focused on shipped features (Recommended)

---

## Claude's Discretion

User did not specify; deferred to planner/executor:
- Order of features within the v1.2 capabilities chapter
- `sing`/`tts` placement (separate chapter vs. integrated)
- Exact musical content of the graduation piece (key, tempo, structure)
- Exact musical content of the showcase ambient piece
- Whether to keep all 348 existing tutorial lines verbatim or trim redundant prints
- Whether `examples/output/` gets a separate `.gitignore` file or top-level entry

## Deferred Ideas

- REQ-ID tags inline — rejected per D-11; revisitable in a future audit phase.
- Central traceability index — rejected per D-12; revisitable in a future doc-hygiene phase.
- "What's next" / coming-soon callouts — rejected per D-13.
- Splitting tutorial into multiple files — rejected by D-01.
- `augment`/`diminish` migration notes — moot per D-14 (C5 dismissed in Phase 11).
