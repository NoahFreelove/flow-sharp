# Research Summary: Flow Language Feature Expansion

**Domain:** Music programming language (interpreted, C#/.NET 9)
**Researched:** 2026-03-29
**Overall confidence:** HIGH

## Executive Summary

Flow is a well-architected music programming language with a clean pipeline (Lexer -> Parser -> AST -> Interpreter -> Value), hand-rolled audio synthesis/DSP, and minimal external dependencies (only Pidgin, which is not even used). The 13 planned features -- polyphonic voice allocation, custom oscillators, sidechain compression, spatial audio, sample import, MIDI export, pattern variation, polyrhythm, beat-synced live reload, loops, string interpolation, chord progression DSL, and sequence visualization -- are all feasible extensions of the existing architecture.

The key finding is that **only MIDI export justifies an external dependency** (DryWetMidi 8.0.3). Every other feature should be hand-rolled in C# following the project's established patterns: immutable AudioBuffer returns from DSP, record-type AST nodes, built-in function registration via InternalFunctionRegistry, and synthesizer implementations via INoteSynthesizer. The project's existing WAV writer, compressor, synthesizers, and musical context system provide direct foundations for most features.

The highest-risk features are custom oscillators (performance trap of per-sample interpreter evaluation), beat-synced live reload (threading between file watcher and audio playback), and chord progression DSL (music theory complexity in voice leading). The lowest-risk features are string interpolation, panning, sidechain compression, and sequence visualization -- all of which are small, well-understood implementations.

The recommended phase ordering prioritizes language foundations first (loops, string interpolation, visualization), then audio pipeline expansion (samples, panning, sidechain, voice allocation), then creative features (custom oscillators, pattern generation, MIDI export), and finally advanced features (chord DSL, polyrhythm, live reload).

## Key Findings

**Stack:** Only one new dependency needed: DryWetMidi 8.0.3 for MIDI export. Everything else is hand-rolled C# following existing patterns.
**Architecture:** No rewrites needed. All features extend existing components (new AST nodes, new built-in functions, new DSP operations).
**Critical pitfall:** Custom oscillator performance -- calling user procs per-sample will be 100-1000x too slow. Use wavetable approach instead.

## Implications for Roadmap

Based on research, suggested phase structure:

1. **Language Foundations** - Low complexity features that unblock downstream work
   - Addresses: Loop constructs, string interpolation, sequence visualization
   - Avoids: No pitfalls in this phase (all straightforward interpreter work)
   - Rationale: Loops unblock iteration patterns needed for later phases; string interpolation improves debugging; visualization provides immediate feedback

2. **Audio Pipeline Expansion** - Core audio features users expect
   - Addresses: Sample import (loadWav), per-voice panning, sidechain compression, polyphonic voice allocation
   - Avoids: WAV format assumption pitfall (support multiple formats), voice stealing click pitfall (fade-out)
   - Rationale: These are table-stakes features; without them Flow is synthesis-only and mono

3. **Creative Features** - Features that differentiate Flow
   - Addresses: Custom oscillator definitions, pattern variation, MIDI export
   - Avoids: Oscillator performance pitfall (wavetable approach), MIDI timing precision pitfall (480 ticks/quarter)
   - Rationale: These make Flow a real production tool, not just a notation language

4. **Advanced Features** - High complexity, high reward
   - Addresses: Chord progression DSL, polyrhythm support, beat-synced live reload
   - Avoids: Voice leading complexity (simple rules first), LCM explosion (cap cycle length), threading issues (double-buffering)
   - Rationale: These are the hardest features; doing them last means the foundation is solid

**Phase ordering rationale:**
- Loops and string interpolation are prerequisites for comfortable iteration in later phases
- Sample import unblocks sidechain compression (need a kick sample to drive sidechain)
- Voice allocation should come before custom oscillators (oscillators need voice management)
- MIDI export is independent but benefits from voice allocation (better note tracking)
- Chord DSL and polyrhythm are the most complex; delaying them de-risks the milestone
- Beat-synced reload is the riskiest feature (threading); do it last when everything else works

**Research flags for phases:**
- Phase 3: Custom oscillators likely need a research spike on wavetable vs block-based evaluation tradeoffs
- Phase 4: Chord progression DSL needs music theory research for voice leading algorithm
- Phase 4: Beat-synced reload needs architecture spike for thread-safe section swapping

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Verified DryWetMidi version/compatibility on NuGet; all other features are hand-rolled C# with no dependency questions |
| Features | HIGH | All features are well-understood in the audio programming domain; comparable systems (Sonic Pi, SuperCollider, Tidal) validate feasibility |
| Architecture | HIGH | Existing codebase was inspected; all extension points are clear and follow established patterns |
| Pitfalls | MEDIUM | Performance concerns for custom oscillators are based on general interpreter overhead knowledge, not profiling data. Threading concerns for live reload are based on standard concurrency patterns, not tested against Flow's specific architecture. |

## Gaps to Address

- **Custom oscillator evaluation strategy:** Need to profile Flow's interpreter to determine exact overhead per-evaluation. The wavetable approach is recommended but the optimal wavetable size (512? 1024? 4096?) depends on measured performance.
- **PipeWire compatibility:** PROJECT.md mentions PulseAudio but modern Linux is migrating to PipeWire. The existing PulseAudio backend may work via PipeWire's compatibility layer, but this should be verified.
- **Sample rate conversion quality:** WAV import needs resampling for non-44100 Hz files. Linear interpolation is fine for v1 but a future phase may need sinc resampling for quality.
- **MIDI channel mapping:** How Flow instruments map to MIDI channels (piano=ch1, drums=ch10, etc.) needs design decisions during implementation.
- **Test strategy:** The project uses .flow script tests, not unit tests. New features (especially DSP) would benefit from deterministic audio buffer comparisons, but this is an implementation detail, not a research gap.
