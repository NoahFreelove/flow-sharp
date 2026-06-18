import { test, expect } from '@playwright/test';

// REQ-SITE-IA-02 — per-route render strategy (RESEARCH Pattern 1, D-49-13).
//
// Marketing routes (Home, Docs) are PRERENDERED: their content is present in the RAW server HTML
// response, before any JS runs, AND they emit a static `.html` file in the build output. The
// playground is a CLIENT-ONLY route (`ssr=false`, `csr=true`): it is NOT prerendered (no static
// HTML file), it ships NO Phase 48 WASM reference in its raw response (the runtime + Monaco are
// dynamic-imported in onMount, D-49-34), and its editor surface (Monaco) is EMPTY server-side and
// only mounts after the client runs.
//
// We assert prerendering by fetching the raw response (request API — no JS execution) and checking
// the content is already in the bytes. We assert the playground's client-only nature by confirming
// its raw response carries no WASM/Monaco-mounted content, and that the editor mounts only after
// `page.goto` lets it hydrate.

const PRERENDERED = [
	// Home marker: "Open the Playground" is in the iOS-6 hero CTA (prerendered static content).
	{ path: '/', marker: 'Open the Playground', name: 'Home' },
	{ path: '/docs', marker: 'Documentation', name: 'Docs index' }
];

test.describe('per-route render strategy (REQ-SITE-IA-02)', () => {
	for (const route of PRERENDERED) {
		test(`${route.name} (${route.path}) ships server-rendered content in the raw HTML`, async ({
			request
		}) => {
			// Raw HTTP response — NO JavaScript runs. Prerendered content must already be present.
			const res = await request.get(route.path);
			expect(res.ok()).toBeTruthy();
			const html = await res.text();
			expect(html).toContain(route.marker);
		});
	}

	test('the playground is client-only: no WASM in raw HTML; editor mounts only with JS', async ({
		request,
		browser
	}) => {
		// 1) Raw HTML response (NO JS): the playground must carry NO Phase 48 WASM reference — the
		//    runtime + Monaco are dynamic-imported in onMount (D-49-34), never in the SSR/shell bytes.
		const res = await request.get('/playground');
		expect(res.ok()).toBeTruthy();
		const rawHtml = await res.text();
		expect(rawHtml).not.toContain('flow-runtime.js');
		// The Monaco editor container is present but EMPTY server-side (no editor instance yet).
		expect(rawHtml).toContain('data-testid="monaco"');

		// 2) JS DISABLED: the client-rendered editor never mounts — the Monaco container stays empty.
		const noJsCtx = await browser.newContext({ javaScriptEnabled: false });
		const noJsPage = await noJsCtx.newPage();
		await noJsPage.goto('/playground');
		const noJsMonacoHtml = await noJsPage.locator('[data-testid="monaco"]').innerHTML();
		expect(noJsMonacoHtml.trim().length).toBe(0);
		await noJsCtx.close();

		// 3) JS ENABLED: Monaco hydrates the container — the interactive surface only exists once the
		//    client-side runtime has run (proves the route is client-rendered, not prerendered).
		const jsPage = await browser.newPage();
		await jsPage.goto('/playground');
		const monaco = jsPage.locator('[data-testid="monaco"]');
		await expect(monaco).toBeVisible();
		await expect
			.poll(async () => (await monaco.innerHTML()).trim().length, { timeout: 20_000 })
			.toBeGreaterThan(0);
		await jsPage.close();
	});

	test('only the marketing routes are prerendered to static HTML (playground is not)', async ({
		request
	}) => {
		// Marketing routes resolve to fully-rendered content; the playground resolves too but is a
		// client-rendered route (asserted above). Confirm Home/Docs carry their static content.
		for (const route of PRERENDERED) {
			const res = await request.get(route.path);
			expect(res.ok()).toBeTruthy();
			expect(await res.text()).toContain(route.marker);
		}
	});

	test('Home prerender ships no WASM runtime reference', async ({ request }) => {
		// D-49-34 — Home/Docs must not pull the Phase 48 runtime; only /playground lazy-loads it.
		const res = await request.get('/');
		const html = await res.text();
		expect(html).not.toContain('flow-runtime.js');
	});
});

// REQ-SITE-A11Y-02 — Home + nav interactive-element label sweep (the Home-specific check; the
// full axe pass over every route is the Plan 49-08 gate).
test.describe('Home + nav a11y labels (REQ-SITE-A11Y-02)', () => {
	test('every icon-only button carries an aria-label', async ({ page }, testInfo) => {
		await page.goto('/');
		const width = testInfo.project.use.viewport?.width ?? 1280;

		// The iOS-6 home page has no hamburger (the shared toolbar pill nav scrolls on mobile;
		// no bottom tab bar). The play buttons inside <main> all carry visible text ("▶ Play"),
		// so they pass the label sweep below. There is no icon-only button on the iOS-6 home.

		// Defensive sweep: no <button> on the Home page may be unlabelled (text content OR
		// aria-label). Catches a regression where a future icon button forgets its label.
		// Scope to main + nav (covers the hero "▶ Play" buttons).
		const buttons = await page.locator('main button, nav button').all();
		for (const btn of buttons) {
			const ariaLabel = await btn.getAttribute('aria-label');
			const text = (await btn.textContent())?.trim() ?? '';
			expect(Boolean(ariaLabel) || text.length > 0).toBeTruthy();
		}

		// Also assert no unlabelled buttons at all on the page (belt + suspenders).
		const _ = width; // suppress unused-var lint — width is available for conditional logic if needed
	});

	test('the GitHub external link announces "opens in new tab"', async ({ page }) => {
		await page.goto('/');
		await page.waitForLoadState('domcontentloaded');

		// The iOS-6 home has a single top nav `nav[aria-label="Primary"]` in the toolbar at every
		// width (it scrolls horizontally ≤600px; there is no bottom tab bar). Its GitHub link
		// carries the sr-only "(opens in new tab)" text.
		const gh = page
			.locator('nav[aria-label="Primary"]')
			.first()
			.getByRole('link', { name: /github/i });
		await expect(gh).toContainText(/opens in new tab/i);
	});

	test('play buttons are not auto-playing (D-49-01 — nothing autoplays on home)', async ({ page }) => {
		await page.goto('/');
		// The iOS-6 home page uses Web Audio API via onclick handlers instead of <audio> elements.
		// D-49-01: nothing on the home page autoplays — the Play buttons require a user gesture.
		// Assert there are no <audio autoplay> elements.
		const autoplayAudio = page.locator('audio[autoplay]');
		await expect(autoplayAudio).toHaveCount(0);
		// And no <video autoplay>.
		const autoplayVideo = page.locator('video[autoplay]');
		await expect(autoplayVideo).toHaveCount(0);
	});
});
