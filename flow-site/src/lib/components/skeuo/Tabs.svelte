<!--
  <Tabs> — embossed segmented rack selector / top-nav (UI-SPEC item 8).
  The 5-tab nav (Home / Docs / Playground / Showcase / GitHub) on .surface-brushed-metal.
  Variants per item: local (router <a>), external (GitHub — target=_blank rel=noopener
  noreferrer + Lucide outbound glyph + visually-hidden "(opens in new tab)").
  States: default embossed → hover bevel-brighten → active/current brass inset underline
  + weight 600 → focus brass 2px ring.
  Reduced-motion: active indicator snaps (no slide).
  A11y: <nav aria-label="Primary">; real <a> links; aria-current="page" on active route;
  tab order matches visual left-to-right order.
-->
<script lang="ts">
	type TabItem = {
		label: string;
		href: string;
		external?: boolean;
	};

	let {
		items,
		current = '/'
	}: {
		items: TabItem[];
		current?: string;
	} = $props();

	function isActive(item: TabItem): boolean {
		if (item.external) return false;
		if (item.href === '/') return current === '/';
		return current === item.href || current.startsWith(item.href + '/');
	}
</script>

<nav class="skeuo-tabs surface-brushed-metal" aria-label="Primary">
	<ul class="skeuo-tabs__list">
		{#each items as item (item.href)}
			<li>
				{#if item.external}
					<a
						class="skeuo-tab skeuo-tab--external"
						href={item.href}
						target="_blank"
						rel="noopener noreferrer"
					>
						{item.label}
						<span class="skeuo-tab__ext" aria-hidden="true">↗</span>
						<span class="sr-only">(opens in new tab)</span>
					</a>
				{:else}
					<a
						class="skeuo-tab"
						class:is-active={isActive(item)}
						href={item.href}
						aria-current={isActive(item) ? 'page' : undefined}
					>
						{item.label}
					</a>
				{/if}
			</li>
		{/each}
	</ul>
</nav>

<style>
	.skeuo-tabs {
		display: flex;
		align-items: center;
		height: 56px;
		padding: 0 var(--space-3);
		border-radius: var(--radius-2);
	}

	.skeuo-tabs__list {
		display: flex;
		align-items: stretch;
		gap: var(--space-1);
		margin: 0;
		padding: 0;
		list-style: none;
	}

	.skeuo-tab {
		position: relative;
		display: inline-flex;
		align-items: center;
		gap: var(--space-1);
		min-height: 44px;
		padding: 0 var(--space-4);
		font-family: var(--font-body);
		font-size: var(--text-small);
		font-weight: 400;
		color: var(--color-paper);
		text-decoration: none;
		border-radius: var(--radius-2);
		transition: filter var(--motion-hover) ease-out;
	}
	.skeuo-tab:hover {
		filter: brightness(1.12); /* bevel brighten */
	}

	/* active/current: brass inset underline indicator + weight 600 (reserved accent) */
	.skeuo-tab.is-active {
		font-weight: 600;
	}
	.skeuo-tab.is-active::after {
		content: '';
		position: absolute;
		left: var(--space-4);
		right: var(--space-4);
		bottom: 6px;
		height: 2px;
		background-color: var(--color-brass);
		border-radius: 2px;
		box-shadow: inset 0 1px 1px rgba(0, 0, 0, 0.4);
	}

	.skeuo-tab__ext {
		font-size: 11px;
		opacity: 0.8;
	}

	.skeuo-tab:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}

	@media (prefers-reduced-motion: reduce) {
		.skeuo-tab {
			transition: none;
		}
	}
</style>
