import { describe, it, expect } from 'vitest';
import { filterRuntimeAdvisories, sourcePlaysAudio } from './advisories';

// The exact advisory the frozen WASM runtime emits at stdlib-load on the Web target (Interpreter.cs
// :1024 — WarnOnce). Pinned here so a wording change that breaks the filter is caught.
const MICBUFFER_NOISE =
	"[target] builtin 'micBuffer' unavailable on Web target — surface declared in stdlib but implementation stripped (Phase 47). Skipping; calls will report 'function not found'.";

describe('filterRuntimeAdvisories', () => {
	it('drops the always-on stripped-builtin (micBuffer) load advisory', () => {
		expect(filterRuntimeAdvisories(MICBUFFER_NOISE)).toBe('');
	});

	it('drops it regardless of the specific stripped builtin name', () => {
		const other =
			"[target] builtin 'loadSfz' unavailable on Web target — surface declared in stdlib but implementation stripped (Phase 47). Skipping; calls will report 'function not found'.";
		expect(filterRuntimeAdvisories(other)).toBe('');
	});

	it('keeps script-relevant advisories and removes only the noise line', () => {
		const stderr = `${MICBUFFER_NOISE}\n[tuning] unmapped MIDI keys under 'partch' — rendered as rest`;
		expect(filterRuntimeAdvisories(stderr)).toBe(
			"[tuning] unmapped MIDI keys under 'partch' — rendered as rest"
		);
	});

	it('keeps an actionable [target] module advisory (only the stripped-surface line is noise)', () => {
		const moduleAdvisory =
			"[target] module '@sfz' unavailable on Web target — line 1. Build with FlowTarget=Desktop to enable.";
		const stderr = `${MICBUFFER_NOISE}\n${moduleAdvisory}`;
		expect(filterRuntimeAdvisories(stderr)).toBe(moduleAdvisory);
	});

	it('collapses the blank gap left behind so there is no leading/trailing whitespace', () => {
		const stderr = `${MICBUFFER_NOISE}\n\n[live] entering live block at line 3`;
		const out = filterRuntimeAdvisories(stderr);
		expect(out).toBe('[live] entering live block at line 3');
		expect(out.startsWith('\n')).toBe(false);
	});

	it('passes an empty blob through untouched', () => {
		expect(filterRuntimeAdvisories('')).toBe('');
	});
});

describe('sourcePlaysAudio', () => {
	it('detects (play ...), the common example shape', () => {
		expect(sourcePlaysAudio('(play (renderSong s "square"))')).toBe(true);
	});

	it('detects (loop ...) and (preview ...)', () => {
		expect(sourcePlaysAudio('(loop buf)')).toBe(true);
		expect(sourcePlaysAudio('(preview seq)')).toBe(true);
	});

	it('tolerates whitespace after the paren', () => {
		expect(sourcePlaysAudio('(  play mix )')).toBe(true);
	});

	it('is false for a write-only script (the case the hint is FOR)', () => {
		expect(sourcePlaysAudio('(writeWav "out.wav" mix)')).toBe(false);
	});

	it('does not match a substring like (player ...) or a comment word', () => {
		expect(sourcePlaysAudio('(playback x)')).toBe(false);
		expect(sourcePlaysAudio('// remember to play it later')).toBe(false);
	});
});
