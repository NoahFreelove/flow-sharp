<!--
  /design — skeuomorphic component showcase (Plan 49-02 Task 3).
  Storybook-style page rendering every one of the 8 components in all documented states,
  grouped by component, with a theme toggle + a "reduced-motion preview" wrapper.
  Used by tests/visual.spec.ts to baseline light / dark / reduced-motion for visual regression
  (REQ-SITE-DESIGN-01..04).
-->
<script lang="ts">
	import Button from '$lib/components/skeuo/Button.svelte';
	import Knob from '$lib/components/skeuo/Knob.svelte';
	import Toggle from '$lib/components/skeuo/Toggle.svelte';
	import Panel from '$lib/components/skeuo/Panel.svelte';
	import MetalRail from '$lib/components/skeuo/MetalRail.svelte';
	import LedIndicator from '$lib/components/skeuo/LedIndicator.svelte';
	import Slider from '$lib/components/skeuo/Slider.svelte';
	import Tabs from '$lib/components/skeuo/Tabs.svelte';
	import { getInitialTheme, setTheme, type Theme } from '$lib/design/theme';

	let theme = $state<Theme>('light');
	$effect(() => {
		theme = getInitialTheme();
	});

	function onThemeToggle(checked: boolean) {
		theme = checked ? 'dark' : 'light';
		setTheme(theme);
	}

	// Live values for the interactive controls.
	let volume = $state(50);
	let gain = $state(-6);
	let fader = $state(70);
	let switchOn = $state(true);

	const navItems = [
		{ label: 'Home', href: '/' },
		{ label: 'Docs', href: '/docs' },
		{ label: 'Playground', href: '/playground' },
		{ label: 'Showcase', href: '/showcase' },
		{ label: 'GitHub', href: 'https://github.com/noahfreelove/flow-sharp', external: true }
	];
</script>

<svelte:head>
	<title>Flow — Design System</title>
	<meta name="robots" content="noindex" />
</svelte:head>

<main class="design-page surface-paper">
	<header class="design-head">
		<h1 class="design-title">Flow Skeuomorphic Design System</h1>
		<p class="design-sub">
			Every base component in all documented states. Toggle the theme; the reduced-motion section
			flattens motion + shadows.
		</p>
		<div class="design-theme">
			<span class="design-theme__label">Theme</span>
			<Toggle theme withIcons label="Dark mode" onchange={onThemeToggle} />
			<code class="design-theme__value">{theme}</code>
		</div>
	</header>

	<!-- 8. Tabs (top-nav) -->
	<section class="design-section" data-component="Tabs">
		<h2>Tabs — top-nav</h2>
		<Tabs items={navItems} current="/playground" />
	</section>

	<!-- 1. Button -->
	<section class="design-section" data-component="Button">
		<h2>Button — variants &amp; states</h2>
		<div class="row">
			<Button variant="primary" label="Run" />
			<Button variant="secondary" label="Share" />
			<Button variant="ghost" label="Cancel" />
			<Button variant="icon" label="Copy">⧉</Button>
			<Button variant="danger" label="Stop" />
			<Button variant="primary" label="Disabled" disabled />
		</div>
	</section>

	<!-- 2. Knob + 7. Slider -->
	<section class="design-section" data-component="Knob">
		<h2>Knob — rotary (role=slider)</h2>
		<div class="row">
			<Knob label="Volume" min={0} max={100} bind:value={volume} unit="%" />
			<Knob label="Gain" min={-24} max={6} bind:value={gain} unit="dB" size="large" />
			<Knob label="Disabled" min={0} max={10} value={5} disabled />
		</div>
	</section>

	<section class="design-section" data-component="Slider">
		<h2>Slider — channel-strip fader</h2>
		<div class="row">
			<Slider label="Fader" min={0} max={100} bind:value={fader} unit="%" orientation="vertical" />
			<Slider label="Pan" min={-100} max={100} value={0} orientation="horizontal" />
			<Slider label="Disabled" min={0} max={10} value={3} disabled />
		</div>
	</section>

	<!-- 3. Toggle -->
	<section class="design-section" data-component="Toggle">
		<h2>Toggle — pill switch</h2>
		<div class="row">
			<Toggle bind:checked={switchOn} label="MIDI export" />
			<Toggle checked={false} label="Off state" />
			<Toggle withIcons checked label="With icons" />
			<Toggle checked={false} label="Disabled" disabled />
		</div>
	</section>

	<!-- 6. LedIndicator -->
	<section class="design-section" data-component="LedIndicator">
		<h2>LedIndicator — status light</h2>
		<div class="row">
			<span class="led-cell"><LedIndicator state="idle" /> idle</span>
			<span class="led-cell"><LedIndicator state="rendering" /> rendering</span>
			<span class="led-cell"><LedIndicator state="playing" /> playing</span>
			<span class="led-cell"><LedIndicator state="error" /> error</span>
		</div>
	</section>

	<!-- 4. Panel + 5. MetalRail -->
	<section class="design-section" data-component="Panel">
		<h2>Panel — wood-framed container</h2>
		<div class="row row--panels">
			<Panel variant="framed" elevation="seated" title="Seated panel">
				<p>Framed wood panel, 1–3px seated shadow, corner screws.</p>
			</Panel>
			<Panel variant="header" elevation="elevated" title="Header / elevated">
				<p>Brushed-metal title strip, 8–16px elevated shadow.</p>
			</Panel>
			<Panel variant="inset" screws={false}>
				<p>Recessed inset well.</p>
			</Panel>
		</div>
	</section>

	<section class="design-section" data-component="MetalRail">
		<h2>MetalRail — brushed-aluminium trim</h2>
		<div class="row rail-row">
			<MetalRail side="left" />
			<span>Decorative rack rails flank panels (aria-hidden).</span>
			<MetalRail side="right" />
		</div>
	</section>

	<!-- Reduced-motion preview wrapper -->
	<section class="design-section design-rm" data-component="reduced-motion">
		<h2>Reduced-motion preview</h2>
		<p class="design-sub">
			Under <code>prefers-reduced-motion: reduce</code>: knobs render as flat sliders, buttons lose
			travel, panels lose drop shadows for a 1px walnut border, LED pulse goes steady. Set the OS /
			emulated preference to see these flattened.
		</p>
		<div class="row">
			<Button variant="primary" label="Run" />
			<Knob label="Volume" min={0} max={100} value={40} unit="%" />
			<Panel variant="framed" title="Panel"><p>Depth via border under reduced-motion.</p></Panel>
			<LedIndicator state="rendering" />
		</div>
	</section>
