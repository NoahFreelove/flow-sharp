<!--
  <CodeCard> — a hero "Play in playground" snippet card (D-49-21 §Hero, UI-SPEC §Home).

  Renders a server-highlighted Flow snippet (shiki via $lib/docs/shiki `highlightFlow`, run at
  prerender time in +page.ts and passed in as `html` — Home ships ZERO client highlight JS) on a
  .surface-paper <Panel> card, with a brass `primary` "Play in playground" <Button>.

  The deep-link follows the SAME contract Plan 49-04's docs "Open in playground" set: the anchor
  carries the raw Flow source in `data-flow-source` plus the `playground#code=` fragment the
  playground reads, and a `data-run="1"` auto-run signal (D-49-08 — clicking Play counts as the
  one user gesture, so the playground may auto-click Run on arrival). Until Plan 49-06's encode.ts
  lands, `#code=` holds the URL-encoded source (the playground already reads `#code=` defensively);
  49-06 swaps in the deflate/base64url encoder without touching this contract. Nothing plays on its
  own — pressing Play is the gesture.
-->
<script lang="ts">
	import Panel from '$lib/components/skeuo/Panel.svelte';
	import { encode } from '$lib/share/encode';

	let {
		title,
		blurb,
		source,
		html
	}: {
		/** Card title. */
		title: string;
		/** One-line description under the title. */
		blurb: string;
		/** Raw Flow source — carried to the playground via data-flow-source + #code. */
		source: string;
		/** Server-rendered shiki HTML for `source` (computed in +page.ts). */
		html: string;
	} = $props();

	// The REAL `#code=` deep link (Plan 49-06): fflate-deflate + base64url via encode(), matching
	// what the playground's decode() consumes. The `&run=1` marker carries the auto-run signal
	// (D-49-08 — clicking Play is the one user gesture, so the playground may auto-run on arrival).
	// `data-flow-source` / `data-run` stay for the docs/showcase carrier-contract symmetry.
	const href = $derived(`/playground#code=${encode(source)}&run=1`);
</script>

<Panel variant="framed" elevation="seated" screws={true}>
	<article class="code-card">
		<header class="code-card__head">
			<h3 class="code-card__title">{title}</h3>
			<p class="code-card__blurb">{blurb}</p>
		</header>

		<!-- Server-rendered, escaped shiki markup (Security V5 — first-party curated source only).
		     The shiki <pre> is itself the keyboard-accessible horizontal scroll region (highlightFlow
		     injects tabindex=0 + role=region) so the long-line code block satisfies axe
		     scrollable-region-focusable (D-49-10). -->
		<div class="code-card__code">
			<!-- eslint-disable-next-line svelte/no-at-html-tags -->
			{@html html}
		</div>

		<a
			class="code-card__cta skeuo-btn skeuo-btn--primary"
			href={href}
			data-flow-source={source}
			data-run="1"
		>
			Play in playground
		</a>
	</article>
</Panel>

<style>
	.code-card {
		display: flex;
		flex-direction: column;
		gap: var(--space-3);
		min-width: 0;
	}
	.code-card__head {
		display: flex;
		flex-direction: column;
		gap: var(--space-1);
	}
	.code-card__title {
		margin: 0;
		font-size: var(--text-h3);
		font-weight: 600;
		letter-spacing: var(--text-h3-ls);
		color: var(--color-ink);
	}
	.code-card__blurb {
		margin: 0;
		font-size: var(--text-small);
		color: var(--color-ink-muted);
	}

	.code-card__code {
		border-radius: var(--radius-2);
		font-size: var(--text-code);
		min-width: 0;
	}
	.code-card__code :global(pre.shiki) {
		margin: 0;
		padding: var(--space-3);
		border-radius: var(--radius-2);
		font-family: var(--font-mono);
		font-size: var(--text-code);
		line-height: var(--text-code-lh, 1.6);
		/* The shiki <pre> is the single focusable scroll region (tabindex=0 from highlightFlow). */
		overflow-x: auto;
		max-width: 100%;
	}
	.code-card__code :global(pre.shiki:focus-visible) {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}

	.code-card__cta {
		align-self: flex-start;
		text-decoration: none;
	}
</style>
