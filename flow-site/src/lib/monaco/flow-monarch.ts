// Hand-written Monaco Monarch tokenizer for the Flow language (RESEARCH Open Q3 / A2).
//
// Derived from the Phase 17 TextMate grammar at vscode-extension/syntaxes/flow.tmLanguage.json
// (scope `source.flow`). We DO NOT load TextMate-in-Monaco for v1 — that needs the onigasm WASM
// payload (a second multi-hundred-KB binary). A Monarch tokenizer is plain JS regex rules and
// covers the editor-only highlighting need. The static doc/Home blocks use shiki + the REAL
// grammar (Plan 49-04); this Monarch view is the derived editor-only approximation.
//
// Monaco Monarch ordering note: rules are tried top-to-bottom within a state, so chord/note/music
// literals MUST precede the bare-identifier rule, matching the TextMate `patterns` precedence
// (chords before notes before numbers before keywords/types before variable-ref).

import type { languages } from 'monaco-editor';

export const FLOW_LANGUAGE_ID = 'flow';

/** Keywords — TextMate `keyword.control.flow` + the reserved musical-context block words. */
const KEYWORDS = [
	'proc',
	'use',
	'return',
	'internal',
	'lazy',
	'fn',
	'section',
	'for',
	'while',
	'break',
	'continue',
	'in',
	'progression',
	'tempo',
	'timesig',
	'key',
	'swing',
	'voicePool',
	'tuning',
	'dynamics',
	'rit',
	'accel',
	'pickup',
	'pan',
	'gain',
	'live',
	'match',
	'enable'
];

/** Types — TextMate `storage.type.flow` (the FlowType surface). */
const TYPES = [
	'Int',
	'Float',
	'Long',
	'Double',
	'String',
	'Bool',
	'Number',
	'Note',
	'Buf',
	'Void',
	'Buffer',
	'Sequence',
	'Chord',
	'Song',
	'Section',
	'MusicalNote',
	'Beat',
	'Bar',
	'TimeSignature',
	'NoteValue',
	'Semitone',
	'Cent',
	'Millisecond',
	'Second',
	'Decibel',
	'Hertz',
	'Envelope',
	'OscillatorState',
	'Voice',
	'Track',
	'Lazy',
	'Function',
	'Tuning',
	'Sfz',
	'MarkovModel',
	'LsystemModel',
	'Dict',
	'Symbol',
	'Tuple'
];

/** The Flow Monarch language definition (Monaco `IMonarchLanguage`). */
export const flowMonarchLanguage: languages.IMonarchLanguage = {
	// `defaultToken: ''` keeps un-matched text un-highlighted rather than red.
	defaultToken: '',
	tokenPostfix: '.flow',

	keywords: KEYWORDS,
	types: TYPES,

	// Music-literal building blocks reused across rules.
	// Note literal: A-G + optional accidental + optional +/- + octave + optional duration/tie/cent.
	// Chord literal: A-G + optional accidental + a chord-quality suffix.
	// Music-numeric suffixes: ms / s / dB / st / c / b / Hz / kHz (case-insensitive on dB/hz).

	tokenizer: {
		root: [
			// Comments first (line-only in Flow).
			[/\/\/.*$/, 'comment'],
			[/^\s*;.*$/, 'comment'],

			// Strings (double-quoted with escapes).
			[/"/, { token: 'string.quote', next: '@string' }],

			// Symbols: #foo (interned identity literal).
			[/#[A-Za-z_][A-Za-z0-9_]*/, 'type.identifier'],

			// Chord literals BEFORE notes (Bb7/Cmaj7 must tokenize as a chord, not note+number).
			[/\b[A-G][#bsf]?(?:maj7|maj|min7|min|m7|m|dim|aug|sus[24]?|7|6|9|11|13)\b/, 'string'],

			// Note literals: C4, Db5, F#3, C4q., Bb6, C4+50c.
			[/\b[A-G][#bsf]?[+-]*[0-9]+(?:[qhwes]\.?~?)?(?:\+[0-9]+c)?\b/, 'attribute.value'],

			// Roman numerals in key context (I, ii, IV, V7, vi) — keep simple, leading word boundary.
			[/\b(?:i{1,3}|iv|vi{0,3}|ix|v|I{1,3}|IV|VI{0,3}|IX|V)(?:7|maj7|°|o)?\b/, 'attribute.value'],

			// Music-numeric literals with a unit suffix (440Hz, 1.5kHz, -12dB, 100ms, 2.5s, +2st, 0.5b, +50c).
			[/[+-]?[0-9]+(?:\.[0-9]+)?(?:ms|s|dB|db|st|c|b|kHz|khz|Hz|hz)\b/, 'number'],

			// Plain numbers.
			[/[+-]?[0-9]+(?:\.[0-9]+)?\b/, 'number'],

			// Booleans.
			[/\b(?:true|false)\b/, 'keyword'],

			// Note-stream / song / array brackets + tuple delimiters as punctuation operators.
			[/<<|>>/, 'operator'],
			[/[|]/, 'operator'],

			// Flow operators: -> ~> => @ and arithmetic/comparison.
			[/~>|->|=>|<=|>=|==|[-+*/<>=@]/, 'operator'],

			// Identifiers — classify against keyword / type tables (Monarch `@` cases dispatch).
			[
				/[A-Za-z_][A-Za-z0-9_]*/,
				{
					cases: {
						'@keywords': 'keyword',
						'@types': 'type',
						'@default': 'identifier'
					}
				}
			],

			// Delimiters / whitespace.
			[/[()[\]{}]/, '@brackets'],
			[/[ \t\r\n]+/, 'white']
		],

		string: [
			[/[^\\"]+/, 'string'],
			[/\\./, 'string.escape'],
			[/"/, { token: 'string.quote', next: '@pop' }]
		]
	}
};

/** Bracket / autoclose / comment configuration for the Flow editor language. */
export const flowLanguageConfiguration: languages.LanguageConfiguration = {
	comments: { lineComment: '//' },
	brackets: [
		['(', ')'],
		['[', ']'],
		['{', '}']
	],
	autoClosingPairs: [
		{ open: '(', close: ')' },
		{ open: '[', close: ']' },
		{ open: '{', close: '}' },
		{ open: '"', close: '"' }
	],
	surroundingPairs: [
		{ open: '(', close: ')' },
		{ open: '[', close: ']' },
		{ open: '{', close: '}' },
		{ open: '"', close: '"' }
	]
};
