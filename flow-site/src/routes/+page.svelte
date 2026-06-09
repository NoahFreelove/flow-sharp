<!--
  Home `/` — the six D-49-21 marketing sections in the skeuo vocabulary (UI-SPEC §Home).

  Sections (top → bottom), one <main> landmark, --space-16 vertical rhythm:
    1. Hero        — .surface-paper inlay framed by .surface-wood; "Flow" Recoleta wordmark
                     (clamp 48→72px) + lead tagline + 3 "Play in playground" <CodeCard>s + a
                     Phase-34-stand-in symphony <AudioEmbed> (explicit play; nothing self-starts).
    2. Value-prop  — 3 <Panel framed> cards (Ergonomics-first / Genre-agnostic / Music-notation
                     roots), copy sourced from CLAUDE.md "## Goals & Non-Goals".
    3. How it sounds — <AudioEmbed>s behind explicit play <Button>s + felt <LedIndicator>s.
    4. Code-first  — the single ~20-line shiki snippet with margin annotations (-> / note streams
                     / musical context).
    5. CTAs        — "Install" copy-command <Button> + brass "Try in browser" → /playground.
    6. Footer      — .surface-wood band: license / repo / wiki / community links. <footer> landmark.

  Prerendered (prerender = true in +page.ts) — no WASM, no client highlight JS.
-->
<script lang="ts">
	import Panel from '$lib/components/skeuo/Panel.svelte';
	import Button from '$lib/components/skeuo/Button.svelte';
	import CodeCard from '$lib/home/CodeCard.svelte';
	import AudioEmbed from '$lib/home/AudioEmbed.svelte';
	import { CODE_FIRST_ANNOTATIONS } from '$lib/home/examples';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const REPO_URL = 'https://github.com/noahfreelove/flow-sharp';
	const INSTALL_CMD = 'git clone https://github.com/noahfreelove/flow-sharp';

	// Value-prop trio (D-49-21 §2) — copy distilled from CLAUDE.md "## Goals & Non-Goals".
	const VALUE_PROPS = [
		{
			icon: '✎',
			title: 'Ergonomics first',
			body: 'Composer ergonomics override runtime efficiency and type strictness. Easy cases stay fast; flexible cases stay flexible.'
		},
		{
			icon: '◷',
			title: 'Genre-agnostic',
			body: 'Classical, EDM, jazz, pop, metal — all in one language. Genre-agnostic by design, never tuned for one kind of music.'
		},
		{
			icon: '♪',
			title: 'Music-notation roots',
			body: 'Notes, chords, note streams, and musical-context blocks are first-class. You write musical ideas as code and hear them immediately.'
		}
	];

	// "How it sounds" embeds (D-49-21 §3) — first-party rendered Flow audio under static/audio/.
	// examples/symphony/ was removed from the worktree (STATE.md); we use the v1.x showcase render
	// (a real Flow audio showpiece) as the symphony-flavoured embed rather than fabricating audio.
	const SOUND_EMBEDS = [
		{
			src: '/audio/flow-showcase.wav',
			title: 'Flow showcase — multi-voice render'
		},
		{
			src: '/audio/microtonal-ji.wav',
			title: 'Just-intonation microtonal sketch'
		}
	];

	let copied = $state(false);
	async function copyInstall(): Promise<void> {
		try {
			await navigator.clipboard.writeText(INSTALL_CMD);
			copied = true;
			setTimeout(() => (copied = false), 2000);
		} catch {
			/* clipboard may be blocked — the command is visible to copy manually */
		}
	}
</script>

<svelte:head>
	<title>Flow — a language for music production</title>
	<meta
		name="description"
		content="Flow is an interpreted, statically-typed language for music production. Write musical ideas as code — note streams, chords, musical-context blocks — and hear them immediately in your browser."
	/>
</svelte:head>

