import { test, expect } from '@playwright/test';

// REQ-SITE-PLAYGROUND-03 — the Run+audio gesture chain resumes the AudioContext (D-48-09).
//
// The Run onclick calls `runtime.resumeAudio()` THEN `runtime.run()` in the SAME async frame
// (HANDOFF §5). The page wraps `AudioContext` so the resumed context's `.state` is mirrored into
// the `data-testid="audio-state"` sr-only span. Headless chromium cannot assert AUDIBLE output —
// that is a Plan 49-08 HUMAN-UAT item — so this spec asserts the gesture SUCCEEDED, i.e. the
// AudioContext reached `running` after the click (proof the autoplay policy was satisfied).

const DESKTOP = 'desktop';

test('REQ-SITE-PLAYGROUND-03: Run gesture resumes AudioContext', async ({ page }, testInfo) => {
	if (testInfo.project.name !== DESKTOP) return;

	// §6.10: pass ?e2e=1 to enable the AudioContext Proxy + __flowRuntimeReady hook.
	await page.goto('/playground?e2e=1', { waitUntil: 'domcontentloaded' });
	await page.waitForFunction(
		() => (window as unknown as { __flowRuntimeReady?: boolean }).__flowRuntimeReady === true,
		{ timeout: 30_000 }
	);

	// The default snippet is the 440 Hz sine — `(play (createSineTone 440Hz 1.0 0.5))`. Click Run
	// (a real user gesture) to chain resumeAudio() + run().
	await page.locator('button.skeuo-btn--primary', { hasText: 'Run' }).click();

	// After the gesture, the resumed AudioContext state is mirrored into the test hook. `running`
	// proves resume() succeeded inside the gesture frame (the precondition for audible output).
	const audioState = page.locator('[data-testid="audio-state"]');
	await expect(audioState).toHaveText('running', { timeout: 15_000 });

	// Audible verification is out of scope for headless — recorded as a 49-08 HUMAN-UAT item.
});
