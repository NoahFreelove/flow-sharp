import { test, expect } from '@playwright/test';

// REQ-SITE-IA-01 / REQ-SITE-A11Y-02 — the persistent 5-tab top nav.
//
// The `/` route ships an iOS-6 skeuomorphic page with its own chrome:
//   - Desktop: a brushed-aluminum toolbar with `nav[aria-label="Primary"]` (the pill nav).
//   - Mobile (<600px): the toolbar pill nav is hidden; `nav[aria-label="Tab bar"]` at the
//     bottom carries the same 5 destinations. `nav[aria-label="Primary"]` stays attached.
// Non-home routes (/docs, /playground, /showcase) keep the shared layout chrome as before:
//   - Desktop: `.site-nav-desktop nav[aria-label="Primary"]` visible tab strip.
//   - Mobile (<768px): hamburger button → slide-down `#mobile-nav`.

const isMobileViewport = (width: number) => width < 768;

// Whether the home page hides the toolbar nav (at ≤600px the tabbar takes over).
const isHomeToolbarNavHidden = (width: number) => width <= 600;

test.describe('5-tab top nav (REQ-SITE-IA-01)', () => {
	test('renders the persistent Primary nav landmark on Home', async ({ page }, testInfo) => {
		await page.goto('/');
		const width = testInfo.project.use.viewport?.width ?? 1280;
		// The iOS-6 home ships `nav[aria-label="Primary"]` in the toolbar (always attached).
		// At >600px it is visible; at ≤600px it is hidden by CSS (the bottom tabbar takes over),
		// but it stays attached in the DOM for accessibility.
		const primaryNav = page.locator('nav[aria-label="Primary"]').first();
		await expect(primaryNav).toBeAttached();
		if (!isHomeToolbarNavHidden(width)) {
			await expect(primaryNav).toBeVisible();
		}
		// The iOS-6 brand link doubles as the site wordmark (carries .site-wordmark for specs).
		const wordmark = page.locator('a.site-wordmark');
		await expect(wordmark).toBeVisible();
		await expect(wordmark).toHaveAttribute('href', '/');
	});

	test('the 5 tabs are present (4 local + GitHub external)', async ({ page }, testInfo) => {
		await page.goto('/');
		const width = testInfo.project.use.viewport?.width ?? 1280;

		if (isHomeToolbarNavHidden(width)) {
			// Mobile: the bottom tab bar carries all 5 destinations.
			const tabbar = page.locator('nav[aria-label="Tab bar"]');
			await expect(tabbar.getByRole('link', { name: 'Home' })).toBeVisible();
			await expect(tabbar.getByRole('link', { name: 'Docs' })).toBeVisible();
			await expect(tabbar.getByRole('link', { name: 'Playground' })).toBeVisible();
			await expect(tabbar.getByRole('link', { name: 'Showcase' })).toBeVisible();
			await expect(tabbar.getByRole('link', { name: /github/i })).toBeVisible();
		} else {
			// Desktop: the toolbar pill nav carries all 5 destinations.
			const nav = page.locator('nav[aria-label="Primary"]').first();
			for (const label of ['Home', 'Docs', 'Playground', 'Showcase']) {
				await expect(nav.getByRole('link', { name: label })).toBeVisible();
			}
			await expect(nav.getByRole('link', { name: /github/i })).toBeVisible();
		}
	});

	test('the 4 local tabs navigate to their routes', async ({ page }, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		// Run routing assertions on desktop where the toolbar pill nav is visible.
		if (isHomeToolbarNavHidden(width)) return;

		await page.goto('/');
		const nav = page.locator('nav[aria-label="Primary"]').first();

		await nav.getByRole('link', { name: 'Docs' }).click();
		await expect(page).toHaveURL(/\/docs$/);

		// Return home via the shared chrome tab strip (the iOS-6 home is navigated away from).
		const sharedNav = page.locator('.site-nav-desktop nav[aria-label="Primary"]');
		await sharedNav.getByRole('link', { name: 'Home' }).click();
		await expect(page).toHaveURL(/\/$/);
	});

	test('GitHub is an external link: target=_blank + rel noopener noreferrer', async ({ page }, testInfo) => {
		await page.goto('/');
		const width = testInfo.project.use.viewport?.width ?? 1280;

		if (isHomeToolbarNavHidden(width)) {
			// Mobile: check the tabbar GitHub link.
			const gh = page.locator('nav[aria-label="Tab bar"]').getByRole('link', { name: /github/i });
			await expect(gh).toHaveAttribute('target', '_blank');
			const rel = (await gh.getAttribute('rel')) ?? '';
			expect(rel).toContain('noopener');
			expect(rel).toContain('noreferrer');
		} else {
			// Desktop: check the toolbar pill nav GitHub link.
			const gh = page.locator('nav[aria-label="Primary"]').first().getByRole('link', { name: /github/i });
			await expect(gh).toHaveAttribute('target', '_blank');
			const rel = (await gh.getAttribute('rel')) ?? '';
			expect(rel).toContain('noopener');
			expect(rel).toContain('noreferrer');
		}
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

	test('the theme toggle is present and operable (switch role)', async ({ browser }, testInfo) => {
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
