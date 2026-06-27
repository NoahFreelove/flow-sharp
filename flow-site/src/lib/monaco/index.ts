// Monaco editor setup — imported ONLY from `onMount` on the playground route (RESEARCH Pitfall 1).
// Monaco touches `self`/`window` at import time and SSR-crashes if loaded during prerender, so the
// playground page (`ssr = false`) dynamic-imports this module inside `onMount`.
//
// Worker wiring (RESEARCH Pattern 3): a single custom language (Flow) needs only the BASE
// editor.worker — no TS/JSON/CSS/HTML language workers. The `?worker` Vite suffix self-hosts the
// worker (no CDN — keeps the _headers CSP tight) and registers it as a module worker (Firefox
// needs `type: 'module'`, which `?worker` handles).

import * as monaco from 'monaco-editor';
import EditorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';
import {
	FLOW_LANGUAGE_ID,
	flowLanguageConfiguration,
	flowMonarchLanguage
} from './flow-monarch';

// Flow has no JSON/TS workers — the base editor worker is the only one needed.
self.MonacoEnvironment = {
	getWorker: () => new EditorWorker()
};

let flowRegistered = false;

/** Register the `flow` language id + the Monarch tokenizer + the slate editor theme (idempotent). */
function registerFlowLanguage(): void {
	if (flowRegistered) return;
	flowRegistered = true;

	monaco.languages.register({ id: FLOW_LANGUAGE_ID, aliases: ['Flow', 'flow'], extensions: ['.flow'] });
	monaco.languages.setMonarchTokensProvider(FLOW_LANGUAGE_ID, flowMonarchLanguage);
	monaco.languages.setLanguageConfiguration(FLOW_LANGUAGE_ID, flowLanguageConfiguration);

	// A slate-chrome theme tuned to the UI-SPEC --color-slate code background. Token colours are
	// mapped from the Monarch token names above (chord=teal, note=amber, keyword=violet, etc.).
	monaco.editor.defineTheme('flow-slate', {
		base: 'vs-dark',
		inherit: true,
		rules: [
			{ token: 'comment', foreground: '7d828c', fontStyle: 'italic' },
			{ token: 'string', foreground: '7fd1b9' },
			{ token: 'string.quote', foreground: '7fd1b9' },
			{ token: 'string.escape', foreground: 'd9a45b' },
			{ token: 'keyword', foreground: 'c792ea' },
			{ token: 'type', foreground: '82aaff' },
			{ token: 'type.identifier', foreground: 'f78c6c' },
			{ token: 'number', foreground: 'f7c873' },
			{ token: 'attribute.value', foreground: 'e5b567' }, // note literals
			{ token: 'operator', foreground: '89ddff' },
			{ token: 'identifier', foreground: 'd6deeb' }
		],
		colors: {
			'editor.background': '#2C2E33', // --color-slate (light theme code bg)
			'editor.foreground': '#d6deeb',
			'editorLineNumber.foreground': '#5a5d66',
			'editorLineNumber.activeForeground': '#c5a572',
			'editor.selectionBackground': '#3d5a80',
			'editorCursor.foreground': '#c5a572'
		}
	});
}

export interface CreateFlowEditorOptions {
	/** Initial source loaded into the editor. */
	value?: string;
	/** Read-only mode (mobile <768px, D-49-09). */
	readOnly?: boolean;
}

/**
 * Create a Monaco editor instance configured for Flow. Caller owns disposal of the returned editor.
 * MUST be invoked only in the browser (this whole module is dynamic-imported from `onMount`).
 */
export function createFlowEditor(
	container: HTMLElement,
	opts: CreateFlowEditorOptions = {}
): monaco.editor.IStandaloneCodeEditor {
	registerFlowLanguage();

	return monaco.editor.create(container, {
		value: opts.value ?? '',
		language: FLOW_LANGUAGE_ID,
		theme: 'flow-slate',
		readOnly: opts.readOnly ?? false,
		fontFamily: "'JetBrains Mono', ui-monospace, monospace",
		fontSize: 14,
		lineHeight: 22,
		lineNumbers: 'on',
		minimap: { enabled: false },
		scrollBeyondLastLine: false,
		automaticLayout: true, // re-layout on container resize (responsive columns)
		tabSize: 2,
		renderWhitespace: 'none',
		padding: { top: 12, bottom: 12 },
		fixedOverflowWidgets: true
	});
}

export { FLOW_LANGUAGE_ID };
