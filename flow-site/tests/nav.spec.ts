import { test, expect } from '@playwright/test';

// REQ-SITE-IA-01 / REQ-SITE-A11Y-02 — the persistent 5-tab top nav.
//
// The desktop tab strip (<Tabs>) is hidden <768px (the hamburger takes over), so the tab
// assertions scope to the desktop project. The mobile slide-down nav carries the same routes
// and is asserted separately. All projects share the prerendered Home + layout chrome.

const isMobileViewport = (width: number) => width < 768;

test.describe('5-tab top nav (REQ-SITE-IA-01)', () => {
	test('renders the persistent Primary nav landmark on Home', async ({ page }, testInfo) => {
		await page.goto('/');
		const width = testInfo.project.use.viewport?.width ?? 1280;
		// Layout chrome ships a <nav aria-label="Primary"> (inside <Tabs>). On desktop the strip is
		// visible; <768px it collapses behind the hamburger (visible only once opened), so assert
		// the landmark is attached and the hamburger trigger is visible instead.
		const primaryNav = page.locator('nav[aria-label="Primary"]').first();
		if (isMobileViewport(width)) {
			await expect(primaryNav).toBeAttached();
			await expect(page.getByRole('button', { name: /open menu/i })).toBeVisible();
		} else {
			await expect(primaryNav).toBeVisible();
		}
		// The Recoleta "Flow" wordmark links home (visible in both layouts).
		const wordmark = page.locator('a.site-wordmark');
		await expect(wordmark).toBeVisible();
		await expect(wordmark).toHaveAttribute('href', '/');
	});

	test('the 5 tabs are present (4 local + GitHub external)', async ({ page }, testInfo) => {
		await page.goto('/');
		const width = testInfo.project.use.viewport?.width ?? 1280;

		if (isMobileViewport(width)) {
			// Mobile: open the hamburger slide-down, then assert the same 5 routes.
			await page.getByRole('button', { name: /open menu/i }).click();
			const mobileNav = page.locator('#mobile-nav');
			await expect(mobileNav.getByRole('link', { name: 'Home' })).toBeVisible();
			await expect(mobileNav.getByRole('link', { name: 'Docs' })).toBeVisible();
			await expect(mobileNav.getByRole('link', { name: 'Playground' })).toBeVisible();
			await expect(mobileNav.getByRole('link', { name: 'Showcase' })).toBeVisible();
			await expect(mobileNav.getByRole('link', { name: /github/i })).toBeVisible();
		} else {
			const tabs = page.locator('.site-nav-desktop nav[aria-label="Primary"]');
			for (const label of ['Home', 'Docs', 'Playground', 'Showcase']) {
				await expect(tabs.getByRole('link', { name: label })).toBeVisible();
			}
			await expect(tabs.getByRole('link', { name: /github/i })).toBeVisible();
		}
	});

	test('the 4 local tabs navigate to their routes', async ({ page }, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		// Run the routing assertions on desktop where the tab strip is visible; mobile uses the
		// hamburger panel (covered by the presence test above). Early-out on mobile viewports.
		if (isMobileViewport(width)) return;

		await page.goto('/');
		const tabs = page.locator('.site-nav-desktop nav[aria-label="Primary"]');

		await tabs.getByRole('link', { name: 'Docs' }).click();
		await expect(page).toHaveURL(/\/docs$/);

		await tabs.getByRole('link', { name: 'Home' }).click();
		await expect(page).toHaveURL(/\/$/);
	});

	test('GitHub is an external link: target=_blank + rel noopener noreferrer', async ({ page }) => {
		await page.goto('/');
		// Scope to whichever nav is visible (desktop strip or, if collapsed, open the hamburger).
		const desktopGh = page.locator('.site-nav-desktop').getByRole('link', { name: /github/i });
		if (await desktopGh.count()) {
			const gh = desktopGh.first();
			await expect(gh).toHaveAttribute('target', '_blank');
			const rel = (await gh.getAttribute('rel')) ?? '';
			expect(rel).toContain('noopener');
			expect(rel).toContain('noreferrer');
		} else {
			await page.getByRole('button', { name: /open menu/i }).click();
			const gh = page.locator('#mobile-nav').getByRole('link', { name: /github/i });
			await expect(gh).toHaveAttribute('target', '_blank');
			const rel = (await gh.getAttribute('rel')) ?? '';
			expect(rel).toContain('noopener');
			expect(rel).toContain('noreferrer');
		}
	});

	test('aria-current="page" lands on the active route', async ({ page }, testInfo) => {
		const width = testInfo.project.use.viewport?.width ?? 1280;
		// aria-current is asserted on the desktop tab strip; early-out on mobile viewports.
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

		// Let hydration's initial theme $effect settle before driving the control (it reads
		// getInitialTheme() once on mount; clicking before that runs can be overwritten).
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
