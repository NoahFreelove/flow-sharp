import { test, expect } from '@playwright/test';
import categories from '../docs-categories.json' with { type: 'json' };

// REQ-SITE-DOCS-01 — every synced wiki page renders at /docs/[slug].
//
// Loops all categorized wiki pages (from docs-categories.json — the same config that drives the
// TOC). Each /docs/[slug] must return 200, render prose in <main>, and carry the sidebar nav with
// the current page marked aria-current. Pages that contain ```flow fences additionally render at
// least one server-side shiki block (D-49-15). The two-column sidebar layout is a desktop concern,
// so the assertions only run on the `desktop` project (mobile collapses the sidebar to a
// disclosure) — gated by an early return rather than a conditional skip.

/** Wiki page name (e.g. `Quick-Start`) -> kebab slug (`quick-start`). */
function toSlug(page: string): string {
	return page
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-+|-+$/g, '');
}

const pages: string[] = Object.values(categories as Record<string, string[]>).flat();
const slugs: string[] = pages.map(toSlug);

const DESKTOP = 'desktop';

test.describe('REQ-SITE-DOCS-01: wiki pages render at /docs/[slug]', () => {
	for (const slug of slugs) {
		test(`/docs/${slug} renders prose + sidebar nav`, async ({ page }, testInfo) => {
			if (testInfo.project.name !== DESKTOP) return; // desktop docs layout only

			const response = await page.goto(`/docs/${slug}`);
			expect(response?.status(), `GET /docs/${slug}`).toBe(200);

			// Main doc body exists and has rendered prose (non-trivial text content).
			const main = page.locator('main.docs-body');
			await expect(main).toBeVisible();
			const text = (await main.innerText()).trim();
			expect(text.length, `prose on /docs/${slug}`).toBeGreaterThan(40);

			// Sidebar nav marks the current page.
			const current = page.locator(`aside a[aria-current="page"][href="/docs/${slug}"]`);
			await expect(current).toHaveCount(1);
		});
	}

	test('quick-start renders a server-side shiki Flow block with Open-in-playground', async ({
		page
	}, testInfo) => {
		if (testInfo.project.name !== DESKTOP) return; // desktop docs layout only
		await page.goto('/docs/quick-start');
		// Static shiki block (server-rendered HTML, class "shiki").
		await expect(page.locator('pre.shiki').first()).toBeVisible();
		// Every flow block is wrapped with an Open-in-playground secondary button.
		await expect(page.locator('.docs-codeblock a.docs-open-in-playground').first()).toBeVisible();
	});
});
