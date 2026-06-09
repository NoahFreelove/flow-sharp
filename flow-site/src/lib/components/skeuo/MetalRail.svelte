<!--
  <MetalRail> — brushed-aluminum side decoration (UI-SPEC item 5).
  Variants: left, right (brush-direction / light source mirrored). Purely decorative
  rack-rail trim. Static, no interaction. A11y: aria-hidden — never holds content or focus.
-->
<script lang="ts">
	let { side = 'left' }: { side?: 'left' | 'right' } = $props();
</script>

<div class="skeuo-rail skeuo-rail--{side} surface-brushed-metal" aria-hidden="true">
	<span class="skeuo-rail__bolt skeuo-rail__bolt--top"></span>
	<span class="skeuo-rail__bolt skeuo-rail__bolt--bottom"></span>
</div>

<style>
	.skeuo-rail {
		position: relative;
		width: 14px;
		min-height: 64px;
		height: 100%;
		border-radius: var(--radius-2);
		box-shadow: var(--bevel-light), var(--shadow-1);
	}
	/* mirror the light source between left/right rails */
	.skeuo-rail--right {
		transform: scaleX(-1);
	}

	.skeuo-rail__bolt {
		position: absolute;
		left: 50%;
		width: 6px;
		height: 6px;
		transform: translateX(-50%);
		border-radius: 50%;
		background-image: radial-gradient(circle at 35% 30%, #d4d4d4, #6a6a6a);
		box-shadow: inset 0 0 0 1px rgba(0, 0, 0, 0.35);
	}
	.skeuo-rail__bolt--top {
		top: 8px;
	}
	.skeuo-rail__bolt--bottom {
		bottom: 8px;
	}
</style>
