// Thin wrapper around the FROZEN Phase 48 `flow-runtime.js` ES module (HANDOFF §4, §8).
//
// This file NEVER edits `flow-runtime.js` (D-48-13 frozen surface — HANDOFF §8 "DO NOT modify").
// It exists purely so SvelteKit code imports a typed `bootRuntime()` instead of touching the
// opaque .NET AppBundle module directly. The runtime self-loads its own `./_framework/dotnet.js`
// from `static/wasm/`; Vite must NOT analyze or pre-bundle it, hence the `@vite-ignore` on the
// dynamic import string (RESEARCH Pattern 2 / Pitfall 2).
//
// Boot errors vs. run errors are distinct (HANDOFF §2.3):
//   - `bootRuntime()` THROWS `Error('Flow runtime boot failed: ...')` if Mono-WASM can't boot.
//   - Per-script errors come back inside `RunResult.errors[]`, never as thrown exceptions.

/** Structured per-script error (HANDOFF §4 / D-48-14). `cancel` is defined but not raised in-browser. */
export interface RunError {
	kind: 'parse' | 'eval' | 'runtime' | 'cancel' | 'platform-not-supported';
	message: string;
	/** 1-based line, when known. */
	line?: number;
	/** 1-based column, when known. */
	column?: number;
	/** Quoted source line for Rust-style diagnostic boxes. */
	sourceSnippet?: string;
}

/**
 * Result of one `run()` (HANDOFF §4 / D-48-14/15/18). camelCase JSON; absent `wav`/`midi` are
 * OMITTED (not null) — test with `result.midi != null`.
 */
export interface RunResult {
	/** Reserved; currently null from run() — in-browser audio plays live via WebAudioBackend. */
	wav?: Float32Array;
	/** Encoded SMF bytes when the Flow source called writeMidi (D-48-17/18). */
	midi?: Uint8Array;
	/** Captured `print` output (D-48-15). */
	stdout: string;
	/** Captured advisory `[X] ...` output (D-48-15). */
	stderr: string;
	/** Structured parse/eval/runtime errors (D-48-14). */
	errors: RunError[];
	/** Wall-clock ms — NOT byte-identical across runs; exclude from any cmp. */
	durationMs: number;
}

/** The frozen runtime surface (HANDOFF §4). Documentation-only typing over the JS module. */
export interface FlowRuntime {
	run(source: string): Promise<RunResult>;
	play(wav: Float32Array | number[], sampleRate?: number, channels?: number): void;
	stop(): void;
	dispose(): void;
	/** D-48-09 — MUST be called from a user-gesture frame in the SAME frame as run(). */
	resumeAudio(): Promise<void>;
}

/** The module's single export. We re-declare it for typing; the real impl is `flow-runtime.js`. */
type FlowRuntimeModule = { loadFlowRuntime: () => Promise<FlowRuntime> };

/**
 * Lazy-boot the Phase 48 WASM runtime. MUST be called only in the browser (onMount), never during
 * SSR/prerender. Idempotent at the runtime layer — `loadFlowRuntime()` caches after first boot.
 *
 * @throws {Error} `Flow runtime boot failed: ...` when Mono-WASM cannot boot — surface this in a
 *   top-level boot-error pane, distinct from per-run `RunResult.errors[]`.
 */
export async function bootRuntime(): Promise<FlowRuntime> {
	// `@vite-ignore` keeps Vite from analyzing the opaque .NET module (RESEARCH Pattern 2). The
	// absolute `/wasm/flow-runtime.js` path resolves against `static/` at runtime; the module
	// then descends into its sibling `./_framework/dotnet.js` (HANDOFF Pitfall 2 layout contract).
	// `@ts-expect-error` — this path is a runtime-only static asset (not a TS-resolvable module);
	// the frozen runtime is `flow-runtime.js` (HANDOFF §8 do-not-edit), so no `.d.ts` ships for it.
	// @ts-expect-error runtime-only static-asset import; typed via FlowRuntimeModule below.
	const mod: FlowRuntimeModule = await import(/* @vite-ignore */ '/wasm/flow-runtime.js');
	// loadFlowRuntime() already wraps its own boot in try/catch and throws the friendly
	// 'Flow runtime boot failed: ...' message (HANDOFF §2.3) — we propagate it unchanged.
	return await mod.loadFlowRuntime();
}
