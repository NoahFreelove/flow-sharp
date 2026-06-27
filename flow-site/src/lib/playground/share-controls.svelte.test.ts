import { describe, it, expect, beforeEach } from 'vitest';
import { captureOAuthToken } from './share-controls.svelte';
import { getGistToken, GIST_TOKEN_KEY } from '../share/gist';

// WR-06 — captureOAuthToken reads a `#token=` fragment into sessionStorage. The fragment is
// attacker-influenceable (any link / redirect / iframe navigation can set it), so the value is now
// SHAPE-validated (GitHub `gho_`-style prefix + charset/length) BEFORE it is stored. A non-token
// `#token=` value must be ignored rather than cached as a bogus credential.

function setHash(hash: string): void {
	window.location.hash = hash;
}

describe('captureOAuthToken — token-shape validation (WR-06)', () => {
	beforeEach(() => {
		sessionStorage.clear();
		setHash('');
	});

	it('stores a shape-valid gho_ token from the fragment', () => {
		setHash('#token=gho_' + 'A1b2C3d4E5f6G7h8I9j0');
		expect(captureOAuthToken()).toBe(true);
		expect(getGistToken()).toBe('gho_A1b2C3d4E5f6G7h8I9j0');
	});

	it('accepts other known GitHub token prefixes (ghu_/ghp_/ghs_/ghr_)', () => {
		for (const prefix of ['ghu_', 'ghp_', 'ghs_', 'ghr_']) {
			sessionStorage.clear();
			const token = prefix + 'Z9y8X7w6V5u4T3s2R1q0';
			setHash('#token=' + token);
			expect(captureOAuthToken()).toBe(true);
			expect(getGistToken()).toBe(token);
		}
	});

	it('REJECTS an arbitrary attacker string with no GitHub prefix', () => {
		setHash('#token=ATTACKER_CONTROLLED_VALUE_123');
		expect(captureOAuthToken()).toBe(false);
		expect(getGistToken()).toBeNull();
		expect(sessionStorage.getItem(GIST_TOKEN_KEY)).toBeNull();
	});

	it('REJECTS a prefixed-but-too-short value', () => {
		setHash('#token=gho_short');
		expect(captureOAuthToken()).toBe(false);
		expect(getGistToken()).toBeNull();
	});

	it('REJECTS a prefixed value with an illegal charset (e.g. injected markup)', () => {
		setHash('#token=' + encodeURIComponent('gho_<script>alert(1)</script>aaaa'));
		expect(captureOAuthToken()).toBe(false);
		expect(getGistToken()).toBeNull();
	});

	it('returns false (no store) when there is no #token= fragment', () => {
		setHash('#code=abc123');
		expect(captureOAuthToken()).toBe(false);
		expect(getGistToken()).toBeNull();
	});
});
