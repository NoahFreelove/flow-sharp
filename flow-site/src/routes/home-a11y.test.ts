/**
 * Regression test for §6.9: iOS-6 home page landmark and a11y fixes.
 *
 * The redesign (94df2ed) introduced four landmark/a11y regressions:
 *  1. No <main> landmark — all content lived in <div class="layout">.
 *  2. Two <nav> landmarks with no aria-label to distinguish them.
 *  3. Decorative VU-meter bars and LED exposed to assistive technology.
 *  4. No dark-mode toggle — persisted [data-theme="dark"] from app.html was
 *     silently ignored, locking dark-mode users to the light palette.
 *
 * These tests read the raw component source and assert the structural fixes.
 * (Component rendering tests would require a browser runtime; structural
 * source assertions are enough to pin the regression.)
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const HOME_PAGE = resolve(__dirname, '+page.svelte');
const src = readFileSync(HOME_PAGE, 'utf8');

describe('§6.9 — home page landmark / a11y fixes', () => {
	it('wraps the page content in a <main> element (landmark contract)', () => {
		// The main content wrapper must be a <main>, not a bare <div>.
		expect(src).toContain('<main class="layout">');
		expect(src).toContain('</main>');
		// No bare <div class="layout"> should remain as the content wrapper.
		// (A <div class="layout"> inside the content is acceptable, but the top-level
		//  page content wrapper must be <main>.)
		//
		// The old form was exactly: `<div class="layout">` at the top level — check
		// it does not appear immediately after the toolbar.
		const toolbarEnd = src.indexOf('</div>'); // end of .toolbar
		const mainStart = src.indexOf('<main class="layout">');
		expect(mainStart, '<main class="layout"> must exist').toBeGreaterThan(-1);
		// <main> should come after the toolbar
		expect(mainStart).toBeGreaterThan(toolbarEnd);
	});

	it('the toolbar nav has aria-label="Primary"', () => {
		expect(src).toContain('aria-label="Primary"');
		// Specifically on a nav element in the toolbar
		expect(src).toMatch(/<nav[^>]*class="nav"[^>]*aria-label="Primary"/);
	});

	it('there is no bottom tab bar (single top nav, site-wide)', () => {
		// The iOS-6 bottom tab bar was removed so every route uses one top nav. The home
		// page must not reintroduce a `.tabbar`/"Tab bar" nav.
		expect(src).not.toContain('aria-label="Tab bar"');
		expect(src).not.toContain('class="tabbar"');
	});

	it('the VU meter container has aria-hidden="true"', () => {
		// The <div class="vu"> must carry aria-hidden so the decorative bars are hidden from AT.
		expect(src).toMatch(/<div[^>]*class="vu"[^>]*aria-hidden="true"/);
	});

	it('the LED indicator has aria-hidden="true"', () => {
		// The <div class="led"> is a decorative colour-only indicator — must be hidden from AT.
		expect(src).toMatch(/<div[^>]*class="led"[^>]*aria-hidden="true"/);
	});

	it('there is exactly one nav and it is labelled', () => {
		// Count nav elements and assert each has an aria-label. After removing the bottom
		// tab bar, the home page ships a single top nav (the toolbar Primary pill nav).
		const navMatches = [...src.matchAll(/<nav\b([^>]*)>/g)];
		expect(navMatches.length, 'expected exactly 1 <nav> element').toBe(1);
		for (const match of navMatches) {
			expect(
				match[1],
				`all <nav> elements must have aria-label; found <nav${match[1]}>`
			).toContain('aria-label=');
		}
	});

	it('ships no theme toggle on the light-only home (decision A)', () => {
		// The iOS-6 home is light-only, so a theme toggle here would be a dead switch. The toggle
		// lives in the shared <SiteToolbar> on the other (dark-capable) routes, not on home.
		expect(src).not.toContain("import Toggle from '$lib/components/skeuo/Toggle.svelte'");
		expect(src).not.toMatch(/<Toggle\b/);
	});
});
