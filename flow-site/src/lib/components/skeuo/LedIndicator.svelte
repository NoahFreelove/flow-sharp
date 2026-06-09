<!--
  <LedIndicator> — amber pinpoint "playing" glow (UI-SPEC item 6). The skeuo status light;
  replaces generic spinners.
  States: idle (unlit dark lens) → rendering (amber pulse/breathe) → playing (steady amber
  glow) → error (red lens, --color-danger).
  Reduced-motion: pulse → steady on/off (no breathing).
  A11y: status by TEXT + ARIA, not colour alone — paired with a visually-hidden
  aria-live="polite" region; the LED lens itself is aria-hidden.
-->
<script lang="ts">
	type LedState = 'idle' | 'rendering' | 'playing' | 'error';

	let { state = 'idle', label = undefined }: { state?: LedState; label?: string } = $props();

	const STATUS: Record<LedState, string> = {
		idle: 'Stopped',
		rendering: 'Rendering…',
		playing: 'Playing',
		error: 'Error'
	};
	const status = $derived(label ? `${label}: ${STATUS[state]}` : STATUS[state]);
</script>

<span class="skeuo-led surface-felt is-{state}">
	<span class="skeuo-led__lens" aria-hidden="true"></span>
	<span class="sr-only" aria-live="polite">{status}</span>
</span>

<style>
	.skeuo-led {
		position: relative;
		display: inline-flex;
		align-items: center;
		justify-content: center;
		width: 22px;
		height: 22px;
		border-radius: 50%;
		padding: 4px;
	}

	.skeuo-led__lens {
		width: 12px;
		height: 12px;
		border-radius: 50%;
		background-color: #2a1410; /* unlit dark lens */
		box-shadow:
			inset 0 1px 1px rgba(0, 0, 0, 0.6),
			inset 0 -1px 1px rgba(255, 255, 255, 0.08);
	}

	.is-rendering .skeuo-led__lens {
		background-color: var(--color-brass);
		box-shadow: 0 0 6px 1px color-mix(in srgb, var(--color-brass) 70%, transparent);
		animation: led-breathe var(--motion-led) ease-in-out infinite;
	}
	.is-playing .skeuo-led__lens {
		background-color: var(--color-brass);
		box-shadow: 0 0 8px 2px color-mix(in srgb, var(--color-brass) 80%, transparent); /* steady amber glow */
	}
	.is-error .skeuo-led__lens {
		background-color: var(--color-danger);
		box-shadow: 0 0 8px 2px color-mix(in srgb, var(--color-danger) 80%, transparent);
	}

	@keyframes led-breathe {
		0%,
		100% {
			opacity: 0.45;
		}
		50% {
			opacity: 1;
		}
	}

	/* reduced-motion: pulse → steady on (no breathing) */
	@media (prefers-reduced-motion: reduce) {
		.is-rendering .skeuo-led__lens {
			animation: none;
			opacity: 1;
		}
	}
</style>
