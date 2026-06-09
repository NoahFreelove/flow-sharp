<!--
  <Toggle> — pill switch with sliding knob (UI-SPEC item 3).
  Variants: default, with-icons (sun/moon — the theme switch). `theme` prop makes it the
  theme toggle: writes localStorage + sets [data-theme] via theme.ts (D-49-20).
  States: off (knob seated left, recessed inset track) → on (knob slides right with
  overshoot ease, track tints brass-adjacent) → focus brass 2px ring → disabled flat.
  Reduced-motion: knob snaps between positions instantly.
  A11y: role=switch, aria-checked, aria-label; operable with Space/Enter.
-->
<script lang="ts">
	import { setTheme } from '../../design/theme';
	import { currentTheme } from '../../design/theme-store.svelte';

	let {
		checked = $bindable(false),
		label,
		theme = false,
		withIcons = false,
		disabled = false,
		onchange = undefined
	}: {
		checked?: boolean;
		label: string;
		theme?: boolean;
		withIcons?: boolean;
		disabled?: boolean;
		onchange?: ((checked: boolean) => void) | undefined;
	} = $props();

	// WR-03: in theme mode, `checked` reflects the SHARED theme rune (single source of truth) so every
	// theme toggle on the page agrees and flipping one updates all. In non-theme mode it is the plain
	// $bindable prop. This replaces the previous $effect that initialised `checked` from a
	// side-effecting getInitialTheme() read (which never notified sibling toggles and risked clobbering
	// an in-flight toggle if any reactive read were added to the effect).
	const isOn = $derived(theme ? currentTheme() === 'dark' : checked);

	const showIcons = $derived(withIcons || theme);

	function flip() {
		if (disabled) return;
		if (theme) {
			// Drive the shared rune via setTheme; `isOn` re-derives from it for THIS and every other toggle.
			setTheme(currentTheme() === 'dark' ? 'light' : 'dark');
			onchange?.(currentTheme() === 'dark');
		} else {
			checked = !checked;
			onchange?.(checked);
		}
	}

	function onkeydown(e: KeyboardEvent) {
		if (e.key === ' ' || e.key === 'Enter') {
			e.preventDefault();
			flip();
		}
	}
</script>

<button
	type="button"
	class="skeuo-toggle"
	class:is-on={isOn}
	class:with-icons={showIcons}
	role="switch"
	aria-checked={isOn}
	aria-label={label}
	aria-disabled={disabled ? 'true' : undefined}
	{disabled}
	onclick={flip}
	{onkeydown}
>
	<span class="skeuo-toggle__track" aria-hidden="true">
		{#if showIcons}
			<span class="skeuo-toggle__icon skeuo-toggle__icon--sun">☀</span>
			<span class="skeuo-toggle__icon skeuo-toggle__icon--moon">☾</span>
		{/if}
		<span class="skeuo-toggle__knob"></span>
	</span>
</button>

<style>
	.skeuo-toggle {
		position: relative;
		display: inline-flex;
		align-items: center;
		min-height: 44px;
		padding: var(--space-2);
		background: none;
		border: none;
		cursor: pointer;
	}

	.skeuo-toggle__track {
		position: relative;
		display: inline-flex;
		align-items: center;
		justify-content: space-between;
		width: 56px;
		height: 28px;
		padding: 0 6px;
		border-radius: 999px;
		background-color: color-mix(in srgb, var(--color-slate) 80%, black);
		box-shadow: var(--shadow-inset);
		transition: background-color var(--motion-slide) var(--ease-overshoot);
	}
	.is-on .skeuo-toggle__track {
		/* subtle brass-adjacent tint — not full brass (accent budget) */
		background-color: color-mix(in srgb, var(--color-brass) 32%, var(--color-slate));
	}

	.skeuo-toggle__knob {
		position: absolute;
		left: 3px;
		width: 22px;
		height: 22px;
		border-radius: 50%;
		background-image: radial-gradient(
			circle at 35% 30%,
			color-mix(in srgb, var(--color-slate) 60%, white),
			var(--color-slate)
		);
		box-shadow: var(--bevel-light), var(--shadow-1);
		transition: transform var(--motion-slide) var(--ease-overshoot);
	}
	.is-on .skeuo-toggle__knob {
		transform: translateX(28px);
	}
	.skeuo-toggle:active:not(:disabled) .skeuo-toggle__knob {
		box-shadow: var(--shadow-inset);
	}

	.skeuo-toggle__icon {
		font-size: 12px;
		line-height: 1;
		color: var(--color-paper);
		opacity: 0.55;
		z-index: 0;
	}
	.skeuo-toggle__icon--moon {
		color: var(--color-brass);
	}

	.skeuo-toggle:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
		border-radius: 999px;
	}

	.skeuo-toggle:disabled {
		opacity: 0.5;
		cursor: not-allowed;
	}
	.skeuo-toggle:disabled .skeuo-toggle__track {
		background-color: var(--color-ink-muted);
	}

	@media (prefers-reduced-motion: reduce) {
		.skeuo-toggle__knob,
		.skeuo-toggle__track {
			transition: none;
		}
	}
</style>
