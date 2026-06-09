<!--
  /showcase — the curated showcase gallery (D-49-24, UI-SPEC §Showcase gallery).

  A grid of <PieceCard> (<Panel framed>) cards — 6–12 curated Flow pieces spanning genres. Each
  card links to its `/showcase/[slug]` detail page. Single <main> landmark. Prerendered (the set is
  static at build time). Nothing plays here — the gesture-gated audio lives on the detail pages
  (D-49-01).
-->
<script lang="ts">
	import PieceCard from '$lib/showcase/PieceCard.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Showcase — hear what Flow makes</title>
	<meta
		name="description"
		content="Curated Flow pieces across genres — generative jazz, granular sound design, microtonal tunings, parameterized song structure, and more. Hear what the language makes."
	/>
</svelte:head>

<main class="showcase">
	<header class="showcase__head">
		<h1 class="showcase__title">Showcase</h1>
		<p class="showcase__lead">
			Hear what Flow makes. A curated set of pieces across genres — each with its source and a note
			on why it’s here. Press play, or open any piece in the playground and make it your own.
		</p>
	</header>

	<ul class="showcase__grid">
		{#each data.pieces as piece (piece.slug)}
			<li class="showcase__cell">
				<PieceCard {piece} />
			</li>
		{/each}
	</ul>
</main>

<style>
	.showcase {
		max-width: 1100px;
		margin: 0 auto;
		padding: var(--space-12) var(--space-6);
	}

	.showcase__head {
		display: flex;
		flex-direction: column;
		gap: var(--space-3);
		margin-bottom: var(--space-8);
		max-width: 70ch;
	}
	.showcase__title {
		margin: 0;
		font-family: var(--font-display);
		font-size: var(--text-h1);
		font-weight: 700;
		letter-spacing: var(--text-h1-ls);
		color: var(--color-ink);
	}
	.showcase__lead {
		margin: 0;
		font-size: var(--text-lead);
		line-height: 1.55;
		color: var(--color-ink-muted);
	}

	.showcase__grid {
		list-style: none;
		margin: 0;
		padding: 0;
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
		gap: var(--space-6);
	}
	.showcase__cell {
		min-width: 0;
	}

	@media (max-width: 600px) {
		.showcase {
			padding: var(--space-8) var(--space-4);
		}
		.showcase__grid {
			grid-template-columns: 1fr;
		}
	}
</style>
