<!--
  Shared site chrome — the iOS-6 brushed-aluminum toolbar, lifted out of the home page so
  EVERY route shows the identical bar (same aqua ♪ logo + Helvetica "Flow" wordmark + pill
  nav) and the nav never jumps between routes.

  The bar is theme-independent (always the light brushed-aluminum "hardware bezel"); only the
  page CONTENT below responds to the dark toggle. `showToggle` is false on the light-only home
  route, so the toggle is never a dead switch there (design decision A).

  Tokens it needs (aqua / metal-edge / Helvetica UI font) are declared locally on .toolbar so
  the component is self-contained and renders correctly outside the home's .ios6-page wrapper.
-->
<script lang="ts">
	import Toggle from '$lib/components/skeuo/Toggle.svelte';

	let { current = '/', showToggle = true }: { current?: string; showToggle?: boolean } = $props();

	const REPO_URL = 'https://github.com/noahfreelove/flow-sharp';
	const NAV = [
		{ label: 'Home', href: '/' },
		{ label: 'Docs', href: '/docs' },
		{ label: 'Playground', href: '/playground' }
	];

	function isActive(href: string): boolean {
		if (href === '/') return current === '/';
		return current === href || current.startsWith(href + '/');
	}
</script>

<header class="toolbar">
	<a class="brand site-wordmark" href="/" aria-label="Flow — home">
		<span class="glyph" aria-hidden="true"></span><span class="word">Flow</span>
	</a>
	<nav class="nav" aria-label="Primary">
		{#each NAV as item (item.href)}
			<a
				href={item.href}
				class:active={isActive(item.href)}
				aria-current={isActive(item.href) ? 'page' : undefined}>{item.label}</a
			>
		{/each}
		<a class="ext" href={REPO_URL} target="_blank" rel="noopener noreferrer"
			>GitHub<span class="sr-only"> (opens in new tab)</span></a
		>
	</nav>
	{#if showToggle}
		<Toggle theme withIcons label="Dark mode" />
	{/if}
</header>

<style>
	/* self-contained tokens (mirror the home .ios6-page values) so the bar paints the same
	   brushed-aluminum + aqua anywhere, independent of the page theme. */
	.toolbar {
		--aqua-1: #8fc0f2;
		--aqua-2: #3f86d8;
		--aqua-3: #2e6cba;
		--aqua-4: #235aa0;
		--metal-edge: #8b897f;
		--font-ui: 'Helvetica Neue', Helvetica, Arial, sans-serif;

		position: sticky;
		top: 0;
		z-index: 50;
		height: 58px;
		display: flex;
		align-items: center;
		gap: 18px;
		padding: 0 18px;
		font-family: var(--font-ui);
		background: linear-gradient(#fdfdfd, #ededeb 48%, #dedcd8 52%, #cdcbc6);
		border-bottom: 1px solid var(--metal-edge);
		box-shadow:
			inset 0 1px 0 #ffffff,
			inset 0 -1px 0 rgba(0, 0, 0, 0.12),
			0 2px 6px rgba(0, 0, 0, 0.28);
	}
	.toolbar::before {
		content: '';
		position: absolute;
		inset: 0;
		pointer-events: none;
		background: repeating-linear-gradient(
			90deg,
			rgba(0, 0, 0, 0.022) 0 1px,
			rgba(255, 255, 255, 0.03) 1px 2px
		);
		opacity: 0.7;
	}
	.toolbar > * {
		position: relative;
		z-index: 1;
	}

	.brand {
		display: inline-flex;
		align-items: center;
		gap: 11px;
		text-decoration: none;
		margin-right: auto;
	}
	.brand .glyph {
		width: 34px;
		height: 34px;
		border-radius: 8px;
		background: linear-gradient(var(--aqua-1), var(--aqua-3));
		box-shadow:
			inset 0 1px 0 rgba(255, 255, 255, 0.85),
			inset 0 -2px 4px rgba(0, 0, 0, 0.3),
			0 1px 2px rgba(0, 0, 0, 0.4);
		position: relative;
		overflow: hidden;
		flex: 0 0 auto;
	}
	.brand .glyph::after {
		content: '♪';
		position: absolute;
		inset: 0;
		display: flex;
		align-items: center;
		justify-content: center;
		color: #fff;
		font-size: 20px;
		text-shadow: 0 -1px 0 rgba(0, 0, 0, 0.35);
	}
	.brand .glyph::before {
		content: '';
		position: absolute;
		left: 0;
		right: 0;
		top: 0;
		height: 48%;
		background: linear-gradient(rgba(255, 255, 255, 0.6), rgba(255, 255, 255, 0));
		border-radius: 8px 8px 60% 60% / 8px 8px 22px 22px;
	}
	.brand .word {
		font-size: 24px;
		font-weight: 800;
		letter-spacing: 0.2px;
		color: #34302a;
		text-shadow:
			0 1px 0 rgba(255, 255, 255, 0.85),
			0 -1px 0 rgba(0, 0, 0, 0.12);
	}

	/* pill nav (segmented control feel) */
	.nav {
		display: flex;
		gap: 2px;
		padding: 3px;
		border-radius: 9px;
		background: linear-gradient(#bcb9b2, #d6d3cc);
		box-shadow:
			inset 0 1px 3px rgba(0, 0, 0, 0.35),
			0 1px 0 rgba(255, 255, 255, 0.7);
	}
	.nav a {
		display: inline-block;
		padding: 7px 15px;
		font-size: 13.5px;
		font-weight: 600;
		text-decoration: none;
		color: #4a443c;
		border-radius: 7px;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.6);
		white-space: nowrap;
	}
	.nav a:hover {
		background: rgba(255, 255, 255, 0.35);
	}
	.nav a.active {
		color: #fff;
		text-shadow: 0 -1px 0 rgba(0, 0, 0, 0.35);
		background: linear-gradient(var(--aqua-2), var(--aqua-4));
		box-shadow:
			inset 0 1px 0 rgba(255, 255, 255, 0.5),
			inset 0 -2px 3px rgba(0, 0, 0, 0.25),
			0 1px 0 rgba(255, 255, 255, 0.5);
	}
	.nav a:focus-visible {
		outline: var(--focus-ring-width, 2px) solid var(--focus-ring-color, #235aa0);
		outline-offset: 2px;
	}
	.nav a.ext::after {
		content: ' ⌃';
		font-size: 10px;
		opacity: 0.6;
	}

	.sr-only {
		position: absolute;
		width: 1px;
		height: 1px;
		padding: 0;
		margin: -1px;
		overflow: hidden;
		clip: rect(0, 0, 0, 0);
		white-space: nowrap;
		border: 0;
	}

	/* Narrow viewports: the pill nav scrolls horizontally INSIDE the bar (min-width:0 lets the
	   flex item shrink below content), so the single top nav works at every width with no
	   document overflow — no hamburger, no bottom bar. */
	@media (max-width: 600px) {
		.toolbar {
			gap: 10px;
			padding: 0 12px;
		}
		.nav {
			min-width: 0;
			overflow-x: auto;
			overflow-y: hidden;
			scrollbar-width: none;
			-webkit-overflow-scrolling: touch;
		}
		.nav::-webkit-scrollbar {
			display: none;
		}
	}
</style>
