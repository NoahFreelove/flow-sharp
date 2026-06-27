<!--
  TEST-ONLY fixture (WR-05). Mirrors the EXACT error-box rendering pattern from
  playground/+page.svelte — `{errorHeading(err)}` heading + `{err.sourceSnippet}` inside a <pre> —
  so a unit test can prove the untrusted RunError fields render as ESCAPED text (Svelte curly
  interpolation), never live HTML. If anyone ever swaps a sink here for {@html}, the test goes red.

  This fixture is NOT shipped UI; it exists only to make the +page.svelte escaping contract testable
  without booting Monaco + the WASM runtime. The interpolation pattern is kept byte-faithful to the
  page so the test reflects real behaviour.
-->
<script lang="ts">
	import type { RunError } from '$lib/runtime';

	let { errors }: { errors: RunError[] } = $props();

	// Same helper shape as +page.svelte (auto-escaped at the sink below).
	function errorHeading(err: RunError): string {
		return `✕ ${err.kind}: ${err.message}`;
	}
</script>

<section class="pg-errors" data-testid="errors" aria-label="Errors">
	{#each errors as err, i (i)}
		<div class="pg-error">
			<p class="pg-error-head">{errorHeading(err)}</p>
			{#if err.sourceSnippet}
				<pre class="pg-error-snippet">│ {err.sourceSnippet}</pre>
			{/if}
		</div>
	{/each}
</section>
