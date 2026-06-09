<!--
  <PieceCard> — a showcase gallery card (UI-SPEC §Showcase gallery, D-49-24).

  A <Panel framed> card with the piece title, a one-line genre tag, and a felt-grille play
  affordance (the speaker-grille motif — decorative here; the real gesture-gated <audio> lives on
  the detail page per D-49-01). The whole card links to `/showcase/<slug>`. Pieces whose source is
  absent from the worktree carry a small "source on GitHub" badge so the gallery is honest at a
  glance.
-->
<script lang="ts">
	import Panel from '$lib/components/skeuo/Panel.svelte';
	import type { ShowcasePiece } from './pieces';

	let { piece }: { piece: ShowcasePiece } = $props();

	const hasAudio = $derived(Boolean(piece.audioSrc));
	const isLinkedOut = $derived(Boolean(piece.sourceRef) && !piece.source);
</script>

<a class="piece-card-link" href={`/showcase/${piece.slug}`} aria-label={`${piece.title} — ${piece.genre}`}>
	<Panel variant="framed" elevation="seated" screws={true}>
		<article class="piece-card">
			<header class="piece-card__head">
				<h2 class="piece-card__title">{piece.title}</h2>
				<p class="piece-card__genre">{piece.genre}</p>
			</header>

			<!-- Felt-grille play affordance (decorative speaker grille — UI-SPEC §Showcase). -->
			<div class="piece-card__grille surface-felt" aria-hidden="true">
				<span class="piece-card__play-glyph">{hasAudio ? '▶' : '↗'}</span>
			</div>

			<footer class="piece-card__meta">
				<span class="piece-card__phase">{piece.phase}</span>
				{#if isLinkedOut}
					<span class="piece-card__badge">source on GitHub</span>
				{:else if hasAudio}
					<span class="piece-card__badge">hear it</span>
				{:else}
					<span class="piece-card__badge">open in playground</span>
				{/if}
			</footer>
		</article>
	</Panel>
</a>

<style>
	.piece-card-link {
		display: block;
		text-decoration: none;
		color: inherit;
		border-radius: var(--radius-4);
	}
	.piece-card-link:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}

	.piece-card {
		display: flex;
		flex-direction: column;
		gap: var(--space-3);
		min-width: 0;
	}

	.piece-card__head {
		display: flex;
		flex-direction: column;
		gap: var(--space-1);
	}
	.piece-card__title {
		margin: 0;
		font-size: var(--text-h3);
		font-weight: 600;
		letter-spacing: var(--text-h3-ls);
		color: var(--color-ink);
	}
	.piece-card__genre {
		margin: 0;
		font-size: var(--text-small);
		color: var(--color-ink-muted);
	}

	.piece-card__grille {
		display: flex;
		align-items: center;
		justify-content: center;
		height: 56px;
		border-radius: var(--radius-3);
		box-shadow: var(--shadow-inset);
	}
	.piece-card__play-glyph {
		font-size: 20px;
		color: var(--color-paper);
		opacity: 0.85;
	}

	.piece-card__meta {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: var(--space-2);
	}
	.piece-card__phase {
		font-size: var(--text-caption);
		color: var(--color-ink-muted);
	}
	.piece-card__badge {
		font-size: var(--text-caption);
		font-weight: 600;
		padding: 2px var(--space-2);
		border-radius: var(--radius-2);
		background: color-mix(in srgb, var(--color-brass) 22%, transparent);
		color: var(--color-ink);
	}

	/* Hover lift mirrors the Button bevel-brighten (no translate — same material). */
	.piece-card-link:hover .piece-card__title {
		text-decoration: underline;
		text-decoration-color: var(--color-brass);
	}
</style>
