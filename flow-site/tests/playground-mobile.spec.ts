import { test, expect } from '@playwright/test';

// REQ-SITE-PLAYGROUND-05 + REQ-SITE-RESPONSIVE-01 — Monaco is read-only below 768px (D-49-09), the
// layout collapses to a single column, a clear banner explains the read-only state, and there is
// NO horizontal overflow down to the 320px target (ROADMAP acceptance #7).
//
// Runs under the `mobile` (375×667) and `mobile-narrow` (320×568) playwright projects; the desktop
// project is skipped (the read-only branch is viewport-gated at <768px).

const NARROW_PROJECTS = new Set(['mobile', 'mobile-narrow']);

test('REQ-SITE-PLAYGROUND-05: Monaco read-only + single-column, no overflow <768px', async ({
	page
}, testInfo) => {
	if (!NARROW_PROJECTS.has(testInfo.project.name)) return;

	// §6.10: pass ?e2e=1 to enable __flowRuntimeReady hook + AudioContext Proxy.
	await page.goto('/playground?e2e=1', { waitUntil: 'domcontentloaded' });

	// Runtime still boots on mobile (Run + playback work; only editing is disabled — D-49-09).
	await page.waitForFunction(
		() => (window as unknown as { __flowRuntimeReady?: boolean }).__flowRuntimeReady === true,
		{ timeout: 30_000 }
	);

	// Read-only banner is shown with the UI-SPEC copy.
	const banner = page.locator('[data-testid="mobile-banner"]');
	await expect(banner).toBeVisible();
	await expect(banner).toContainText('read-only on small screens');

	// Monaco is marked read-only (the container mirrors the readOnly option into data-readonly).
	await expect(page.locator('[data-testid="monaco"]')).toHaveAttribute('data-readonly', 'true');

	// Single-column collapse: the rail, editor, and output all stack in one column — assert their
	// left edges align (same x), i.e. no side-by-side columns.
	const railBox = await page.locator('aside[aria-label="Snippets and controls"]').boundingBox();
	const editorBox = await page.locator('.pg-editor').boundingBox();
	const outputBox = await page.locator('aside[aria-label="Output"]').boundingBox();
	expect(railBox).not.toBeNull();
	expect(editorBox).not.toBeNull();
	expect(outputBox).not.toBeNull();
	// Stacked vertically: editor sits below the rail, output below the editor.
	expect(editorBox!.y).toBeGreaterThanOrEqual(railBox!.y + railBox!.height - 2);
	expect(outputBox!.y).toBeGreaterThanOrEqual(editorBox!.y + editorBox!.height - 2);

	// NO horizontal overflow — the document does not scroll sideways at this viewport.
	const overflow = await page.evaluate(
		() => document.documentElement.scrollWidth - document.documentElement.clientWidth
	);
	expect(overflow, `horizontal overflow at ${testInfo.project.name}`).toBeLessThanOrEqual(0);

	// Run still works on mobile (shared snippets are runnable, just not editable). Click Run and
	// confirm the gesture resumed audio (no editing needed — the default snippet is loaded).
	await page.locator('button.skeuo-btn--primary', { hasText: 'Run' }).click();
	await expect(page.locator('[data-testid="audio-state"]')).toHaveText('running', {
		timeout: 15_000
	});
});
