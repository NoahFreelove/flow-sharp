/**
 * Regression test for the static-examples refactor: the playground's example sources moved out
 * of this module into `static/examples/*.flow`, loaded at runtime via `static/examples/manifest.json`.
 *
 * This test pins the on-disk contract the playground page depends on:
 *  1. manifest.json parses to a non-empty array;
 *  2. every entry has id/label/blurb/file (all non-empty strings);
 *  3. DEFAULT_SNIPPET_ID is present in the manifest;
 *  4. each referenced `file` exists under static/examples/ and is non-empty;
 *  5. the new ragtime entry is present.
 *
 * Reads files via node fs against the repo's static/examples/ dir (the same files CF Pages serves).
 */

import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';
import { DEFAULT_SNIPPET_ID } from './snippets';

const __dirname = dirname(fileURLToPath(import.meta.url));
// src/lib/playground → repo root is three levels up; static/examples lives off the project root.
const EXAMPLES_DIR = resolve(__dirname, '../../../static/examples');
const MANIFEST_PATH = resolve(EXAMPLES_DIR, 'manifest.json');

interface SnippetMeta {
	id: string;
	label: string;
	blurb: string;
	file: string;
}

function loadManifest(): SnippetMeta[] {
	return JSON.parse(readFileSync(MANIFEST_PATH, 'utf8'));
}

describe('static/examples manifest', () => {
	it('manifest.json exists and parses to a non-empty array', () => {
		expect(existsSync(MANIFEST_PATH), 'manifest.json should exist').toBe(true);
		const manifest = loadManifest();
		expect(Array.isArray(manifest)).toBe(true);
		expect(manifest.length).toBeGreaterThan(0);
	});

	it('every entry has non-empty id/label/blurb/file', () => {
		for (const entry of loadManifest()) {
			for (const key of ['id', 'label', 'blurb', 'file'] as const) {
				expect(typeof entry[key], `${key} should be a string`).toBe('string');
				expect(entry[key].length, `${key} should be non-empty`).toBeGreaterThan(0);
			}
		}
	});

	it('entry ids are unique', () => {
		const ids = loadManifest().map((e) => e.id);
		expect(new Set(ids).size).toBe(ids.length);
	});

	it(`DEFAULT_SNIPPET_ID (${DEFAULT_SNIPPET_ID}) is present in the manifest`, () => {
		const ids = loadManifest().map((e) => e.id);
		expect(ids).toContain(DEFAULT_SNIPPET_ID);
	});

	it('each referenced .flow file exists under static/examples/ and is non-empty', () => {
		for (const entry of loadManifest()) {
			const filePath = resolve(EXAMPLES_DIR, entry.file);
			expect(existsSync(filePath), `${entry.file} should exist`).toBe(true);
			const body = readFileSync(filePath, 'utf8');
			expect(body.length, `${entry.file} should be non-empty`).toBeGreaterThan(0);
		}
	});

	it('the ragtime example is present and points at ragtime.flow', () => {
		const ragtime = loadManifest().find((e) => e.id === 'ragtime');
		expect(ragtime, 'a ragtime entry should exist').toBeDefined();
		expect(ragtime?.file).toBe('ragtime.flow');
		// Label intentionally not pinned — it's freely curated copy, not a structural contract.
	});
});
