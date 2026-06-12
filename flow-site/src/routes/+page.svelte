<!--
  Home `/` — the iOS-6 skeuomorphic marketing page (REDESIGN-HANDOFF §4).

  Linen ground · brushed-aluminum toolbar · Aqua jelly buttons · inset code wells · leather
  audio rack · bottom tab bar. Ported faithfully from `.design-handoff/project/index.html`
  (markup + landing-inline <style>) + `.design-handoff/project/flow.css` (the skeuo system).

  Prerendered (prerender = true in +page.ts): the 3 hero snippets are Flow-highlighted at
  script init via the pure `highlightFlow` (no client highlight JS, no window/DOM at module
  top). The Play buttons make Web-Audio sound via the `tones` helper (NOT the WASM runtime —
  the real engine lives on /playground). The layout's old chrome is suppressed on `/` via the
  +layout.svelte `isHome` guard, so this page owns its own toolbar + tab bar.
-->
<script lang="ts">
	import { highlightFlow } from '$lib/home/flow-highlight';
	import { playTone, playMelody, playChord, type ToneType } from '$lib/home/tones';
	import { encode } from '$lib/share/encode';

	const REPO_URL = 'https://github.com/noahfreelove/flow-sharp';

	// The 3 hero snippets — copied VERBATIM from index.html (must_not_break). highlightFlow is
	// pure, so these precompute at prerender time; the {@html} in markup just emits the strings.
	const HELLO = `use "@audio"
(play (createSineTone 440Hz 1.0 0.5))`;

	const SCALE = `use "@audio"
use "@composition"

tempo 120 {
  (play | C4q D4q E4q F4q G4q |)
}`;

	const CADENCE = `use "@composition"

key Cmajor {
  (play | [D4 F4 A4]h [G3 B3 D4]h [C4 E4 G4]w |)
}`;

	const hello = highlightFlow(HELLO);
	const scale = highlightFlow(SCALE);
	const cadence = highlightFlow(CADENCE);

	// D-49-08 playground deep-links: encode each snippet as a #code= fragment so clicking
	// "Open in playground →" loads the exact code the user is looking at (§6.6 fix).
	// encode() is fflate-deflate + base64url — safe at prerender time (no window/DOM access).
	// &run=1 is intentionally omitted here: cold-load auto-run is gated by another packet.
	const helloHref = `/playground#code=${encode(HELLO)}`;
	const scaleHref = `/playground#code=${encode(SCALE)}`;
	const cadenceHref = `/playground#code=${encode(CADENCE)}`;

	// Hero card Play handlers — Web-Audio tones, fired only inside onclick (autoplay-safe).
	function playHello(): void {
		playTone(440, 0.9, 'triangle');
	}
	function playScale(): void {
		playMelody(['C4', 'D4', 'E4', 'F4', 'G4'], 0.34);
	}
	function playCadence(): void {
		// ii–V–I — three chords struck 900ms apart.
		playChord(['D4', 'F4', 'A4'], 1.0);
		setTimeout(() => playChord(['G3', 'B3', 'D4'], 1.0), 900);
		setTimeout(() => playChord(['C4', 'E4', 'G4'], 1.0), 1800);
	}

	// Leather "How it sounds" rack — melodies/types from index.html L185/192/199.
	const PLAYERS: { melody: string[]; type: ToneType; title: string; sub: string }[] = [
		{
			melody: ['C4', 'E4', 'G4', 'C5', 'G4', 'E4'],
			type: 'sine',
			title: 'In Five Voices — symphony excerpt',
			sub: 'multi-voice render · 0:42'
		},
		{
			melody: ['G4', 'A4', 'B4', 'C5', 'B4', 'G4', 'E4'],
			type: 'square',
			title: 'Stride & Stomp — ragtime',
			sub: 'stride piano · 0:38'
		},
		{
			melody: ['C4', 'Db4', 'Eb4', 'E4', 'Gb4', 'G4'],
			type: 'triangle',
			title: 'Just-intonation microtonal sketch',
			sub: 'cent-offset tuning · 0:24'
		}
	];

	const VU_BARS = 14;
	let playingIndex = $state(-1);
	let vu = $state<number[]>(new Array(VU_BARS).fill(6));

	// Guarded timers so a second click can't leak the prior animation interval/timeout.
	let vuInterval: ReturnType<typeof setInterval> | null = null;
	let vuTimeout: ReturnType<typeof setTimeout> | null = null;

	function clearVuTimers(): void {
		if (vuInterval !== null) {
			clearInterval(vuInterval);
			vuInterval = null;
		}
		if (vuTimeout !== null) {
			clearTimeout(vuTimeout);
			vuTimeout = null;
		}
	}

	function playLeather(i: number): void {
		clearVuTimers();
		playingIndex = i;
		const p = PLAYERS[i];
		const dur = playMelody(p.melody, 0.34, p.type);
		vuInterval = setInterval(() => {
			vu = Array.from({ length: VU_BARS }, () => 6 + Math.random() * 20);
		}, 90);
		vuTimeout = setTimeout(
			() => {
				clearVuTimers();
				vu = new Array(VU_BARS).fill(6);
				playingIndex = -1;
			},
			dur * 1000
		);
	}
