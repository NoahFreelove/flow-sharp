import { test, expect } from '@playwright/test';
import categories from '../docs-categories.json' with { type: 'json' };

// REQ-SITE-DOCS-03 — the /docs index is a CONFIG-DRIVEN categorized TOC (D-49-22).
//
// Asserts the four category headers render (sourced from docs-categories.json, not hard-coded) and
// that the number of `/docs/[slug]` links equals the number of categorized wiki pages. The four
// category names + the page count are read from docs-categories.json so the test tracks the config.

const config = categories as Record<string, string[]>;
const categoryNames = Object.keys(config);
const categorizedPageCount = Object.values(config).reduce((n, pages) => n + pages.length, 0);

test.describe('REQ-SITE-DOCS-03: categorized TOC renders from docs-categories.json', () => {
	test('renders the four category headers + the right link count', async ({ page }, testInfo) => {
		if (testInfo.project.name !== 'desktop') return; // assert the layout once, on desktop

		await page.goto('/docs');

		// One <main> with the index.
		await expect(page.locator('main.docs-index')).toBeVisible();

		// Each category name from the config appears as a panel header (.skeuo-panel__title).
		const headers = page.locator('.skeuo-panel__title');
		await expect(headers).toHaveCount(categoryNames.length);
		for (const name of categoryNames) {
			await expect(
				headers.filter({ hasText: name }),
				`category header "${name}"`
			).toHaveCount(1);
		}

		// The link count equals the number of categorized wiki pages (all 26 exist as wiki files).
		const links = page.locator('a.docs-index__link');
		await expect(links).toHaveCount(categorizedPageCount);

		// Every link points at a /docs/ route.
		const count = await links.count();
		for (let i = 0; i < count; i++) {
			const href = await links.nth(i).getAttribute('href');
			expect(href).toMatch(/^\/docs\//);
		}
	});

	test('the config declares exactly four categories covering 26 pages', () => {
		expect(categoryNames).toEqual([
			'Getting Started',
			'Music Concepts',
			'Audio + Output',
			'Reference'
		]);
		expect(categorizedPageCount).toBe(26);
	});
});
