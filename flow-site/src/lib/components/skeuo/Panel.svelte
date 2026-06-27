<!--
  <Panel> — wood-framed container (UI-SPEC item 4), the "rack module" frame.
  Variants: framed (.surface-wood border + corner-screw motif), inset (recessed well),
  header (brushed-metal title strip).
  Elevation: elevated (8-16px floating shadow) vs seated (1-3px).
  Reduced-motion: cards/panels lose drop shadows → flattened to a 1px walnut border (D-49-10).
  A11y: landmark role + aria-label when used as a region (e.g. console = aside); decorative
  screws/grain are aria-hidden.
-->
<script lang="ts">
	import type { Snippet } from 'svelte';

	type Variant = 'framed' | 'inset' | 'header';
	type Elevation = 'seated' | 'elevated';

	let {
		variant = 'framed',
		elevation = 'seated',
		title = undefined,
		as = 'div',
		ariaLabel = undefined,
		role = undefined,
		screws = true,
		children = undefined,
		header = undefined
	}: {
		variant?: Variant;
		elevation?: Elevation;
		title?: string;
		as?: 'div' | 'aside' | 'section' | 'article';
		ariaLabel?: string;
		role?: string;
		screws?: boolean;
		children?: Snippet;
		header?: Snippet;
	} = $props();
</script>

<svelte:element
	this={as}
	class="skeuo-panel skeuo-panel--{variant} is-{elevation}"
	aria-label={ariaLabel}
	{role}
>
	{#if variant === 'framed' && screws}
		<span class="skeuo-panel__screw skeuo-panel__screw--tl" aria-hidden="true"></span>
		<span class="skeuo-panel__screw skeuo-panel__screw--tr" aria-hidden="true"></span>
		<span class="skeuo-panel__screw skeuo-panel__screw--bl" aria-hidden="true"></span>
		<span class="skeuo-panel__screw skeuo-panel__screw--br" aria-hidden="true"></span>
	{/if}

	{#if variant === 'header' || title || header}
		<div class="skeuo-panel__header surface-brushed-metal">
			{#if header}{@render header()}{:else}<span class="skeuo-panel__title">{title}</span>{/if}
		</div>
	{/if}

	<div class="skeuo-panel__body surface-paper">
		{#if children}{@render children()}{/if}
	</div>
</svelte:element>

<style>
	.skeuo-panel {
		position: relative;
		border-radius: var(--radius-4);
		overflow: hidden;
		/* As a grid/flex item the panel must be allowed to shrink below its content's intrinsic
		   width — otherwise a long code line inside (overflow-x:auto on the inner <pre>) blows the
		   track out and the whole page overflows horizontally at narrow widths (320/375px, D-49-09). */
		min-width: 0;
	}

	.skeuo-panel--framed {
		padding: var(--space-2);
		background-color: var(--color-walnut);
		background-image: linear-gradient(
			90deg,
			var(--color-walnut),
			var(--color-walnut-soft) 50%,
			var(--color-walnut)
		);
		border: 1px solid color-mix(in srgb, var(--color-walnut) 60%, black);
	}
	.skeuo-panel--inset .skeuo-panel__body {
		box-shadow: var(--shadow-inset);
	}

	.is-seated {
		box-shadow: var(--shadow-3);
	}
	.is-elevated {
		box-shadow: var(--shadow-16);
	}

	.skeuo-panel__header {
		display: flex;
		align-items: center;
		padding: var(--space-2) var(--space-3);
		border-radius: var(--radius-2) var(--radius-2) 0 0;
		min-height: 36px;
	}
	.skeuo-panel__title {
		font-family: var(--font-body);
		font-weight: 600;
		font-size: var(--text-small);
		color: var(--color-on-chrome);
	}

	.skeuo-panel__body {
		padding: var(--space-4);
		border-radius: var(--radius-3);
		color: var(--color-ink);
	}
	.skeuo-panel--framed .skeuo-panel__body {
		border-radius: var(--radius-3);
	}

	/* corner screws */
	.skeuo-panel__screw {
		position: absolute;
		width: 8px;
		height: 8px;
		border-radius: 50%;
		background-image: radial-gradient(circle at 35% 30%, #cfcfcf, #6a6a6a);
		box-shadow: inset 0 0 0 1px rgba(0, 0, 0, 0.4);
		z-index: 2;
	}
	.skeuo-panel__screw--tl {
		top: 6px;
		left: 6px;
	}
	.skeuo-panel__screw--tr {
		top: 6px;
		right: 6px;
	}
	.skeuo-panel__screw--bl {
		bottom: 6px;
		left: 6px;
	}
	.skeuo-panel__screw--br {
		bottom: 6px;
		right: 6px;
	}

	/* reduced-motion: lose drop shadows → 1px walnut border preserves depth/grouping (D-49-10) */
	@media (prefers-reduced-motion: reduce) {
		.is-seated,
		.is-elevated {
			box-shadow: none;
			border: 1px solid var(--color-walnut);
		}
		.skeuo-panel--inset .skeuo-panel__body {
			box-shadow: none;
			border: 1px solid var(--color-walnut);
		}
	}
</style>
