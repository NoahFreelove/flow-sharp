// §6.10 regression: the AudioContext Proxy, __flowEditorValue, and __flowRuntimeReady globals
// must be gated behind the ?e2e=1 URL parameter. This test verifies the URL-param detection
// logic independently of the page's onMount (which is WASM-dependent).

import { describe, it, expect } from 'vitest';

/**
 * Mirrors the isE2eMode detection in +page.svelte §6.10.
 * Returns true only when the URL search string contains 'e2e'.
 */
function detectE2eMode(searchString: string): boolean {
	return new URLSearchParams(searchString).has('e2e');
}

describe('§6.10 — e2e mode detection', () => {
	it('detects ?e2e=1', () => {
		expect(detectE2eMode('?e2e=1')).toBe(true);
	});

	it('detects ?e2e with no value', () => {
		expect(detectE2eMode('?e2e')).toBe(true);
	});

	it('detects ?other=x&e2e=1 (e2e among other params)', () => {
		expect(detectE2eMode('?other=x&e2e=1')).toBe(true);
	});

	it('is false for a plain /playground URL with no e2e param', () => {
		expect(detectE2eMode('')).toBe(false);
	});

	it('is false for ?run=1 alone (no e2e param)', () => {
		expect(detectE2eMode('?run=1')).toBe(false);
	});

	it('is false for ?foo=e2e (e2e only as a value, not a key)', () => {
		// 'foo' is the key; 'e2e' is the value — 'e2e' key not present.
		expect(detectE2eMode('?foo=e2e')).toBe(false);
	});
});
