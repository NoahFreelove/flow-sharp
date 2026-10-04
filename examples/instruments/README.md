# Flow instruments

`project-sample.flow` plays a project audio asset as a pitched instrument and
returns editable notes, an instrument and a mixer graph. No `writeMidi` or file
render call is needed: the DAW accepts the named outputs and routes them.

To use your recording:

1. Import its WAV into the project's asset collection. Keep the returned asset ID.
2. Replace the example's `11111111-1111-1111-1111-111111111111` with that ID.
3. Grant this source access to the asset. The host API is
   `ProjectGeneratorHost.RequestBuildWithAssets(descriptor, code, [assetId])`.
   Successful acceptance saves the grant; subsequent `RequestBuild` calls reuse it.
4. Place the `main` note output on a track, select `synth` as its instrument and
   use `mixer` as the master graph. The notes can then be moved, cut or edited with
   the same project operations as other note clips.

The sample's root pitch is 440 Hz (A4); set it to the recording's actual root pitch.
The sampler uses eight voices, immediate attack and a 20 ms release. It plays
one-shot samples with linear resampling; looping, streaming and automatic pitch
detection are not implemented. For an explicit envelope/velocity graph, see
`sampled-instrument.flow`, which builds its sample in memory.

The host checks the granted file's content hash and supplies bounded captured PCM
to the worker. Missing, changed or ungranted assets fail the build and preserve
the previous accepted output. This explicit path does not enable legacy implicit
piano/sample-directory lookup. Worker process isolation is not an OS sandbox.

Run the executable host example test from the repository root:

```sh
dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter FullyQualifiedName~GrantedSampleExample
```

The test imports a generated WAV, builds this exact example in an isolated worker,
routes its outputs, saves/reopens the project and checks that playback is audible
and sample-identical after reopening.
