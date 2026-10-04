# Harmonic oscillators and subtractive instrument example — 2026-10-04

P7-26 adds saw and square waveforms for Flow-authored instruments.

## Implemented

`dawSawOsc`/`flow.sawOsc` and `dawSquareOsc`/`flow.squareOsc` take a stereo frequency
stream in Hz. Each channel owns independent normalized phase, emits before advance
and clamps frequency to ±49% of sample rate, like the existing sine oscillator.
NaN means zero; reset/seek clears phase. Square has fixed 50% duty cycle.

A fixed-cost polynomial edge correction reduces discontinuity aliasing. Saw uses
`2*phase-1-edge(phase)`; square adds the edge at zero and subtracts the edge half a
cycle later. Correction width is absolute frequency/sampleRate, so reverse phase
motion uses the same periodic waveform. At zero frequency, correction is disabled
and phase freezes at its current waveform value (possibly DC). Both nodes have no
parameters, bypass, intrinsic tail or additional latency. Generic device Flow
export, graph JSON and instrument persistence support both.

This is not oversampling or an alias-free arbitrary-modulation guarantee. PWM,
hard sync, resonance and higher-order filters remain separate work.

`examples/instruments/subtractive-synth.flow` builds a saw oscillator, envelope-
controlled cutoff, one-pole low-pass, amplitude ADSR and velocity response. It
returns an instrument, demonstration score and master mixer using only Flow.
The instrument remains a generated graph rather than an instrument plugin package.

## Evidence

Three focused tests passed (`/tmp/flow-harmonic-oscillators.log`). They cover stereo
forward/reverse frequency, bounded output, partition equivalence, deterministic
reset, no callback allocations and both public/generic-export Flow construction.
A spectral regression verifies lower folded third-harmonic energy than a naive saw
at a representative frequency; it does not establish broadband alias elimination.

Module snapshots intentionally add two public signatures: native 695→697,
reachable 689→691. The same six legacy unreachable bindings remain. The saved
subtractive example is included in the real worker → accepted project → save/reopen
→ routed compiler/audio integration test. `git diff --check` passed.

All **373** affected backend/module tests passed with zero failures
(`/tmp/flow-harmonic-regression.log`), including the saved subtractive example.
Full core/Web and physical gates were not repeated in this slice.

## Next

Refresh full core/Web compatibility evidence, then continue instrument plugin
contracts, sampler graph primitives and required parameter/device/recording
workflows. Physical audio qualification remains open; native JUI frontend and
historical callback-gap diagnosis are deferred.
