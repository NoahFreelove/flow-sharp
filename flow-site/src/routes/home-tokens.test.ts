/**
 * Regression test for §6.5: the home page must NOT declare --font-mono on :root.
 *
 * Svelte emits :root rules unscoped/globally, so any CSS custom property declared
 * in :root inside a component leaks to every route once the user visits /. The fix
 * moves the iOS-6 token set from :root onto .ios6-page, where they inherit to all
 * descendants but cannot override tokens.css's JetBrains Mono --font-mono on other
 * routes (/docs, /playground).
 *
 * This test reads the raw component source and asserts:
 *  1. No :root block contains --font-mono.
 *  2. The .ios6-page rule DOES contain --font-mono (the token is still present, just scoped).
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const HOME_PAGE = resolve(__dirname, '+page.svelte');
const src = readFileSync(HOME_PAGE, 'utf8');

describe('§6.5 — home page :root token scope', () => {
	it('does NOT declare --font-mono inside a :root block', () => {
		// Extract all :root { ... } blocks from the source.
		// Match :root { ... } allowing for whitespace.
		const rootBlockPattern = /:root\s*\{([^}]*(?:\{[^}]*\}[^}]*)*)\}/g;
		let match: RegExpExecArray | null;
		const rootDeclarations: string[] = [];
		while ((match = rootBlockPattern.exec(src)) !== null) {
			rootDeclarations.push(match[1]);
		}
		// None of the :root blocks should declare --font-mono.
		for (const block of rootDeclarations) {
			expect(block, ':root block must not contain --font-mono').not.toContain('--font-mono');
		}
	});

	it('declares --font-mono inside the .ios6-page scope (not lost)', () => {
		// The token must still exist in the file so the component renders correctly.
		expect(src).toContain('--font-mono:');
	});

	it('the .ios6-page rule appears before --font-mono declaration (sanity)', () => {
		const iosPageIdx = src.indexOf('.ios6-page {');
		const fontMonoIdx = src.indexOf('--font-mono:');
		// .ios6-page selector must exist and --font-mono must appear after it.
		expect(iosPageIdx).toBeGreaterThan(-1);
		expect(fontMonoIdx).toBeGreaterThan(iosPageIdx);
	});
});
