namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Runtime representation of a voice - a positioned audio clip on a timeline.
/// </summary>
public class Voice
{
    /// <summary>
    /// The audio buffer containing the clip data.
    /// </summary>
    public AudioBuffer Buffer { get; }

    /// <summary>
    /// Position on timeline in beats.
    /// </summary>
    public double OffsetBeats { get; set; }

    /// <summary>
    /// Gain multiplier (1.0 = unity gain).
    /// </summary>
    public double Gain { get; set; }

    /// <summary>
    /// Pan position (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    public double Pan { get; set; }

    /// <summary>
    /// Phase 38 LIVE-03 — stable identifier used by the live-block swap path
    /// (<see cref="VoiceAllocator.DiffByVoiceName"/>) to decide which voices
    /// SURVIVE across a re-render vs. which fade out. The naming convention
    /// (set by <see cref="SongRenderer"/> at allocation time) is
    /// <c>"{instrumentLabel}:{ordinalIdx}"</c> — e.g. the 3rd piano voice in
    /// the new render becomes <c>"piano:2"</c>. Matches the per-instrument
    /// breakdown format used by the Phase 28 voice-pool docs and the Phase 38
    /// UI panel row 3.
    ///
    /// <para>
    /// Default is the empty string so legacy non-live-mode construction
    /// (Phase 28 offline renders) continues unchanged — the diff path simply
    /// produces an empty key-set when Name is unset, and the live-swap
    /// machinery treats every voice as Added (full re-render, no preservation).
    /// </para>
    /// </summary>
    public string Name { get; init; } = "";

    public Voice(AudioBuffer buffer, double offsetBeats)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        OffsetBeats = offsetBeats;
        Gain = 1.0;
        Pan = 0.0;
    }

    /// <summary>
    /// Phase 38 Plan 38-03 LIVE-03 — transfers playback-position state from
    /// <paramref name="prev"/> onto this voice. Called by the live-block swap
    /// path (<see cref="VoiceAllocator.DiffByVoiceName"/> preserved set) so
    /// voices that survive a re-render don't restart from their onset — the
    /// composer hears no click and no envelope retrigger.
    ///
    /// <para>
    /// v1.5 scope (RESEARCH §B step 3 line 690-693): transfers
    /// <see cref="OffsetBeats"/> only. The Voice class does not currently
    /// expose an explicit envelope-cursor field; the rendered buffer holds the
    /// per-frame ADSR-shaped samples, and OffsetBeats positions the buffer on
    /// the timeline. Transferring OffsetBeats alone is sufficient for the
    /// composer-facing "no click on save" promise. If a future Voice extension
    /// adds explicit envelope-cursor mirrors, extend this method to copy them
    /// too — the call site (LiveReloadManager.PreserveVoiceState) does not
    /// need to change.
    /// </para>
    /// </summary>
    public void CopyStateFrom(Voice prev)
    {
        if (prev == null) throw new ArgumentNullException(nameof(prev));
        OffsetBeats = prev.OffsetBeats;
    }

    public override string ToString()
    {
        return $"Voice[Name={Name}, Offset={OffsetBeats:F2} beats, Gain={Gain:F2}, Pan={Pan:F2}, Duration={Buffer.Frames} frames]";
    }
}
