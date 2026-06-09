<!--
  <Slider> — channel-strip fader (UI-SPEC item 7).
  Variants: vertical (channel-strip), horizontal (inline / the <Knob> reduced-motion fallback).
  role=slider with the SAME aria-valuemin/max/now/valuetext + arrow-key contract as <Knob>.
  States: recessed inset track, metal fader cap, brass fill below the cap; brass 2px focus ring.
  Reduced-motion: no fill tween — value jumps instantly.
-->
<script lang="ts">
	type Orientation = 'vertical' | 'horizontal';

	let {
		label,
		min = 0,
		max = 100,
		value = $bindable(0),
		step = 1,
		largeStep = 10,
		unit = '',
		orientation = 'horizontal',
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
		orientation?: Orientation;
		disabled?: boolean;
		onchange?: ((v: number) => void) | undefined;
	} = $props();

	const clamp = (v: number) => Math.min(max, Math.max(min, v));
	const pct = $derived(((value - min) / (max - min)) * 100);
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

<div
	class="skeuo-slider skeuo-slider--{orientation}"
	role="slider"
	tabindex={disabled ? -1 : 0}
	aria-label={label}
	aria-valuemin={min}
	aria-valuemax={max}
	aria-valuenow={value}
	aria-valuetext={valueText}
	aria-orientation={orientation}
	aria-disabled={disabled ? 'true' : undefined}
	{onkeydown}
	style="--fill: {pct}%"
>
	<span class="skeuo-slider__track" aria-hidden="true">
		<span class="skeuo-slider__fill"></span>
		<span class="skeuo-slider__cap"></span>
	</span>
</div>

<style>
	.skeuo-slider {
		position: relative;
		display: inline-flex;
		cursor: pointer;
		outline: none;
	}
	.skeuo-slider--horizontal {
		width: 160px;
		height: 44px;
		align-items: center;
	}
	.skeuo-slider--vertical {
		width: 44px;
		height: 160px;
		justify-content: center;
	}

	.skeuo-slider__track {
		position: relative;
		display: block;
		background-color: color-mix(in srgb, var(--color-slate) 80%, black);
		box-shadow: var(--shadow-inset);
		border-radius: var(--radius-knob);
	}
	.skeuo-slider--horizontal .skeuo-slider__track {
		width: 100%;
		height: 8px;
	}
	.skeuo-slider--vertical .skeuo-slider__track {
		width: 8px;
		height: 100%;
	}

	.skeuo-slider__fill {
		position: absolute;
		background-color: var(--color-brass);
		border-radius: var(--radius-knob);
		transition: all var(--motion-slide) var(--ease-overshoot);
	}
	.skeuo-slider--horizontal .skeuo-slider__fill {
		left: 0;
		top: 0;
		bottom: 0;
		width: var(--fill);
	}
	.skeuo-slider--vertical .skeuo-slider__fill {
		left: 0;
		right: 0;
		bottom: 0;
		height: var(--fill);
	}

	.skeuo-slider__cap {
		position: absolute;
		width: 18px;
		height: 18px;
		border-radius: 50%;
		background-image: radial-gradient(
			circle at 35% 30%,
			color-mix(in srgb, var(--color-slate) 70%, white),
			var(--color-slate)
		);
		box-shadow: var(--bevel-light), var(--shadow-3);
		transition: all var(--motion-slide) var(--ease-overshoot);
	}
	.skeuo-slider--horizontal .skeuo-slider__cap {
		top: 50%;
		left: var(--fill);
		transform: translate(-50%, -50%);
	}
	.skeuo-slider--vertical .skeuo-slider__cap {
		left: 50%;
		bottom: var(--fill);
		transform: translate(-50%, 50%);
	}

	.skeuo-slider:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
		border-radius: var(--radius-knob);
	}
	.skeuo-slider[aria-disabled='true'] {
		opacity: 0.5;
		cursor: not-allowed;
	}
	.skeuo-slider[aria-disabled='true'] .skeuo-slider__fill {
		background-color: var(--color-ink-muted);
	}

	@media (prefers-reduced-motion: reduce) {
		.skeuo-slider__fill,
		.skeuo-slider__cap {
			transition: none;
		}
	}
</style>
