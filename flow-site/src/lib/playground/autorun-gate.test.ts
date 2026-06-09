// §6.7 regression: #code=...&run=1 auto-run must be gated on navigator.userActivation.hasBeenActive.
// When no user activation is present (cold load), pendingAutoRun stays true so the
// 'Press Run to hear it' affordance is shown instead of triggering a suspended AudioContext.
//
// This is a logic-level unit test that verifies the activation-check predicate the page uses,
// since the page's onMount logic cannot be exercised directly in jsdom (no Monaco / WASM).

import { describe, it, expect } from 'vitest';

/**
 * Mirrors the check in +page.svelte §6.7: returns true only when the browser reports that
 * the page has been activated by a user gesture.
 */
function hasUserActivation(
	nav: { userActivation?: { hasBeenActive?: boolean } } | undefined
): boolean {
	return nav?.userActivation?.hasBeenActive === true;
}

describe('§6.7 — auto-run activation gate', () => {
	it('returns true when userActivation.hasBeenActive is true', () => {
		expect(hasUserActivation({ userActivation: { hasBeenActive: true } })).toBe(true);
	});

	it('returns false when userActivation.hasBeenActive is false (cold load)', () => {
		expect(hasUserActivation({ userActivation: { hasBeenActive: false } })).toBe(false);
	});

	it('returns false when userActivation is absent (old browser)', () => {
		expect(hasUserActivation({})).toBe(false);
	});

	it('returns false when navigator is undefined (SSR context)', () => {
		expect(hasUserActivation(undefined)).toBe(false);
	});

	it('returns false when hasBeenActive is undefined', () => {
		expect(hasUserActivation({ userActivation: {} })).toBe(false);
	});
});