<main class="home">
	<!-- 1. HERO -->
	<section class="home-hero surface-wood" aria-labelledby="hero-title">
		<div class="home-hero__inlay surface-paper">
			<h1 id="hero-title" class="home-hero__wordmark">Flow</h1>
			<p class="home-hero__tagline">
				An interpreted, statically-typed language for music production. Write the music as code —
				note streams, chords, and musical-context blocks — and hear it immediately.
			</p>

			<!-- Visually-hidden group heading so the per-card <h3>s nest under an <h2> instead of
			     jumping h1→h3 (Lighthouse heading-order a11y, D-49-10). No visual change. -->
			<h2 class="sr-only" id="hero-examples-title">Try Flow snippets</h2>
			<div class="home-hero__cards" aria-labelledby="hero-examples-title">
				{#each data.heroExamples as ex (ex.id)}
					<CodeCard title={ex.title} blurb={ex.blurb} source={ex.source} html={ex.html} />
				{/each}
			</div>

			<div class="home-hero__audio">
				<AudioEmbed src={SOUND_EMBEDS[0].src} title={SOUND_EMBEDS[0].title} />
			</div>
		</div>
	</section>

	<!-- 2. VALUE-PROP TRIO -->
	<section class="home-values" aria-labelledby="values-title">
		<h2 id="values-title" class="home-section__title">Why Flow</h2>
		<div class="home-values__grid">
			{#each VALUE_PROPS as vp (vp.title)}
				<Panel variant="framed" elevation="seated" screws={true}>
					<div class="home-value">
						<span class="home-value__icon" aria-hidden="true">{vp.icon}</span>
						<h3 class="home-value__title">{vp.title}</h3>
						<p class="home-value__body">{vp.body}</p>
					</div>
				</Panel>
			{/each}
		</div>
	</section>

	<!-- 3. HOW IT SOUNDS -->
	<section class="home-sounds" aria-labelledby="sounds-title">
		<h2 id="sounds-title" class="home-section__title">How it sounds</h2>
		<p class="home-section__lead">
			Every example below is rendered straight from Flow source. Nothing plays automatically —
			press play to listen.
		</p>
		<div class="home-sounds__list">
			{#each SOUND_EMBEDS as embed (embed.src)}
				<AudioEmbed src={embed.src} title={embed.title} />
			{/each}
		</div>
	</section>

	<!-- 4. CODE-FIRST EXPLANATION -->
	<section class="home-codefirst" aria-labelledby="codefirst-title">
		<h2 id="codefirst-title" class="home-section__title">Music as code</h2>
		<p class="home-section__lead">
			The flow operator, inline note streams, and scoped musical context — the three ideas that
			make Flow read like the music it plays.
		</p>

		<div class="home-codefirst__row">
			<Panel variant="framed" elevation="seated" screws={true}>
				<!-- The shiki <pre> is the focusable horizontal scroll region (tabindex=0 + role from
				     highlightFlow) so the long-line code block satisfies axe scrollable-region-focusable. -->
				<div class="home-codefirst__code">
					<!-- eslint-disable-next-line svelte/no-at-html-tags -->
					{@html data.codeFirst.html}
				</div>
			</Panel>

			<ul class="home-codefirst__annotations" aria-label="Code annotations">
				{#each CODE_FIRST_ANNOTATIONS as note (note.line)}
					<li class="home-annotation">
						<span class="home-annotation__label">{note.label}</span>
						<span class="home-annotation__text">{note.text}</span>
					</li>
				{/each}
			</ul>
		</div>
	</section>

	<!-- 5. CTAs -->
	<section class="home-cta" aria-labelledby="cta-title">
		<h2 id="cta-title" class="home-section__title">Get started</h2>
		<div class="home-cta__row">
			<div class="home-cta__install surface-brushed-metal">
				<!-- Focusable scroll region (the command scrolls horizontally on narrow screens) so it
				     satisfies axe scrollable-region-focusable (D-49-10). -->
				<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
				<code class="home-cta__cmd" tabindex="0" role="region" aria-label="Install command">
					{INSTALL_CMD}</code>
				<Button variant="secondary" label={copied ? 'Copied' : 'Copy'} onclick={copyInstall} />
			</div>
			<a class="home-cta__try skeuo-btn skeuo-btn--primary" href="/playground">
				Try in browser
			</a>
		</div>
	</section>

	<!-- 6. FOOTER -->
	<footer class="home-footer surface-wood">
		<nav class="home-footer__links" aria-label="Footer">
			<a href="{REPO_URL}/blob/main/LICENSE" target="_blank" rel="noopener noreferrer"
				>License <span aria-hidden="true">↗</span><span class="sr-only">(opens in new tab)</span></a
			>
			<a href={REPO_URL} target="_blank" rel="noopener noreferrer"
				>Repository <span aria-hidden="true">↗</span><span class="sr-only"
					>(opens in new tab)</span
				></a
			>
			<a href="/docs">Docs</a>
			<a href="{REPO_URL}/wiki" target="_blank" rel="noopener noreferrer"
				>Wiki <span aria-hidden="true">↗</span><span class="sr-only">(opens in new tab)</span></a
			>
			<a href="{REPO_URL}/discussions" target="_blank" rel="noopener noreferrer"
				>Community <span aria-hidden="true">↗</span><span class="sr-only"
					>(opens in new tab)</span
				></a
			>
		</nav>
		<p class="home-footer__legal">Flow — a language for music production.</p>
	</footer>
</main>

<style>
	.home {
		display: flex;
		flex-direction: column;
		gap: var(--space-16);
		max-width: 1100px;
		margin: 0 auto;
		padding: var(--space-16) var(--space-4) 0;
	}

	.home-section__title {
		margin: 0 0 var(--space-3);
		font-size: var(--text-h2);
		font-weight: 600;
		letter-spacing: var(--text-h2-ls);
		color: var(--color-ink);
	}
	.home-section__lead {
		margin: 0 0 var(--space-6);
		font-size: var(--text-lead);
		color: var(--color-ink-muted);
		max-width: 64ch;
	}

	/* 1. HERO — paper inlay framed by wood. */
	.home-hero {
		padding: var(--space-3);
		border-radius: var(--radius-4);
	}
	.home-hero__inlay {
		padding: var(--space-12) var(--space-8);
		border-radius: var(--radius-3);
		display: flex;
		flex-direction: column;
		gap: var(--space-8);
	}
	.home-hero__wordmark {
		margin: 0;
		font-family: var(--font-display);
		/* clamp 48 → 72px (UI-SPEC wordmark) */
		font-size: clamp(48px, 8vw, 72px);
		font-weight: 700;
		letter-spacing: var(--text-wordmark-ls);
		line-height: 1;
		color: var(--color-walnut);
	}
	[data-theme='dark'] .home-hero__wordmark {
		color: var(--color-ink);
	}
	.home-hero__tagline {
		margin: 0;
		font-size: var(--text-lead);
		line-height: var(--text-lead-lh);
		color: var(--color-ink);
		max-width: 60ch;
	}
	.home-hero__cards {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
		gap: var(--space-6);
	}
	.home-hero__audio {
		margin-top: var(--space-2);
	}

	/* 2. VALUE-PROP TRIO */
	.home-values__grid {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
		gap: var(--space-6);
	}
	.home-value {
		display: flex;
		flex-direction: column;
		gap: var(--space-2);
	}
	.home-value__icon {
		font-size: 28px;
		line-height: 1;
		color: var(--color-brass);
	}
	.home-value__title {
		margin: 0;
		font-size: var(--text-h3);
		font-weight: 600;
		color: var(--color-ink);
	}
	.home-value__body {
		margin: 0;
		font-size: var(--text-body);
		color: var(--color-ink-muted);
	}

	/* 3. HOW IT SOUNDS */
	.home-sounds__list {
		display: flex;
		flex-direction: column;
		gap: var(--space-4);
	}

	/* 4. CODE-FIRST */
	.home-codefirst__row {
		display: grid;
		grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr);
		gap: var(--space-8);
		align-items: start;
	}
	.home-codefirst__code {
		border-radius: var(--radius-2);
		min-width: 0;
	}
	.home-codefirst__code :global(pre.shiki) {
		margin: 0;
		padding: var(--space-4);
		border-radius: var(--radius-2);
		font-family: var(--font-mono);
		font-size: var(--text-code);
		line-height: var(--text-code-lh, 1.6);
		/* The shiki <pre> is the single focusable scroll region (tabindex=0 from highlightFlow). */
		overflow-x: auto;
		max-width: 100%;
	}
	.home-codefirst__code :global(pre.shiki:focus-visible) {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}
	.home-codefirst__annotations {
		list-style: none;
		margin: 0;
		padding: 0;
		display: flex;
		flex-direction: column;
		gap: var(--space-4);
	}
	.home-annotation {
		display: flex;
		flex-direction: column;
		gap: var(--space-1);
		padding-left: var(--space-3);
		border-left: 3px solid var(--color-brass);
	}
	.home-annotation__label {
		font-size: var(--text-small);
		font-weight: 600;
		color: var(--color-ink);
	}
	.home-annotation__text {
		font-size: var(--text-small);
		color: var(--color-ink-muted);
	}

	/* 5. CTAs */
	.home-cta__row {
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		gap: var(--space-4);
	}
	.home-cta__install {
		display: flex;
		align-items: center;
		gap: var(--space-3);
		padding: var(--space-2) var(--space-3);
		border-radius: var(--radius-3);
		/* Never wider than its container — the long install command scrolls inside .home-cta__cmd
		   instead of pushing the whole page wide at 320/375px (D-49-09). */
		max-width: 100%;
	}
	.home-cta__cmd {
		font-family: var(--font-mono);
		font-size: var(--text-code);
		color: var(--color-paper);
		white-space: nowrap;
		overflow-x: auto;
		/* Allow the flex item to shrink below the command's intrinsic width so overflow-x engages. */
		min-width: 0;
	}
	.home-cta__cmd:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}
	.home-cta__try {
		text-decoration: none;
	}

	/* 6. FOOTER */
	.home-footer {
		margin-top: var(--space-8);
		padding: var(--space-8) var(--space-6);
		border-radius: var(--radius-4) var(--radius-4) 0 0;
		display: flex;
		flex-direction: column;
		gap: var(--space-3);
	}
	.home-footer__links {
		display: flex;
		flex-wrap: wrap;
		gap: var(--space-6);
	}
	.home-footer__links a {
		color: var(--color-paper);
		font-size: var(--text-small);
		text-decoration: underline;
		text-underline-offset: 2px;
	}
	.home-footer__legal {
		margin: 0;
		font-size: var(--text-caption);
		color: var(--color-paper);
		opacity: 0.8;
	}

	@media (max-width: 767px) {
		.home {
			gap: var(--space-12);
			padding: var(--space-8) var(--space-3) 0;
		}
		.home-hero__inlay {
			padding: var(--space-8) var(--space-4);
		}
		.home-codefirst__row {
			grid-template-columns: 1fr;
			gap: var(--space-4);
		}
	}
</style>
