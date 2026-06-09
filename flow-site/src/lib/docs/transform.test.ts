import { describe, it, expect } from 'vitest';
import { rewriteWikiLinks } from './transform.js';
import { toSlug, slugMap, RESERVED_SLUG } from './slug.js';
import { buildCategories, categorizedPageCount } from './categories';
// Vite `?raw` loads the fixture file as a string (works under vitest's Vite transform —
// `import.meta.url` is not a file: URL in that context, so node:fs cannot be used here).
import fixture from './__fixtures__/synthetic-wiki-link.md?raw';

// REQ-SITE-DOCS-02 — [[link]] rewrite + slug kebab-case.
//
// The real flow-sharp wiki contains ZERO `[[Page-Name]]` links (RESEARCH Pitfall 7) — the only
// `[[` is array data inside a fenced code block in Collections.md. So these tests run against a
// SYNTHETIC fixture (`__fixtures__/synthetic-wiki-link.md`) that contains real `[[ ]]` links AND
// a fenced code block holding `[[1,10], [2,20], [3,30]]`. Acceptance = "transform runs without
// error + the synthetic fixture round-trips", NOT "real wiki links work".

describe('rewriteWikiLinks — [[link]] transform', () => {
	it('rewrites [[Quick-Start]] -> [Quick Start](/docs/quick-start)', () => {
		const out = rewriteWikiLinks(fixture);
		expect(out).toContain('[Quick Start](/docs/quick-start)');
		// No bare wiki-link tokens survive in prose.
		expect(out).not.toContain('[[Quick-Start]]');
	});

	it('honors the [[Page|label]] piped form', () => {
		const out = rewriteWikiLinks('See [[Note-Streams|note streams]] for syntax.');
		expect(out).toBe('See [note streams](/docs/note-streams) for syntax.');
	});

	it('rewrites multiple links in one pass', () => {
		const out = rewriteWikiLinks(fixture);
		expect(out).toContain('[Effects](/docs/effects)');
		expect(out).toContain('[note streams](/docs/note-streams)');
	});

	it('leaves [[...]] inside a fenced code block untouched', () => {
		const out = rewriteWikiLinks(fixture);
		// The Collections.md-style array literal must survive verbatim.
		expect(out).toContain('[[1,10], [2,20], [3,30]]');
		// And it must NOT have been turned into a markdown link.
		expect(out).not.toContain('](/docs/1-10');
	});

	it('leaves [[...]] inside an inline code span untouched', () => {
		const out = rewriteWikiLinks(fixture);
		expect(out).toContain('`[[not-a-link]]`');
	});

	it('is idempotent — running twice == running once', () => {
		const once = rewriteWikiLinks(fixture);
		const twice = rewriteWikiLinks(once);
		expect(twice).toBe(once);
	});

	it('does not throw on markdown with no wiki links at all', () => {
		const plain = '# Heading\n\nJust prose, no links, no code.\n';
		expect(rewriteWikiLinks(plain)).toBe(plain);
	});

	it('does not mangle a tilde-fenced code block either', () => {
		const md = 'before\n\n~~~flow\n[[1,2]]\n~~~\n\nafter [[Effects]]';
		const out = rewriteWikiLinks(md);
		expect(out).toContain('~~~flow\n[[1,2]]\n~~~');
		expect(out).toContain('[Effects](/docs/effects)');
	});
});

describe('toSlug / slugMap — lowercase-kebab from filename (D-49-27)', () => {
	it('kebab-cases a filename', () => {
		expect(toSlug('Quick-Start.md')).toBe('quick-start');
		expect(toSlug('Audio-and-Synthesis.md')).toBe('audio-and-synthesis');
		expect(toSlug('String-Interpolation')).toBe('string-interpolation');
	});

	it('rejects the reserved `index` slug', () => {
		expect(() => toSlug('Index.md')).toThrow(/reserved/i);
		expect(() => toSlug(`${RESERVED_SLUG}.md`)).toThrow(/reserved/i);
	});

	it('throws on slug collisions', () => {
		// `Quick-Start.md` and `quick start.md` both kebab to `quick-start`.
		expect(() => slugMap(['Quick-Start.md', 'quick start.md'])).toThrow(/collision/i);
	});

	it('builds a clean map when there are no collisions', () => {
		const map = slugMap(['Quick-Start.md', 'Effects.md', 'Note-Streams.md']);
		expect(map.get('Quick-Start.md')).toBe('quick-start');
		expect(map.get('Effects.md')).toBe('effects');
		expect(map.size).toBe(3);
	});
});

describe('buildCategories — config-driven docs TOC (D-49-22)', () => {
	it('maps all 26 wiki pages into the 4 declared categories', () => {
		expect(categorizedPageCount()).toBe(26);
	});

	it('groups pages with resolved slugs + titles', () => {
		const cats = buildCategories();
		const names = cats.map((c) => c.name);
		expect(names).toEqual([
			'Getting Started',
			'Music Concepts',
			'Audio + Output',
			'Reference'
		]);
		const gettingStarted = cats.find((c) => c.name === 'Getting Started')!;
		const quickStart = gettingStarted.links.find((l) => l.page === 'Quick-Start')!;
		expect(quickStart.slug).toBe('quick-start');
		expect(quickStart.title).toBe('Quick Start');
	});

	it('appends an Uncategorized group + warns when a page is missing from config', () => {
		const cats = buildCategories(['Quick-Start', 'Brand-New-Page']);
		const uncategorized = cats.find((c) => c.name === 'Uncategorized');
		expect(uncategorized).toBeDefined();
		expect(uncategorized!.links.some((l) => l.page === 'Brand-New-Page')).toBe(true);
	});
});
