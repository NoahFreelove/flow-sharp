#if !FLOW_WEB
using System;
using System.Collections.Generic;
using FlowLang.Audio;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Notation;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Midi;

/// <summary>
/// Phase 40 MIDI-RT-01/02/04 (D-40-01/02) — registration entry point for the
/// <c>@midi</c> stdlib surface. Ships the high-level <c>midiOut</c> path + the
/// low-level event escape hatch:
/// <c>midiPorts</c> / <c>openMidiOutput</c> / <c>midiOut</c> / <c>midiNoteOn</c> /
/// <c>midiNoteOff</c> / <c>midiCC</c> / <c>midiSysex</c> + the
/// <c>__enableMidiModule</c> marker.
///
/// <para>All surface builtins gate on <see cref="ExecutionContext.MidiEnabled"/>
/// (flipped by the trailing <c>(__enableMidiModule)</c> in <c>flow-lang/midi.flow</c>),
/// mirroring <see cref="FlowLang.StandardLibrary.Network.OscFunctions"/>.</para>
///
/// <para><b>Input validation (T-40-01 / T-40-04, Security V5):</b> channel is
/// clamped 0..15, pitch/velocity/CC 0..127, sysex length-capped — clamp +
/// WarnOnce, NEVER throwing to native (Phase 44 input-perimeter precedent). The
/// charitable rule: an absent device / dead handle degrades to a quiet no-op.</para>
///
/// <para><b>GM routing (D-40-02):</b> <c>midiOut</c> resolves each sequence to a
/// (GM program, channel) via <see cref="InstrumentRouting.ResolveGmProgram"/>
/// VERBATIM — the SAME table as <c>writeMidi</c> — so a hardware port sounds
/// identical to the exported <c>.mid</c>. The named-arg <c>overrides=</c> Dict
/// (name→channel) layers on top; the program still derives from the name.</para>
///
/// <para><c>#if !FLOW_WEB</c> — Compile-Removed on Web (T-40-03).</para>
/// </summary>
public static class MidiFunctions
{
    /// <summary>Max sysex byte length accepted at the builtin boundary (T-40-04
    /// DoS guard). Generous enough for real device dumps (64 KiB) but bounded so
    /// a pathological array never reaches native unbounded.</summary>
    private const int SysexMaxBytes = 65536;

    /// <summary>
    /// Shared per-process MIDI backend manager. Lazily detected; falls back to
    /// <see cref="NullMidiBackend"/> charitably when <c>librtmidi.so</c> is absent.
    /// </summary>
    private static readonly MidiPlaybackManager _manager = new();

    /// <summary>
    /// Test-only seam: when non-null, <see cref="GetBackend"/> returns this
    /// instead of the real manager-selected backend, so VirtualMidiTests +
    /// Plan 02 clock tests can inject a <c>CaptureMidiBackend</c> with NO real
    /// ALSA. Always restore to null in test teardown.
    /// </summary>
    public static IMidiBackend? BackendOverride { get; set; }

    private static IMidiBackend GetBackend() => BackendOverride ?? _manager.GetBackend();

    /// <summary>
    /// Wire the @midi builtins into the registry. Idempotent — called once per
    /// <see cref="FlowLang.Core.FlowEngine"/> at construction (inside the
    /// <c>#if !FLOW_WEB</c> guard at the OSC register site).
    /// </summary>
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // ----- Marker: __enableMidiModule -----
        var sigMarker = new FunctionSignature("__enableMidiModule", System.Array.Empty<FlowType>());
        registry.Register("__enableMidiModule", sigMarker, _ =>
        {
            context.MidiEnabled = true;
            return Value.Void();
        });

        // ----- midiPorts() -> (returns Void; port names go to stdout) -----
        // Kept charitable + side-effect-light: prints discovered ports and
        // returns Void. On a lib-absent box this prints nothing and never throws.
        var sigPorts = new FunctionSignature("midiPorts", System.Array.Empty<FlowType>());
        registry.Register("midiPorts", sigPorts, _ =>
        {
            RequireModuleActivated(context, "midiPorts");
            var ports = GetBackend().ListPorts();
            foreach (var p in ports) Console.WriteLine(p);
            return Value.Void();
        });

