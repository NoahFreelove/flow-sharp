/**
 * Categorized docs TOC (D-49-22).
 *
 * The category grouping is CONFIG-DRIVEN from `docs-categories.json`, NOT hard-coded in any
 * Svelte component. This module loads that config, resolves each page name to its slug + title,
 * and groups them in category order for the `/docs` index. Any wiki page absent from the config
 * surfaces a loud `console.warn` (so a newly added wiki page is never silently dropped from the
 * TOC) — it is then appended to an "Uncategorized" group rather than vanishing.
 */

import rawCategories from '../../../docs-categories.json';
import { toSlug, titleFromPageName } from './slug.js';

/** A single docs page entry in the TOC. */
export interface DocLink {
	/** Wiki page name without extension, e.g. `Quick-Start`. */
	page: string;
	/** Human title, e.g. `Quick Start`. */
	title: string;
	/** Route slug, e.g. `quick-start` (→ `/docs/quick-start`). */
	slug: string;
}

/** A named category and its ordered list of doc links. */
export interface DocCategory {
	name: string;
	links: DocLink[];
}

/** The raw config shape: `{ "Category Name": ["Page-Name", ...], ... }`. */
const config = rawCategories as Record<string, string[]>;

/** Ordered category names exactly as declared in `docs-categories.json`. */
export const categoryOrder: string[] = Object.keys(config);

/** Group of catch-all pages not present in the config (should normally be empty). */
const UNCATEGORIZED = 'Uncategorized';

function toLink(page: string): DocLink {
	return { page, title: titleFromPageName(page), slug: toSlug(page) };
}

/**
 * Build the categorized TOC. `allPageNames` is the set of wiki page names (no extension)
 * discovered from the synced markdown — when supplied, any page missing from the config is
 * warned about and appended to an "Uncategorized" group. When omitted, the TOC is built
 * purely from the config (the common prerender path where the page set == the config set).
 */
export function buildCategories(allPageNames?: string[]): DocCategory[] {
	const categorized = new Set<string>();
	const categories: DocCategory[] = categoryOrder.map((name) => {
		const links = config[name].map((page) => {
			categorized.add(page);
			return toLink(page);
		});
		return { name, links };
	});

	if (allPageNames && allPageNames.length > 0) {
		const orphans = allPageNames.filter((page) => !categorized.has(page));
		if (orphans.length > 0) {
			console.warn(
				`[docs-categories] ${orphans.length} wiki page(s) not in docs-categories.json — ` +
					`add them to a category so they appear in the /docs TOC: ${orphans.join(', ')}`
			);
			categories.push({ name: UNCATEGORIZED, links: orphans.map(toLink) });
		}
	}

	return categories;
}

/** Flat count of all categorized page links (used by tests + the TOC link-count assertion). */
export function categorizedPageCount(): number {
	return Object.values(config).reduce((n, pages) => n + pages.length, 0);
}
