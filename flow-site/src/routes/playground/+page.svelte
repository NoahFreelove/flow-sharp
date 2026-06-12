<script module lang="ts">
	// Playground is a client-only SPA shell (D-49-13). Monaco + the Phase 48 WASM runtime are
	// browser-only and SSR-crash if imported during prerender, so this route opts out of SSR.
	// All Monaco + runtime imports are DYNAMIC, inside onMount (Pitfall 1 — never top-level here).
	export const prerender = false;
	export const ssr = false;
	export const csr = true;
</script>

<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import Button from '$lib/components/skeuo/Button.svelte';
	import Panel from '$lib/components/skeuo/Panel.svelte';
	import Toggle from '$lib/components/skeuo/Toggle.svelte';
	import LedIndicator from '$lib/components/skeuo/LedIndicator.svelte';
	import { PlaygroundState } from '$lib/playground/state.svelte';
	import { ShareControls, captureOAuthToken } from '$lib/playground/share-controls.svelte';
	import { decode, ShareDecodeError } from '$lib/share/encode';
	import { getGistToken, consumePendingGistSource } from '$lib/share/gist';
	import { SNIPPETS } from '$lib/playground/snippets';
	import type { FlowRuntime, RunError } from '$lib/runtime';
	// Monaco's editor type is dynamic-imported; keep a loose handle so SSR never sees the module.
	type Editor = { getValue(): string; setValue(v: string): void; updateOptions(o: { readOnly: boolean }): void; dispose(): void };

	const MOBILE_BREAKPOINT = 768; // <768px → Monaco read-only (D-49-09, D-49-23)

	const pg = new PlaygroundState();
	const share = new ShareControls();

	let runtime: FlowRuntime | null = null;
	let editor: Editor | null = null;
	let editorContainer: HTMLDivElement;
	let booting = $state(true);
	let runtimeReady = $state(false);
	let isMobile = $state(false);
	let confirmingBlank = $state(false);
	// True while a shared `#code=` fragment failed to decode (renders the friendly UI-SPEC copy).
	let shareDecodeError = $state(false);
	// Set when the arrival URL signals auto-run (D-49-08 deep-link); consumed once the runtime is up.
	let pendingAutoRun = $state(false);
	// Test-only hook: the AudioContext state observed after the last audio-resume gesture.
	// Only populated when ?e2e=1 is present in the URL (§6.10 — test scaffolding must not ship in prod).
	let audioState = $state<string>('unknown');
	// True when the page was opened with ?e2e=1 — enables test-only instrumentation.
	let isE2eMode = $state(false);

	function checkMobile(): void {
		isMobile = typeof window !== 'undefined' && window.innerWidth < MOBILE_BREAKPOINT;
		editor?.updateOptions({ readOnly: isMobile });
	}

	// The AudioContext constructor we may have patched (needs restoring in onDestroy for §6.10).
	let _savedAudioContext: (typeof window)['AudioContext'] | null = null;

	onMount(() => {
		let disposed = false;

		// Detect e2e test mode from the URL (?e2e=1). Only in test mode do we install the
		// AudioContext Proxy so the audio-state span reflects the resumed context's .state.
		// §6.10: the Proxy must NEVER run in production — it wraps every AudioContext consumer
		// (including the home page's tones.ts) and is never restored on normal navigation.
		isE2eMode =
			typeof window !== 'undefined' &&
			new URLSearchParams(window.location.search).has('e2e');

		if (isE2eMode) {
			// Wrap AudioContext (via a Proxy construct-trap) so the test hook can observe its state
			// after the audio-resume gesture — the frozen runtime keeps its context module-private
			// (HANDOFF §8, do not edit). Idempotent; a Proxy avoids a class declaration below top level.
			const flags = window as unknown as {
				__flowAudioWrapped?: boolean;
				__flowAudioCtx?: AudioContext;
			};
			const NativeAudioContext = window.AudioContext;
			if (NativeAudioContext && !flags.__flowAudioWrapped) {
				_savedAudioContext = NativeAudioContext;
				flags.__flowAudioWrapped = true;
				window.AudioContext = new Proxy(NativeAudioContext, {
					construct(target, args) {
						const ctx = Reflect.construct(target, args) as AudioContext;
						flags.__flowAudioCtx = ctx;
						return ctx;
					}
				});
			}
		}

		checkMobile();
		window.addEventListener('resize', checkMobile);

		// 0) OAuth return: if we came back from the gist worker with `#token=…`, cache it into
		//    sessionStorage and clean the URL so the token never lingers in history (T-49-SCOPE).
		//    §6.1: also consume any stashed editor source so the composer's code is restored and
		//    the pending save auto-fires with the correct source (no code loss on OAuth round-trip).
		let pendingGistSource: string | null = null;
		if (captureOAuthToken()) {
			history.replaceState(null, '', window.location.pathname + window.location.search);
			pendingGistSource = consumePendingGistSource();
		}

		// Resolve the initial editor value from the `#code=` fragment (decoded) BEFORE Monaco mounts.
		const arrival = resolveArrivalSource();
		pendingAutoRun = arrival.autoRun;
		shareDecodeError = arrival.decodeError;

		(async () => {
			// 1) Mount Monaco (dynamic import — Pitfall 1).
			try {
				const { createFlowEditor } = await import('$lib/monaco');
				if (disposed) return;
				// §6.1: if we're returning from an OAuth round-trip, prefer the stashed source
				// over the arrival URL and the default snippet so the composer's code is restored.
				const initialValue =
					pendingGistSource ?? arrival.source ?? pg.editorValue;
				editor = createFlowEditor(editorContainer, {
					value: initialValue,
					readOnly: isMobile
				}) as unknown as Editor;
				pg.editorValue = editor.getValue();
				// Test-only hook: lets the share E2E read Monaco's value to assert a round-trip.
				// §6.10: gated behind e2e mode so the global is not present in production builds.
				if (isE2eMode) {
					(window as unknown as { __flowEditorValue?: () => string }).__flowEditorValue = () =>
						editor?.getValue() ?? '';
				}
			} catch (e) {
				console.error('[playground] Monaco mount failed', e);
			}

			// 2) Lazy-boot the Phase 48 runtime (D-49-34 — only here, never on Home/Docs).
			try {
				const { bootRuntime } = await import('$lib/runtime');
				runtime = await bootRuntime();
				if (disposed) {
					runtime.dispose();
					return;
				}
				runtimeReady = true;
				// Expose a test hook so the E2E can await readiness deterministically.
				// §6.10: gated behind e2e mode so the global is not present in production builds.
				if (isE2eMode) {
					(window as unknown as { __flowRuntimeReady?: boolean }).__flowRuntimeReady = true;
				}

				// 3a) §6.1: if we returned from an OAuth round-trip with a pending gist source,
				//     auto-resume the save now that both the token and the editor are ready.
				if (pendingGistSource !== null && editor) {
					pendingGistSource = null; // consume — one-shot
					await share.saveToGist(editor.getValue());
				}

				// 3b) Honor the deep-link auto-run signal (D-49-08). Arriving via a "Play in playground"
				//    click IS the user gesture (D-48-09), so resuming audio + running here is allowed.
				//    §6.7: gate on navigator.userActivation?.hasBeenActive — a cold load (new tab /
				//    pasted URL / browser-restore) has no activation and the AudioContext will be
				//    suspended. Pre-load the code but skip the run; a 'Press Run to hear it' affordance
				//    is shown via the `pendingAutoRun` state flag instead.
				if (pendingAutoRun && !shareDecodeError) {
					const hasActivation =
						typeof navigator !== 'undefined' &&
						// userActivation is not available in all browsers (Safari 16.4+, Chromium, FF 120+).
						(navigator as Navigator & { userActivation?: { hasBeenActive: boolean } })
							.userActivation?.hasBeenActive === true;
					if (hasActivation) {
						pendingAutoRun = false;
						await onRun();
					}
					// else: pendingAutoRun stays true → the page shows a 'Press Run to hear it' prompt
					// and the Run button fires normally when the user activates it.
				}
			} catch (e) {
				pg.bootError =
					e instanceof Error ? e.message : 'The Flow runtime didn’t load.';
			} finally {
				booting = false;
			}
		})();

		return () => {
			disposed = true;
		};
	});

	onDestroy(() => {
		if (typeof window !== 'undefined') {
			window.removeEventListener('resize', checkMobile);
			// §6.10: restore the original AudioContext if we patched it (so SPA navigation
			// back to the home page doesn't leave a stale Proxy on window.AudioContext).
			if (_savedAudioContext) {
				window.AudioContext = _savedAudioContext;
				const flags = window as unknown as { __flowAudioWrapped?: boolean };
				delete flags.__flowAudioWrapped;
				_savedAudioContext = null;
			}
		}
		editor?.dispose();
		runtime?.dispose();
	});

	/**
	 * Resolve the editor's initial source from the arrival URL (Plan 49-06). The `#code=` fragment
	 * carries fflate-deflated, base64url-encoded source (encode.ts); `decode` is defensive +
	 * decompression-bomb-guarded (T-49-CSP-FRAG). The auto-run signal (D-49-08) rides a `run=1`
	 * marker in the FRAGMENT ONLY (`#code=…&run=1`), which the CodeCard/showcase anchors emit on a real
	 * click — a bare `?run=1` query string is intentionally NOT honoured (WR-01: it has no preceding
	 * user gesture, so resumeAudio() would be rejected by the browser autoplay policy).
	 *
	 * Returns `{ source, autoRun, decodeError }`. On a malformed `#code=`, `decodeError` is set so the
	 * page shows the UI-SPEC "Couldn't decode this shared snippet" message instead of crashing.
	 */
	function resolveArrivalSource(): { source: string | null; autoRun: boolean; decodeError: boolean } {
		if (typeof window === 'undefined') return { source: null, autoRun: false, decodeError: false };
		const hash = window.location.hash;
		// WR-01: auto-run ONLY honours the `#code=…&run=1` fragment that CodeCard/showcase anchors
		// produce on a real click — NOT a bare `?run=1` query string. A hand-typed/copied `?run=1`
		// arrival has no preceding user gesture, so resumeAudio() would be rejected by the browser
		// autoplay policy (contradicting D-48-09's "must be called from a user-gesture frame"). Dropping
		// the query-string source means audio is only ever resumed from an actual gesture.
		if (!hash.startsWith('#code=')) {
			return { source: null, autoRun: false, decodeError: false };
		}
		// Split off any `&run=1` (or other `&k=v`) markers riding the fragment.
		const body = hash.slice('#code='.length);
		const ampIndex = body.indexOf('&');
		const codePart = ampIndex === -1 ? body : body.slice(0, ampIndex);
		const fragParams = new URLSearchParams(ampIndex === -1 ? '' : body.slice(ampIndex + 1));
		const autoRun = fragParams.get('run') === '1';
		try {
			return { source: decode(codePart), autoRun, decodeError: false };
		} catch (e) {
			if (e instanceof ShareDecodeError) {
				return { source: null, autoRun: false, decodeError: true };
			}
			// Unexpected — treat as a decode failure rather than letting it crash mount.
			console.error('[playground] fragment decode failed', e);
			return { source: null, autoRun: false, decodeError: true };
		}
	}

	/** True when a gist token is already cached — drives the Save button label (no auth prompt needed). */
	const gistAuthed = $derived(typeof window !== 'undefined' && getGistToken() != null);

	async function onShare(): Promise<void> {
		if (!editor) return;
		await share.shareLink(editor.getValue());
	}
	async function onSaveToGist(): Promise<void> {
		if (!editor) return;
		await share.saveToGist(editor.getValue());
	}
	async function onCopyToastLink(): Promise<void> {
		await share.copyToastLink();
	}

	/**
	 * Run handler — the MANDATORY single gesture frame (HANDOFF §5, D-48-09): the audio-resume
	 * call THEN the run call in the SAME async function. The resume is idempotent + cheap; calling
	 * it every Run is recommended. It is NEVER called on mount — that would be a silent no-op.
	 */
	async function onRun(): Promise<void> {
		if (!runtime || !editor) return;
		// §6.7: the user pressed Run — consume any pending auto-run signal so the banner hides.
		pendingAutoRun = false;
		const source = editor.getValue();
		// The MANDATORY single gesture frame: resumeAudio() THEN run() back-to-back (HANDOFF §5).
		await runtime.resumeAudio();
		const runPromise = pg.run(runtime, source);
		// Reflect the resumed AudioContext state for the test hook (headless can't assert audio).
		// §6.10: only in e2e mode — __flowAudioCtx is only populated when the Proxy is active.
		if (isE2eMode) {
			audioState =
				(window as unknown as { __flowAudioCtx?: AudioContext }).__flowAudioCtx?.state ??
				'unknown';
		}
		await runPromise;
	}

	function onStop(): void {
		pg.stop(runtime);
	}

	function onLoadSnippet(id: string): void {
		pg.loadSnippet(id);
		editor?.setValue(pg.editorValue);
	}

	function requestBlank(): void {
		// Only confirm when there are unsaved edits (UI-SPEC destructive copy).
		if (editor && editor.getValue().trim().length > 0) {
			confirmingBlank = true;
		} else {
			doBlank();
		}
	}
	function doBlank(): void {
		confirmingBlank = false;
		pg.newBlank();
		editor?.setValue('');
	}

	function onDownloadMidi(): void {
		pg.downloadMidi();
	}

	/**
	 * Render-safe error heading; drops the never-raised cancel kind defensively.
	 *
	 * SECURITY (WR-05 — UNTRUSTED): `err.kind`, `err.message`, and `err.sourceSnippet` come from the
	 * WASM runtime's RunResult.errors[], which is ultimately derived from USER-SUPPLIED Flow source (a
	 * parse error echoes tokens straight from the source). They are SAFE today ONLY because every sink
	 * renders them via Svelte curly interpolation (`{errorHeading(err)}`, `{err.sourceSnippet}` in
	 * <pre>), which auto-escapes. NEVER route `err.*` through `{@html}` or any set-innerHTML helper —
	 * doing so would be a stored-XSS injection. Keep these strings escaped. (Regression-pinned by
	 * src/routes/playground/error-box-escaping.test.ts.)
	 */
	function errorHeading(err: RunError): string {
		return `✕ ${err.kind}: ${err.message}`;
	}
	function caretRow(err: RunError): string {
		// Render the caret marker only when a column is known.
		if (err.column == null) return '';
		return `${' '.repeat(Math.max(0, err.column - 1))}^^^^ here`;
	}
