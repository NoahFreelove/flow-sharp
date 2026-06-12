// Playground state — Svelte 5 runes (D-49-23). A single class instance holds editor + console +
// run-status state and exposes the `run`/`stop`/`newBlank` actions the +page.svelte wires up.
//
// The console split (D-48-15): `stdout` (print output, ink) is separate from `stderr` (advisories,
// ink-muted). `errors` are structured RunError[] rendered as escaped Rust-style boxes — NEVER
// `{@html}` (Security V5, T-49-05-XSS). The LED status drives <LedIndicator>:
//   idle → rendering (run() in flight) → playing (a tone went out) / error (errors present) → idle.

import type { FlowRuntime, RunError, RunResult } from '../runtime';
import { offerMidiDownload } from './download';
import { DEFAULT_SNIPPET_ID, snippetById } from './snippets';

export type RunStatus = 'idle' | 'rendering' | 'playing' | 'error';

/**
 * How long the LED shows 'playing' after a successful run before settling back to 'idle'.
 * The frozen runtime (D-48-09) plays audio internally during `run()` and exposes NO
 * playback-ended signal — `result.wav` comes back null and there is no onended callback — so
 * the 'playing' LED is a brief post-run HINT, not a live playback clock. Without this, the LED
 * stuck on 'playing' forever. (A precise clock would need a runtime-ended event; reserved for a
 * future non-frozen runtime.)
 */
const PLAYING_SETTLE_MS = 2000;

export class PlaygroundState {
	/** Current editor contents (mirrored from Monaco on Run). */
	editorValue = $state(snippetById(DEFAULT_SNIPPET_ID).source);

	/** Top-level boot-error message (HANDOFF §2.3) — distinct from per-run errors. */
	bootError = $state<string | null>(null);

	/** LED / run lifecycle status. */
	runStatus = $state<RunStatus>('idle');

	/** Captured `print` output (D-48-15). */
	stdout = $state('');
	/** Captured advisory `[X] ...` output (D-48-15). */
	stderr = $state('');
	/** Structured parse/eval/runtime errors (D-48-14) — rendered as escaped boxes. */
	errors = $state<RunError[]>([]);
	/** Encoded MIDI bytes from the last run, when writeMidi was called (D-48-18). */
	midi = $state<Uint8Array | null>(null);

	/** Wall-clock ms of the last run (status bar). */
	lastDurationMs = $state<number | null>(null);
	/** ISO timestamp of the last run (status bar). */
	lastRunAt = $state<string | null>(null);
	/** Which snippet is currently loaded (left-rail highlight). */
	activeSnippetId = $state<string>(DEFAULT_SNIPPET_ID);

	/** Pending timer that settles the LED from 'playing' back to 'idle' (see PLAYING_SETTLE_MS).
	 *  Not $state — it's plumbing, never rendered. Guarded so a new run/stop/load supersedes it. */
	private settleTimer: ReturnType<typeof setTimeout> | null = null;

	/** Cancel any pending 'playing' → 'idle' settle (idempotent). */
	private clearSettleTimer(): void {
		if (this.settleTimer !== null) {
			clearTimeout(this.settleTimer);
			this.settleTimer = null;
		}
	}

	/** True once at least one run has produced output (empty-state gating). */
	hasRun = $derived(
		this.stdout.length > 0 ||
			this.stderr.length > 0 ||
			this.errors.length > 0 ||
			this.lastRunAt !== null
	);

	/** True when the last run produced a downloadable export. */
	hasMidi = $derived(this.midi != null);

	/**
	 * Load a named snippet into the editor. Clears the run outputs (stdout/stderr/errors/midi/status)
	 * so the right-rail console + MIDI-download button never show STALE results from the previously-run
	 * snippet (WR-02 — downloading "Download MIDI" after switching would otherwise grab bytes that no
	 * longer match the loaded source). Mirrors newBlank()'s reset, minus the editor-clear.
	 */
	loadSnippet(id: string): void {
		const snip = snippetById(id);
		this.clearSettleTimer();
		this.editorValue = snip.source;
		this.activeSnippetId = id;
		this.stdout = '';
		this.stderr = '';
		this.errors = [];
		this.midi = null;
		this.runStatus = 'idle';
		// Reset the run timestamps too so the console returns to its empty state (hasRun is derived
		// from stdout/stderr/errors/lastRunAt) — a freshly-loaded snippet shows "nothing has run yet".
		this.lastRunAt = null;
		this.lastDurationMs = null;
	}

