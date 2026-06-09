import adapter from '@sveltejs/adapter-cloudflare';
import { mdsvex } from 'mdsvex';
import rehypeSlug from 'rehype-slug';
import { highlightFlow } from './src/lib/docs/highlight.js';
import { remarkWikiLinks } from './src/lib/docs/remark-wiki-links.js';
import { encode as encodeShare } from './src/lib/share/encode-node.js';

/**
 * Escape a string for safe insertion into mdsvex output: `{`, `}`, and backticks would otherwise
 * be parsed by Svelte as expression delimiters / template literals.
 * @param {string} s
 */
function svelteSafe(s) {
	return s.replace(/\{/g, '&lbrace;').replace(/\}/g, '&rbrace;').replace(/`/g, '&grave;');
}

/** Escape a string for use inside a double-quoted HTML attribute. */
function attrSafe(s) {
	return s
		.replace(/&/g, '&amp;')
		.replace(/"/g, '&quot;')
		.replace(/</g, '&lt;')
		.replace(/>/g, '&gt;');
}

/**
 * mdsvex `highlight` hook (D-49-15/16): fenced code blocks render server-side via shiki + the
 * Phase 17 Flow TextMate grammar (`highlightFlow`). `flow` blocks are wrapped in a
 * `<figure class="docs-codeblock">` carrying an "Open in playground" anchor styled as a secondary
 * button (UI-SPEC §Docs page — NEVER inline-playable on doc pages, D-49-01). The anchor links to
 * `/playground` (the plain fallback until Plan 49-06's encode.ts lands) and stashes the source in
 * `data-flow-source` so the playground can hydrate it. mdsvex inserts the returned HTML into a
 * Svelte component, so `{`/`}`/backticks are escaped AFTER shiki produces the markup.
 *
 * @param {string} code
 * @param {string | null} lang
 */
async function mdsvexHighlight(code, lang) {
	const resolved = lang ?? 'flow';
	const html = await highlightFlow(code, resolved);
	if (resolved !== 'flow') {
		return svelteSafe(html);
	}
	// Plan 49-06: the REAL /playground#code=... deep link, encoded with the share carrier (byte-
	// identical to the browser encode.ts, so the playground's decode() consumes it). `&run=1` carries
	// the auto-run signal (D-49-08). `data-flow-source` stays for carrier-contract symmetry.
	const href = `/playground#code=${encodeShare(code)}&run=1`;
	const figure =
		`<figure class="docs-codeblock" data-flow-source="${attrSafe(code)}">` +
		html +
		`<a class="docs-open-in-playground skeuo-btn skeuo-btn--secondary" href="${attrSafe(href)}" ` +
		`data-flow-source="${attrSafe(code)}" data-run="1">Open in playground</a>` +
		`</figure>`;
	return svelteSafe(figure);
}

/**
 * mdsvex preprocess for /docs MDX-flavored markdown (D-49-16) with shiki Flow highlighting.
 *
 * @type {import('mdsvex').MdsvexOptions}
 */
const mdsvexOptions = {
	extensions: ['.md', '.svx'],
	highlight: { highlighter: mdsvexHighlight },
	// [[Page-Name]] + relative `Page.md(#anchor)` links -> /docs routes (D-49-26, Task 1 transform).
	remarkPlugins: [remarkWikiLinks],
	// rehype-slug adds id="" to headings so in-page `#anchor` links (e.g. `#voice-pool`) resolve.
	rehypePlugins: [rehypeSlug]
};

/** @type {import('@sveltejs/kit').Config} */
const config = {
	// `.md` + `.svx` are treated as Svelte components via the mdsvex preprocessor.
	extensions: ['.svelte', '.svx', '.md'],
	preprocess: [mdsvex(mdsvexOptions)],
	compilerOptions: {
		// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
		runes: ({ filename }) => (filename.split(/[/\\]/).includes('node_modules') ? undefined : true)
	},
	kit: {
		// adapter-cloudflare (NOT adapter-static) — gives optional server route handlers for the
		// gist OAuth endpoint while the rest of the site is statically prerendered (D-49-13).
		adapter: adapter(),
		prerender: {
			// The wiki contains a few cross-links to pages that don't exist as wiki files (e.g.
			// `Articulations.md`) — a CONTENT gap, not a code bug. Warn on such dangling /docs links
			// rather than failing the whole deploy; every REAL wiki page still prerenders.
			handleHttpError: ({ path, referrer, message }) => {
				if (path.startsWith('/docs/')) {
					console.warn(`[prerender] dangling docs link ${path} (from ${referrer}) — ${message}`);
					return;
				}
				// /showcase + /showcase/[slug] ship in Plan 49-07 and prerender fully — the wave-3
				// warn-not-fail allowance for the not-yet-routed nav tab is removed, so a /showcase 404
				// now fails the build (it would signal a real routing/manifest bug).
				throw new Error(message);
			},
			// Some wiki anchors point at sections that don't exist in the page content (a content
			// gap). rehype-slug resolves the real ones; warn (don't fail) on any leftover.
			handleMissingId: 'warn'
		}
	}
};

export default config;
