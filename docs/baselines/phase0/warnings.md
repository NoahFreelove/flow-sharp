# Phase 0 warning triage

The fresh Desktop solution build succeeds. Warnings remain visible; neither
`NoWarn` nor a blanket warnings-as-errors switch was added. This baseline is
clean of test failures, not warning-free. The verification artifacts retain
full build logs, including repeats from MSBuild summaries and referenced projects.

## Fixed in this phase

| Diagnostic | Location | Change |
| --- | --- | --- |
| Invalid SDK version | `global.json` | Real `10.0.100` feature band, stable .NET 10 roll-forward, no prerelease selection |
| CS8602 nullable dereference | `PolyrhythmFunctions.GetTimeSignatureNumerator` | Capture the checked time-signature reference once with a property pattern, retaining the 4/4 fallback |
| CS8765 override nullability | `TimeSignatureData.Equals` | Accept `object?`, matching `object.Equals` and the existing null-safe pattern |
| CS0105 duplicate import | `ExpressionEvaluator` | Remove duplicate Diagnostics import |
| CS0219 dead local | `AbcImport` | Remove unused `currentAccidental`; the actual accidental state remains unchanged |

Test reliability defects fixed separately: inherited `NO_COLOR`/`TERM`,
process-global JACK advisory dedup leaking between tests, and generated reports
rewriting tracked files. Runtime semantics are unchanged by these fixes.

## Classified remaining debt

| Codes / owner | Assessment and follow-up |
| --- | --- |
| NU1701 / Rug.Osc 1.2.5 | Desktop package restored through .NET Framework compatibility. Existing OSC tests exercise it locally. Keep pinned and visible; replacing/isolating the OSC adapter belongs with dependency separation. Web excludes it. |
| CS8604 / `ExpressionEvaluator` Bar.TimeSignature | Bar metadata can be null but `Value.TimeSignature` is declared non-null. Choosing a default or absence representation is observable language behavior. Characterize it in Phase 1 before changing it; do not silence with `!`. |
| VSTHRD002 / CLI, REPL, live reload, async test waits | Synchronous waits can deadlock or block interactive work. The existing CLI process boundary differs from a UI synchronization context. Review as part of Phase 2 job coordination; do not mechanically replace waits without fixing ownership/cancellation. |
| VSTHRD110 / `LiveReloadManager.StartRenderTask` | Unobserved background task/lifetime concern, related to the roadmap's abandoned timeout worker. Phase 2 must track and terminate/cooperatively cancel workers; assigning to discard would only hide the warning. |
| VSTHRD200 / REPL | Awaitable method naming convention; low-risk naming cleanup when those APIs change. |
| xUnit1031 / xUnit1051 | Blocking test waits and missing test cancellation tokens; convert when async worker tests are revised. These are not permission to ignore timed-out or failing tests. |
| xUnit3003 / custom Fact attributes | Missing source-info forwarding constructor; test reporting quality issue, not a skip policy. Existing explicit prerequisite reasons are retained. |
| xUnit1026 | Unused theory labels/diagnostic metadata; decide whether to assert the omitted dimension or remove it. Avoid an assertion-free test rewrite. |
| xUnit2013 / xUnit2031 | Prefer collection assertions / predicate overloads; stylistic, deferred. |
| CS0414 / RtMidi.PortChanged; CS0169 / LiveReloadManager | Unused event/field, not proof of implemented hotplug or voice preservation. Keep API limitations explicit; remove or implement in the owning milestone. |
| CS0219 / DSP tests; CS0649 / D2 test harness | Dead test locals/unassigned failure-message field; low-priority harness cleanup. |

A Windows/macOS or Web build can have additional target-specific warnings.
Classifications here concern the tested Linux Desktop/Web paths, not a blanket
statement that warnings are harmless. Phase 0 does not claim to fix the
concurrency architecture that these warnings help identify.
