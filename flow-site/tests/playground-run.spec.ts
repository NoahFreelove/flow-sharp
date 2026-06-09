import { test, expect } from '@playwright/test';

// REQ-SITE-PLAYGROUND-02 — Run produces stdout, split into the stdout pane (D-48-15).
//
// Loads the "Print to console" snippet (`(print "hello flow")` + `(print (str (add 1 2)))`),
// clicks Run, and asserts the stdout pane contains the printed text. Editing isn't required (the
// snippet is loaded via the rail), but Run + console rendering are desktop-layout features, so the
// spec runs on the `desktop` project.

const DESKTOP = 'desktop';

test('REQ-SITE-PLAYGROUND-02: Run produces stdout', async ({ page }, testInfo) => {
	if (testInfo.project.name !== DESKTOP) return;

	await page.goto('/playground', { waitUntil: 'domcontentloaded' });
	await page.waitForFunction(
		() => (window as unknown as { __flowRuntimeReady?: boolean }).__flowRuntimeReady === true,
		{ timeout: 30_000 }
	);

	// Empty state shown before the first run.
	await expect(page.locator('.pg-empty-title')).toHaveText('Nothing has run yet');

	// Load the print snippet, then Run.
	await page.locator('.pg-snippet', { hasText: 'Print to console' }).click();
	await page.locator('button.skeuo-btn--primary', { hasText: 'Run' }).click();

	// stdout pane renders the printed output (escaped text — Svelte auto-escapes; no innerHTML).
	const stdout = page.locator('[data-testid="stdout"]');
	await expect(stdout).toBeVisible({ timeout: 15_000 });
	await expect(stdout).toContainText('hello flow');
	await expect(stdout).toContainText('3');

	// No error boxes for a clean run.
	await expect(page.locator('[data-testid="errors"]')).toHaveCount(0);
});

// WR-05 (untrusted-error-text escaping) is pinned deterministically without the WASM runtime by
// src/routes/playground/error-box-escaping.test.ts (vitest + the __fixtures__/ErrorBox.svelte
// fixture mirroring this page's error-box markup), so it does not need a flaky Monaco-typing E2E.