</script>

<svelte:head>
	<title>Playground · Flow</title>
	<meta
		name="description"
		content="Run Flow code in your browser and hear it instantly — an interactive WebAssembly playground for the Flow music-production language. Edit note streams, chords, and synthesis, then press Run to listen."
	/>
</svelte:head>

<main class="pg" class:is-mobile={isMobile}>
	{#if pg.bootError}
		<!-- Top-level boot-error pane (HANDOFF §2.3) — distinct from per-run errors. -->
		<div class="pg-boot-error surface-paper" role="alert" data-testid="boot-error">
			<h2>The Flow runtime didn’t load.</h2>
			<p>
				Refresh to try again — if it keeps happening, your browser may not support WebAssembly
				audio.
			</p>
			<p class="pg-boot-detail">{pg.bootError}</p>
		</div>
	{/if}

	<!-- LEFT 30% — snippets + controls (UI-SPEC §Playground left rail). -->
	<aside class="pg-rail surface-wood" aria-label="Snippets and controls">
		<h2 class="pg-rail-title">Snippets</h2>
		<ul class="pg-snippets">
			{#each SNIPPETS as snip (snip.id)}
				<li>
					<button
						type="button"
						class="pg-snippet"
						class:is-active={pg.activeSnippetId === snip.id}
						aria-current={pg.activeSnippetId === snip.id ? 'true' : undefined}
						onclick={() => onLoadSnippet(snip.id)}
					>
						<span class="pg-snippet-label">{snip.label}</span>
						<span class="pg-snippet-blurb">{snip.blurb}</span>
					</button>
				</li>
			{/each}
			<li>
				<button type="button" class="pg-snippet pg-snippet--blank" onclick={requestBlank}>
					<span class="pg-snippet-label">New blank</span>
				</button>
			</li>
		</ul>

		<div class="pg-rail-controls">
			<!-- Share = secondary (copy a #code= link); Save to gist = brass primary (OAuth promote). -->
			<Button
				variant="secondary"
				label="Share"
				disabled={!runtimeReady && booting}
				onclick={onShare}
			/>
			<Button
				variant="primary"
				label={share.saving ? 'Saving…' : gistAuthed ? 'Save to gist' : 'Save to gist ↗'}
				disabled={share.saving}
				onclick={onSaveToGist}
			/>
		</div>

		{#if share.toast}
			<!-- Escaped Svelte text (never {@html}) — T-49-XSS-SHARE. -->
			<div
				class="pg-toast surface-paper pg-toast--{share.toast.kind}"
				role="status"
				aria-live="polite"
				data-testid="share-toast"
			>
				<p class="pg-toast-msg">{share.toast.message}</p>
				{#if share.toast.copyLink}
					<div class="pg-toast-actions">
						<Button variant="secondary" label="Copy link" onclick={onCopyToastLink} />
						<Button variant="ghost" label="Dismiss" onclick={() => share.dismiss()} />
					</div>
				{:else}
					<Button variant="ghost" label="Dismiss" onclick={() => share.dismiss()} />
				{/if}
			</div>
		{/if}
		<div class="pg-rail-theme">
			<Toggle theme={true} label="Toggle dark mode" />
		</div>

		{#if confirmingBlank}
			<div class="pg-confirm surface-paper" role="dialog" aria-label="Start a blank snippet?">
				<p class="pg-confirm-title">Start a blank snippet?</p>
				<p class="pg-confirm-body">
					Your current edits aren’t saved. Share or Save to gist first if you want to keep
					them.
				</p>
				<div class="pg-confirm-actions">
					<Button variant="danger" label="Start blank" onclick={doBlank} />
					<Button variant="ghost" label="Keep editing" onclick={() => (confirmingBlank = false)} />
				</div>
			</div>
		{/if}
	</aside>

	<!-- CENTER 50% — Monaco editor in a framed panel + Run strip. -->
	<section class="pg-editor">
		<Panel variant="framed" ariaLabel="Flow editor">
			<div class="pg-editor-strip">
				<Button
					variant="primary"
					label={pg.runStatus === 'rendering' ? 'Running…' : 'Run'}
					disabled={!runtimeReady || pg.runStatus === 'rendering'}
					onclick={onRun}
				/>
				{#if booting}
					<span class="pg-booting" aria-live="polite">Loading runtime…</span>
				{/if}
			</div>

			{#if shareDecodeError}
				<!-- UI-SPEC §Copywriting — friendly decode failure (NO crash); escaped text. -->
				<p class="pg-decode-error" role="alert" data-testid="decode-error">
					Couldn’t decode this shared snippet — the link may be incomplete or corrupted.
				</p>
			{/if}
			{#if pendingAutoRun && runtimeReady && !shareDecodeError}
				<!-- §6.7: auto-run was deferred (no user activation on cold load) — prompt instead. -->
				<p class="pg-autorun-banner" role="status" data-testid="autorun-banner">
					Press Run to hear it.
				</p>
			{/if}

			{#if isMobile}
				<p class="pg-mobile-banner" data-testid="mobile-banner">
					Editing is read-only on small screens. You can still press Run to hear this snippet —
					open on a desktop to edit.
				</p>
			{/if}

			<div
				class="pg-monaco"
				bind:this={editorContainer}
				data-testid="monaco"
				data-readonly={isMobile ? 'true' : 'false'}
			></div>
		</Panel>
	</section>

	<!-- RIGHT 20% — console output + audio status + downloads. -->
	<aside class="pg-output" aria-label="Output">
		<div class="pg-audio-row">
			<LedIndicator state={pg.runStatus} label="Playback" />
			<Button variant="danger" label="Stop" onclick={onStop} disabled={!runtimeReady} />
			{#if pg.hasMidi}
				<Button variant="secondary" label="Download MIDI" onclick={onDownloadMidi} />
			{/if}
		</div>

		<!-- Test-only mirror of the resumed AudioContext state (headless can't assert audibility). -->
		<span class="sr-only" data-testid="audio-state">{audioState}</span>

		<div class="pg-console surface-paper" data-testid="console">
			{#if !pg.hasRun}
				<div class="pg-empty">
					<p class="pg-empty-title">Nothing has run yet</p>
					<p class="pg-empty-body">
						Edit the code and press Run to hear it. Output, audio, and downloads show up here.
					</p>
				</div>
			{:else}
				{#if pg.stdout}
					<!-- stdout = ink. ESCAPED text (Svelte curly-expr auto-escapes) — never raw HTML (Security V5). -->
					<pre class="pg-stdout" data-testid="stdout">{pg.stdout}</pre>
				{/if}
				{#if pg.stderr}
					<section class="pg-stderr" data-testid="stderr" aria-label="Advisories">
						<h3 class="pg-stderr-title">Advisories</h3>
						<pre class="pg-stderr-body">{pg.stderr}</pre>
					</section>
				{/if}
				{#if pg.errors.length > 0}
					<section class="pg-errors" data-testid="errors" aria-label="Errors">
						{#each pg.errors as err, i (i)}
							<!-- Rust-style box on .surface-paper + --color-danger rail; ALL escaped text. -->
							<div class="pg-error surface-paper">
								<p class="pg-error-head">{errorHeading(err)}</p>
								{#if err.line != null}
									<p class="pg-error-loc">┌─ line {err.line}{err.column != null ? `:${err.column}` : ''}</p>
								{/if}
								{#if err.sourceSnippet}
									<pre class="pg-error-snippet">│ {err.sourceSnippet}</pre>
									{#if caretRow(err)}
										<pre class="pg-error-caret">│ {caretRow(err)}</pre>
									{/if}
								{/if}
							</div>
						{/each}
					</section>
				{/if}
			{/if}
		</div>
	</aside>

	<!-- Bottom status bar — runtime version · bundle size · last-run timestamp. -->
	<div class="pg-status surface-brushed-metal" data-testid="status-bar">
		<span>Flow WASM · Phase 48 runtime</span>
		<span aria-hidden="true">·</span>
		<span>~1.6 MB bundle</span>
		<span aria-hidden="true">·</span>
		<span
			>{pg.lastRunAt
				? `last run ${new Date(pg.lastRunAt).toLocaleTimeString()}${pg.lastDurationMs != null ? ` (${Math.round(pg.lastDurationMs)} ms)` : ''}`
				: 'no runs yet'}</span
		>
	</div>
</main>

<style>
	.pg {
		display: grid;
		/* fr units (not %) so the column track sizing accounts for the gap + padding — percent
		   columns summing to 100% leave no room for the 2× gap and overflow the viewport ~12px
		   on desktop (D-49-09 no-horizontal-overflow). minmax(0,…) lets Monaco's intrinsic width
		   shrink instead of forcing the track wider. */
		grid-template-columns: minmax(0, 0.3fr) minmax(0, 0.5fr) minmax(0, 0.2fr);
		grid-template-rows: 1fr auto;
		grid-template-areas:
			'rail editor output'
			'status status status';
		gap: var(--space-3);
		padding: var(--space-3);
		min-height: calc(100vh - 56px);
		max-width: 100%;
		box-sizing: border-box;
	}

	.pg-rail {
		grid-area: rail;
		padding: var(--space-4);
		border-radius: var(--radius-3);
		display: flex;
		flex-direction: column;
		gap: var(--space-3);
		min-width: 0;
	}
	.pg-rail-title {
		font-family: var(--font-display);
		font-size: var(--text-h4, 18px);
		color: var(--color-on-chrome);
		margin: 0;
	}
	.pg-snippets {
		list-style: none;
		margin: 0;
		padding: 0;
		display: flex;
		flex-direction: column;
		gap: var(--space-2);
	}
	.pg-snippet {
		width: 100%;
		text-align: left;
		display: flex;
		flex-direction: column;
		gap: 2px;
		padding: var(--space-2) var(--space-3);
		min-height: 44px;
		background: color-mix(in srgb, var(--color-walnut) 70%, black);
		color: var(--color-on-chrome);
		border: 1px solid color-mix(in srgb, var(--color-walnut) 40%, black);
		border-radius: var(--radius-2);
		cursor: pointer;
	}
	.pg-snippet.is-active {
		border-color: var(--color-brass);
		box-shadow: inset 0 0 0 1px var(--color-brass);
	}
	.pg-snippet:focus-visible {
		outline: var(--focus-ring-width) solid var(--focus-ring-color);
		outline-offset: var(--focus-ring-offset);
	}
	.pg-snippet-label {
		font-weight: 600;
		font-size: var(--text-small);
	}
	.pg-snippet-blurb {
		font-size: var(--text-caption, 12px);
		color: var(--color-ink-muted);
	}
	.pg-rail-controls {
		display: flex;
		gap: var(--space-2);
		flex-wrap: wrap;
	}

	.pg-toast {
		padding: var(--space-2) var(--space-3);
		border-radius: var(--radius-2);
		border-left: 4px solid var(--color-brass);
		display: flex;
		flex-direction: column;
		gap: var(--space-2);
	}
	.pg-toast--error {
		border-left-color: var(--color-danger);
	}
	.pg-toast--info {
		border-left-color: var(--color-slate);
	}
	.pg-toast-msg {
		margin: 0;
		font-size: var(--text-small);
		color: var(--color-ink);
		word-break: break-word;
	}
	.pg-toast-actions {
		display: flex;
		gap: var(--space-2);
		flex-wrap: wrap;
	}

	.pg-decode-error {
		margin: 0 0 var(--space-2);
		padding: var(--space-2) var(--space-3);
		font-size: var(--text-small);
		background: color-mix(in srgb, var(--color-danger) 8%, var(--color-paper));
		border-left: 4px solid var(--color-danger);
		border-radius: var(--radius-1, 2px);
		color: var(--color-ink);
	}

	.pg-editor {
		grid-area: editor;
		min-width: 0;
		display: flex;
	}
	.pg-editor :global(.skeuo-panel) {
		flex: 1;
		display: flex;
		flex-direction: column;
		min-width: 0;
	}
	.pg-editor :global(.skeuo-panel__body) {
		flex: 1;
		display: flex;
		flex-direction: column;
		min-height: 0;
		padding: var(--space-3);
	}
	.pg-editor-strip {
		display: flex;
		align-items: center;
		gap: var(--space-3);
		margin-bottom: var(--space-2);
	}
	.pg-booting {
		font-size: var(--text-caption, 12px);
		color: var(--color-ink-muted);
	}
	.pg-mobile-banner {
		margin: 0 0 var(--space-2);
		padding: var(--space-2) var(--space-3);
		font-size: var(--text-small);
		background: color-mix(in srgb, var(--color-brass) 18%, var(--color-paper));
		border-left: 4px solid var(--color-brass);
		border-radius: var(--radius-1, 2px);
		color: var(--color-ink);
	}
	/* §6.7 — deferred auto-run prompt: a snippet arrived via deep link but no user activation */
	.pg-autorun-banner {
		margin: 0 0 var(--space-2);
		padding: var(--space-2) var(--space-3);
		font-size: var(--text-small);
		background: color-mix(in srgb, var(--color-brass) 12%, var(--color-paper));
		border-left: 4px solid var(--color-brass);
		border-radius: var(--radius-1, 2px);
		color: var(--color-ink);
		font-weight: 500;
	}
	.pg-monaco {
		flex: 1;
		min-height: 320px;
		width: 100%;
		border: 1px solid var(--color-slate);
		border-radius: var(--radius-2);
		overflow: hidden;
	}

	.pg-output {
		grid-area: output;
		min-width: 0;
		display: flex;
		flex-direction: column;
		gap: var(--space-3);
	}
	.pg-audio-row {
		display: flex;
		align-items: center;
		gap: var(--space-2);
		flex-wrap: wrap;
	}
	.pg-console {
		flex: 1;
		padding: var(--space-3);
		border-radius: var(--radius-3);
		overflow: auto;
		min-height: 240px;
		font-family: var(--font-mono);
		font-size: var(--text-code, 14px);
	}
	.pg-empty {
		color: var(--color-ink-muted);
	}
	.pg-empty-title {
		font-family: var(--font-display);
		font-size: var(--text-h4, 18px);
		color: var(--color-ink);
		margin: 0 0 var(--space-1);
	}
	.pg-empty-body {
		margin: 0;
		font-size: var(--text-small);
	}
	.pg-stdout {
		margin: 0 0 var(--space-3);
		white-space: pre-wrap;
		word-break: break-word;
		color: var(--color-ink);
	}
	.pg-stderr {
		margin: 0 0 var(--space-3);
		padding-top: var(--space-2);
		border-top: 1px dashed color-mix(in srgb, var(--color-ink) 25%, transparent);
	}
	.pg-stderr-title {
		font-size: var(--text-caption, 12px);
		text-transform: uppercase;
		letter-spacing: 0.06em;
		color: var(--color-ink-muted);
		margin: 0 0 var(--space-1);
	}
	.pg-stderr-body {
		margin: 0;
		white-space: pre-wrap;
		word-break: break-word;
		color: var(--color-ink-muted);
		font-style: italic;
	}
	.pg-error {
		margin: 0 0 var(--space-2);
		padding: var(--space-2) var(--space-3);
		border-left: 4px solid var(--color-danger);
		border-radius: var(--radius-1, 2px);
		background: color-mix(in srgb, var(--color-danger) 8%, var(--color-paper));
	}
	.pg-error-head {
		margin: 0 0 var(--space-1);
		font-weight: 600;
		color: var(--color-danger);
	}
	.pg-error-loc,
	.pg-error-snippet,
	.pg-error-caret {
		margin: 0;
		white-space: pre-wrap;
		word-break: break-word;
		color: var(--color-ink);
		font-family: var(--font-mono);
		font-size: var(--text-small);
	}

	.pg-status {
		grid-area: status;
		display: flex;
		align-items: center;
		gap: var(--space-2);
		padding: var(--space-2) var(--space-3);
		border-radius: var(--radius-2);
		font-family: var(--font-mono);
		font-size: var(--text-caption, 12px);
		color: var(--color-ink-muted);
		flex-wrap: wrap;
	}

	.pg-boot-error {
		grid-column: 1 / -1;
		padding: var(--space-4);
		border-left: 4px solid var(--color-danger);
		border-radius: var(--radius-3);
		margin-bottom: var(--space-3);
	}
	.pg-boot-detail {
		font-family: var(--font-mono);
		font-size: var(--text-caption, 12px);
		color: var(--color-ink-muted);
	}

	.pg-confirm {
		padding: var(--space-3);
		border-radius: var(--radius-2);
		border-left: 4px solid var(--color-danger);
	}
	.pg-confirm-title {
		font-weight: 600;
		margin: 0 0 var(--space-1);
	}
	.pg-confirm-body {
		font-size: var(--text-small);
		color: var(--color-ink-muted);
		margin: 0 0 var(--space-2);
	}
	.pg-confirm-actions {
		display: flex;
		gap: var(--space-2);
	}

	/* Single-column mobile stack: controls → editor → console → status (D-49-23).
	   No horizontal overflow at 320px (ROADMAP acceptance #7). */
	@media (max-width: 767px) {
		.pg {
			grid-template-columns: 1fr;
			grid-template-areas:
				'rail'
				'editor'
				'output'
				'status';
			gap: var(--space-2);
			padding: var(--space-2);
		}
		.pg-monaco {
			min-height: 240px;
		}
	}
</style>
