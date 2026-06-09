import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

// REQ-SITE-A11Y-01..03 (D-49-10, Lighthouse Accessibility ≥90) — the axe-core a11y gate.
//
// Runs @axe-core/playwright (AxeBuilder) across the three primary routes — `/`, `/docs` (the
// index AND one `[slug]` page), and `/playground` (post-mount) — and asserts ZERO critical or
// serious violations on each. Critical is the plan's hard bar; serious is included as
// defence-in-depth (both are real, user-blocking failures — moderate/minor are advisory).
//
// Plus the non-automatable-by-Lighthouse contracts from UI-SPEC §Accessibility Contract:
//   - keyboard-only nav: Tab order matches visual order, no traps, visible :focus-visible
//   - aria-current="page" on the active nav route (landmark contract)
//   - aria-live status mirror on the playground LED (status not by colour alone)
//
// axe rules WCAG 2.1 A + AA. Decorative skeuo textures/screws/rails are aria-hidden by the
// component layer (Plan 49-02), so they don't trip axe.
//
// The `/` route ships the iOS-6 skeuomorphic home with its own chrome:
//   `nav[aria-label="Primary"]` in the toolbar + `nav[aria-label="Tab bar"]` at the bottom.
//   The brand link carries `.site-wordmark` for spec compatibility.
//   The shared layout chrome (`.site-wordmark`, `.site-nav-desktop`) is present on non-home routes.

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

/** Run axe on the current page and assert 0 critical + 0 serious violations. */
async function expectNoCriticalAxe(page: Page, label: string): Promise<void> {
	const results = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze();
	const blocking = results.violations.filter(
		(v) => v.impact === 'critical' || v.impact === 'serious'
	);
	// Surface a readable diagnostic when something trips — id + impact + the offending nodes.
	const summary = blocking
		.map(
			(v) =>
				`  [${v.impact}] ${v.id}: ${v.help}\n` +
				v.nodes.map((n) => `      ${n.target.join(' ')}`).join('\n')
		)
		.join('\n');
	expect(blocking, `axe critical/serious violations on ${label}:\n${summary}`).toEqual([]);
}

test.describe('axe a11y gate — 0 critical violations (REQ-SITE-A11Y-01..03)', () => {
	test('Home (/) has no critical/serious axe violations', async ({ page }) => {
		await page.goto('/');
		await page.getByRole('heading', { level: 1 }).first().waitFor();
		await expectNoCriticalAxe(page, 'Home /');
	});

	test('Docs index (/docs) has no critical/serious axe violations', async ({ page }) => {
		await page.goto('/docs');
		await page.getByRole('heading', { name: /Documentation/i }).waitFor();
		await expectNoCriticalAxe(page, 'Docs index /docs');
	});

	test('Docs page (/docs/[slug]) has no critical/serious axe violations', async ({ page }) => {
		await page.goto('/docs/flow-operator');
		await page.locator('main').waitFor();
		await expectNoCriticalAxe(page, 'Docs slug /docs/flow-operator');
	});

	test('Playground (/playground) has no critical/serious axe violations', async ({ page }) => {
		await page.goto('/playground');
		// Wait for the client-only shell to mount its landmarks (the output aside + console).
		await page.locator('aside[aria-label="Output"]').waitFor();
		await page.getByTestId('console').waitFor();
		await expectNoCriticalAxe(page, 'Playground /playground');
	});

	test('Showcase (/showcase) has no critical/serious axe violations', async ({ page }) => {
		await page.goto('/showcase');
		await page.locator('main').waitFor();
		await expectNoCriticalAxe(page, 'Showcase /showcase');
	});
});

test.describe('keyboard + ARIA landmark contracts (UI-SPEC §Accessibility Contract)', () => {
	test('the Primary nav landmark exists and is labelled', async ({ page }) => {
		await page.goto('/');
		// The iOS-6 home ships `nav[aria-label="Primary"]` in the toolbar.
		// It is always attached (at ≤600px it is CSS-hidden but still in the DOM).
		await expect(page.locator('nav[aria-label="Primary"]').first()).toBeAttached();
		// Exactly one <main> per page (landmark contract).
		await expect(page.locator('main')).toHaveCount(1);
	});

	test('aria-current="page" lands on the active route (desktop nav)', async ({ page }, testInfo) => {
		// aria-current is asserted on the desktop tab strip; the mobile hamburger panel mirrors it
		// (covered by nav.spec.ts). Early-out on the <768px viewport projects.
		const width = testInfo.project.use.viewport?.width ?? 1280;
		if (width < 768) return;
		await page.goto('/docs');
		const tabs = page.locator('.site-nav-desktop nav[aria-label="Primary"]');
		await expect(tabs.getByRole('link', { name: 'Docs' })).toHaveAttribute('aria-current', 'page');
	});

	test('keyboard Tab reaches the wordmark and nav without a trap (desktop)', async ({ page }, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		if (width < 768) return;
		await page.goto('/');
		await page.waitForLoadState('domcontentloaded');
		// Tab from the top: the first focusable in reading order is the brand/wordmark home link.
		// On the iOS-6 home this is the `.brand.site-wordmark` element in the toolbar.
		await page.keyboard.press('Tab');
		const firstFocused = await page.evaluate(() => {
			const el = document.activeElement as HTMLElement | null;
			return { tag: el?.tagName, cls: el?.className, text: el?.textContent?.trim().slice(0, 20) };
		});
		// The brass :focus-visible ring is CSS — here we assert focus actually MOVED into the page
		// (no trap on <body>) and reached an interactive element in the chrome.
		expect(firstFocused.tag).toBe('A');
		// The iOS-6 home brand link carries both .brand and .site-wordmark classes.
		expect(firstFocused.cls).toContain('site-wordmark');

		// A few more Tabs must keep advancing through real interactive elements (no keyboard trap):
		// each press lands focus on a NEW focusable, never sticking on one element or escaping to body.
		const seen = new Set<string>();
		for (let i = 0; i < 5; i++) {
			await page.keyboard.press('Tab');
			const id = await page.evaluate(() => {
				const el = document.activeElement as HTMLElement | null;
				if (!el || el === document.body) return 'BODY';
				return `${el.tagName}.${el.className}#${el.id}`;
			});
			expect(id, 'focus must not escape to <body> (no trap / no dead end)').not.toBe('BODY');
			seen.add(id);
		}
		// Focus advanced through multiple distinct elements (tab order is live, not stuck).
		expect(seen.size).toBeGreaterThan(1);
	});

	test('playground LED status is mirrored in an aria-live region (status not by colour alone)', async ({
		page
	}) => {
		await page.goto('/playground');
		await page.locator('aside[aria-label="Output"]').waitFor();
		// The LedIndicator ships a visually-hidden aria-live="polite" status mirror (Plan 49-02).
		// Assert at least one polite live region exists inside the Output landmark.
		const liveRegions = page.locator('aside[aria-label="Output"] [aria-live="polite"]');
		await expect(liveRegions.first()).toBeAttached();
	});
});
