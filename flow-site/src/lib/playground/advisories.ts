// Playground advisory hygiene — keep the stderr console relevant to the script that just ran.
//
// Two always-on sources of noise the playground used to surface on EVERY run:
//
//  1. The frozen WASM runtime emits a WarnOnce advisory at stdlib-load time for any builtin whose
//     surface ships in the stdlib but whose implementation is stripped on FlowTarget=Web (Phase 47
//     — e.g. `micBuffer`). A fresh FlowEngine is created per run, so it re-fires every single run
//     regardless of the script. It is not actionable here: a composer who actually *calls* such a
//     builtin still gets a real "function not found" in `result.errors` (which we never filter).
//
//  2. The playground's own "rendered to a file" hint. The frozen runtime always returns
//     `result.wav === null` and routes `(play …)` straight to the AudioContext (D-48-09), so the
//     result alone can't distinguish "played audio" from "only wrote a file" — every `(play …)`
//     script wrongly tripped the hint. `sourcePlaysAudio` lets the caller suppress it by source.

/**
 * Stable middle of the Phase-47 stripped-builtin advisory sentence. Matching on this substring
 * (rather than the whole line) avoids coupling to the em-dash or the specific builtin name, so it
 * keeps working if other stdlib surfaces get stripped on Web later.
 */
const STRIPPED_BUILTIN_MARKER = 'surface declared in stdlib but implementation stripped';

/**
 * Drop the always-on Web-target stdlib-load advisories from a stderr blob, leaving script-relevant
 * advisories (e.g. `[tuning] …`, an actionable `[target] module '@sfz' unavailable …`) intact.
 */
export function filterRuntimeAdvisories(stderr: string): string {
	if (!stderr) return stderr;
	return stderr
		.split('\n')
		.filter((line) => !line.includes(STRIPPED_BUILTIN_MARKER))
		.join('\n')
		.replace(/\n{3,}/g, '\n\n')
		.trim();
}

/**
 * Whether the Flow source asks to play audio directly — `(play …)`, `(loop …)`, or `(preview …)`.
 * Used to suppress the "rendered to a file" hint for scripts that genuinely played a tone.
 */
export function sourcePlaysAudio(source: string): boolean {
	return /\(\s*(play|loop|preview)\b/.test(source);
}
