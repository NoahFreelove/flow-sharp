<!--
  /showcase/[slug] — a single showcase piece detail (UI-SPEC §Showcase detail, D-49-24).

  Layout (top → bottom): hero audio (gesture-gated <AudioEmbed> — plays only on a user press,
  D-49-01) OR a "hear it in the playground" poster when no pre-rendered asset exists; composer notes
  ("why this piece, what Flow features show up"); the shiki-highlighted source block (server-
  rendered, escaped) OR a "view source on GitHub" link for pieces whose .flow is absent from the
  worktree; and an "Open in playground" <Button secondary> deep-linking the source (only for
  Web-runnable pieces — same #code= contract as Home's CodeCard).

  Honesty rules (49-CONTEXT): absent-source pieces never show fabricated .flow — they link out.
  Non-Web-runnable pieces (filesystem reads / content-only packs) show the source for READING but no
  Open-in-playground button (it would print nothing audible).

  The <audio> element (in AudioEmbed) carries no self-starting attribute — playback starts only from
  the user pressing Play (the single gesture).
-->
<script lang="ts">
	import Panel from '$lib/components/skeuo/Panel.svelte';
	import AudioEmbed from '$lib/home/AudioEmbed.svelte';
	import LedIndicator from '$lib/components/skeuo/LedIndicator.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
	const piece = $derived(data.piece);
</script>

<svelte:head>
	<title>{piece.title} — Flow showcase</title>
	<meta name="description" content={`${piece.title} — ${piece.genre}. ${piece.notes.slice(0, 140)}`} />
</svelte:head>

<main class="piece">
	<nav class="piece__back" aria-label="Breadcrumb">
		<a href="/showcase">← Showcase</a>
	</nav>

	<header class="piece__head">
		<h1 class="piece__title">{piece.title}</h1>
		<p class="piece__genre">{piece.genre} · {piece.phase}</p>
	</header>

	<!-- Hero audio: gesture-gated AudioEmbed when a real asset exists; otherwise a poster that
	     points to the playground (no fabricated audio, D-49-01). -->
	<section class="piece__hero" aria-label="Listen">
		{#if piece.audioSrc}
			<AudioEmbed src={piece.audioSrc} title={piece.title} />
		{:else}
			<div class="piece__poster surface-felt">
				<LedIndicator state="idle" label={piece.title} />
				<div class="piece__poster-text">
					<span class="piece__poster-title">No pre-rendered audio yet</span>
					{#if piece.playgroundHref}
						<span class="piece__poster-caption"
							>Open it in the playground below and press Run to hear it.</span
						>
					{:else}
						<span class="piece__poster-caption"
							>Run it on the desktop CLI to hear it — see the source below.</span
						>
					{/if}
				</div>
			</div>
		{/if}
	</section>

	<!-- Composer notes — why this piece, what Flow features show up. -->
	<section class="piece__notes" aria-label="Composer notes">
		<Panel variant="header" title="Composer notes" elevation="seated">
			<p class="piece__notes-body">{piece.notes}</p>
		</Panel>
	</section>

	<!-- Source: shiki block for in-repo pieces; a "view source on GitHub" link for absent pieces. -->
	<section class="piece__source" aria-label="Source">
		<h2 class="piece__section-title">Source</h2>
		{#if piece.sourceHtml}
			<figure class="piece__codeblock" data-flow-source={piece.source}>
				<!-- Server-rendered, escaped shiki markup (Security: first-party curated source only). -->
				<!-- eslint-disable-next-line svelte/no-at-html-tags -->
				{@html piece.sourceHtml}
				<figcaption class="piece__codecaption">{piece.sourcePath}</figcaption>
			</figure>

			{#if piece.playgroundHref}
				<a
					class="piece__open skeuo-btn skeuo-btn--secondary"
					href={piece.playgroundHref}
					data-flow-source={piece.source}
					data-run="1"
				>
					Open in playground
				</a>
			{:else}
				<p class="piece__source-note">
					This piece reads files off disk or registers content at engine init, so it runs on the
					desktop CLI rather than in the browser playground.
				</p>
			{/if}
		{:else if piece.sourceRef}
			<p class="piece__source-note">
				The <code>.flow</code> source for this piece isn’t bundled with the site — view it in the repository.
			</p>
			<a
				class="piece__open skeuo-btn skeuo-btn--secondary"
				href={piece.sourceRef}
				target="_blank"
				rel="noopener noreferrer"
			>
				View source on GitHub
				<span class="sr-only"> (opens in new tab)</span>
			</a>
		{/if}
	</section>
</main>

<style>
	.piece {
		max-width: 860px;
		margin: 0 auto;
		padding: var(--space-12) var(--space-6);
		display: flex;
		flex-direction: column;
		gap: var(--space-8);
	}

	.piece__back a {
		font-size: var(--text-small);
		color: var(--color-ink-muted);
		text-decoration: none;
	}
	.piece__back a:hover {
		text-decoration: underline;
		text-decoration-color: var(--color-brass);
	}
	.piece__back a:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}

	.piece__head {
		display: flex;
		flex-direction: column;
		gap: var(--space-2);
	}
	.piece__title {
		margin: 0;
		font-family: var(--font-display);
		font-size: var(--text-h1);
		font-weight: 700;
		letter-spacing: var(--text-h1-ls);
		color: var(--color-ink);
	}
	.piece__genre {
		margin: 0;
		font-size: var(--text-lead);
		color: var(--color-ink-muted);
	}

	.piece__poster {
		display: flex;
		align-items: center;
		gap: var(--space-3);
		padding: var(--space-4) var(--space-4);
		border-radius: var(--radius-3);
		box-shadow: var(--shadow-inset);
	}
	.piece__poster-text {
		display: flex;
		flex-direction: column;
		gap: 2px;
		min-width: 0;
	}
	.piece__poster-title {
		font-size: var(--text-small);
		font-weight: 600;
		color: var(--color-on-chrome);
	}
	.piece__poster-caption {
		font-size: var(--text-caption);
		color: var(--color-on-chrome);
		opacity: 0.85;
	}

	.piece__notes-body {
		margin: 0;
		font-size: var(--text-body);
		line-height: 1.6;
		color: var(--color-ink);
	}

	.piece__section-title {
		margin: 0 0 var(--space-3);
		font-size: var(--text-h3);
		font-weight: 600;
		letter-spacing: var(--text-h3-ls);
		color: var(--color-ink);
	}

	.piece__codeblock {
		margin: 0 0 var(--space-4);
		border-radius: var(--radius-3);
		overflow: hidden;
	}
	.piece__codeblock :global(pre.shiki) {
		margin: 0;
		padding: var(--space-4);
		border-radius: var(--radius-3);
		font-family: var(--font-mono);
		font-size: var(--text-code);
		line-height: var(--text-code-lh, 1.6);
		overflow-x: auto;
	}
	.piece__codecaption {
		margin-top: var(--space-2);
		font-size: var(--text-caption);
		font-family: var(--font-mono);
		color: var(--color-ink-muted);
	}

	.piece__open {
		display: inline-flex;
		align-self: flex-start;
		text-decoration: none;
	}

	.piece__source-note {
		margin: 0 0 var(--space-3);
		font-size: var(--text-small);
		color: var(--color-ink-muted);
	}

	@media (max-width: 600px) {
		.piece {
			padding: var(--space-8) var(--space-4);
			gap: var(--space-6);
		}
	}
</style>
