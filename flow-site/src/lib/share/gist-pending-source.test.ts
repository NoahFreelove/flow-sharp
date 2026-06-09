// §6.1 regression: beginGistAuth stashes the editor source before navigating, and
// consumePendingGistSource pops it exactly once so the playground can restore it on #token= return.

import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import {
	beginGistAuth,
	consumePendingGistSource,
	GIST_PENDING_SOURCE_KEY
} from './gist';

const SAMPLE_SOURCE = `tempo 120 {\n  timesig 4/4 {\n    key Cmajor {\n      (print "hello")\n    }\n  }\n}`;

describe('§6.1 — OAuth source stash/restore', () => {
	beforeEach(() => {
		sessionStorage.clear();
		// Prevent actual navigation during tests.
		Object.defineProperty(window, 'location', {
			value: { href: '' },
			writable: true
		});
	});

	afterEach(() => {
		sessionStorage.clear();
		vi.restoreAllMocks();
	});

	it('beginGistAuth(source) stashes source in sessionStorage before navigating', () => {
		beginGistAuth(SAMPLE_SOURCE);
		expect(sessionStorage.getItem(GIST_PENDING_SOURCE_KEY)).toBe(SAMPLE_SOURCE);
		expect(window.location.href).toBe('/api/auth/github');
	});

	it('beginGistAuth() without source does not stash anything', () => {
		beginGistAuth();
		expect(sessionStorage.getItem(GIST_PENDING_SOURCE_KEY)).toBeNull();
	});

	it('consumePendingGistSource returns the stashed source', () => {
		sessionStorage.setItem(GIST_PENDING_SOURCE_KEY, SAMPLE_SOURCE);
		expect(consumePendingGistSource()).toBe(SAMPLE_SOURCE);
	});

	it('consumePendingGistSource removes the key after reading (one-shot)', () => {
		sessionStorage.setItem(GIST_PENDING_SOURCE_KEY, SAMPLE_SOURCE);
		consumePendingGistSource();
		expect(sessionStorage.getItem(GIST_PENDING_SOURCE_KEY)).toBeNull();
	});

	it('consumePendingGistSource returns null when nothing is stashed', () => {
		expect(consumePendingGistSource()).toBeNull();
	});

	it('beginGistAuth stash + consume round-trips the source exactly', () => {
		beginGistAuth(SAMPLE_SOURCE);
		const restored = consumePendingGistSource();
		expect(restored).toBe(SAMPLE_SOURCE);
		// Second consume returns null — not double-consumed.
		expect(consumePendingGistSource()).toBeNull();
	});
});
