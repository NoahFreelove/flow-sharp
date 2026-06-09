import { error } from '@sveltejs/kit';
import { toSlug, titleFromPageName } from '$lib/docs/slug.js';
import { buildCategories } from '$lib/docs/categories';
import type { EntryGenerator, PageLoad } from './$types';

// Static prerender of every synced wiki page (D-49-13/16/25). The wiki markdown lives at
// src/docs/wiki/*.md (synced by scripts/sync-wiki.sh) and is compiled to Svelte components by the
// mdsvex preprocessor; the [[link]] transform runs INSIDE that pipeline (remark-wiki-links).
export const prerender = true;

// Lazy glob of the compiled wiki components — keyed by absolute module path. eager:false so each
// page only loads its own component at prerender time.
const wikiModules = import.meta.glob('/src/docs/wiki/*.md');

interface WikiEntry {
	/** Original wiki page name (no extension), e.g. `Audio-and-Synthesis`. */
	page: string;
	/** Lazy loader for the compiled Svelte component. */
	loader: () => Promise<unknown>;
}

/** slug -> { page, loader }, derived once from the glob keys (filenames are the real page names). */
const bySlug = new Map<string, WikiEntry>();
for (const [path, loader] of Object.entries(wikiModules)) {
	const filename = path.split('/').pop() ?? path;
	const page = filename.replace(/\.md$/i, '');
	bySlug.set(toSlug(filename), { page, loader: loader as () => Promise<unknown> });
}

/** All real page names (no extension) — feeds the categorized sidebar TOC + orphan warning. */
const allPageNames = [...bySlug.values()].map((e) => e.page);

// entries() tells SvelteKit which [slug] values to prerender — all synced wiki pages (D-49-27).
export const entries: EntryGenerator = () => {
	return [...bySlug.keys()].map((slug) => ({ slug }));
};

interface WikiModule {
	default: unknown; // the compiled Svelte component
	metadata?: Record<string, unknown>;
}

export const load: PageLoad = async ({ params }) => {
	const entry = bySlug.get(params.slug);
	if (!entry) {
		throw error(404, `No docs page for "${params.slug}"`);
	}
	const mod = (await entry.loader()) as WikiModule;

	// Sidebar nav is the same categorized TOC as /docs (D-49-22), current page highlighted.
	const categories = buildCategories(allPageNames);

	return {
		slug: params.slug,
		component: mod.default,
		title: titleFromPageName(entry.page),
		categories
	};
};