        // ----- openMidiOutput(String port) -> MidiDevice -----
        var sigOpen = new FunctionSignature("openMidiOutput",
            new FlowType[] { StringType.Instance },
            ParameterNames: new[] { "port" });
        registry.Register("openMidiOutput", sigOpen, args =>
        {
            RequireModuleActivated(context, "openMidiOutput");
            string port = args[0].As<string>();
            var handle = GetBackend().OpenOutput(port);   // null = charitable dead handle
            if (handle == null)
            {
                RenderingDiagnostics.WarnOnce(
                    $"midi-open-dead:{port}",
                    $"[midi] openMidiOutput('{port}') — no such port (or librtmidi.so absent); returning a dead handle");
            }
            return Value.MidiDevice(new MidiDeviceData { PortName = port, Handle = handle });
        });

        // ----- midiNoteOn(MidiDevice dev, Int ch, Int pitch, Int vel) -> Void -----
        var sigNoteOn = new FunctionSignature("midiNoteOn",
            new FlowType[] { MidiDeviceType.Instance, IntType.Instance, IntType.Instance, IntType.Instance },
            ParameterNames: new[] { "dev", "ch", "pitch", "vel" });
        registry.Register("midiNoteOn", sigNoteOn, args =>
        {
            RequireModuleActivated(context, "midiNoteOn");
            var dev = args[0].As<MidiDeviceData>();
            int ch = ClampChannel(args[1].As<int>(), "midiNoteOn");
            int pitch = Clamp7Bit(args[2].As<int>(), "midiNoteOn", "pitch");
            int vel = Clamp7Bit(args[3].As<int>(), "midiNoteOn", "velocity");
            dev.Handle?.SendNoteOn(ch, pitch, vel);
            return Value.Void();
        });

        // ----- midiNoteOff(MidiDevice dev, Int ch, Int pitch) -> Void -----
        var sigNoteOff = new FunctionSignature("midiNoteOff",
            new FlowType[] { MidiDeviceType.Instance, IntType.Instance, IntType.Instance },
            ParameterNames: new[] { "dev", "ch", "pitch" });
        registry.Register("midiNoteOff", sigNoteOff, args =>
        {
            RequireModuleActivated(context, "midiNoteOff");
            var dev = args[0].As<MidiDeviceData>();
            int ch = ClampChannel(args[1].As<int>(), "midiNoteOff");
            int pitch = Clamp7Bit(args[2].As<int>(), "midiNoteOff", "pitch");
            dev.Handle?.SendNoteOff(ch, pitch);
            return Value.Void();
        });

        // ----- midiCC(MidiDevice dev, Int ch, Int ctrl, Int val) -> Void -----
        var sigCC = new FunctionSignature("midiCC",
            new FlowType[] { MidiDeviceType.Instance, IntType.Instance, IntType.Instance, IntType.Instance },
            ParameterNames: new[] { "dev", "ch", "ctrl", "val" });
        registry.Register("midiCC", sigCC, args =>
        {
            RequireModuleActivated(context, "midiCC");
            var dev = args[0].As<MidiDeviceData>();
            int ch = ClampChannel(args[1].As<int>(), "midiCC");
            int ctrl = Clamp7Bit(args[2].As<int>(), "midiCC", "controller");
            int val = Clamp7Bit(args[3].As<int>(), "midiCC", "value");
            dev.Handle?.SendControlChange(ch, ctrl, val);
            return Value.Void();
        });

        // ----- midiSysex(MidiDevice dev, Buffer data) -> Void -----
        var sigSysex = new FunctionSignature("midiSysex",
            new FlowType[] { MidiDeviceType.Instance, BufferType.Instance },
            ParameterNames: new[] { "dev", "data" });
        registry.Register("midiSysex", sigSysex, args =>
        {
            RequireModuleActivated(context, "midiSysex");
            var dev = args[0].As<MidiDeviceData>();
            var bytes = BufferToSysexBytes(args[1]);
            dev.Handle?.SendSysex(bytes);
            return Value.Void();
        });

