import { test, expect } from '@playwright/test';
import { PIECES, isRunnable } from '../src/lib/showcase/pieces';

// REQ-SITE-SHOWCASE-01 — the curated showcase gallery (/showcase) + per-piece detail
// (/showcase/[slug]). D-49-24 (6–12 curated pieces), D-49-01 (gesture-gated audio, no autoplay),
// D-49-15 (server-rendered shiki source). The manifest is the source of truth — this spec asserts
// against the REAL PIECES set so adding/removing a piece keeps the test honest.
//
// Honest-worktree contract (49-CONTEXT): pieces with embedded source render a shiki block + (when
// Web-runnable) an Open-in-playground deep-link; pieces whose .flow is absent link out to GitHub
// (no fabricated source). The spec checks both branches.

const RUNNABLE = PIECES.filter(isRunnable);
const LINKED_OUT = PIECES.filter((p) => p.sourceRef && !p.source);
const WITH_AUDIO = PIECES.filter((p) => p.audioSrc);

/**
 * Svelte/SvelteKit HTML-escapes text in the prerendered markup, so a raw `request.get().text()`
 * carries `&amp;`/`&lt;` rather than literal `&`/`<`. Titles like "Stride & Stomp" only match the
 * raw bytes once escaped — assert against the escaped form when checking the unparsed HTML string.
 */
function escapeHtml(s: string): string {
	return s
		.replace(/&/g, '&amp;')
		.replace(/</g, '&lt;')
		.replace(/>/g, '&gt;');
}

test.describe('showcase gallery (REQ-SITE-SHOWCASE-01)', () => {
	test('the manifest curates 6–12 pieces (D-49-24)', () => {
		expect(PIECES.length).toBeGreaterThanOrEqual(6);
		expect(PIECES.length).toBeLessThanOrEqual(12);
	});

	test('/showcase is prerendered: every piece card is in the raw HTML', async ({ request }) => {
		// Raw HTTP response — NO JS runs. Prerendered content must already be present (D-49-13).
		const res = await request.get('/showcase');
		expect(res.ok()).toBeTruthy();
		const html = await res.text();
		for (const piece of PIECES) {
			expect(html, `gallery must list "${piece.title}"`).toContain(escapeHtml(piece.title));
			expect(html, `gallery must link to /showcase/${piece.slug}`).toContain(
				`/showcase/${piece.slug}`
			);
		}
	});

	test('the gallery lives in a single <main> landmark', async ({ page }) => {
		await page.goto('/showcase');
		await expect(page.locator('main')).toHaveCount(1);
		// One card link per manifest piece.
		const cards = page.locator('main a[href^="/showcase/"]');
		await expect(cards).toHaveCount(PIECES.length);
	});

	test('/showcase pulls NO WASM runtime (only /playground lazy-loads it, D-49-34)', async ({
		request
	}) => {
		const res = await request.get('/showcase');
		const html = await res.text();
		expect(html).not.toContain('flow-runtime.js');
	});
});

test.describe('showcase detail pages (REQ-SITE-SHOWCASE-01)', () => {
	for (const piece of PIECES) {
		test(`/showcase/${piece.slug} renders (200) with title + composer notes`, async ({
			request
		}) => {
			const res = await request.get(`/showcase/${piece.slug}`);
			expect(res.status(), `${piece.slug} must prerender to 200`).toBe(200);
			const html = await res.text();
			expect(html).toContain(escapeHtml(piece.title));
			// Composer notes are always present (the "why this piece" prose).
			expect(html).toContain('Composer notes');
		});
	}

	test('audio embeds never autoplay and always sit behind a play control (D-49-01)', async ({
		page
	}) => {
		// Use a piece that HAS a pre-rendered audio asset.
		expect(WITH_AUDIO.length).toBeGreaterThan(0);
		const piece = WITH_AUDIO[0];
		await page.goto(`/showcase/${piece.slug}`);

		const players = page.locator('audio');
		const count = await players.count();
		expect(count).toBeGreaterThan(0);
		for (let i = 0; i < count; i++) {
			// No self-starting attribute anywhere (D-49-01 — nothing autoplays).
			expect(await players.nth(i).getAttribute('autoplay')).toBeNull();
			// Accessible name on the embed.
			await expect(players.nth(i)).toHaveAttribute('aria-label', /.+/);
		}
		// An explicit play control gates playback (the AudioEmbed brass Play button).
		await expect(page.getByRole('button', { name: /play/i }).first()).toBeVisible();
	});

	test('present-source pieces render a server-highlighted shiki source block', async ({
		request
	}) => {
		expect(RUNNABLE.length).toBeGreaterThan(0);
		const piece = RUNNABLE[0];
		const res = await request.get(`/showcase/${piece.slug}`);
		const html = await res.text();
		// shiki emits <pre class="shiki ..."> server-side (zero client highlight JS, D-49-15).
		expect(html).toContain('shiki');
		expect(html).toContain(piece.sourcePath!);
	});

	test('Web-runnable pieces carry an Open-in-playground deep-link (#code=)', async ({ request }) => {
		const piece = RUNNABLE[0];
		const res = await request.get(`/showcase/${piece.slug}`);
		const html = await res.text();
		expect(html).toContain('Open in playground');
		expect(html).toContain('/playground#code=');
	});

	test('absent-source pieces link out to GitHub — no fabricated source block', async ({
		request
	}) => {
		// The symphony/ragtime pieces had their .flow removed from the worktree (49-CONTEXT).
		expect(LINKED_OUT.length).toBeGreaterThan(0);
		const piece = LINKED_OUT[0];
		const res = await request.get(`/showcase/${piece.slug}`);
		const html = await res.text();
		expect(html).toContain('View source on GitHub');
		expect(html).toContain(piece.sourceRef!);
		// And it must NOT show a shiki source block (no fabricated .flow).
		expect(html).not.toContain('pre class="shiki');
	});

	test('a detail page is reachable by clicking a gallery card', async ({ page }) => {
		await page.goto('/showcase');
		const first = PIECES[0];
		await page.locator(`main a[href="/showcase/${first.slug}"]`).first().click();
		await expect(page).toHaveURL(new RegExp(`/showcase/${first.slug}$`));
		await expect(page.getByRole('heading', { level: 1, name: first.title })).toBeVisible();
	});
});
