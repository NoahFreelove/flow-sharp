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

	<main class="docs-body surface-paper surface-paper--staff">
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

	/* On desktop the disclosure is always open (it's the persistent sidebar nav). */
	.docs-toc-disclosure > summary {
		display: none;
		font-weight: 600;
		color: var(--color-paper);
		cursor: pointer;
		padding: var(--space-2) 0;
	}

	.docs-cat + .docs-cat {
		margin-top: var(--space-4);
	}
	.docs-cat__name {
		font-size: var(--text-small);
		font-weight: 600;
		text-transform: uppercase;
		letter-spacing: 0.04em;
		color: var(--color-brass);
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
		color: var(--color-paper);
		text-decoration: none;
		font-size: var(--text-small);
	}
	.docs-cat__list a:hover {
		text-decoration: underline;
		text-decoration-color: var(--color-brass);
	}
	.docs-cat__list a.is-current {
		background: color-mix(in srgb, var(--color-brass) 24%, transparent);
		color: var(--color-paper);
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
	/* Code blocks (shiki <pre>, plus the highlighter's <figure class="docs-codeblock">) scroll
	   horizontally inside a keyboard-accessible region rather than overflowing the page. The
	   highlighter already wraps flow blocks in a focusable figure; bare prose <pre> get tabindex
	   via the rule below so axe scrollable-region-focusable stays satisfied. */
	.docs-prose :global(pre) {
		max-width: 100%;
		overflow-x: auto;
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