        // ----- midiOut(Song song, String port [, Dict overrides]) -> Void -----
        //
        // CR-02: register TWO overloads per input shape so the documented
        // `overrides=` named-arg is actually callable. The original single
        // signature registered 2 InputTypes but 3 ParameterNames — an arity
        // mismatch the OverloadResolver rejects (FunctionSignature.Matches +
        // OverloadResolver.cs:259). The 2-arg form keeps `(midiOut song "port")`
        // fast; the 3-arg form (DictType slot) makes
        // `(midiOut song "port" overrides=(dict ...))` resolve and reach args[2].
        // The override Dict is wildcard-typed (Dict<Void,Void>) so any concrete
        // Dict<String,Int> binds — same posture as the @std dict-side ops.
        var dictWildcard = new DictType(VoidType.Instance, VoidType.Instance);

        var sigOutSong2 = new FunctionSignature("midiOut",
            new FlowType[] { SongType.Instance, StringType.Instance },
            ParameterNames: new[] { "song", "port" });
        registry.Register("midiOut", sigOutSong2, args =>
        {
            RequireModuleActivated(context, "midiOut");
            var song = args[0].As<SongData>();
            string port = args[1].As<string>();
            MidiOutSong(song, port, overrides: null);
            return Value.Void();
        });

        var sigOutSong3 = new FunctionSignature("midiOut",
            new FlowType[] { SongType.Instance, StringType.Instance, dictWildcard },
            ParameterNames: new[] { "song", "port", "overrides" });
        registry.Register("midiOut", sigOutSong3, args =>
        {
            RequireModuleActivated(context, "midiOut");
            var song = args[0].As<SongData>();
            string port = args[1].As<string>();
            // Charitable: a non-Dict / empty override falls back to pure GM routing.
            var overrides = ReadOverrides(args[2]);
            MidiOutSong(song, port, overrides);
            return Value.Void();
        });

        // ----- midiOut(Sequence seq, String port [, Dict overrides]) -> Void -----
        var sigOutSeq2 = new FunctionSignature("midiOut",
            new FlowType[] { SequenceType.Instance, StringType.Instance },
            ParameterNames: new[] { "seq", "port" });
        registry.Register("midiOut", sigOutSeq2, args =>
        {
            RequireModuleActivated(context, "midiOut");
            var seq = args[0].As<SequenceData>();
            string port = args[1].As<string>();
            // A bare sequence has no name — default routing (piano/ch0).
            MidiOutBareSequence(seq, port, overrides: null);
            return Value.Void();
        });

