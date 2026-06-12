<!--
  <AudioEmbed> — a gesture-gated audio embed (D-49-01 — nothing ever starts on its own;
  UI-SPEC §"How it sounds").

  An explicit-play <audio> wrapper: a brass play/pause <Button> + a felt-grille <LedIndicator>
  (idle → playing) + a "Press play to listen" caption (UI-SPEC §Copywriting). The <audio> element
  has no self-starting attribute — playback only ever starts from the user pressing Play (the one
  gesture). The LED state mirrors the real <audio> play/pause/ended events, so the SR aria-live
  status ("Playing" / "Stopped") stays truthful (status not by colour alone, D-49-10).
-->
<script lang="ts">
	import Button from '$lib/components/skeuo/Button.svelte';
	import LedIndicator from '$lib/components/skeuo/LedIndicator.svelte';

	let {
		src,
		title,
		caption = 'Press play to listen'
	}: {
		/** Audio file URL (first-party, under static/audio/). */
		src: string;
		/** Accessible name for this embed (announced on the play button + <audio>). */
		title: string;
		/** Visible caption — defaults to the UI-SPEC "press play to listen" affordance copy. */
		caption?: string;
	} = $props();

	let audio = $state<HTMLAudioElement | null>(null);
	let playing = $state(false);

	function toggle(): void {
		if (!audio) return;
		if (audio.paused) {
			// The user gesture. Nothing starts on its own — this is the only path that starts audio.
			void audio.play();
		} else {
			audio.pause();
		}
	}
</script>

<div class="audio-embed surface-felt">
	<LedIndicator state={playing ? 'playing' : 'idle'} label={title} />

	<Button variant="primary" label={playing ? `Pause ${title}` : `Play ${title}`} onclick={toggle}>
		<span aria-hidden="true">{playing ? '❚❚' : '▶'}</span>
		<span class="audio-embed__cta-text">{playing ? 'Pause' : 'Play'}</span>
	</Button>

	<div class="audio-embed__meta">
		<span class="audio-embed__title">{title}</span>
		<span class="audio-embed__caption">{caption}</span>
	</div>

	<!--
	  No self-starting attribute — D-49-01. `preload="none"` so the bytes aren't fetched on Home
	  until the user opts in. Native controls hidden (the skeuo Button + LED are the affordance) but
	  the element keeps an accessible name + a track of its play state.
	-->
	<audio
		bind:this={audio}
		{src}
		preload="none"
		aria-label={title}
		onplay={() => (playing = true)}
		onpause={() => (playing = false)}
		onended={() => (playing = false)}
	></audio>
</div>

<style>
	.audio-embed {
		display: flex;
		align-items: center;
		gap: var(--space-3);
		padding: var(--space-3) var(--space-4);
		border-radius: var(--radius-3);
		box-shadow: var(--shadow-inset);
	}
	.audio-embed__meta {
		display: flex;
		flex-direction: column;
		gap: 2px;
		min-width: 0;
	}
	.audio-embed__title {
		font-size: var(--text-small);
		font-weight: 600;
		color: var(--color-on-chrome);
	}
	.audio-embed__caption {
		font-size: var(--text-caption);
		color: var(--color-on-chrome);
		opacity: 0.8;
	}
	.audio-embed__cta-text {
		font-size: var(--text-small);
	}
</style>
