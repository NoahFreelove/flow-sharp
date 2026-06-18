<!--
  /docs/[slug] — a single rendered wiki page (D-49-22, UI-SPEC §Docs page).

  Two-column desktop: left `.surface-wood`-framed sidebar (the categorized nav, current page
  carries aria-current), right `.surface-paper` doc body with faint staff-line ruling. Code blocks
  are static shiki HTML (server-rendered, zero client JS) with an "Open in playground" secondary
  button injected by the mdsvex highlighter. Mobile (<768px): sidebar collapses to a "Contents"
  disclosure above the full-width body.
-->
<script lang="ts">
	import type { Component } from 'svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	// mdsvex compiles each wiki .md into a Svelte component; render it dynamically.
	const DocComponent = $derived(data.component as Component);
</script>

<svelte:head>
	<title>{data.title} — Flow docs</title>
	<meta name="description" content={`${data.title} — Flow language documentation.`} />
</svelte:head>

<div class="docs-shell">
	<aside class="docs-sidebar surface-wood" aria-label="Docs navigation">
		<details class="docs-toc-disclosure" open>
			<summary>Contents</summary>
			<nav aria-label="Documentation pages">
				{#each data.categories as category (category.name)}
					<div class="docs-cat">
						<h2 class="docs-cat__name">{category.name}</h2>
						<ul class="docs-cat__list">
							{#each category.links as link (link.slug)}
								<li>
									<a
										href={`/docs/${link.slug}`}
										aria-current={link.slug === data.slug ? 'page' : undefined}
										class:is-current={link.slug === data.slug}
									>
										{link.title}
									</a>
								</li>
							{/each}
						</ul>
					</div>
				{/each}
			</nav>
		</details>
	</aside>

	<main class="docs-body surface-paper">
		<article class="docs-prose">
			<DocComponent />
		</article>
	</main>
</div>

<style>
	.docs-shell {
		display: grid;
		grid-template-columns: minmax(220px, 280px) minmax(0, 1fr);
		gap: var(--space-6);
		align-items: start;
		max-width: 1200px;
		margin: 0 auto;
		padding: var(--space-6);
	}

	.docs-sidebar {
		position: sticky;
		top: var(--space-4);
		padding: var(--space-4);
		border-radius: var(--radius-4);
		/* Grid item must shrink below its content's intrinsic width (otherwise it forces the
		   shared column wide and the page overflows horizontally at 320/375px, D-49-09). */
		min-width: 0;
	}

	/* Lift the nav content above the wood-grain ::before texture. The .surface-wood overlay is a
	   positioned ::before painted ON TOP of in-flow content (multiply blend), which was muddying
	   the headings + links into the wood. A stacking context (position + z-index) puts the text
	   back on top so cream + brass read at full contrast. */
	.docs-toc-disclosure {
		position: relative;
		z-index: 1;
	}

	/* On desktop the disclosure is always open (it's the persistent sidebar nav). */
	.docs-toc-disclosure > summary {
		display: none;
		font-weight: 600;
		color: var(--color-on-chrome);
		cursor: pointer;
		padding: var(--space-2) 0;
	}

	.docs-cat + .docs-cat {
		margin-top: var(--space-4);
	}
	.docs-cat__name {
		font-size: var(--text-small);
		font-weight: 700;
		text-transform: uppercase;
		letter-spacing: 0.04em;
		/* Brightened brass so the category labels read clearly on the dark wood (plain --color-brass
		   was too low-contrast). */
		color: color-mix(in srgb, var(--color-brass) 78%, white);
		margin: 0 0 var(--space-2);
	}
	.docs-cat__list {
		list-style: none;
		margin: 0;
		padding: 0;
		display: flex;
		flex-direction: column;
		gap: var(--space-1, 4px);
	}
	.docs-cat__list a {
		display: block;
		padding: 4px var(--space-2);
		border-radius: var(--radius-2);
		color: var(--color-on-chrome);
		text-decoration: none;
		font-size: var(--text-small);
	}
	.docs-cat__list a:hover {
		text-decoration: underline;
		text-decoration-color: var(--color-brass);
	}
	.docs-cat__list a.is-current {
		background: color-mix(in srgb, var(--color-brass) 24%, transparent);
		color: var(--color-on-chrome);
		font-weight: 600;
	}
	.docs-cat__list a:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}

	.docs-body {
		padding: var(--space-6) var(--space-8, 32px);
		border-radius: var(--radius-4);
		min-height: 60vh;
		/* Shrink below intrinsic content width so long code lines scroll inside their own block
		   instead of widening the shared grid column (D-49-09 no horizontal overflow). */
		min-width: 0;
	}

	.docs-prose {
		max-width: 72ch;
		color: var(--color-ink);
		line-height: 1.6;
		min-width: 0;
	}

	/* Heading hierarchy — serif display face, heavier weight, and a hairline under h2 so the
	   document structure reads clearly above body prose (composer: headers were too weak to tell
	   apart from text). Sizes/leadings come from the type scale tokens. */
	.docs-prose :global(h1) {
		font-family: var(--font-display);
		font-size: var(--text-h1);
		line-height: var(--text-h1-lh);
		letter-spacing: var(--text-h1-ls);
		font-weight: 800;
		margin: 0 0 var(--space-4);
		color: var(--color-ink);
	}
	.docs-prose :global(h2) {
		font-family: var(--font-display);
		font-size: var(--text-h2);
		line-height: var(--text-h2-lh);
		letter-spacing: var(--text-h2-ls);
		font-weight: 700;
		margin: var(--space-8) 0 var(--space-3);
		padding-bottom: var(--space-2);
		border-bottom: 1px solid color-mix(in srgb, var(--color-walnut) 22%, transparent);
		color: var(--color-ink);
	}
	.docs-prose :global(h3) {
		font-family: var(--font-display);
		font-size: var(--text-h3);
		line-height: var(--text-h3-lh);
		letter-spacing: var(--text-h3-ls);
		font-weight: 700;
		margin: var(--space-6) 0 var(--space-2);
		color: var(--color-ink);
	}

	/* Inline code — a subtle tinted chip so it stands apart from surrounding prose. Scoped to
	   `code` NOT inside a `pre` (shiki block code keeps its own theme styling). */
	.docs-prose :global(:not(pre) > code) {
		font-family: var(--font-mono);
		font-size: 0.9em;
		background: color-mix(in srgb, var(--color-walnut) 12%, transparent);
		padding: 0.1em 0.36em;
		border-radius: var(--radius-2);
	}

	/* Code blocks (shiki <pre>) get a clear boxed container — border + padding + radius + a soft
	   lift — so they no longer blend into the .surface-paper body (composer: code vs text was hard
	   to tell apart). The shiki theme keeps its own background so the syntax colors stay readable;
	   the box is the border/shadow around it. They also scroll horizontally inside a
	   keyboard-accessible region (tabindex added by the highlighter) rather than overflowing. */
	.docs-prose :global(pre) {
		max-width: 100%;
		overflow-x: auto;
		margin: var(--space-5, 20px) 0;
		padding: var(--space-4);
		border: 1px solid color-mix(in srgb, var(--color-walnut) 26%, transparent);
		border-radius: var(--radius-3);
		box-shadow: 0 1px 2px rgba(0, 0, 0, 0.06);
		font-size: var(--text-code);
		line-height: var(--text-code-lh);
	}

	/* Light-mode code wells sit a touch DARKER than the cream page so a code block reads as a
	   distinct surface, not just bordered prose (shiki's own light bg is near-white = barely
	   different). `!important` overrides shiki's inline background-color. */
	.docs-prose :global(pre.shiki) {
		background-color: #ece4d1 !important;
	}
	/* Dark mode: activate shiki's github-dark variant. With defaultColor:'light' shiki ships the
	   dark colors as inert `--shiki-dark*` CSS vars; swap them in here so dark-theme code blocks
	   render dark (and distinct from the deep-walnut page) instead of a glaring light box. */
	:global([data-theme='dark']) .docs-prose :global(pre.shiki) {
		background-color: var(--shiki-dark-bg) !important;
		color: var(--shiki-dark) !important;
	}
	:global([data-theme='dark']) .docs-prose :global(pre.shiki span) {
		color: var(--shiki-dark) !important;
	}
	.docs-prose :global(pre:focus-visible),
	.docs-prose :global(.docs-codeblock:focus-visible) {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}

	/* Mobile: sidebar becomes a "Contents" disclosure above a full-width body (UI-SPEC). */
	@media (max-width: 767px) {
		.docs-shell {
			grid-template-columns: 1fr;
			padding: var(--space-4);
		}
		.docs-sidebar {
			position: static;
		}
		.docs-toc-disclosure > summary {
			display: block;
		}
		.docs-toc-disclosure:not([open]) nav {
			display: none;
		}
	}
</style>
