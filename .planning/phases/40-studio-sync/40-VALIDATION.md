---
phase: 40
slug: studio-sync
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-06
---

# Phase 40 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `40-RESEARCH.md` §Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (C# integration/unit under `flow-lang.Tests/`) + `.flow` script smokes |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj`; per-target gating via `flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs` |
| **Quick run command** | `dotnet test flow-lang.Tests --filter FullyQualifiedName~Phase40` |
| **Full suite command** | `dotnet test flow-lang.Tests` (Desktop) + `dotnet build flow-lang -p:FlowTarget=Web` (strip invariant) |
| **Estimated runtime** | ~90 seconds (Desktop suite) + ~30s Web build |

---

## Sampling Rate

- **After every task commit:** `dotnet test flow-lang.Tests --filter FullyQualifiedName~Phase40` + `dotnet build flow-lang -p:FlowTarget=Web`
- **After every plan wave:** `dotnet test flow-lang.Tests` (full Desktop suite — keep all prior phases green)
- **Before `/gsd:verify-work`:** Full suite green + Web build green + `40-HUMAN-UAT.md` authored
- **Max feedback latency:** ~120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 40-0x | TBD | 1 | MIDI-RT-01 | — | charitable null on absent device | unit | `dotnet test --filter MidiBackendEnumeratesPorts` | ❌ W0 | ⬜ pending |
| 40-0x | TBD | 1 | MIDI-RT-02 | T-40-01 | in-range bytes reach virtual port | integration | `dotnet test --filter VirtualMidiNoteBytes` | ❌ W0 (charitable-skip) | ⬜ pending |
| 40-0x | TBD | 1 | MIDI-RT-04 | T-40-04 | sysex queue; hot-plug never throws | unit | `dotnet test --filter MidiHotPlugNeverThrows` | ❌ W0 | ⬜ pending |
| 40-0x | TBD | 2 | CLOCK-01 | — | 24 pulses/quarter at active tempo | integration | `dotnet test --filter ClockMaster24PpqnRate` | ❌ W0 | ⬜ pending |
| 40-0x | TBD | 2 | CLOCK-02 | — | slave derives BPM + 8-pulse settle | unit (byte-stream seam) | `dotnet test --filter ClockSlaveDrivesTempo` | ❌ W0 | ⬜ pending |
| 40-0x | TBD | 1 | LINK-02 | T-40-03 | offline render ignores all sync state | determinism | `dotnet test --filter OfflineRenderIgnoresSync` | ❌ W0 | ⬜ pending |
| 40-0x | TBD | 3 | JACK-01 | — | absent server → no-op | integration | `dotnet test --filter JackAbsentServerNoOp` | ❌ W0 (charitable-skip) | ⬜ pending |
| 40-0x | TBD | 1 | Web-strip | T-40-03 | RtMidi.Core/JackSharp absent from Web dll | invariant | `dotnet test --filter AssemblyReferenceScan` | ✅ extend | ⬜ pending |
| — | — | — | MIDI-RT-03 | — | CoreMIDI/WinMM (deferred Phase 41) | — | — | n/a | ⬜ deferred |
| — | — | — | LINK-01 | T-40-02 | Ableton Link (DEFERRED — GPL, D-40-06) | — | community stub + advisory | n/a | ⬜ deferred |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Integration/Phase40/MidiBackendTests.cs` — MIDI-RT-01/04 (enumerate, charitable null, hot-plug never-throws)
- [ ] `flow-lang.Tests/Integration/Phase40/VirtualMidiTests.cs` — MIDI-RT-02 byte assertions via `CaptureMidiBackend` seam
- [ ] `flow-lang.Tests/Integration/Phase40/ClockMasterTests.cs` — CLOCK-01 24-PPQN rate
- [ ] `flow-lang.Tests/Integration/Phase40/ClockSlaveTests.cs` — CLOCK-02 byte-stream injection + 8-pulse settle
- [ ] `flow-lang.Tests/Integration/Phase40/OfflineRenderDeterminismTests.cs` — LINK-02 invariant (writable even if Link deferred)
- [ ] `CaptureMidiBackend` in-process loopback test seam (models OSC `HandlerInvokeOverride` / `PulseAudioCaptureBackend.CaptureOverride`)
- [ ] Extend `AssemblyReferenceScanTests.ForbiddenTypeRefPrefixes` with `JackSharp` (`RtMidi.Core` already present per D-47-14)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real hardware synth produces sound on `(midiOut song "port")` | MIDI-RT-01/02 | needs physical/virtual MIDI device + synth | `40-HUMAN-UAT.md` row — connect synth, run example, confirm audible |
| DAW follows Flow as clock master / Flow follows DAW as slave | CLOCK-01/02 | needs a DAW (Ardour/Bitwig/Reaper) | `40-HUMAN-UAT.md` row — set DAW external sync, confirm BPM lock |
| sample-accuracy of MIDI-audio alignment | MIDI-RT-04 | best-effort ms-aligned (blocking playback path); not sample-accurate by design | documented limitation; HUMAN-UAT confirms "tight enough" perceptually |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
