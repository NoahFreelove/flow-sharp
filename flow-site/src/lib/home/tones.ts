// Web Audio "press play to hear" helper for the iOS-6 home page.
//
// Ported from the Claude Design handoff `flow.js`. These are real oscillator tones so the landing
// Play buttons make sound WITHOUT booting the full Phase-48 WASM runtime (that's the /playground's
// job). Browser-only: the AudioContext is created lazily on the first user gesture (autoplay-safe),
// so importing this module during SSR/prerender is inert until a button is actually clicked.

export type ToneType = OscillatorType; // 'sine' | 'square' | 'triangle' | 'sawtooth'

interface SeqEvent {
	/** frequency in Hz */
	f: number;
	/** duration in seconds */
	d: number;
	/** offset from sequence start, seconds */
	t?: number;
	/** peak gain */
	g?: number;
}

let ctx: AudioContext | null = null;
function audio(): AudioContext {
	if (!ctx) {
		const Ctor: typeof AudioContext =
			window.AudioContext ?? (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
		ctx = new Ctor();
	}
	// A user-gesture click may still find the context suspended (Safari/iOS) — best-effort resume.
	if (ctx.state === 'suspended') void ctx.resume();
	return ctx;
}

/** Schedule a short sequence of enveloped oscillator tones. Returns the sequence length in seconds. */
function playSeq(seq: SeqEvent[], type: ToneType = 'triangle'): number {
	const c = audio();
	const t0 = c.currentTime + 0.04;
	for (const ev of seq) {
		const o = c.createOscillator();
		const g = c.createGain();
		o.type = type;
		o.frequency.value = ev.f;
		const start = t0 + (ev.t ?? 0);
		const end = start + ev.d;
		g.gain.setValueAtTime(0.0001, start);
		g.gain.exponentialRampToValueAtTime(ev.g ?? 0.22, start + 0.012);
		g.gain.exponentialRampToValueAtTime(0.0001, end);
		o.connect(g);
		g.connect(c.destination);
		o.start(start);
		o.stop(end + 0.02);
	}
	return seq.reduce((m, e) => Math.max(m, (e.t ?? 0) + e.d), 0);
}

const SEMITONE: Record<string, number> = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 };

/** Note name (e.g. "C4", "F#5", "Db4") → frequency in Hz (A4 = 440). */
export function noteFreq(name: string): number {
	const m = name.match(/^([A-G])([#b]?)(\d)$/);
	if (!m) return 440;
	const accidental = m[2] === '#' ? 1 : m[2] === 'b' ? -1 : 0;
	const semis = SEMITONE[m[1]] + accidental + (parseInt(m[3], 10) - 4) * 12 - 9;
	return 440 * Math.pow(2, semis / 12);
}

/** A single sustained tone. */
export function playTone(f = 440, d = 0.7, type: ToneType = 'triangle'): number {
	return playSeq([{ f, d }], type);
}

/** A melody — notes played in sequence. */
export function playMelody(notes: string[], dur = 0.32, type: ToneType = 'triangle'): number {
	return playSeq(
		notes.map((nm, i) => ({ f: noteFreq(nm), d: dur * 0.9, t: i * dur })),
		type
	);
}

/** A chord — notes struck together. */
export function playChord(notes: string[], dur = 1.1, type: ToneType = 'triangle'): number {
	return playSeq(
		notes.map((nm) => ({ f: noteFreq(nm), d: dur, t: 0, g: 0.14 })),
		type
	);
}
