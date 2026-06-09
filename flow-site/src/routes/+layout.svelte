<!--
  Persistent site chrome (D-49-07, D-49-21 IA, UI-SPEC §Top nav + §Tabs).

  A `.surface-brushed-metal` `<nav aria-label="Primary">` bar (56px desktop / 48px mobile):
    - left:  the Recoleta "Flow" wordmark (home link, embossed text-shadow — NOT a gradient)
    - centre: the 5-tab <Tabs> nav (Home · Docs · Playground · Showcase · GitHub↗) — GitHub is
              the `external` variant (target=_blank + rel=noopener noreferrer + outbound glyph)
    - right: the <Toggle> theme switch (initialises from theme.ts, persists on change)

  Mobile (<768px): the tabs collapse into a metal hamburger <Button icon> → slide-down panel;
  the wordmark + theme toggle stay visible. `aria-current="page"` is wired from the live route
  via $app/state's `page` rune (passed into <Tabs current>). Each route owns its own <main>.
-->
<script lang="ts">
	import '../app.css';
	import { page } from '$app/state';
	import favicon from '$lib/assets/favicon.svg';
	import Tabs from '$lib/components/skeuo/Tabs.svelte';
	import Toggle from '$lib/components/skeuo/Toggle.svelte';
	import Button from '$lib/components/skeuo/Button.svelte';
	import { getInitialTheme } from '$lib/design/theme';
	import { initThemeStore } from '$lib/design/theme-store.svelte';

	let { children } = $props();

	// WR-03: hydrate the SHARED theme rune once, in the browser, from the resolved initial theme (the
	// same value app.html's inline FOUC script already wrote to [data-theme]). Every <Toggle theme>
	// derives its `checked` from this single source of truth, so the two theme switches (site chrome +
	// playground rail) stay in lockstep. Runs once on mount; not a reactive dependency.
	$effect(() => {
		initThemeStore(getInitialTheme());
	});

	// The 5-tab IA (D-49-07). Order matches the visual left-to-right reading order; the four local
	// routes resolve in-router, GitHub is the external 5th item.
	const NAV_ITEMS = [
		{ label: 'Home', href: '/' },
		{ label: 'Docs', href: '/docs' },
		{ label: 'Playground', href: '/playground' },
		{ label: 'Showcase', href: '/showcase' },
		{ label: 'GitHub', href: 'https://github.com/noahfreelove/flow-sharp', external: true }
	];

	// Live pathname → drives aria-current on the active tab.
	const current = $derived(page.url?.pathname ?? '/');

	// The iOS-6 home (`/`) ships its own toolbar + bottom tab bar (see +page.svelte), so the shared
	// Logic-Pro-wood site chrome + mobile nav are suppressed on that route only. Every other route
	// (/docs, /playground, /showcase) keeps the existing chrome exactly as before.
	const isHome = $derived(current === '/');

	// Mobile hamburger slide-down state. Collapses again on an ACTUAL navigation — guarded by the
	// previous pathname so the effect's mount run (and hydration's null→path transition) can't slam
	// the menu shut right after the user opens it.
	let mobileOpen = $state(false);
	let lastPath = $state<string | null>(null);
	$effect(() => {
		const path = current;
		if (lastPath !== null && path !== lastPath) {
			mobileOpen = false;
		}
		lastPath = path;
	});

	function isActive(href: string): boolean {
		if (href === '/') return current === '/';
		return current === href || current.startsWith(href + '/');
	}
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
</svelte:head>

