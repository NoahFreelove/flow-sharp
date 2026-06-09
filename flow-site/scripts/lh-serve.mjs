// Production-accurate static server for the Lighthouse CI gate (Plan 49-08, D-49-31).
//
// WHY THIS EXISTS: `vite preview` serves the built output UNCOMPRESSED and with no cache
// headers. Cloudflare Pages (the production host) serves every text/JS/CSS/WASM asset
// brotli-compressed with long-lived `immutable` cache TTLs. On the /playground route — which
// ships the ~5.4 MB Phase 48 Mono-WASM runtime (lazy-loaded in onMount per D-49-34) — the
// uncompressed `vite preview` measurement penalises Performance by ~11 points purely as a
// dev-server artifact (Lighthouse "Enable text compression" alone is a ~2.4 MB saving).
//
// This server mimics how Cloudflare Pages actually serves the AppBundle:
//   - brotli / gzip content negotiation on compressible types (incl. .wasm)
//   - `cache-control: public, max-age=31536000, immutable` for hashed/immutable assets
//   - SPA fallback to index.html for the client-only /playground route (prerender=false)
// so `lhci autorun` measures the PRODUCTION condition, not a dev-server straw man. The
// four-axis ≥0.9 bar (ROADMAP AC-6, locked D-49-31) is asserted against this, unconditionally.
//
// Serves the adapter-cloudflare output at .svelte-kit/cloudflare (run `pnpm build` first).
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..', '.svelte-kit', 'cloudflare');
const PORT = Number(process.env.LH_SERVE_PORT ?? 4182);

const MIME = {
	'.html': 'text/html',
	'.js': 'text/javascript',
	'.css': 'text/css',
	'.json': 'application/json',
	'.wasm': 'application/wasm',
	'.woff2': 'font/woff2',
	'.svg': 'image/svg+xml',
	'.png': 'image/png',
	'.webp': 'image/webp',
	'.avif': 'image/avif',
	'.wav': 'audio/wav',
	'.map': 'application/json',
	'.txt': 'text/plain',
	'.symbols': 'application/octet-stream',
	'.dat': 'application/octet-stream'
};
// Mirrors the Cloudflare default compressible set (text-ish + wasm).
const COMPRESSIBLE = new Set(['.html', '.js', '.css', '.json', '.wasm', '.svg', '.txt', '.map', '.symbols']);

function resolveFile(urlPath) {
	let fp = path.join(ROOT, urlPath);
	if (urlPath.endsWith('/')) fp = path.join(fp, 'index.html');
	if (!fs.existsSync(fp) || fs.statSync(fp).isDirectory()) {
		if (fs.existsSync(fp + '.html')) return fp + '.html';
		if (fs.existsSync(path.join(fp, 'index.html'))) return path.join(fp, 'index.html');
		// SPA fallback for client-only routes (e.g. /playground — prerender=false, ssr=false).
		return path.join(ROOT, 'index.html');
	}
	return fp;
}

const server = http.createServer((req, res) => {
	const urlPath = decodeURIComponent((req.url ?? '/').split('?')[0].split('#')[0]);
	const fp = resolveFile(urlPath);
	if (!fs.existsSync(fp)) {
		res.writeHead(404, { 'content-type': 'text/plain' });
		res.end('not found');
		return;
	}
	const ext = path.extname(fp);
	const immutable =
		urlPath.includes('/immutable/') ||
		urlPath.includes('/_framework/') ||
		urlPath.startsWith('/fonts/') ||
		urlPath.startsWith('/textures/');
	const headers = {
		'content-type': MIME[ext] ?? 'application/octet-stream',
		'cache-control': immutable ? 'public, max-age=31536000, immutable' : 'public, max-age=3600',
		'x-content-type-options': 'nosniff'
	};
	let buf = fs.readFileSync(fp);
	const accept = req.headers['accept-encoding'] ?? '';
	if (COMPRESSIBLE.has(ext)) {
		if (/\bbr\b/.test(accept)) {
			buf = zlib.brotliCompressSync(buf);
			headers['content-encoding'] = 'br';
		} else if (/\bgzip\b/.test(accept)) {
			buf = zlib.gzipSync(buf);
			headers['content-encoding'] = 'gzip';
		}
		headers['vary'] = 'Accept-Encoding';
	}
	headers['content-length'] = buf.length;
	res.writeHead(200, headers);
	res.end(buf);
});

server.listen(PORT, () => {
	// startServerReadyPattern matches "lh-serve ready" in lighthouserc.cjs.
	console.log(`lh-serve ready on http://localhost:${PORT} (serving ${path.relative(process.cwd(), ROOT)})`);
});
