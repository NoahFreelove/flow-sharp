<!--
  <Knob> — rotary control, a11y-critical (UI-SPEC item 2, RESEARCH WAI-ARIA slider).
  role=slider + tabindex=0 + aria-valuemin/max/now + aria-valuetext "{value} {unit}".
  Keyboard: ArrowUp/Right +step, ArrowDown/Left -step, Home→min, End→max, PageUp/Down ±large.
  Visual: circular metal cap, brass pointer tick at the current angle, swept brass value-arc,
  caption row (parameter label + visible value read-out) below.
  Reduced-motion: renders a flat <Slider> (role=slider, same value + keyboard model, no rotation).
  Variants: default, large.
-->
<script lang="ts">
	import Slider from './Slider.svelte';

	let {
		label,
		min = 0,
		max = 100,
		value = $bindable(50),
		step = 1,
		largeStep = 10,
		unit = '',
		size = 'default',
		disabled = false,
		onchange = undefined
	}: {
		label: string;
		min?: number;
		max?: number;
		value?: number;
		step?: number;
		largeStep?: number;
		unit?: string;
		size?: 'default' | 'large';
		disabled?: boolean;
		onchange?: ((v: number) => void) | undefined;
	} = $props();

	// Detect reduced-motion at mount (SSR-safe) so the Knob can fall back to a flat Slider.
	let reducedMotion = $state(false);
	$effect(() => {
		if (typeof window === 'undefined' || !window.matchMedia) return;
		const mq = window.matchMedia('(prefers-reduced-motion: reduce)');
		reducedMotion = mq.matches;
		const onChange = (e: MediaQueryListEvent) => (reducedMotion = e.matches);
		mq.addEventListener?.('change', onChange);
		return () => mq.removeEventListener?.('change', onChange);
	});

	const clamp = (v: number) => Math.min(max, Math.max(min, v));
	// 270° sweep, centred (−135°…+135°) — vintage-synth knob travel.
	const angle = $derived(((value - min) / (max - min)) * 270 - 135);
	const valueText = $derived(unit ? `${value} ${unit}` : `${value}`);

	function set(next: number) {
		const c = clamp(next);
		if (c !== value) {
			value = c;
			onchange?.(c);
		}
	}

	function onkeydown(e: KeyboardEvent) {
		if (disabled) return;
		let handled = true;
		switch (e.key) {
			case 'ArrowUp':
			case 'ArrowRight':
				set(value + step);
				break;
			case 'ArrowDown':
			case 'ArrowLeft':
				set(value - step);
				break;
			case 'Home':
				set(min);
				break;
			case 'End':
				set(max);
				break;
			case 'PageUp':
				set(value + largeStep);
				break;
			case 'PageDown':
				set(value - largeStep);
				break;
			default:
				handled = false;
		}
		if (handled) e.preventDefault();
	}
</script>

{#if reducedMotion}
	<!-- reduced-motion fallback: flat slider, same value + keyboard model, no rotation -->
	<div class="skeuo-knob__wrap">
		<Slider
			{label}
			{min}
			{max}
			bind:value
			{step}
			{largeStep}
			{unit}
			{disabled}
			orientation="horizontal"
			{onchange}
		/>
		<span class="skeuo-knob__caption">{label}: <strong>{valueText}</strong></span>
	</div>
{:else}
	<div class="skeuo-knob__wrap">
		<div
			class="skeuo-knob skeuo-knob--{size}"
			role="slider"
			tabindex={disabled ? -1 : 0}
			aria-label={label}
			aria-valuemin={min}
			aria-valuemax={max}
			aria-valuenow={value}
			aria-valuetext={valueText}
			aria-disabled={disabled ? 'true' : undefined}
			{onkeydown}
			style="--angle: {angle}deg; --pct: {((value - min) / (max - min)) * 100}"
		>
			<svg class="skeuo-knob__arc" viewBox="0 0 100 100" aria-hidden="true">
				<!-- track arc -->
				<circle
					class="skeuo-knob__arc-track"
					cx="50"
					cy="50"
					r="44"
					pathLength="100"
					stroke-dasharray="75 100"
					transform="rotate(135 50 50)"
				/>
				<!-- swept brass value-arc -->
				<circle
					class="skeuo-knob__arc-value"
					cx="50"
					cy="50"
					r="44"
					pathLength="100"
					stroke-dasharray="{((value - min) / (max - min)) * 75} 100"
					transform="rotate(135 50 50)"
				/>
			</svg>
			<span class="skeuo-knob__cap" aria-hidden="true">
				<span class="skeuo-knob__pointer"></span>
			</span>
		</div>
		<span class="skeuo-knob__caption">{label}: <strong>{valueText}</strong></span>
	</div>
{/if}

<style>
	.skeuo-knob__wrap {
		display: inline-flex;
		flex-direction: column;
		align-items: center;
		gap: var(--space-2);
	}

	.skeuo-knob {
		position: relative;
		width: 72px;
		height: 72px;
		outline: none;
		cursor: pointer;
	}
	.skeuo-knob--large {
		width: 104px;
		height: 104px;
	}

	.skeuo-knob__arc {
		position: absolute;
		inset: 0;
		width: 100%;
		height: 100%;
	}
	.skeuo-knob__arc-track {
		fill: none;
		stroke: color-mix(in srgb, var(--color-slate) 70%, black);
		stroke-width: 5;
		stroke-linecap: round;
	}
	.skeuo-knob__arc-value {
		fill: none;
		stroke: var(--color-brass); /* swept brass value-arc — reserved accent */
		stroke-width: 5;
		stroke-linecap: round;
		transition: stroke-dasharray var(--motion-slide) var(--ease-knob);
	}

	.skeuo-knob__cap {
		position: absolute;
		inset: 14%;
		border-radius: 50%;
		background-image: radial-gradient(
			circle at 35% 28%,
			color-mix(in srgb, var(--color-slate) 64%, white),
			var(--color-slate) 70%
		);
		box-shadow: var(--bevel-light), var(--shadow-3);
		transform: rotate(var(--angle));
		transition: transform var(--motion-slide) var(--ease-knob);
	}

	.skeuo-knob__pointer {
		position: absolute;
		top: 8%;
		left: 50%;
		width: 3px;
		height: 32%;
		transform: translateX(-50%);
		background-color: var(--color-brass); /* brass pointer tick — reserved accent */
		border-radius: 2px;
	}

	.skeuo-knob:active:not([aria-disabled='true']) .skeuo-knob__cap {
		box-shadow: var(--shadow-inset); /* inset press while dragging */
	}

	.skeuo-knob:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
		border-radius: 50%;
	}

	.skeuo-knob[aria-disabled='true'] {
		opacity: 0.5;
		cursor: not-allowed;
	}
	.skeuo-knob[aria-disabled='true'] .skeuo-knob__pointer,
	.skeuo-knob[aria-disabled='true'] .skeuo-knob__arc-value {
		stroke: var(--color-ink-muted);
		background-color: var(--color-ink-muted);
	}

	.skeuo-knob__caption {
		font-family: var(--font-body);
		font-size: var(--text-caption);
		color: var(--color-ink-muted);
		letter-spacing: var(--text-caption-ls);
	}
	.skeuo-knob__caption strong {
		color: var(--color-ink);
		font-family: var(--font-mono);
	}

	@media (prefers-reduced-motion: reduce) {
		.skeuo-knob__cap,
		.skeuo-knob__arc-value {
			transition: none;
		}
	}
</style>