</script>

<svelte:head>
	<title>Flow — a language for music</title>
	<meta
		name="description"
		content="Flow is an interpreted, statically-typed language for music production. Write musical ideas as code — note streams, chords, musical-context blocks — and hear them the instant you press play."
	/>
	<!-- REQ-SITE-RESPONSIVE-01: prevent horizontal overflow at the document level on the home
	     page. Only the iOS-6 marketing page sets this; other routes are unaffected. -->
	<style>
		html:has(.ios6-page) {
			overflow-x: hidden;
		}
	</style>
</svelte:head>

<div class="ios6-page">
	<div class="toolbar">
		<a class="brand site-wordmark" href="/"><span class="glyph" aria-hidden="true"></span><span class="word">Flow</span></a>
		<nav class="nav" aria-label="Primary">
			<a href="/" class="active" aria-current="page">Home</a>
			<a href="/docs">Docs</a>
			<a href="/playground">Playground</a>
			<a href="/showcase">Showcase</a>
			<a href={REPO_URL} class="ext" target="_blank" rel="noopener noreferrer"
				>GitHub<span class="sr-only"> (opens in new tab)</span></a
			>
		</nav>
		<!-- No theme toggle on home: the iOS-6 home is light-only, so a toggle here would be a
		     dead switch (design decision A). The toggle lives in the shared <SiteToolbar> on the
		     other routes, which do have a dark mode. -->
	</div>

	<main class="layout">
		<!-- HERO -->
		<section class="plate hero">
			<span class="screw tl"></span><span class="screw tr"></span><span class="screw bl"></span
			><span class="screw br"></span>
			<div class="hero-top">
				<div class="reflect" data-text="Flow">
					<p class="tagline">Interpreted · functional · strongly typed</p>
					<h1 class="hero-word">Flow</h1>
				</div>
				<div class="hero-blurb">
					<p>
						A programming language for <strong>making music</strong>. Write note streams, chords,
						and musical-context blocks as code — and hear them the instant you press play.
					</p>
					<div class="hero-cta">
						<a class="btn lg" href="/playground">Open the Playground</a>
						<a class="btn lg gray" href="/docs">Read the Docs</a>
					</div>
				</div>
			</div>

			<!-- three example wells -->
			<div class="cards3">
				<div class="ex">
					<h3>A pure tone</h3>
					<p>The hello-world — a one-second A4 sine. Press play to hear it.</p>
					<div class="well">
						<div class="code-head">
							<span class="dot r"></span><span class="dot y"></span><span class="dot g"></span
							>hello.flow
						</div>
						<!-- eslint-disable-next-line svelte/no-at-html-tags -->
						<pre class="code flow-code">{@html hello}</pre>
					</div>
					<div class="row">
						<button class="btn sm green" onclick={playHello}>▶ Play</button>
						<a class="open" href={helloHref}>Open in playground →</a>
					</div>
				</div>

				<div class="ex">
					<h3>A note-stream melody</h3>
					<p>A C-major run, written inline as a note stream at 120 BPM.</p>
					<div class="well">
						<div class="code-head">
							<span class="dot r"></span><span class="dot y"></span><span class="dot g"></span
							>scale.flow
						</div>
						<!-- eslint-disable-next-line svelte/no-at-html-tags -->
						<pre class="code flow-code">{@html scale}</pre>
					</div>
					<div class="row">
						<button class="btn sm green" onclick={playScale}>▶ Play</button>
						<a class="open" href={scaleHref}>Open in playground →</a>
					</div>
				</div>

				<div class="ex">
					<h3>A chord progression</h3>
					<p>A ii–V–I in C, played from chord brackets inside a key context.</p>
					<div class="well">
						<div class="code-head">
							<span class="dot r"></span><span class="dot y"></span><span class="dot g"></span
							>cadence.flow
						</div>
						<!-- eslint-disable-next-line svelte/no-at-html-tags -->
						<pre class="code flow-code">{@html cadence}</pre>
					</div>
					<div class="row">
						<button class="btn sm green" onclick={playCadence}>▶ Play</button>
						<a class="open" href={cadenceHref}>Open in playground →</a>
					</div>
				</div>
			</div>
		</section>

		<!-- WHY -->
		<div class="h-rule"><h2>Why Flow</h2><span class="line"></span></div>
		<div class="feat">
			<div class="plate">
				<div class="ic">✎</div>
				<h3>Ergonomics first</h3>
				<p>
					Composer ergonomics override runtime efficiency and type strictness. Easy cases stay fast;
					flexible cases stay flexible.
				</p>
			</div>
			<div class="plate">
				<div class="ic">◷</div>
				<h3>Genre-agnostic</h3>
				<p>
					Classical, EDM, jazz, pop, metal — all in one language. Designed for every kind of music,
					never tuned for one.
				</p>
			</div>
			<div class="plate">
				<div class="ic">♪</div>
				<h3>Notation roots</h3>
				<p>
					Notes, chords, note streams and musical-context blocks are first-class. Write musical ideas
					as code and hear them at once.
				</p>
			</div>
		</div>

		<!-- HOW IT SOUNDS -->
		<div class="h-rule"><h2>How it sounds</h2><span class="line"></span></div>
		<div class="leather">
			<span
				class="screw tl"
				style="background:radial-gradient(circle at 35% 30%,#e8c79a,#7a5230 55%,#3a2410)"
			></span>
			<span
				class="screw tr"
				style="background:radial-gradient(circle at 35% 30%,#e8c79a,#7a5230 55%,#3a2410)"
			></span>
			<p style="margin:2px 0 16px; font-size:13.5px; color:#e7d4b4; max-width:60ch;">
				Every example below is rendered straight from Flow source. Nothing plays automatically —
				press play to listen.
			</p>

			{#each PLAYERS as p, i (p.title)}
				<div
					class="player plate"
					class:playing={playingIndex === i}
					style="background:linear-gradient(#3a2a18,#241608); border-color:#1a0f04;"
				>
					<!-- Decorative status LED — aria-hidden so AT ignores the colour change;
					     the playing state is conveyed by the button text and page context (§6.9). -->
					<div class="led" aria-hidden="true"></div>
					<button class="btn sm amber" onclick={() => playLeather(i)}>▶ Play</button>
					<div class="meta">
						<div class="t">{p.title}</div>
						<div class="s">{p.sub}</div>
					</div>
					<!-- Decorative VU meter — purely visual animation, aria-hidden (§6.9). -->
					<div class="vu" aria-hidden="true">
						{#if playingIndex === i}
							<!-- keyed by index, NOT value: vu initializes to 14 identical
							     heights, and duplicate keys throw each_key_duplicate -->
							{#each vu as h, b (b)}
								<i style="height:{h}px"></i>
							{/each}
						{:else}
							{#each new Array(VU_BARS) as _, b (b)}
								<i style="height:6px"></i>
							{/each}
						{/if}
					</div>
				</div>
			{/each}
		</div>

		<div class="footer">
			Flow v1.4 · written in C# on .NET 10 · <a href="/docs">Docs</a> ·
			<a href={REPO_URL} target="_blank" rel="noopener noreferrer">GitHub ⌃</a><br />
			Press <kbd>Play</kbd> anywhere to hear real Web Audio tones.
		</div>
	</main>

</div>

<style>
	/* =========================================================================
	   iOS-6 skeuomorphic home — ported from flow.css + index.html inline <style>.
	   Component-scoped (HANDOFF §4.1 option a): Svelte auto-scopes these generic
	   class names to .ios6-page descendants, so they cannot leak to the other
	   routes. Design tokens are declared on .ios6-page (NOT :root) so they inherit
	   to all descendants here but do NOT override tokens.css on other routes (§6.5).
	   The {@html} code-token classes are :global() because that output is NOT scoped.
	   ========================================================================= */

	/* =========================================================================
	   iOS-6 design tokens scoped to .ios6-page so they do NOT leak globally.
	   Custom properties inherit through all descendants (same reach as :root for
	   this component), but Svelte does NOT emit them as a bare :root rule —
	   previously this was :root which overrode tokens.css's JetBrains Mono
	   --font-mono on every other route after the user visited / (§6.5 fix).
	   ========================================================================= */
	.ios6-page {
		--ink: #2b2722;
		--ink-soft: #5c554c;
		--ink-faint: #8a8278;
		--emboss: rgba(255, 255, 255, 0.72);
		--emboss-dk: rgba(0, 0, 0, 0.55);

		--linen: #c9c3b6;
		--linen-dk: #b3ac9c;

		--aqua-1: #8fc0f2;
		--aqua-2: #3f86d8;
		--aqua-3: #2e6cba;
		--aqua-4: #235aa0;

		--leather-1: #7a5230;
		--leather-2: #5c3c20;
		--leather-3: #43290f;
		--stitch: #d9b483;

		--felt-1: #2f6a44;
		--felt-2: #245538;
		--felt-3: #18402a;

		--paper: #f6f2e7;
		--paper-line: #e3dcc8;

		--metal-1: #fbfbfb;
		--metal-2: #e2e2e0;
		--metal-3: #c7c6c2;
		--metal-edge: #8b897f;

		--code-bg: #fbf8ee;
		--code-kw: #9a3b2e;
		--code-str: #2f6a44;
		--code-num: #235aa0;
		--code-com: #9a948a;
		--code-fn: #7a5230;

		--r-card: 14px;
		--r-btn: 9px;
		--font-ui: 'Helvetica Neue', Helvetica, Arial, sans-serif;
		/* Scoped here (not :root) so this does NOT override tokens.css's JetBrains
		   Mono --font-mono on /docs, /playground, /showcase, etc. (§6.5). */
		--font-mono: Menlo, Monaco, Consolas, 'Courier New', monospace;
	}

	/* Linen ground as a full-viewport wrapper (NOT the shared <body>; app.css owns body). */
	.ios6-page {
		min-height: 100dvh;
		/* REQ-SITE-RESPONSIVE-01: prevent horizontal overflow at all viewport widths (320px floor).
		   position:relative + overflow-x:clip acts as a stacking-context clipping container so that
		   no child element's intrinsic width expands the document scrollWidth past the viewport. */
		position: relative;
		overflow-x: clip;
		font-family: var(--font-ui);
		color: var(--ink);
		-webkit-font-smoothing: antialiased;
		text-rendering: optimizeLegibility;
		background-color: var(--linen);
		background-image: radial-gradient(
				120% 90% at 50% -10%,
				rgba(255, 255, 255, 0.45),
				rgba(255, 255, 255, 0) 55%
			),
			radial-gradient(120% 120% at 50% 120%, rgba(0, 0, 0, 0.22), rgba(0, 0, 0, 0) 55%),
			repeating-linear-gradient(0deg, rgba(255, 255, 255, 0.05) 0 1px, rgba(0, 0, 0, 0.045) 1px 2px),
			repeating-linear-gradient(
				90deg,
				rgba(255, 255, 255, 0.05) 0 1px,
				rgba(0, 0, 0, 0.045) 1px 2px
			);
		background-attachment: fixed;
	}

	.ios6-page :global(a) {
		color: #235aa0;
	}

	/* ---- top bar — brushed aluminum ---- */
	.toolbar {
		position: sticky;
		top: 0;
		z-index: 50;
		height: 58px;
		display: flex;
		align-items: center;
		gap: 18px;
		padding: 0 18px;
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
	.nav a.ext::after {
		content: ' ⌃';
		font-size: 10px;
		opacity: 0.6;
	}

	/* ---- buttons — Aqua jelly ---- */
	.btn {
		-webkit-appearance: none;
		appearance: none;
		cursor: pointer;
		display: inline-flex;
		align-items: center;
		gap: 8px;
		white-space: nowrap;
		font-family: var(--font-ui);
		font-size: 14px;
		font-weight: 700;
		padding: 10px 20px;
		border: 1px solid var(--aqua-4);
		border-radius: var(--r-btn);
		color: #fff;
		text-shadow: 0 -1px 0 rgba(0, 0, 0, 0.4);
		position: relative;
		overflow: hidden;
		background: linear-gradient(var(--aqua-1), var(--aqua-2) 48%, var(--aqua-3) 52%, var(--aqua-4));
		box-shadow:
			inset 0 1px 0 rgba(255, 255, 255, 0.7),
			inset 0 -3px 5px rgba(0, 0, 0, 0.28),
			0 2px 4px rgba(0, 0, 0, 0.32);
	}
	.btn::before {
		content: '';
		position: absolute;
		left: 1px;
		right: 1px;
		top: 1px;
		height: 46%;
		border-radius: 8px 8px 70% 70% / 8px 8px 26px 26px;
		background: linear-gradient(rgba(255, 255, 255, 0.62), rgba(255, 255, 255, 0.05));
		pointer-events: none;
	}
	.btn:active {
		box-shadow:
			inset 0 2px 6px rgba(0, 0, 0, 0.45),
			0 1px 2px rgba(0, 0, 0, 0.3);
		transform: translateY(1px);
	}
	.btn.gray {
		border-color: #7d7a73;
		background: linear-gradient(#fafafa, #e6e4e0 48%, #cfccc5 52%, #bdbab3);
		color: #3c3830;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.8);
	}
	.btn.green {
		border-color: #1d4a30;
		background: linear-gradient(#6bbf8b, #2f8a55 48%, #247046 52%, #1d5d39);
	}
	.btn.amber {
		border-color: #8a6a25;
		background: linear-gradient(#f4d690, #e0b24f 48%, #cf9a35 52%, #b9842a);
		color: #4a3a14;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.5);
	}
	.btn.lg {
		font-size: 16px;
		padding: 13px 26px;
	}
	.btn.sm {
		font-size: 12.5px;
		padding: 7px 13px;
	}

	/* ---- panels / cards ---- */
	.layout {
		max-width: 1080px;
		margin: 0 auto;
		/* generous, viewport-scaling side buffer so content never sits flush to the edge */
		padding: 34px clamp(20px, 4vw, 48px) 90px;
	}

	.plate {
		position: relative;
		background: linear-gradient(#fdfcf8, #efeadd);
		border: 1px solid #b8b1a0;
		border-radius: var(--r-card);
		box-shadow:
			inset 0 1px 0 rgba(255, 255, 255, 0.9),
			inset 0 -1px 2px rgba(0, 0, 0, 0.06),
			0 1px 0 rgba(255, 255, 255, 0.55),
			0 6px 16px rgba(0, 0, 0, 0.22);
		padding: 22px;
	}

	.screw {
		position: absolute;
		width: 7px;
		height: 7px;
		border-radius: 50%;
		background: radial-gradient(circle at 35% 30%, #fff, #b9b3a4 55%, #7d776a);
		box-shadow:
			inset 0 0 0 1px rgba(0, 0, 0, 0.25),
			0 1px 0 rgba(255, 255, 255, 0.6);
	}
	.screw.tl {
		top: 8px;
		left: 8px;
	}
	.screw.tr {
		top: 8px;
		right: 8px;
	}
	.screw.bl {
		bottom: 8px;
		left: 8px;
	}
	.screw.br {
		bottom: 8px;
		right: 8px;
	}

	.well {
		background: var(--code-bg);
		border: 1px solid #c9c0a8;
		border-radius: 10px;
		box-shadow:
			inset 0 2px 5px rgba(0, 0, 0, 0.22),
			inset 0 -1px 0 rgba(255, 255, 255, 0.7),
			0 1px 0 rgba(255, 255, 255, 0.5);
		overflow: hidden;
	}

	.leather {
		position: relative;
		color: #f3e6d2;
		background:
			radial-gradient(120% 80% at 50% 0%, rgba(255, 255, 255, 0.1), rgba(255, 255, 255, 0) 60%),
			linear-gradient(var(--leather-1), var(--leather-2) 55%, var(--leather-3));
		border: 1px solid #2c1a09;
		border-radius: var(--r-card);
		box-shadow:
			inset 0 1px 0 rgba(255, 255, 255, 0.18),
			inset 0 -10px 26px rgba(0, 0, 0, 0.4),
			0 8px 18px rgba(0, 0, 0, 0.32);
		padding: 24px;
	}
	.leather::before {
		content: '';
		position: absolute;
		inset: 9px;
		border-radius: 9px;
		border: 2px dashed var(--stitch);
		opacity: 0.55;
		pointer-events: none;
	}
	.leather::after {
		content: '';
		position: absolute;
		inset: 0;
		border-radius: var(--r-card);
		pointer-events: none;
		background: repeating-linear-gradient(
			115deg,
			rgba(0, 0, 0, 0.05) 0 2px,
			rgba(255, 255, 255, 0.02) 2px 4px
		);
		opacity: 0.5;
		mix-blend-mode: overlay;
	}
	.leather > * {
		position: relative;
		z-index: 1;
	}

	/* ---- code blocks ---- */
	.code {
		margin: 0;
		font-family: var(--font-mono);
		font-size: 13px;
		line-height: 1.7;
		color: #43352a;
		padding: 14px 16px;
		white-space: pre;
		overflow: auto;
	}
	.code-head {
		display: flex;
		align-items: center;
		gap: 8px;
		padding: 7px 12px;
		font-size: 11px;
		font-weight: 700;
		letter-spacing: 0.4px;
		text-transform: uppercase;
		color: #6a6256;
		background: linear-gradient(#efe9d8, #e1d9c2);
		border-bottom: 1px solid #cbc2aa;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.6);
	}
	.code-head .dot {
		width: 9px;
		height: 9px;
		border-radius: 50%;
		box-shadow:
			inset 0 1px 1px rgba(255, 255, 255, 0.5),
			0 1px 1px rgba(0, 0, 0, 0.3);
	}
	.dot.r {
		background: radial-gradient(circle at 35% 30%, #ff9a8f, #d6483a);
	}
	.dot.y {
		background: radial-gradient(circle at 35% 30%, #ffe48a, #e0a92e);
	}
	.dot.g {
		background: radial-gradient(circle at 35% 30%, #a6e69a, #3f9a42);
	}

	/* {@html} highlightFlow output is NOT Svelte-scoped — keep these :global, namespaced. */
	:global(.flow-code .tk-kw) {
		color: var(--code-kw);
		font-weight: 700;
	}
	:global(.flow-code .tk-str) {
		color: var(--code-str);
	}
	:global(.flow-code .tk-num) {
		color: var(--code-num);
	}
	:global(.flow-code .tk-com) {
		color: var(--code-com);
		font-style: italic;
	}
	:global(.flow-code .tk-fn) {
		color: var(--code-fn);
	}
	:global(.flow-code .tk-op) {
		color: #7a5230;
	}

	/* ---- section heading ---- */
	.h-rule {
		display: flex;
		align-items: center;
		gap: 14px;
		margin: 46px 0 18px;
	}
	.h-rule h2 {
		margin: 0;
		font-size: 22px;
		font-weight: 800;
		color: #3a342c;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.7);
	}
	.h-rule .line {
		flex: 1;
		height: 2px;
		border-radius: 2px;
		background: linear-gradient(90deg, rgba(0, 0, 0, 0.18), rgba(0, 0, 0, 0));
		box-shadow: 0 1px 0 rgba(255, 255, 255, 0.6);
	}

	/* ---- reflection under hero word ---- */
	.reflect {
		position: relative;
	}
	.reflect::after {
		content: attr(data-text);
		position: absolute;
		left: 0;
		top: 100%;
		transform: scaleY(-1);
		opacity: 0.16;
		pointer-events: none;
		-webkit-mask-image: linear-gradient(rgba(0, 0, 0, 0.9), rgba(0, 0, 0, 0) 55%);
		mask-image: linear-gradient(rgba(0, 0, 0, 0.9), rgba(0, 0, 0, 0) 55%);
	}

	/* =========================================================================
	   landing-specific (ported from index.html inline <style> L9-66)
	   ========================================================================= */
	.hero {
		padding: 34px 30px 30px;
		margin-bottom: 30px;
	}
	.hero-top {
		display: flex;
		gap: 30px;
		align-items: flex-start;
		flex-wrap: wrap;
	}
	.hero-word {
		font-size: 104px;
		line-height: 0.82;
		font-weight: 800;
		letter-spacing: -2px;
		margin: 4px 0 0;
		color: #3a312a;
		background: linear-gradient(#5a4a3a, #2c241d);
		-webkit-background-clip: text;
		background-clip: text;
		-webkit-text-fill-color: transparent;
		filter: drop-shadow(0 1px 0 rgba(255, 255, 255, 0.7));
	}
	.hero-blurb {
		flex: 1;
		/* min-width clamps to 0 at narrow viewports so flex-wrap can collapse the hero-top row. */
		min-width: min(280px, 100%);
	}
	.hero-blurb p {
		font-size: 16.5px;
		line-height: 1.62;
		color: #4c453b;
		margin: 6px 0 18px;
		max-width: 46ch;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.55);
	}
	.hero-cta {
		display: flex;
		gap: 12px;
		flex-wrap: wrap;
	}
	.tagline {
		font-size: 13px;
		font-weight: 700;
		letter-spacing: 0.5px;
		text-transform: uppercase;
		color: #8a6a3a;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.6);
		margin: 0 0 2px;
	}

	.cards3 {
		display: grid;
		grid-template-columns: repeat(3, 1fr);
		gap: 16px;
		margin-top: 26px;
	}
	.ex h3 {
		margin: 0 0 4px;
		font-size: 16px;
		font-weight: 800;
		color: #36302a;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.7);
	}
	.ex p {
		margin: 0 0 12px;
		font-size: 12.5px;
		line-height: 1.5;
		color: #6a6256;
		min-height: 34px;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.5);
	}
	.ex .well {
		margin-bottom: 12px;
	}
	.ex .row {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 8px;
	}
	.ex .open {
		font-size: 12px;
		font-weight: 700;
		color: #235aa0;
		text-decoration: none;
	}
	.ex .open:hover {
		text-decoration: underline;
	}

	.feat {
		display: grid;
		grid-template-columns: repeat(3, 1fr);
		gap: 16px;
	}
	.feat .plate {
		padding: 20px;
	}
	.feat .ic {
		width: 40px;
		height: 40px;
		border-radius: 9px;
		display: flex;
		align-items: center;
		justify-content: center;
		font-size: 20px;
		margin-bottom: 12px;
		background: linear-gradient(#fafafa, #dedbd3);
		border: 1px solid #b6afa0;
		box-shadow:
			inset 0 1px 0 #fff,
			0 1px 2px rgba(0, 0, 0, 0.18);
	}
	.feat h3 {
		margin: 0 0 7px;
		font-size: 16px;
		font-weight: 800;
		color: #36302a;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.7);
	}
	.feat p {
		margin: 0;
		font-size: 13.5px;
		line-height: 1.55;
		color: #5c554b;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.5);
	}

	/* audio player rows */
	.player {
		display: flex;
		align-items: center;
		gap: 16px;
		margin-bottom: 14px;
		padding: 14px 16px;
	}
	.player .led {
		width: 13px;
		height: 13px;
		border-radius: 50%;
		flex: 0 0 auto;
		background: radial-gradient(circle at 35% 30%, #ff7a6a, #8e241a);
		box-shadow:
			inset 0 1px 1px rgba(255, 255, 255, 0.4),
			0 0 6px rgba(220, 60, 40, 0.5);
	}
	.player.playing .led {
		background: radial-gradient(circle at 35% 30%, #8ff0a0, #1c7a36);
		box-shadow:
			inset 0 1px 1px rgba(255, 255, 255, 0.5),
			0 0 10px rgba(60, 220, 90, 0.8);
		animation: blink 1s infinite;
	}
	@keyframes blink {
		50% {
			opacity: 0.5;
		}
	}
	.player .meta {
		flex: 1;
	}
	.player .meta .t {
		font-size: 14.5px;
		font-weight: 800;
		color: #f4e6cf;
		text-shadow: 0 -1px 0 rgba(0, 0, 0, 0.5);
	}
	.player .meta .s {
		font-size: 12px;
		color: #d9c3a0;
		opacity: 0.8;
	}
	.vu {
		display: flex;
		gap: 3px;
		align-items: flex-end;
		height: 26px;
	}
	.vu i {
		width: 5px;
		border-radius: 1px;
		background: linear-gradient(#9be6a6, #2f8a55);
		box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.3);
		height: 6px;
		transition: height 0.1s;
	}

	.footer {
		text-align: center;
		color: #6b6458;
		font-size: 12.5px;
		margin-top: 40px;
		padding-bottom: 24px;
		text-shadow: 0 1px 0 rgba(255, 255, 255, 0.5);
	}
	.footer a {
		font-weight: 700;
	}

	@media (max-width: 820px) {
		.cards3,
		.feat {
			grid-template-columns: 1fr;
		}
		.hero-word {
			font-size: 74px;
		}
	}

	/* No bottom tab bar — the toolbar pill nav is the only navigation at every width.
	   On narrow viewports it scrolls horizontally INSIDE the bar so the row never overflows
	   the document (min-width:0 lets the flex item shrink below its content width). */
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
		.nav a {
			white-space: nowrap;
		}
	}

	/* Screen-reader-only utility (mirrors app.css .sr-only). Component-scoped so it applies
	   only inside .ios6-page where this file's styles are active. */
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

	/* <main> wrapping .layout — purely a landmark, no extra visual. */
	main {
		display: contents;
	}
</style>
