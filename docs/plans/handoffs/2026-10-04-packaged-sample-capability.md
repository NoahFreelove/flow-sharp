# Packaged sample capability and drum plugin — 2026-10-04

P7-37 enables sample assets inside declared plugin builds without arbitrary file IO.

## Implemented

`dawPluginSample(dependencyId)` resolves only an exact dependency ID included in the
pinned package. The build-local implementation revalidates bytes against manifest
version/hash, decodes a bounded in-memory RIFF/WAVE stream using WaveAssetReader,
and caches immutable PCM by ID. Aggregate decoded cache is limited to 16 MiB per
build; decoding observes the shared cancellation budget. Empty/malformed/unsupported
WAVE data rejects. Normal engine use without a declared package rejects capability
access. There is no path fallback, network lookup or arbitrary filesystem read.

The signature belongs to the existing detached DAW construction allowlist, and its
implementation is installed only in the fresh plugin build engine. Imports now
strictly decode pinned bytes as UTF-8 when that dependency is actually imported,
so packages may contain binary assets without eager module-decoding failures. Module
resolution still permits only declared IDs and the supported bundled API list.
A binary dependency imported as code must still satisfy UTF-8/parser requirements.
No OS sandbox or global process-memory limit is implied.

`examples/plugins/sampled-drum.flow` and its `.flowplugin` compose the sample reader,
ADSR, velocity response, cutoff and public level control. The package embeds and
hash-pins `assets/kick.wav`, a small locally generated mono PCM hit. Decode duplicates
mono to stereo explicitly. Runtime voices share immutable PCM and perform no IO.
Level/cutoff support live controls; release remains structural. The ordinary sampler
generator example continues to work independently of this capability.

## Evidence

Four focused example/module tests passed (`/tmp/flow-packaged-samples.log`). The
saved drum package passes actual isolated host build, public value/audio scaling,
save/reopen, executable project export and rebuild with retained overrides, using
the same parity test as the subtractive instrument. Module snapshots add exactly
one native/public function: 701→702 native, 695→696 reachable; the same six legacy
unreachable signatures remain.

Negative coverage checks packaged WAVE bytes equal the checked-in asset, undeclared
absolute-looking IDs cannot resolve, correctly hash-pinned malformed WAVE rejects,
and ordinary engines cannot use the capability. Existing native-policy tests still
cover rejected arbitrary builtin IO.

All **389** affected backend/module tests passed with zero failures
(`/tmp/flow-packaged-sample-regression.log`). `git diff --check` passed.

## Next

Refresh full core/Web compatibility, then advance MIDI input/basic recording,
device instances/presets and remaining integrated DAW workflows. Multi-zone/looping
samplers and higher-quality resampling remain beyond the current one-shot primitive.
Native frontend and historical callback-gap diagnosis remain deferred; physical
audio qualification is still open.
