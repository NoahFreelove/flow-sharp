# Offline audio processor input — 2026-10-04

P7-42 adds actual input-consuming offline plugin execution to the shared generator
worker. This is an execution/transport milestone; selected-clip capture and an
integrated processing acceptance workflow remain next.

## Execution contract

GeneratorBuildRequest optionally carries PluginAudioInput. It contains immutable
stereo PcmAsset buses with the same sample rate and frame count. The current input
envelope supports 1–32 buses and an aggregate 16 MiB PCM bound. Existing worker
request limits still apply to the complete serialized package plus audio; requests
that exceed the 16 Mi-character transport budget fail explicitly. This is bounded
inline processing, not streaming processing for long recordings.

An OfflineAudio package builder has the signature:

```flow
proc process (Dict<String, Double>: context, Buffers: inputs)
    AudioGraph level = (dawGain "gain" (dawInput "input" 0) 0.5)
    (dawResult "processed" (dawAudio (dawProcess level inputs)))
end proc
```

The builder name/output layer come from the manifest. Input buses are mutable
evaluation-local buffer copies, never views into the host's immutable PCM.
Declared parameters enter context as `parameter:<id>` using a validated override
from GenerationContext.Parameters or the manifest default. Processor parameters
have no graph target mappings: their values drive the offline invocation itself.
Packages declaring processor kinds must therefore have an empty Targets list.

Before evaluating source, the adapter validates input presence, port count, typed
parameters and format limits. The offline graph helper uses its existing 256-frame
processing blocks; the processor manifest must support that size. The result must
contain exactly the declared audio layer at the input sample rate, with no score,
graph or instrument layers. Output length may include graph tails. Existing generated
PCM/output budgets still apply. Other builder kinds reject audio input explicitly.

Native policy, cancellation and process ownership remain the existing worker paths.
Protocol **3** carries the optional bounded input envelope and is checked on both
ends. The parent also validates returned processor output, rather than relying only
on the child. Old worker protocol versions are rejected, not silently adapted.

## Accepted output semantics

If a caller explicitly accepts processor output into a project, its saved public
values come from the actual result context, including defaults. JSON preserves
them alongside the pinned package and rendered PCM. Direct SetParameter/ResetParameter
and ApplyPreset reject processor instances: changing a label/value without rerunning
the input must not pretend to change the audio. The forthcoming host workflow must
retain input provenance and submit a new processing request with the selected values.
ProjectGeneratorHost still rejects processor categories; its ordinary build API does
not guess which clips or audio to consume.

## Example and evidence

`examples/plugins/offline-bus-mix.flow` and its matching `.flowplugin` mix two supplied
buses using a declared gain. The package is executable through the public worker
request contract without new native DSP functions.

- Final affected backend/platform/music/hosting/module suite: **480 passed**, zero
  failures or skips. `/tmp/flow-offline-processor-regression-final.log`.
- Initial focused processor/worker suite: **11 passed**.
  `/tmp/flow-offline-processor-focused.log`.
- Coverage includes default and override parameters, matching multibus format,
  immutable host input, exact PCM input/result interchange, real process execution
  and exit, missing/extra input rejection, invalid values, wrong output layer/type,
  executed-value persistence, live-edit rejection, and the packaged example.
- Existing process identity/cancellation tests pass with protocol 3. Module
  signatures are unchanged (703 native / 697 reachable). `git diff --check` passed.
- Full core/Web publication checkpoints in the effect-chain handoff predate this
  slice. No hardware was opened or qualified.

## Continue

Add the note-transform input/result path and a host processing transaction that
captures the selected input, rejects stale completion, and applies processed clips
as one undoable action. Do not conflate generic note/audio generation with those
input-consuming workflows. Then integrate bounce/stems and autosave/recovery, and
audit backend readiness. Native JUI frontend and callback-gap diagnosis remain
deferred. The overall backend goal remains active.
