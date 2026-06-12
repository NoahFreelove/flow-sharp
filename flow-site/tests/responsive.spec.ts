import { test, expect, type Page } from '@playwright/test';

// REQ-SITE-RESPONSIVE-01 (D-49-09, ROADMAP AC-7) — the <768px single-column collapse, proven
// down to the narrowest supported 320px target with NO horizontal overflow on any route.
//
// This spec runs under all three viewport projects (desktop 1280 / mobile 375 / mobile-narrow
// 320). The mobile-specific assertions (single-column stack, Monaco read-only, Contents
// disclosure, hamburger, single-column showcase) gate on width < 768; the no-horizontal-overflow
// check runs on EVERY project (it must hold at 320, 375, AND 1280 — a desktop overflow is just
// as broken). The 320px project is the hard ROADMAP AC-7 floor.
//
// The `/` route ships the iOS-6 skeuomorphic home with its own chrome (no shared layout header):
//   - All widths: toolbar pill nav visible (scrolls horizontally ≤600px); no hamburger, no tabbar.
// Non-home routes now render the SAME shared <SiteToolbar> — identical bar, no hamburger.

const ROUTES = ['/', '/docs', '/docs/flow-operator', '/playground', '/showcase'];

/** No route may scroll horizontally: the document's scrollWidth must fit its clientWidth. */
async function expectNoHorizontalOverflow(page: Page, route: string): Promise<void> {
	// +1px tolerance for sub-pixel rounding in the layout engine.
	const overflow = await page.evaluate(() => {
		const doc = document.documentElement;
		return { scrollWidth: doc.scrollWidth, clientWidth: doc.clientWidth };
	});
	expect(
		overflow.scrollWidth,
		`${route} overflows horizontally: scrollWidth ${overflow.scrollWidth} > clientWidth ${overflow.clientWidth}`
	).toBeLessThanOrEqual(overflow.clientWidth + 1);
}

test.describe('no horizontal overflow on any route (REQ-SITE-RESPONSIVE-01, 320px floor)', () => {
	for (const route of ROUTES) {
		test(`no horizontal overflow: ${route}`, async ({ page }) => {
			await page.goto(route);
			// Wait for the first landmark (main or aside) to be visible before measuring.
			// The iOS-6 home wraps its content in <main>; other routes also have <main>.
			await page.locator('main, aside').first().waitFor();
			// Let any client-only mount (playground shell) settle before measuring.
			await page.waitForLoadState('networkidle');
			await expectNoHorizontalOverflow(page, route);
		});
	}
});

test.describe('single-column collapse <768px (D-49-09)', () => {
	test('the shared toolbar nav is the single nav at every width (no hamburger, no tab bar)', async ({ page }) => {
		// Both non-home and home render the SAME iOS-6 toolbar: one pill nav, visible at every
		// width (it scrolls horizontally ≤600px). There is no hamburger and no bottom tab bar.
		for (const route of ['/docs', '/']) {
			await page.goto(route);
			await expect(page.locator('nav[aria-label="Primary"]').first()).toBeVisible();
			await expect(page.getByRole('button', { name: /open menu/i })).toHaveCount(0);
			await expect(page.locator('nav[aria-label="Tab bar"]')).toHaveCount(0);
		}
	});

	test('playground stacks controls→editor→console + Monaco read-only + mobile banner', async ({
		page
	}, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		await page.goto('/playground');
		await page.locator('aside[aria-label="Output"]').waitFor();

		const rail = page.locator('aside[aria-label="Snippets and controls"]');
		const editor = page.locator('.pg-editor');
		const output = page.locator('aside[aria-label="Output"]');
		const monaco = page.getByTestId('monaco');

		if (width < 768) {
			// Single-column vertical stack: the rail sits above the editor, which sits above the output.
			const railBox = await rail.boundingBox();
			const editorBox = await editor.boundingBox();
			const outputBox = await output.boundingBox();
			expect(railBox, 'rail visible').not.toBeNull();
			expect(editorBox, 'editor visible').not.toBeNull();
			expect(outputBox, 'output visible').not.toBeNull();
			// Stacked top-to-bottom: rail.top < editor.top < output.top (controls → editor → console).
			expect(railBox!.y).toBeLessThan(editorBox!.y);
			expect(editorBox!.y).toBeLessThan(outputBox!.y);

			// Monaco is READ-ONLY <768px (D-49-09, D-49-23) — the page tags the container.
			await expect(monaco).toHaveAttribute('data-readonly', 'true');
			// The read-only banner is shown.
			await expect(page.getByTestId('mobile-banner')).toBeVisible();
		} else {
			// Desktop: Monaco is editable, no mobile banner.
			await expect(monaco).toHaveAttribute('data-readonly', 'false');
			await expect(page.getByTestId('mobile-banner')).toHaveCount(0);
		}
	});

	test('docs sidebar becomes a Contents disclosure <768px', async ({ page }, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		await page.goto('/docs/flow-operator');
		await page.locator('main').waitFor();
		// The disclosure summary "Contents" exists in both layouts; <768px it's the collapsible
		// control above the body (desktop keeps it open as the persistent sidebar).
		const disclosure = page.locator('.docs-toc-disclosure');
		await expect(disclosure).toBeAttached();
		await expect(disclosure.locator('summary', { hasText: 'Contents' })).toBeAttached();
		if (width < 768) {
			// On mobile the summary is an interactive disclosure control.
			await expect(disclosure.locator('summary')).toBeVisible();
		}
	});

	test('showcase cards are single-column <768px', async ({ page }, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		await page.goto('/showcase');
		await page.locator('main').waitFor();
		const cards = page.locator('.showcase__grid > li');
		const count = await cards.count();
		expect(count).toBeGreaterThan(1);
		if (width < 768) {
			// Single column → every card shares (approximately) the same left edge x.
			const first = await cards.nth(0).boundingBox();
			const second = await cards.nth(1).boundingBox();
			expect(first, 'first card visible').not.toBeNull();
			expect(second, 'second card visible').not.toBeNull();
			// Same left edge (within 2px) AND stacked vertically (second is below the first).
			expect(Math.abs(first!.x - second!.x)).toBeLessThanOrEqual(2);
			expect(second!.y).toBeGreaterThanOrEqual(first!.y + first!.height - 2);
		}
	});
});
