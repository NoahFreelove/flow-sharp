import { buildCategories } from '$lib/docs/categories';
import { toSlug } from '$lib/docs/slug.js';
import type { PageLoad } from './$types';

// The /docs index is the categorized TOC (D-49-22). Static prerender — the category grouping is
// CONFIG-DRIVEN from docs-categories.json (resolved by categories.ts), never hard-coded here.
export const prerender = true;

// Discover the synced wiki page set so categories.ts can warn on any page missing from the config.
const wikiModules = import.meta.glob('/src/docs/wiki/*.md');
const allPageNames: string[] = Object.keys(wikiModules).map((path) => {
	const filename = path.split('/').pop() ?? path;
	return filename.replace(/\.md$/i, '');
});

// Slugs of pages that actually exist as synced wiki files — used to skip dangling TOC links.
const existingSlugs = new Set<string>(allPageNames.map((p) => toSlug(p)));

export const load: PageLoad = () => {
	return {
		categories: buildCategories(allPageNames),
		existingSlugs: [...existingSlugs]
	};
};
