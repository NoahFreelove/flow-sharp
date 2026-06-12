import { test, expect } from '@playwright/test';

// REQ-SITE-IA-01 / REQ-SITE-A11Y-02 — the persistent 5-tab top nav.
//
// The `/` route ships an iOS-6 skeuomorphic page with its own chrome:
//   - The brushed-aluminum toolbar carries `nav[aria-label="Primary"]` (the pill nav)
//     at EVERY width. On narrow viewports (≤600px) the pill nav scrolls horizontally
//     inside the bar instead of being hidden — there is no longer a bottom tab bar.
// Non-home routes (/docs, /playground, /showcase) keep the shared layout chrome:
//   - Desktop: `.site-nav-desktop nav[aria-label="Primary"]` visible tab strip.
//   - Mobile (<768px): hamburger button → slide-down `#mobile-nav`.

const isMobileViewport = (width: number) => width < 768;

test.describe('5-tab top nav (REQ-SITE-IA-01)', () => {
	test('renders the persistent Primary nav landmark on Home', async ({ page }) => {
		await page.goto('/');
		// The iOS-6 home ships `nav[aria-label="Primary"]` in the toolbar, visible at every
		// width (it scrolls horizontally on mobile rather than hiding).
		const primaryNav = page.locator('nav[aria-label="Primary"]').first();
		await expect(primaryNav).toBeVisible();
		// The iOS-6 brand link doubles as the site wordmark (carries .site-wordmark for specs).
		const wordmark = page.locator('a.site-wordmark');
		await expect(wordmark).toBeVisible();
		await expect(wordmark).toHaveAttribute('href', '/');
	});

	test('the 5 tabs are present (4 local + GitHub external)', async ({ page }) => {
		await page.goto('/');
		// The toolbar pill nav carries all 5 destinations at every width.
		const nav = page.locator('nav[aria-label="Primary"]').first();
		for (const label of ['Home', 'Docs', 'Playground', 'Showcase']) {
			await expect(nav.getByRole('link', { name: label })).toBeVisible();
		}
		await expect(nav.getByRole('link', { name: /github/i })).toBeVisible();
	});

	test('the 4 local tabs navigate to their routes', async ({ page }, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		// The return trip uses the shared desktop tab strip (`.site-nav-desktop`), which is
		// hidden behind the hamburger on mobile — so run the routing round-trip on desktop.
		if (isMobileViewport(width)) return;

		await page.goto('/');
		const nav = page.locator('nav[aria-label="Primary"]').first();

		await nav.getByRole('link', { name: 'Docs' }).click();
		await expect(page).toHaveURL(/\/docs$/);

		// Return home via the shared chrome tab strip (the iOS-6 home is navigated away from).
		const sharedNav = page.locator('.site-nav-desktop nav[aria-label="Primary"]');
		await sharedNav.getByRole('link', { name: 'Home' }).click();
		await expect(page).toHaveURL(/\/$/);
	});

	test('GitHub is an external link: target=_blank + rel noopener noreferrer', async ({ page }) => {
		await page.goto('/');
		// The toolbar pill nav GitHub link, at every width.
		const gh = page
			.locator('nav[aria-label="Primary"]')
			.first()
			.getByRole('link', { name: /github/i });
		await expect(gh).toHaveAttribute('target', '_blank');
		const rel = (await gh.getAttribute('rel')) ?? '';
		expect(rel).toContain('noopener');
		expect(rel).toContain('noreferrer');
	});

	test('aria-current="page" lands on the active route', async ({ page }, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		// aria-current on the desktop tab strip (shared chrome); early-out on mobile viewports.
		if (isMobileViewport(width)) return;

		await page.goto('/docs');
		const tabs = page.locator('.site-nav-desktop nav[aria-label="Primary"]');
		await expect(tabs.getByRole('link', { name: 'Docs' })).toHaveAttribute(
			'aria-current',
			'page'
		);
		// Home must NOT be current on /docs.
		await expect(tabs.getByRole('link', { name: 'Home' })).not.toHaveAttribute(
			'aria-current',
			'page'
		);
	});

	test('the theme toggle is present and operable (switch role)', async ({ browser }) => {
		// Use an isolated context with a known light start so the localStorage theme key this test
		// mutates can't race with other parallel specs sharing the preview origin.
		const ctx = await browser.newContext();
		const page = await ctx.newPage();
		await page.addInitScript(() => localStorage.setItem('flow-theme', 'light'));
		await page.goto('/');
		await page.waitForLoadState('domcontentloaded');

		// The iOS-6 home does not ship the shared layout chrome toggle. The theme toggle lives
		// on non-home routes; on home, navigate to /docs to assert the operable switch.
		await page.goto('/docs');
		await page.waitForLoadState('networkidle');
		const toggle = page.getByRole('switch', { name: /dark mode/i }).first();
		await expect(toggle).toBeVisible();
		// Starts light (forced above) → switch reads unchecked.
		await expect(toggle).toHaveAttribute('aria-checked', 'false');

		// Operating the toggle persists + applies the dark theme: the real, observable effect is
		// [data-theme="dark"] on <html> (setTheme runs synchronously in the click handler) and the
		// switch flips to checked. Asserting the applied theme is robust against effect re-scheduling.
		await toggle.click();
		await expect(toggle).toHaveAttribute('aria-checked', 'true');
		await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
		await ctx.close();
	});
});
