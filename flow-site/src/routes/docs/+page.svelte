<!--
  /docs — the categorized documentation index (D-49-22, UI-SPEC §Docs index).

  Renders the four-category TOC (Getting Started / Music Concepts / Audio + Output / Reference)
  from `categories.ts` / `docs-categories.json` — CONFIG-DRIVEN, never hard-coded here. Each
  category is a <Panel header> card listing its page links (walnut-underline links to /docs/[slug])
  on the `.surface-paper` body.
-->
<script lang="ts">
	import Panel from '$lib/components/skeuo/Panel.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const existing = $derived(new Set(data.existingSlugs));
</script>

<svelte:head>
	<title>Docs — Flow</title>
	<meta
		name="description"
		content="Flow language documentation — getting started, music concepts, audio + output, and reference."
	/>
</svelte:head>

<main class="docs-index surface-paper">
	<header class="docs-index__head">
		<h1>Documentation</h1>
		<p class="docs-index__lead">
			Everything you need to write music as code — start at the top, or jump to a topic.
		</p>
	</header>

	<div class="docs-index__grid">
		{#each data.categories as category (category.name)}
			<Panel variant="header" title={category.name} elevation="seated">
				<ul class="docs-index__list">
					{#each category.links as link (link.slug)}
						{#if existing.has(link.slug)}
							<li>
								<a class="docs-index__link" href={`/docs/${link.slug}`}>{link.title}</a>
							</li>
						{/if}
					{/each}
				</ul>
			</Panel>
		{/each}
	</div>
</main>

<style>
	.docs-index {
		max-width: 1100px;
		margin: 0 auto;
		padding: var(--space-8, 32px) var(--space-6);
		border-radius: var(--radius-4);
	}

	.docs-index__head {
		margin-bottom: var(--space-6);
	}
	.docs-index h1 {
		font-size: var(--text-h2);
		font-weight: 600;
		color: var(--color-ink);
		margin: 0 0 var(--space-2);
	}
	.docs-index__lead {
		color: var(--color-ink-muted);
		font-size: var(--text-body);
		margin: 0;
	}

	.docs-index__grid {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
		gap: var(--space-6);
		align-items: start;
	}

	.docs-index__list {
		list-style: none;
		margin: 0;
		padding: 0;
		display: flex;
		flex-direction: column;
		gap: var(--space-2);
	}

	.docs-index__link {
		color: var(--color-ink);
		text-decoration: underline;
		text-decoration-color: var(--color-walnut);
		text-underline-offset: 3px;
		font-size: var(--text-body);
	}
	.docs-index__link:hover {
		text-decoration-color: var(--color-brass);
		color: var(--color-walnut);
	}
	.docs-index__link:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
		border-radius: var(--radius-2);
	}
</style>
