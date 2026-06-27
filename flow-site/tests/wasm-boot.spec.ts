import { test, expect } from '@playwright/test';

// REQ-SITE-PLAYGROUND-01 — the Phase 48 WASM runtime boots without a boot-error pane.
//
// The playground route lazy-imports `/wasm/flow-runtime.js` in onMount (D-49-34) and calls
// `loadFlowRuntime()`. On success it sets `window.__flowRuntimeReady`; on boot failure it shows
// the top-level boot-error pane (HANDOFF §2.3). This spec asserts a clean boot in headless
// chromium against the committed AppBundle.
//
// Runs on the `desktop` project only — the boot path is viewport-independent (the mobile projects
// re-exercise the same boot via playground-mobile.spec.ts).

const DESKTOP = 'desktop';

test('REQ-SITE-PLAYGROUND-01: WASM runtime boots (no boot error)', async ({ page }, testInfo) => {
	if (testInfo.project.name !== DESKTOP) return;

	// §6.10: pass ?e2e=1 to enable the __flowRuntimeReady readiness hook.
	await page.goto('/playground?e2e=1', { waitUntil: 'domcontentloaded' });

	// The runtime boots lazily in onMount; wait for the readiness hook (generous — Mono-WASM boot
	// can take a few seconds on a cold cache).
	await page.waitForFunction(
		() => (window as unknown as { __flowRuntimeReady?: boolean }).__flowRuntimeReady === true,
		{ timeout: 30_000 }
	);

	// No top-level boot-error pane.
	await expect(page.locator('[data-testid="boot-error"]')).toHaveCount(0);

	// The three-column scaffold is present: snippets rail + Monaco + output.
	await expect(page.locator('aside[aria-label="Snippets and controls"]')).toBeVisible();
	await expect(page.locator('[data-testid="monaco"]')).toBeVisible();
	await expect(page.locator('aside[aria-label="Output"]')).toBeVisible();
});
