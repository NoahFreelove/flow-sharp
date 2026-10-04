# Isolated DAW generator builds — 2026-10-04

P7-02 moves the structured composition-generator boundary through a real child
process. The previous goal turn added direct output; this turn adds hard process
termination, transport validation and verified process cleanup. The full backend
readiness goal is still active; no frontend or gap investigation was started.

## Implemented

- `flow-interpreter --daw-worker`: one bounded JSON request from stdin, one reply
  on stdout, then exit. Each build uses a fresh process and fresh Flow engine.
- `ProcessGeneratorWorker`: serializes builds, applies the request deadline to
  pipe writes/reads and process exit, drains stderr, kills the owned process tree
  on cancellation/timeout/failure, and awaits termination before releasing its
  build slot. A failed join retains ownership and is retried before any new child.
- Request/reply protocol version, per-request UUID, source ID, code revision and
  context revision are checked before a reply can become a musical result.
  Invalid JSON, unexpected exit, wrong identity, invalid layer count, invalid
  musical metadata and oversized messages fail the build without publishing.
- Input and output text limits are 16 Mi and 40 Mi characters respectively.
  Bounded reads apply before parsing. Source and generation limits from P7-01
  still apply; encoded output may hit transport limits before note-count limits.
- `CompositionJson`: versioned detached-score serialization with a shared section
  table. It preserves repeats, tuned pitches, notes/rests, articulation, ties,
  overlaps, portamento, exact tuplets, bars, controls and provenance. Repeated
  placements restore shared section objects. Import checks schema, references,
  metadata and resource limits before constructing the score.
- `LatestRequestCoordinator.SubmitAsync` supports async worker jobs with the same
  latest-generation publication rule. Existing synchronous Submit wraps it.
- `FlowDawGeneratorHost` accepts an owned ProcessGeneratorWorker. The cooperative
  constructor remains available for trusted tools/tests; DAW integration should use
  the isolated constructor. LastGood is not replaced on failed builds.

```csharp
using var host = new FlowDawGeneratorHost(sourceId,
    ProcessGeneratorWorker.ForInterpreter(interpreterDll));
var completion = await host.Submit(request);
// Commit only the accepted matching result through the document transaction path.
```

Disposal cancels in-flight work and joins the child. The synchronization objects
remain valid for callers already waiting during disposal. Parent-side decoding is
bounded and checks cancellation between layers; it is not a preemptible realtime
operation and must stay off the audio callback.

## Verification

Release interpreter build passed. **60 related tests passed** across StudioModel,
Hosting and SnapshotRenderingTests. Nine new cases cover:

1. Actual child-process output matches direct generation's complete serialized score.
2. Unresponsive child is killed/reaped; next build succeeds in a fresh process.
3. Cancellation interrupts a blocked large stdin write and reaps the child.
4. Malformed stdout fails publication and leaves no child running.
5. Isolated source host preserves LastGood after invalid Flow source.
6. A reply with the wrong request identity is rejected.
7. Disposal during a live build cancels and joins it; repeated disposal is safe.
8. Complete note metadata and shared repeated sections round-trip.
9. Invalid score references, controls and schema versions are rejected.

Logs: `/tmp/flow-daw-process-build.log`, `/tmp/flow-daw-process-tests.log`,
`/tmp/flow-daw-process-regression.log`. Existing package/analyzer warnings remain.
No full solution/Web publish or hardware stress run was performed. The real worker
and deliberate unresponsive workers were verified absent by their PIDs after join.

## Remaining scope

Process separation isolates engine GC and permits terminating a hung build. It is
not an OS privilege sandbox or hard resident-memory cap. General imported Flow
side effects still have the worker user's permissions. Metadata/text budgets do
not replace graph/voice/preparation memory budgets or OS-level restrictions.

Continue with the versioned shared device catalog and prepared graph, then the
instrument → ordered effects → group/master live/offline slice. Required follow-up
also includes full tempo/tuning/asset context, graph/audio generator outputs,
persisted source acceptance in one undo action, clip binding reconciliation,
project assets/devices/automation, and complete end-to-end edit/export verification.
Do not mark Phase 7 or frontend readiness complete based on this generator slice.