        var sigOutSeq3 = new FunctionSignature("midiOut",
            new FlowType[] { SequenceType.Instance, StringType.Instance, dictWildcard },
            ParameterNames: new[] { "seq", "port", "overrides" });
        registry.Register("midiOut", sigOutSeq3, args =>
        {
            RequireModuleActivated(context, "midiOut");
            var seq = args[0].As<SequenceData>();
            string port = args[1].As<string>();
            var overrides = ReadOverrides(args[2]);
            // A bare sequence has no name; the override Dict keys on "" for a
            // bare sequence (composer can remap the default slot).
            MidiOutBareSequence(seq, port, overrides);
            return Value.Void();
        });
    }

    /// <summary>
    /// CR-03 path for a bare (un-named) Sequence: open the port, schedule its notes
    /// on the default-tempo wall-clock timeline (no section context, so default
    /// 120 BPM), and dispatch. Charitable on a dead handle.
    /// </summary>
    private static void MidiOutBareSequence(SequenceData seq, string port, Dictionary<string, int>? overrides)
    {
        var handle = GetBackend().OpenOutput(port);
        if (handle == null)
        {
            RenderingDiagnostics.WarnOnce(
                $"midi-out-dead:{port}",
                $"[midi] midiOut('{port}') — no such port (or librtmidi.so absent); nothing sent");
            return;
        }
        var usedChannels = new HashSet<int>();
        var events = new List<ScheduledEvent>();
        long seqCounter = 0;
        double lenMs = ScheduleOneSequence(events, ref seqCounter, 0.0, DefaultBpm, "", seq, overrides, usedChannels);
        AppendAllNotesOff(events, ref seqCounter, lenMs, usedChannels);
        // Audit §5.6 — the native output device + ALSA port leaked on every call
        // (handle never closed; no finalizer on RtMidiOutputHandle). Close in a
        // finally; the All-Notes-Off CC123 events appended above flush stuck
        // notes as part of the dispatched timeline before the handle is freed.
        try { DispatchScheduled(handle, events); }
        finally { try { handle.Close(); } catch { /* idempotent best-effort */ } }
    }

    /// <summary>
    /// Audit §5.6 — append an All-Notes-Off (CC123 value 0) event per used channel
    /// at <paramref name="atMs"/> (after the last note), so the dispatched timeline
    /// releases every voice before the handle is closed. Routing these as
    /// scheduled events (rather than a side send) keeps them in the same
    /// time-sorted dispatch the timing seam reports.
    /// </summary>
    private static void AppendAllNotesOff(
        List<ScheduledEvent> events, ref long seqCounter, double atMs, HashSet<int> usedChannels)
    {
        foreach (int ch in usedChannels)
        {
            int c = ch;
            events.Add(new ScheduledEvent(atMs, seqCounter++, h => h.SendControlChange(c, 123, 0)));
        }
    }

    private static void RequireModuleActivated(FlowLang.Runtime.ExecutionContext context, string builtinName)
    {
        if (!context.MidiEnabled)
            throw new System.InvalidOperationException($"{builtinName} requires `use \"@midi\"`");
    }

    // ===== Input validation (T-40-01 / T-40-04, Security V5 — clamp + WarnOnce, never throw) =====

    private static int ClampChannel(int raw, string builtin)
    {
        if (raw < 0 || raw > 15)
        {
            int c = raw < 0 ? 0 : 15;
            RenderingDiagnostics.WarnOnce(
                $"midi-clamp-ch:{builtin}",
                $"[midi] {builtin}: channel {raw} out of range 0..15 — clamped to {c}");
            return c;
        }
        return raw;
    }

    private static int Clamp7Bit(int raw, string builtin, string what)
    {
        if (raw < 0 || raw > 127)
        {
            int v = raw < 0 ? 0 : 127;
            RenderingDiagnostics.WarnOnce(
                $"midi-clamp-{what}:{builtin}",
                $"[midi] {builtin}: {what} {raw} out of range 0..127 — clamped to {v}");
            return v;
        }
        return raw;
    }

    /// <summary>SysEx framing bytes — every valid System Exclusive message is
    /// <c>0xF0 &lt;data...&gt; 0xF7</c>.</summary>
    private const byte SysexStart = 0xF0;
    private const byte SysexEnd = 0xF7;

    /// <summary>
    /// Flatten a Buffer Value's float samples to a FRAMED sysex byte array
    /// (<c>0xF0 &lt;data...&gt; 0xF7</c>), length-capped at <see cref="SysexMaxBytes"/>
    /// of DATA (T-40-04). Composer Buffers carry float PCM; each sample maps to one
    /// 7-bit data byte via a 0..127 clamp of round(sample*127) so a sysex payload
    /// round-trips sanely. Charitable on empty/non-buffer (returns an empty array →
    /// the caller's <c>Handle?.SendSysex</c> is a no-op).
    ///
    /// <para><b>WR-05:</b> the framing is added here because the raw librtmidi send
    /// (<c>rtmidi_out_send_message</c>) puts the array on the wire verbatim — an
    /// unframed payload is not a valid sysex message and devices reject it. If a
    /// composer already supplied a framed buffer (first data byte 0xF0 / last 0xF7),
    /// the framing is NOT duplicated. Any stray 0xF0/0xF7 in the INTERIOR of the data
    /// is impossible because the per-sample clamp caps data bytes at 0x7F (&lt; 0xF0).</para>
    /// </summary>
    private static byte[] BufferToSysexBytes(Value v)
    {
        if (v.Data is not AudioBuffer buf || buf.Data.Length == 0)
            return Array.Empty<byte>();

        int len = buf.Data.Length;
        if (len > SysexMaxBytes)
        {
            RenderingDiagnostics.WarnOnce(
                "midi-sysex-cap",
                $"[midi] midiSysex: payload {len} bytes exceeds cap {SysexMaxBytes} — truncated");
            len = SysexMaxBytes;
        }

        var data = new byte[len];
        for (int i = 0; i < len; i++)
        {
            // float sample in [-1,1] → 0..127 sysex data byte (clamped). The 0x7F
            // ceiling guarantees no interior byte collides with 0xF0/0xF7 framing.
            int b = (int)Math.Round((buf.Data[i] * 0.5 + 0.5) * 127.0);
            data[i] = (byte)(b < 0 ? 0 : (b > 127 ? 127 : b));
        }

        // WR-05: frame the payload. Detect a buffer the composer already framed so
        // we don't double-wrap (charitable). Note: with the 0x7F data clamp above a
        // float-PCM buffer can never produce a 0xF0/0xF7 itself, so this branch only
        // fires for an intentionally pre-framed payload.
        bool alreadyFramed = data.Length >= 2 && data[0] == SysexStart && data[^1] == SysexEnd;
        if (alreadyFramed)
            return data;

        var framed = new byte[data.Length + 2];
        framed[0] = SysexStart;
        Array.Copy(data, 0, framed, 1, data.Length);
        framed[^1] = SysexEnd;
        return framed;
    }

    /// <summary>Read the optional <c>overrides=</c> named-arg Dict (name→channel
    /// Int). Returns null if absent / not a Dict (charitable).</summary>
    private static Dictionary<string, int>? ReadOverrides(Value v)
    {
        if (v.Data is not DictData dict) return null;
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kv in dict.Entries)
        {
            if (kv.Key.Data is string name && kv.Value.Data is int ch)
                map[name] = ch;
        }
        return map.Count > 0 ? map : null;
    }

    // ===== midiOut high-level path (D-40-02 GM routing + CR-03 ms scheduling) =====

    /// <summary>Default per-section tempo when no tempo is in scope — matches
    /// <c>SongRenderer.DefaultBpm</c> so the wall-clock timing of midiOut tracks
    /// the rendered audio.</summary>
    private const double DefaultBpm = 120.0;

    /// <summary>Hard cap on total scheduled wall-clock length (ms). A pathological
    /// arrangement (huge repeat counts) cannot make midiOut block indefinitely —
    /// events past the cap are dropped + WarnOnce'd (T-40-04 DoS posture). 10
    /// minutes is generous for any musical phrase a composer would play live.</summary>
    private const double MaxScheduleMs = 10.0 * 60.0 * 1000.0;

    /// <summary>One scheduled MIDI dispatch: the absolute ms offset (from playback
    /// origin) at which <see cref="Action"/> fires on the opened handle.</summary>
    private readonly struct ScheduledEvent
    {
        public readonly double AtMs;
        public readonly long Seq;          // stable tiebreaker → deterministic order
        public readonly Action<IMidiOutputHandle> Action;
        public ScheduledEvent(double atMs, long seq, Action<IMidiOutputHandle> action)
        {
            AtMs = atMs;
            Seq = seq;
            Action = action;
        }
    }

    /// <summary>
    /// Test-only seam (CR-03): when non-null, the scheduler hands the FULLY-BUILT,
    /// time-sorted event list to this hook and dispatches every event IMMEDIATELY
    /// (no wall-clock sleeps) instead of running the real timed background loop.
    /// Lets a Fact assert the computed note spacing (NoteOn/NoteOff offsets) via the
    /// CaptureMidiBackend with zero real-time delay. Always restore to null in
    /// teardown. The first param is each event's planned AtMs.
    /// </summary>
    public static Action<IReadOnlyList<double>>? ScheduleInspectOverride { get; set; }

    private static void MidiOutSong(SongData song, string port, Dictionary<string, int>? overrides)
    {
        var handle = GetBackend().OpenOutput(port);
        if (handle == null)
        {
            RenderingDiagnostics.WarnOnce(
                $"midi-out-dead:{port}",
                $"[midi] midiOut('{port}') — no such port (or librtmidi.so absent); nothing sent");
            return;
        }

        var usedChannels = new HashSet<int>();
        var events = new List<ScheduledEvent>();
        long seqCounter = 0;
        double sectionStartMs = 0.0;

        // Walk the arrangement chronologically: sections are SEQUENTIAL (one after
        // another, honoring RepeatCount); the sequences WITHIN a section are
        // PARALLEL (they share the section's start offset). This mirrors the
        // renderer's section→sequence→voice layering so the port timeline matches
        // the exported audio/.mid (D-40-02).
        foreach (var secRef in song.Sections)
        {
            if (!song.SectionRegistry.TryGetValue(secRef.Name, out var section)) continue;

            double bpm = section.Context?.Tempo ?? DefaultBpm;
            if (!MusicalContext.IsValidTempo(bpm)) bpm = DefaultBpm;
            int repeats = secRef.RepeatCount < 1 ? 1 : secRef.RepeatCount;

            for (int r = 0; r < repeats; r++)
            {
                double sectionLenMs = 0.0;
                foreach (var (seqName, seqData) in section.Sequences)
                {
                    double seqLenMs = ScheduleOneSequence(
                        events, ref seqCounter, sectionStartMs, bpm, seqName, seqData, overrides, usedChannels);
                    if (seqLenMs > sectionLenMs) sectionLenMs = seqLenMs;
                }
                sectionStartMs += sectionLenMs;
                if (sectionStartMs > MaxScheduleMs)
                {
                    RenderingDiagnostics.WarnOnce(
                        "midi-out-schedule-cap",
                        $"[midi] midiOut('{port}') — arrangement exceeds {MaxScheduleMs / 1000.0:0}s schedule cap; remaining events dropped");
                    break;
                }
            }
            if (sectionStartMs > MaxScheduleMs) break;
        }

        // Audit §5.6 — append All-Notes-Off (CC123) per used channel at the end of
        // the timeline so the dispatched arrangement releases every voice, then
        // Close the handle in a finally (it leaked a native device + ALSA port on
        // every call — no finalizer on RtMidiOutputHandle).
        AppendAllNotesOff(events, ref seqCounter, sectionStartMs, usedChannels);
        try { DispatchScheduled(handle, events); }
        finally { try { handle.Close(); } catch { /* idempotent best-effort */ } }
    }

    /// <summary>
    /// Build the timed NoteOn/NoteOff events for ONE named sequence into
    /// <paramref name="events"/>, returning the sequence's total length in ms so
    /// the caller can advance the section cursor. Program-change + channel resolve
    /// via <see cref="InstrumentRouting.ResolveGmProgram"/> (D-40-02 VERBATIM);
    /// the per-sequence override Dict (name→channel) replaces the channel while the
    /// GM program still derives from the name. Onsets/durations come from
    /// <c>bar.ToTimeline()</c> + <c>note.GetBeats()</c> — the SAME quarter-relative
    /// beat math the renderer + writeMidi use — converted to ms via the active
    /// tempo. Best-effort ms, NOT sample-accurate (MIDI-RT-04 honesty).
    /// </summary>
    private static double ScheduleOneSequence(
        List<ScheduledEvent> events, ref long seqCounter, double startMs, double bpm,
        string seqName, SequenceData seq, Dictionary<string, int>? overrides,
        HashSet<int>? usedChannels = null)
    {
        var (gmProgram, channel) = InstrumentRouting.ResolveGmProgram(seqName);
        if (overrides != null && overrides.TryGetValue(seqName, out var ovCh))
            channel = ovCh;
        channel = channel < 0 ? 0 : (channel > 15 ? 15 : channel);

        int ch = channel;
        // Audit §5.6 — record the channel so the caller can send All-Notes-Off
        // (CC123) on every used channel in its finally before closing the handle.
        usedChannels?.Add(ch);
        int prog = gmProgram;
        // GM program select at the sequence start (D-40-02). Drums (ch9) ignore
        // program per GM, but we emit it harmlessly for symmetry with writeMidi.
        events.Add(new ScheduledEvent(startMs, seqCounter++, h => h.SendProgramChange(ch, prog)));

        // 60000/bpm ms per quarter note; GetBeats() returns quarter-relative beats.
        double msPerBeat = 60000.0 / bpm;
        double barStartBeats = 0.0;

        foreach (var bar in seq.Bars)
        {
            int denom = bar.TimeSignature?.Denominator ?? 4;
            double barBeats = 0.0;
            foreach (var (note, offsetBeats) in bar.ToTimeline())
            {
                double noteBeats = note.GetBeats(denom);
                if (!note.IsChordTone)
                {
                    double endBeats = offsetBeats + noteBeats;
                    if (endBeats > barBeats) barBeats = endBeats;
                }
                if (note.IsRest) continue;

                int pitch = NoteType.ToMidiNote(note.NoteName, note.Octave, note.Alteration);
                pitch = pitch < 0 ? 0 : (pitch > 127 ? 127 : pitch);
                int vel = (int)Math.Round(note.Velocity * 127.0);
                vel = vel < 1 ? 1 : (vel > 127 ? 127 : vel);

                double onMs = startMs + (barStartBeats + offsetBeats) * msPerBeat;
                // NoteOff is scheduled the note's duration LATER — this is the fix
                // for the zero-length-note bug (CR-03): On and Off no longer fire
                // back-to-back.
                double offMs = onMs + Math.Max(noteBeats, 0.0) * msPerBeat;

                int p = pitch, v = vel, c = ch;
                events.Add(new ScheduledEvent(onMs, seqCounter++, h => h.SendNoteOn(c, p, v)));
                events.Add(new ScheduledEvent(offMs, seqCounter++, h => h.SendNoteOff(c, p)));
            }
            barStartBeats += barBeats;
        }

        return barStartBeats * msPerBeat;
    }

    /// <summary>
    /// Dispatch the built event list in time order. The events are stable-sorted by
    /// (AtMs, Seq) so concurrent sequences interleave deterministically. The real
    /// path runs a bounded background thread that sleeps to each event's offset
    /// relative to a single playback origin (wired to the
    /// <c>AudioBuffer.PlaybackStartTime</c> seam idea — a Stopwatch origin captured
    /// the instant before the first dispatch, MIDI-RT-04). The test seam
    /// (<see cref="ScheduleInspectOverride"/>) fires every event immediately and
    /// reports the planned offsets so spacing can be asserted with no real delay.
    ///
    /// <para>Consistent with how <c>play</c> blocks the caller for the audio
    /// duration, midiOut blocks until the song finishes — but the dispatch loop is
    /// cancellation-cheap and capped by <see cref="MaxScheduleMs"/>, so it never
    /// blocks indefinitely.</para>
    /// </summary>
    private static void DispatchScheduled(IMidiOutputHandle handle, List<ScheduledEvent> events)
    {
        // Stable sort by time then insertion order (deterministic interleave).
        events.Sort((a, b) =>
        {
            int c = a.AtMs.CompareTo(b.AtMs);
            return c != 0 ? c : a.Seq.CompareTo(b.Seq);
        });

        var inspect = ScheduleInspectOverride;
        if (inspect != null)
        {
            // Test path: no wall-clock sleeps. Report planned offsets, fire now.
            var offsets = new List<double>(events.Count);
            foreach (var ev in events) offsets.Add(ev.AtMs);
            inspect(offsets);
            foreach (var ev in events)
            {
                try { ev.Action(handle); } catch { /* charitable per-event */ }
            }
            return;
        }

        // Real path: a single Stopwatch origin (the PlaybackStartTime seam, MIDI-RT-04)
        // anchors every event's wall-clock dispatch. Sleep coarsely to each offset.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var ev in events)
        {
            double waitMs = ev.AtMs - watch.Elapsed.TotalMilliseconds;
            if (waitMs > 0)
            {
                if (waitMs > MaxScheduleMs) waitMs = MaxScheduleMs;
                System.Threading.Thread.Sleep((int)waitMs);
            }
            try { ev.Action(handle); } catch { /* charitable per-event */ }
        }
    }

    /// <summary>Test-only: reset module state between Facts.</summary>
    public static void ResetForTesting()
    {
        BackendOverride = null;
        ScheduleInspectOverride = null;
    }
}
#endif
