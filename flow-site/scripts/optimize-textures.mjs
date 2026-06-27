// optimize-textures.mjs — wood-grain raster pipeline (D-49-18, D-49-32).
//
// The skeuo design system uses inline-SVG <feTurbulence> for brushed-metal / paper /
// felt overlays (smaller, scalable, inlinable). Wood grain is the ONE surface SVG can't
// fake convincingly (D-49-18), so it ships as a Sharp-optimized raster in three formats:
// AVIF (≥80% quality per D-49-32) + WebP + PNG fallback, consumed by `.surface-wood`.
//
// The wood-grain SOURCE is generated procedurally (deterministic — same bytes every run, so
// `tests/visual.spec.ts` baselines stay stable) from a hand-built SVG: a walnut base gradient
// + many vertical grain striations + a turbulence displacement, rasterized by Sharp's SVG
// loader (librsvg). No binary source asset is committed; the script is the source of truth.
//
// Run:  node scripts/optimize-textures.mjs   (also invoked from `pnpm build` via prebuild)

import sharp from 'sharp';
import { mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const OUT_DIR = join(__dirname, '..', 'static', 'textures');

// Tileable wood-grain panel. 512×512 tiles vertically for rails (D-49-18 "tiled vertically").
const W = 512;
const H = 512;

// Walnut palette from tokens (D-49-17): walnut #5C3A21, walnut-soft #7A5235, ink-edge #3A2414.
function buildWoodGrainSvg() {
	// Deterministic vertical striations — fixed seed positions, no Math.random (keeps bytes stable).
	const lines = [];
	let seed = 0x9e3779b9 >>> 0; // golden-ratio constant — deterministic LCG, NOT a PRNG-sanctioned site
	const next = () => {
		// xorshift32 — pure-deterministic, same sequence every run
		seed ^= seed << 13;
		seed ^= seed >>> 17;
		seed ^= seed << 5;
		seed >>>= 0;
		return seed / 0xffffffff;
	};
	for (let i = 0; i < 90; i++) {
		const x = next() * W;
		const width = 0.5 + next() * 2.5;
		const opacity = 0.04 + next() * 0.16;
		// Slight horizontal wander gives the grain an organic, not-ruled feel.
		const sway = (next() - 0.5) * 14;
		const tone = next() > 0.5 ? '#3A2414' : '#7A5235';
		lines.push(
			`<path d="M ${x.toFixed(1)} 0 C ${(x + sway).toFixed(1)} ${(H / 3).toFixed(1)}, ` +
				`${(x - sway).toFixed(1)} ${((2 * H) / 3).toFixed(1)}, ${x.toFixed(1)} ${H}" ` +
				`stroke="${tone}" stroke-width="${width.toFixed(2)}" fill="none" ` +
				`opacity="${opacity.toFixed(3)}" stroke-linecap="round"/>`
		);
	}

	return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
  <defs>
    <linearGradient id="walnut" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0"   stop-color="#4A2E1A"/>
      <stop offset="0.5" stop-color="#5C3A21"/>
      <stop offset="1"   stop-color="#4A2E1A"/>
    </linearGradient>
    <filter id="grain">
      <feTurbulence type="fractalNoise" baseFrequency="0.012 0.18" numOctaves="3" seed="7" result="noise"/>
      <feColorMatrix in="noise" type="matrix"
        values="0 0 0 0 0.36  0 0 0 0 0.23  0 0 0 0 0.13  0 0 0 0.22 0" result="tinted"/>
      <feComposite in="tinted" in2="SourceGraphic" operator="over"/>
    </filter>
  </defs>
  <rect width="${W}" height="${H}" fill="url(#walnut)"/>
  <g filter="url(#grain)"><rect width="${W}" height="${H}" fill="transparent"/></g>
  <g>${lines.join('')}</g>
  <!-- soft top bevel-light + bottom shadow so tiled rails read as carved panels -->
  <rect width="${W}" height="${H}" fill="url(#walnut)" opacity="0.0"/>
  <linearGradient id="bevel" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="#7A5235" stop-opacity="0.18"/>
    <stop offset="0.06" stop-color="#7A5235" stop-opacity="0"/>
    <stop offset="0.94" stop-color="#000000" stop-opacity="0"/>
    <stop offset="1" stop-color="#1C140E" stop-opacity="0.22"/>
  </linearGradient>
  <rect width="${W}" height="${H}" fill="url(#bevel)"/>
</svg>`;
}

async function main() {
	await mkdir(OUT_DIR, { recursive: true });
	const svg = Buffer.from(buildWoodGrainSvg());
	const base = sharp(svg, { density: 144 }).resize(W, H, { fit: 'fill' });

	// PNG fallback (lossless).
	await base.clone().png({ compressionLevel: 9 }).toFile(join(OUT_DIR, 'wood-grain.png'));
	// WebP.
	await base.clone().webp({ quality: 86 }).toFile(join(OUT_DIR, 'wood-grain.webp'));
	// AVIF — D-49-32 floor is 80%; ship at 82% for the grain-detail the skeuo look depends on.
	await base.clone().avif({ quality: 82 }).toFile(join(OUT_DIR, 'wood-grain.avif'));

	// eslint-disable-next-line no-console
	console.log('[textures] wrote wood-grain.{avif,webp,png} to static/textures/');
}

main().catch((err) => {
	// eslint-disable-next-line no-console
	console.error('[textures] generation failed:', err);
	process.exit(1);
});
