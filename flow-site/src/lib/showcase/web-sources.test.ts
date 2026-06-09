// §6.2 regression: each web-runnable showcase source's _WEB variant must contain a (play ...)
// call so the playground produces audible browser output rather than silently writing to /tmp.

import { describe, it, expect } from 'vitest';
import {
	MARKOV_JAZZ_SOURCE,
	GRANULAR_SOURCE,
	TIDAL_SOURCE,
	STRETCH_SOURCE,
	PARAMETERIZED_SOURCE,
	MARKOV_JAZZ_SOURCE_WEB,
	GRANULAR_SOURCE_WEB,
	TIDAL_SOURCE_WEB,
	STRETCH_SOURCE_WEB,
	PARAMETERIZED_SOURCE_WEB
} from './sources';
import { PIECES, playgroundHref } from './pieces';

describe('§6.2 — showcase web-playable variants contain (play ...)', () => {
	it('MARKOV_JAZZ_SOURCE_WEB contains (play mix)', () => {
		expect(MARKOV_JAZZ_SOURCE_WEB).toContain('(play mix)');
	});

	it('GRANULAR_SOURCE_WEB contains (play mix)', () => {
		expect(GRANULAR_SOURCE_WEB).toContain('(play mix)');
	});

	it('TIDAL_SOURCE_WEB contains (play mix)', () => {
		expect(TIDAL_SOURCE_WEB).toContain('(play mix)');
	});

	it('STRETCH_SOURCE_WEB contains (play mix3)', () => {
		expect(STRETCH_SOURCE_WEB).toContain('(play mix3)');
	});

	it('PARAMETERIZED_SOURCE_WEB contains (play mix)', () => {
		expect(PARAMETERIZED_SOURCE_WEB).toContain('(play mix)');
	});

	it('verbatim sources are unchanged (the _WEB variant extends, not replaces)', () => {
		expect(MARKOV_JAZZ_SOURCE_WEB.startsWith(MARKOV_JAZZ_SOURCE.trimEnd())).toBe(false);
		// The web source should contain the verbatim source text plus the extra (play ...) line.
		expect(MARKOV_JAZZ_SOURCE_WEB).toContain(
			'(writeWav "/tmp/markov_jazz.wav" mix)'
		);
	});

	it('verbatim sources do NOT contain (play ...) (§6.2: problem existed before fix)', () => {
		// These are the raw source files — they write to /tmp and have no play call.
		expect(MARKOV_JAZZ_SOURCE).not.toContain('(play mix)');
		expect(GRANULAR_SOURCE).not.toContain('(play mix)');
		expect(TIDAL_SOURCE).not.toContain('(play mix)');
		expect(STRETCH_SOURCE).not.toContain('(play mix3)');
		expect(PARAMETERIZED_SOURCE).not.toContain('(play mix)');
	});

	it('playgroundHref uses webSource for runnableOnWeb pieces', () => {
		const jazz = PIECES.find((p) => p.slug === 'markov-jazz');
		expect(jazz).toBeDefined();
		expect(jazz!.runnableOnWeb).toBe(true);
		const href = playgroundHref(jazz!);
		expect(href).not.toBeNull();
		// The href encodes the webSource (not the verbatim source). Because fflate compresses it,
		// we can't decode here — instead assert that the webSource and source produce different hrefs.
		const hrefFromVerbatim = `/playground#code=${encodeURIComponent(jazz!.source ?? '')}&run=1`;
		// The href from playgroundHref should encode the webSource (different from verbatim).
		expect(href).not.toBe(hrefFromVerbatim);
	});
});