{#if !isHome}
	<header class="site-chrome surface-brushed-metal">
		<a class="site-wordmark" href="/" aria-label="Flow — home">Flow</a>

	<!-- Desktop: the full 5-tab nav. (Hidden <768px via CSS; the hamburger takes over.) -->
	<div class="site-nav-desktop">
		<Tabs items={NAV_ITEMS} {current} />
	</div>

	<div class="site-chrome-right">
		<div class="site-theme">
			<Toggle theme={true} label="Toggle dark mode" />
		</div>

		<!-- Mobile: hamburger toggles a slide-down panel with the same tabs. -->
		<div class="site-hamburger">
			<Button
				variant="icon"
				label={mobileOpen ? 'Close menu' : 'Open menu'}
				aria-expanded={mobileOpen}
				aria-controls="mobile-nav"
				onclick={() => (mobileOpen = !mobileOpen)}
			>
				<span class="site-hamburger__glyph" aria-hidden="true">{mobileOpen ? '✕' : '☰'}</span>
			</Button>
		</div>
		</div>
	</header>
{/if}

<!-- Mobile slide-down nav — real <nav> landmark distinct from the desktop tabs; same routes. -->
{#if !isHome}
	{#if mobileOpen}
		<nav id="mobile-nav" class="site-mobile-nav surface-brushed-metal" aria-label="Primary mobile">
			<ul>
				{#each NAV_ITEMS as item (item.href)}
					<li>
						{#if item.external}
							<a href={item.href} target="_blank" rel="noopener noreferrer">
								{item.label}
								<span aria-hidden="true">↗</span>
								<span class="sr-only">(opens in new tab)</span>
							</a>
						{:else}
							<a
								href={item.href}
								class:is-active={isActive(item.href)}
								aria-current={isActive(item.href) ? 'page' : undefined}
							>
								{item.label}
							</a>
						{/if}
					</li>
				{/each}
			</ul>
		</nav>
	{/if}
{/if}

{@render children()}

<style>
	.site-chrome {
		position: sticky;
		top: 0;
		z-index: 50;
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: var(--space-4);
		height: 56px;
		padding: 0 var(--space-4);
	}

	.site-wordmark {
		font-family: var(--font-display);
		font-size: var(--text-wordmark);
		font-weight: 700;
		letter-spacing: var(--text-wordmark-ls);
		line-height: 1;
		color: var(--color-paper);
		text-decoration: none;
		/* embossed on the brushed metal — a 1px inner-bevel highlight, NOT a gradient fill */
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.35);
	}
	/* keep the bar height fixed — scale the wordmark down so 48px+ display type fits the 56px bar */
	.site-wordmark {
		font-size: 30px;
	}

	.site-nav-desktop {
		flex: 1;
		display: flex;
		justify-content: center;
		min-width: 0;
	}
	/* The <Tabs> component ships its own .surface-brushed-metal; inside the bar it should be flush. */
	.site-nav-desktop :global(.skeuo-tabs) {
		background: none;
		box-shadow: none;
		height: 56px;
	}

	.site-chrome-right {
		display: flex;
		align-items: center;
		gap: var(--space-2);
	}

	/* The hamburger is desktop-hidden; the desktop tabs are mobile-hidden (swap at 768px). */
	.site-hamburger {
		display: none;
	}

	.site-mobile-nav {
		position: sticky;
		top: 56px;
		z-index: 49;
		padding: var(--space-2) var(--space-4);
	}
	.site-mobile-nav ul {
		list-style: none;
		margin: 0;
		padding: 0;
		display: flex;
		flex-direction: column;
		gap: var(--space-1);
	}
	.site-mobile-nav a {
		display: flex;
		align-items: center;
		gap: var(--space-1);
		min-height: 44px;
		padding: 0 var(--space-3);
		color: var(--color-paper);
		text-decoration: none;
		border-radius: var(--radius-2);
		font-size: var(--text-small);
	}
	.site-mobile-nav a.is-active {
		font-weight: 600;
		box-shadow: inset 0 -2px 0 var(--color-brass);
	}
	.site-mobile-nav a:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}

	@media (max-width: 767px) {
		.site-chrome {
			height: 48px;
		}
		.site-nav-desktop {
			display: none;
		}
		.site-hamburger {
			display: block;
		}
		.site-mobile-nav {
			top: 48px;
		}
	}
</style>
