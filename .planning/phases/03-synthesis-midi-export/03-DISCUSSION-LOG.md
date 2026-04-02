# Phase 3: Synthesis & MIDI Export - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-02
**Phase:** 03-synthesis-midi-export
**Areas discussed:** Custom oscillator API, Wavetable approach, MIDI export scope, MIDI instrument mapping
**Mode:** auto (all decisions auto-selected using recommended defaults)

---

## Custom Oscillator API

| Option | Description | Selected |
|--------|-------------|----------|
| Wavetable proc | User writes a proc that returns Float[] for one cycle, registered via `oscillator("name", proc)` | auto |
| Per-sample callback | User proc called per sample during playback (interpreter overhead) | |
| DSP graph | Node-based oscillator definition (complex, scope creep) | |

**User's choice:** [auto] Wavetable proc — matches CLAUDE.md technology stack decision, avoids per-sample interpreter overhead
**Notes:** CLAUDE.md explicitly recommends wavetable approach for custom oscillators

## Wavetable Size

| Option | Description | Selected |
|--------|-------------|----------|
| 2048 samples (configurable) | Good balance of quality vs memory, optional override | auto |
| 512 samples (fixed) | Smallest, but may have audible aliasing | |
| 4096 samples (fixed) | High quality but more memory per oscillator | |

**User's choice:** [auto] 2048 samples with configurable override (recommended)
**Notes:** STATE.md noted this needs profiling — 2048 is a safe default that can be tuned later

## MIDI Export Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Full song structure | Tempo, time sig, key sig, per-note velocities, one track per section/instrument | auto |
| Notes only | Just note-on/note-off, minimal metadata | |
| Multi-file | Separate .mid per section | |

**User's choice:** [auto] Full song structure (recommended — matches MIDI-01/MIDI-02 success criteria)
**Notes:** Success criteria explicitly require tempo, time signature, key signature, and per-note velocities

## MIDI Instrument Mapping

| Option | Description | Selected |
|--------|-------------|----------|
| Built-in mapping table + default | piano→0, brass→56, sax→65, flute→73, drums→ch10; custom oscillators→piano | auto |
| User-specified per section | Require explicit MIDI program numbers | |
| No mapping | All tracks use piano | |

**User's choice:** [auto] Built-in mapping table with piano default (recommended)
**Notes:** General MIDI standard programs are well-known; custom oscillators can't be represented in MIDI

## Claude's Discretion

- Internal wavetable interpolation (linear vs cubic)
- DryWetMidi API patterns (low-level vs high-level)
- ADSR envelope defaults for custom oscillators
- Whether to expose MIDI program mapping query function

## Deferred Ideas

None noted during auto-mode discussion