	/**
	 * Run the current source through the runtime. The CALLER (the Run onclick handler) is
	 * responsible for calling `runtime.resumeAudio()` in the SAME gesture frame BEFORE this — the
	 * autoplay gesture chain (D-48-09) lives in the page, not here, so this stays pure state logic.
	 */
	async run(runtime: FlowRuntime, source: string): Promise<void> {
		this.editorValue = source;
		// A new run supersedes a prior run's pending LED settle.
		this.clearSettleTimer();
		this.runStatus = 'rendering';
		this.errors = [];
		this.midi = null;

		let result: RunResult;
		try {
			result = await runtime.run(source);
		} catch (e) {
			// run() should not throw (per-script errors come back in RunResult.errors), but be
			// defensive: surface an unexpected throw as a runtime error box rather than crashing.
			this.runStatus = 'error';
			this.stdout = '';
			this.stderr = '';
			this.errors = [{ kind: 'runtime', message: e instanceof Error ? e.message : String(e) }];
			this.lastRunAt = new Date().toISOString();
			return;
		}

		this.stdout = result.stdout ?? '';
		this.stderr = result.stderr ?? '';
		// Drop the `cancel` kind defensively — it is defined but never raised in-browser (D-48-10),
		// and surfacing "cancelled" would confuse composers (UI-SPEC §error box note).
		this.errors = (result.errors ?? []).filter((err) => err.kind !== 'cancel');
		this.midi = result.midi ?? null;
		this.lastDurationMs = result.durationMs ?? null;
		this.lastRunAt = new Date().toISOString();

		if (this.errors.length > 0) {
			this.runStatus = 'error';
		} else {
			// The (play ...) tone already went out through WebAudioBackend during run(); reflect
			// "playing" briefly, then settle to idle. We can't observe the source-node's lifetime
			// from JS (frozen runtime: no playback-ended signal), so this is a status hint, not a
			// precise playback clock — settle to 'idle' after PLAYING_SETTLE_MS so the LED never
			// stays lit forever. The guard ensures a newer run/stop/load that moved us off
			// 'playing' is not clobbered by a stale timer.
			this.runStatus = 'playing';
			this.settleTimer = setTimeout(() => {
				if (this.runStatus === 'playing') this.runStatus = 'idle';
				this.settleTimer = null;
			}, PLAYING_SETTLE_MS);
			// §6.2(b): if the run produced no audio bytes (wav), no MIDI bytes, no stdout, and no
			// errors, the script likely only called (writeWav ...) — which writes to /tmp (not
			// audible in the browser). Surface a friendly advisory so the composer knows why they
			// heard nothing, rather than leaving them staring at a silent playground.
			const hasOutput =
				result.wav != null ||
				result.midi != null ||
				(result.stdout && result.stdout.trim().length > 0);
			if (!hasOutput) {
				const hint =
					'[playground] This script rendered to a file — add (play mix) (or the name of your final Buffer) to hear it in the browser.';
				this.stderr = this.stderr ? `${this.stderr}\n${hint}` : hint;
			}
		}
	}

	/**
	 * Offer the last run's MIDI as a download (HANDOFF §9). No-op when none was produced. Returns
	 * whether the download started; on failure (sandboxed/blocked context — WR-07) surfaces an
	 * advisory in the console rather than silently no-op'ing.
	 */
	downloadMidi(): boolean {
		if (this.midi == null) return false;
		const ok = offerMidiDownload(this.midi);
		if (!ok) {
			const note = "Couldn't start the MIDI download — your browser may be blocking it.";
			this.stderr = this.stderr ? `${this.stderr}\n${note}` : note;
		}
		return ok;
	}

	/** Stop any active playback + settle the LED to idle (idempotent). */
	stop(runtime: FlowRuntime | null): void {
		runtime?.stop();
		this.clearSettleTimer();
		this.runStatus = 'idle';
	}

	/** Clear to a blank snippet (the destructive "New blank" — caller owns the unsaved-edits confirm). */
	newBlank(): void {
		this.clearSettleTimer();
		this.editorValue = '';
		this.activeSnippetId = '';
		this.stdout = '';
		this.stderr = '';
		this.errors = [];
		this.midi = null;
		this.runStatus = 'idle';
	}
}
