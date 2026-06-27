<!--
  Persistent site chrome. Every route shows the SAME iOS-6 brushed-aluminum toolbar (aqua ♪ logo
  + Helvetica "Flow" wordmark + pill nav) so the nav never jumps between routes:
    - Home (`/`) renders its own inline copy of the bar inside its .ios6-page wrapper.
    - Every OTHER route gets the shared <SiteToolbar> here (pixel-identical to home's).
  The theme toggle lives only on these non-home routes — home is light-only, so a toggle there
  would be a dead switch (design decision A). aria-current is driven from $app/state's `page`.
-->
<script lang="ts">
	import '../app.css';
	import { page } from '$app/state';
	import favicon from '$lib/assets/favicon.svg';
	import SiteToolbar from '$lib/components/SiteToolbar.svelte';
	import { getInitialTheme } from '$lib/design/theme';
	import { initThemeStore } from '$lib/design/theme-store.svelte';

	let { children } = $props();

	// WR-03: hydrate the SHARED theme rune once, in the browser, from the resolved initial theme
	// (the same value app.html's inline FOUC script already wrote to [data-theme]). Every
	// <Toggle theme> derives its `checked` from this single source of truth.
	$effect(() => {
		initThemeStore(getInitialTheme());
	});

	// Live pathname → drives aria-current on the active tab + the home/non-home split.
	const current = $derived(page.url?.pathname ?? '/');
	const isHome = $derived(current === '/');
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
</svelte:head>

{#if !isHome}
	<SiteToolbar {current} showToggle={true} />
{/if}

{@render children()}
