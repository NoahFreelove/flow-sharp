/**
 * Regression test for §6.6: the home page's "Open in playground →" anchors must carry
 * the exact snippet shown on the card as a #code= playground deep link (D-49-08 contract).
 *
 * Before the fix the three anchors were bare `/playground` — clicking them landed on the
 * default playground snippet, discarding the code the user was looking at.
 *
 * After the fix each href is built with encode() from $lib/share/encode:
 *   /playground#code=<base64url-deflated-source>
 * and decode() round-trips the source back to the original snippet.
 *
 * This test:
 *  1. Reads the raw source to confirm no anchor has a bare '/playground' href.
 *  2. Verifies the three snippet constants (HELLO / SCALE / CADENCE) are each encoded
 *     and decode back correctly.
 *  3. Confirms CodeCard.svelte and examples.ts are gone from $lib/home.
 */

import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';
import { encode, decode } from '$lib/share/encode';

const __dirname = dirname(fileURLToPath(import.meta.url));
const HOME_PAGE = resolve(__dirname, '+page.svelte');
const src = readFileSync(HOME_PAGE, 'utf8');

// The three snippets as they appear in +page.svelte (must stay in sync if snippets change).
const HELLO = `use "@audio"\n(play (createSineTone 440Hz 1.0 0.5))`;
const SCALE = `use "@audio"\nuse "@composition"\n\ntempo 120 {\n  (play | C4q D4q E4q F4q G4q |)\n}`;
const CADENCE = `use "@composition"\n\nkey Cmajor {\n  (play | [D4 F4 A4]h [G3 B3 D4]h [C4 E4 G4]w |)\n}`;

describe('§6.6 — home playground deep-links carry the snippet source', () => {
	it('no "Open in playground →" snippet anchor has a bare /playground href', () => {
		// The old bug: the three "Open in playground →" anchors had href="/playground" (bare),
		// dropping the snippet. After the fix they use dynamic hrefs (helloHref/scaleHref/cadenceHref).
		// We check by looking for the specific pattern: open class + bare playground href on same line.
		const lines = src.split('\n');
		for (const line of lines) {
			if (line.includes('class="open"') && line.includes('href="/playground"')) {
				throw new Error(
					`Found a bare /playground href on an "open" anchor — snippet will be lost:\n  ${line.trim()}`
				);
			}
		}
	});

	it('the page imports encode from $lib/share/encode', () => {
		expect(src).toContain("from '$lib/share/encode'");
	});

	it('the page builds encoded hrefs for all three snippets', () => {
		expect(src).toContain('encode(HELLO)');
		expect(src).toContain('encode(SCALE)');
		expect(src).toContain('encode(CADENCE)');
	});

	it('encode/decode round-trips HELLO correctly', () => {
		const encoded = encode(HELLO);
		expect(decode(encoded)).toBe(HELLO);
	});

	it('encode/decode round-trips SCALE correctly', () => {
		const encoded = encode(SCALE);
		expect(decode(encoded)).toBe(SCALE);
	});

	it('encode/decode round-trips CADENCE correctly', () => {
		const encoded = encode(CADENCE);
		expect(decode(encoded)).toBe(CADENCE);
	});

	it('CodeCard.svelte is removed (dead code)', () => {
		const path = resolve(__dirname, '../lib/home/CodeCard.svelte');
		expect(existsSync(path), 'CodeCard.svelte should be deleted').toBe(false);
	});

	it('examples.ts is removed (dead code)', () => {
		const path = resolve(__dirname, '../lib/home/examples.ts');
		expect(existsSync(path), 'examples.ts should be deleted').toBe(false);
	});
});
