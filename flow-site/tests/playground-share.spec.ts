import { test, expect } from '@playwright/test';

// REQ-SITE-SHARE-01 — the URL-fragment share path, end-to-end in the real playground (D-49-30).
//
// Three things are proven here:
//   1. Share encodes the editor value, copies a `/playground#code=...` link, and toasts the
//      UI-SPEC "Link copied — anyone can open this snippet" confirmation.
//   2. Opening that copied link round-trips the source back into the Monaco editor.
//   3. A MALFORMED `#code=` fragment shows the friendly "Couldn't decode this shared snippet"
//      message — NO crash, NO white screen (the threat-model T-49-CSP-FRAG defensive decode).
//
// Live gist creation against the real GitHub API is a HUMAN-action checkpoint (VALIDATION
// §Manual-Only) — this spec never hits api.github.com.

const DESKTOP = 'desktop';

async function waitForRuntime(page: import('@playwright/test').Page) {
	await page.waitForFunction(
		() => (window as unknown as { __flowRuntimeReady?: boolean }).__flowRuntimeReady === true,
		{ timeout: 30_000 }
	);
}

test('REQ-SITE-SHARE-01: Share copies a #code= link', async ({ page, context }, testInfo) => {
	if (testInfo.project.name !== DESKTOP) return;
	await context.grantPermissions(['clipboard-read', 'clipboard-write']);

	// §6.10: pass ?e2e=1 to enable __flowRuntimeReady + __flowEditorValue hooks.
	await page.goto('/playground?e2e=1', { waitUntil: 'domcontentloaded' });
	await waitForRuntime(page);

	// Click Share.
	await page.locator('button.skeuo-btn--secondary', { hasText: 'Share' }).click();

	// The toast shows the UI-SPEC confirmation copy.
	const toast = page.locator('[data-testid="share-toast"]');
	await expect(toast).toBeVisible({ timeout: 5_000 });
	await expect(toast).toContainText('Link copied — anyone can open this snippet');

	// The clipboard now holds a /playground#code=... link (no +, /, = in the fragment).
	const clip = await page.evaluate(() => navigator.clipboard.readText());
	expect(clip).toContain('/playground#code=');
	const frag = clip.split('#code=')[1] ?? '';
	const codePart = frag.split('&')[0];
	expect(codePart).not.toMatch(/[+/=]/);
});

test('REQ-SITE-SHARE-01: opening a #code= link round-trips the source into the editor', async ({
	page,
	context
}, testInfo) => {
	if (testInfo.project.name !== DESKTOP) return;
	await context.grantPermissions(['clipboard-read', 'clipboard-write']);

	// First visit: capture a real share link for the default snippet.
	// §6.10: pass ?e2e=1 to enable __flowRuntimeReady + __flowEditorValue hooks.
	await page.goto('/playground?e2e=1', { waitUntil: 'domcontentloaded' });
	await waitForRuntime(page);
	const original = await page.evaluate(() =>
		(window as unknown as { __flowEditorValue?: () => string }).__flowEditorValue?.()
	);
	await page.locator('button.skeuo-btn--secondary', { hasText: 'Share' }).click();
	await expect(page.locator('[data-testid="share-toast"]')).toBeVisible();
	const shareUrl = await page.evaluate(() => navigator.clipboard.readText());
	expect(shareUrl).toContain('#code=');

	// Open the shared link in a fresh page load (with ?e2e=1 so __flowEditorValue is active)
	// — the source must reappear in the editor.
	const shareUrlWithE2e = shareUrl.replace('/playground#', '/playground?e2e=1#');
	await page.goto(shareUrlWithE2e, { waitUntil: 'domcontentloaded' });
	await waitForRuntime(page);
	// No decode error for a valid link.
	await expect(page.locator('[data-testid="decode-error"]')).toHaveCount(0);
	const roundTripped = await page.evaluate(() =>
		(window as unknown as { __flowEditorValue?: () => string }).__flowEditorValue?.()
	);
	expect(roundTripped).toBe(original);
});

test('REQ-SITE-SHARE-01: a malformed #code= shows the friendly error, no crash', async ({
	page
}, testInfo) => {
	if (testInfo.project.name !== DESKTOP) return;

	// Garbage that is not a valid deflate stream → defensive decode → friendly message.
	await page.goto('/playground?e2e=1#code=not-valid-deflate-data-zzzz', {
		waitUntil: 'domcontentloaded'
	});
	await waitForRuntime(page);

	const decodeErr = page.locator('[data-testid="decode-error"]');
	await expect(decodeErr).toBeVisible({ timeout: 5_000 });
	await expect(decodeErr).toContainText('Couldn’t decode this shared snippet');

	// The page did NOT crash — the editor + Run button are still present and interactive.
	await expect(page.locator('[data-testid="monaco"]')).toBeVisible();
	await expect(page.locator('button.skeuo-btn--primary', { hasText: 'Run' })).toBeVisible();
});
