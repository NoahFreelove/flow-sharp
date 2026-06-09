/**
 * Slug generation for docs pages (D-49-27).
 *
 * Slugs are lowercase-kebab derived from the wiki filename:
 *   `Quick-Start.md` -> `quick-start`
 *   `Audio-and-Synthesis.md` -> `audio-and-synthesis`
 *
 * The slug `index` is RESERVED for the categorized TOC page (`/docs`). A wiki file that slugs to
 * `index` is rejected at build time so it can never shadow the TOC. Slug collisions (two filenames
 * producing the same slug) throw a clear build-time error rather than silently dropping a page.
 *
 * Plain JS (not TS) so the Node-loaded `svelte.config.js` mdsvex/remark chain can import it
 * directly (Node ESM cannot import `.ts`). App + test code consume it the same way.
 */

/** The reserved slug owned by the `/docs` TOC index page. */
export const RESERVED_SLUG = 'index';

/**
 * Convert a wiki filename (or bare page name) to a lowercase-kebab slug.
 * @param {string} filename
 * @returns {string}
 */
export function toSlug(filename) {
	const base = filename.replace(/\.(md|svx|markdown)$/i, '');
	const slug = base
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-+|-+$/g, '');

	if (slug === RESERVED_SLUG) {
		throw new Error(
			`Wiki file "${filename}" slugs to the reserved "${RESERVED_SLUG}" slug (owned by the /docs TOC). Rename the wiki page.`
		);
	}
	if (slug.length === 0) {
		throw new Error(`Wiki file "${filename}" produces an empty slug — rename the wiki page.`);
	}
	return slug;
}

/**
 * Build a `filename -> slug` map for a set of wiki filenames, throwing on collision.
 * @param {string[]} filenames
 * @returns {Map<string, string>}
 */
export function slugMap(filenames) {
	/** @type {Map<string, string>} */
	const map = new Map();
	/** @type {Map<string, string>} slug -> first filename that produced it */
	const seen = new Map();

	for (const filename of filenames) {
		const slug = toSlug(filename);
		const prior = seen.get(slug);
		if (prior !== undefined) {
			throw new Error(
				`Slug collision: "${filename}" and "${prior}" both produce "/docs/${slug}". Rename one wiki page.`
			);
		}
		seen.set(slug, filename);
		map.set(filename, slug);
	}
	return map;
}

/**
 * Turn a `[[Page-Name]]` token into its display title: `Quick-Start` -> `Quick Start`.
 * @param {string} pageName
 * @returns {string}
 */
export function titleFromPageName(pageName) {
	return pageName.replace(/[-_]+/g, ' ').trim();
}
