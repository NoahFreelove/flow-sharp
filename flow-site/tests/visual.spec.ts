import { test, expect } from '@playwright/test';

// REQ-SITE-DESIGN-01..04 — the ≤8 skeuo components render in light / dark / reduced-motion.
// Plan 49-02 visual-regression baselines: the /design showcase renders every component in all
// documented states; we screenshot it in three configurations to pin the skeuomorphic look.
//
// Theme is applied via addInitScript (localStorage 'flow-theme' + an early [data-theme] write,
// per theme.ts) so the page boots already in the target theme — no post-load toggle flash.
// Reduced-motion uses Playwright's emulated `reducedMotion: 'reduce'` so the component-level
// fallbacks (knob→flat slider, panel→1px border, LED steady, button no-travel) engage.
//
// Baselines are written on first run (--update-snapshots) and asserted on every run after.
// Each screenshot disables animations so the capture is stable across runs.

// `maxDiffPixelRatio` absorbs cross-environment font subpixel / anti-aliasing drift on these
// large full-page skeuo captures (the 49-03/49-07 deferred-items entry: desktop baselines drift
// ~0.06–0.13 between this machine and CI purely on font rendering, with no real visual change).
// 0.04 is well under any genuine skeuo regression (a dropped texture / wrong colour shifts far
// more) while keeping the suite green across environments. Baselines regenerated in Plan 49-08.
const SHOT = {
	fullPage: true,
	animations: 'disabled' as const,
	maxDiffPixelRatio: 0.04
};

test.describe('skeuo design system visual regression (REQ-SITE-DESIGN-01..04)', () => {
	test('light theme', async ({ page }) => {
		await page.addInitScript(() => {
			localStorage.setItem('flow-theme', 'light');
			document.documentElement.setAttribute('data-theme', 'light');
		});
		await page.goto('/design');
		await page.getByRole('heading', { name: /Skeuomorphic Design System/i }).waitFor();
		await expect(page).toHaveScreenshot('design-light.png', SHOT);
	});

	test('dark theme', async ({ page }) => {
		await page.addInitScript(() => {
			localStorage.setItem('flow-theme', 'dark');
			document.documentElement.setAttribute('data-theme', 'dark');
		});
		await page.goto('/design');
		await page.getByRole('heading', { name: /Skeuomorphic Design System/i }).waitFor();
		await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
		await expect(page).toHaveScreenshot('design-dark.png', SHOT);
	});

	test('reduced-motion (light)', async ({ page, browser }) => {
		const context = await browser.newContext({ reducedMotion: 'reduce' });
		const rmPage = await context.newPage();
		await rmPage.addInitScript(() => {
			localStorage.setItem('flow-theme', 'light');
			document.documentElement.setAttribute('data-theme', 'light');
		});
		await rmPage.goto('/design');
		await rmPage.getByRole('heading', { name: /Skeuomorphic Design System/i }).waitFor();
		// Knobs render as flat sliders under reduced-motion — assert the fallback engaged.
		await expect(rmPage.getByRole('slider').first()).toBeVisible();
		await expect(rmPage).toHaveScreenshot('design-reduced-motion.png', SHOT);
		await context.close();
	});
});
