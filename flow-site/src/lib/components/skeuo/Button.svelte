<!--
  <Button> — embossed satin-metal action (UI-SPEC item 1).
  Variants: primary (brass face + walnut label — the reserved CTA), secondary (brushed-metal),
  ghost, icon (square 44px, requires aria-label), danger (--color-danger rail).
  States: default embossed (light bevel top + drop shadow bottom) → hover bevel +6% →
  active INSET (50ms depress) → focus brass 2px ring → disabled flat + aria-disabled.
  Reduced-motion: no 50ms travel — instant bevel-swap (shadows kept; only motion removed).
  A11y: real <button>; icon-only carries aria-label; 44px min hit; label ≥14px.
-->
<script lang="ts">
	import type { Snippet } from 'svelte';

	type Variant = 'primary' | 'secondary' | 'ghost' | 'icon' | 'danger';

	let {
		variant = 'secondary',
		label = undefined,
		disabled = false,
		type = 'button',
		onclick = undefined,
		children = undefined,
		...rest
	}: {
		variant?: Variant;
		label?: string;
		disabled?: boolean;
		type?: 'button' | 'submit' | 'reset';
		onclick?: ((e: MouseEvent) => void) | undefined;
		children?: Snippet;
		[key: string]: unknown;
	} = $props();
</script>

<button
	{type}
	class="skeuo-btn skeuo-btn--{variant}"
	class:is-icon={variant === 'icon'}
	{disabled}
	aria-disabled={disabled ? 'true' : undefined}
	aria-label={label}
	{onclick}
	{...rest}
>
	{#if children}{@render children()}{:else}{label}{/if}
</button>

<style>
	.skeuo-btn {
		position: relative;
		display: inline-flex;
		align-items: center;
		justify-content: center;
		gap: var(--space-2);
		min-height: 44px; /* 44px min hit area (D-49-09) */
		min-width: 44px;
		padding: var(--space-3) var(--space-4);
		font-family: var(--font-body);
		font-size: var(--text-small);
		font-weight: 600;
		line-height: 1;
		border: 1px solid color-mix(in srgb, var(--color-walnut) 40%, transparent);
		border-radius: var(--radius-3);
		cursor: pointer;
		color: var(--color-ink);
		/* embossed: light bevel top edge + 1-3px drop shadow bottom edge */
		box-shadow: var(--bevel-light), var(--shadow-3);
		transition:
			box-shadow var(--motion-hover) ease-out,
			transform var(--motion-press) var(--ease-overshoot),
			filter var(--motion-hover) ease-out;
	}

	.skeuo-btn:hover:not(:disabled) {
		filter: brightness(1.06); /* bevel brightens ~6%; shadow unchanged */
	}

	/* active/pressed: INSET — invert bevel, label travels 1px down, depress over 50ms */
	.skeuo-btn:active:not(:disabled) {
		box-shadow: var(--shadow-inset);
		transform: translateY(1px);
	}

	.skeuo-btn:disabled {
		filter: saturate(0.6) brightness(0.96);
		box-shadow: none; /* flat */
		cursor: not-allowed;
		opacity: 0.6;
	}

	/* Variant faces */
	.skeuo-btn--primary {
		background-color: var(--color-brass);
		background-image: linear-gradient(
			180deg,
			color-mix(in srgb, var(--color-brass) 88%, white) 0%,
			var(--color-brass) 100%
		);
		color: var(--color-walnut); /* walnut-on-brass ≥4.5:1, never white-on-brass */
		border-color: color-mix(in srgb, var(--color-brass) 60%, black);
	}

	.skeuo-btn--secondary {
		background-color: var(--color-slate);
		background-image: linear-gradient(
			180deg,
			color-mix(in srgb, var(--color-slate) 84%, white) 0%,
			var(--color-slate) 100%
		);
		color: var(--color-paper);
	}

	.skeuo-btn--ghost {
		background: transparent;
		border-color: transparent;
		box-shadow: none;
		color: var(--color-ink);
	}
	.skeuo-btn--ghost:active:not(:disabled) {
		box-shadow: none;
	}

	.skeuo-btn--icon {
		width: 44px;
		height: 44px;
		padding: 0;
		background-color: var(--color-slate);
		color: var(--color-paper);
	}

	.skeuo-btn--danger {
		background-color: var(--color-danger);
		background-image: linear-gradient(
			180deg,
			color-mix(in srgb, var(--color-danger) 86%, white) 0%,
			var(--color-danger) 100%
		);
		color: #fff;
		border-color: color-mix(in srgb, var(--color-danger) 60%, black);
	}

	.skeuo-btn:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}

	/* reduced-motion: lose the 50ms travel — instant bevel-swap, shadows kept */
	@media (prefers-reduced-motion: reduce) {
		.skeuo-btn {
			transition: none;
		}
		.skeuo-btn:active:not(:disabled) {
			transform: none;
		}
	}
</style>
