# Ordinary generator native policy — 2026-10-04

Ordinary DAW builds now use a default-deny native invocation policy populated from
reviewed in-memory registrations: core/DAW construction, music units/notation,
bars/harmony/transforms, composition, patterns/generative algorithms, buffer and
timeline operations, synthesis and offline effects/DSP. Mixed registrars contribute
only explicitly listed native names/signatures. Packaged builders retain their
existing narrower policy. Invocation checks cover aliases/callbacks and user-written
internal declarations, not just imports. General-purpose FlowEngine defaults remain
unrestricted.

`AllowImplicitSampleFiles` is an engine option, default true for compatibility and
false in DAW workers. SampleCache rejects sampled-instrument disk discovery before
probing/loading files; synthesis-only renderSong remains usable. Host/package sample
graphs already use supplied PCM. Legacy sampled rendering needs explicit host assets
before it is usable in restricted generators. User style loading and native IO/device/
network families are not in the ordinary-generator allowed set.

This is capability enforcement at native/module boundaries, not an OS sandbox.
Direct cooperative builds still lack a hard memory limit; isolated workers retain
their existing timeout/kill/join behavior. The remaining host-owned sample/style
asset path and tuning snapshot work must not be waived to declare authoring complete.
Review approved render paths whenever their implementations gain new side effects.

Tests cover a source declaring writeWav itself, actual absent-output verification,
direct and isolated rejection, loadWav/play denial, synthesis success and explicit
sample-file rejection. Initial focused run had three assertion-wording failures
(runtime says 'unavailable in this host', tests expected 'denied'); assertions were
corrected without weakening the denied-call checks.

Final affected backend/API regression: **534 passed**, zero failures/skips,
`/tmp/flow-native-generator-regression.log`. `git diff --check` passed. Full-core,
browser/publish and hardware checks are still pending.

Next: complete host-owned asset support and tuning context, review remaining
authoring surface gaps (including styles/vocalization), then integrated lifecycle
and full core/browser verification. Native UI/historical callback-gap work remains
deferred; physical device qualification is still open.