</main>

<style>
	.design-page {
		min-height: 100vh;
		padding: var(--space-8);
		font-family: var(--font-body);
	}
	.design-head {
		margin-bottom: var(--space-12);
	}
	.design-title {
		font-family: var(--font-display);
		font-size: var(--text-h1);
		line-height: var(--text-h1-lh);
		letter-spacing: var(--text-h1-ls);
		color: var(--color-walnut);
		margin: 0 0 var(--space-2);
	}
	[data-theme='dark'] .design-title {
		color: var(--color-ink);
	}
	.design-sub {
		font-size: var(--text-lead);
		color: var(--color-ink-muted);
		max-width: 60ch;
	}
	.design-theme {
		display: flex;
		align-items: center;
		gap: var(--space-3);
		margin-top: var(--space-4);
	}
	.design-theme__label {
		font-size: var(--text-small);
		font-weight: 600;
		color: var(--color-ink);
	}
	.design-theme__value {
		font-family: var(--font-mono);
		font-size: var(--text-caption);
		color: var(--color-ink-muted);
	}

	.design-section {
		margin-bottom: var(--space-12);
	}
	.design-section h2 {
		font-size: var(--text-h3);
		line-height: var(--text-h3-lh);
		letter-spacing: var(--text-h3-ls);
		color: var(--color-ink);
		margin: 0 0 var(--space-4);
		padding-bottom: var(--space-2);
		border-bottom: 1px solid color-mix(in srgb, var(--color-walnut) 25%, transparent);
	}

	.row {
		display: flex;
		flex-wrap: wrap;
		align-items: flex-end;
		gap: var(--space-6);
	}
	.row--panels {
		align-items: stretch;
	}
	.row--panels :global(.skeuo-panel) {
		width: 260px;
	}

	.led-cell {
		display: inline-flex;
		align-items: center;
		gap: var(--space-2);
		font-size: var(--text-small);
		color: var(--color-ink);
	}

	.rail-row {
		align-items: center;
		min-height: 80px;
	}
	.rail-row :global(.skeuo-rail) {
		height: 72px;
	}

	.design-rm {
		padding: var(--space-6);
		border: 1px dashed color-mix(in srgb, var(--color-walnut) 40%, transparent);
		border-radius: var(--radius-4);
	}
</style>
