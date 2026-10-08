using UnityEngine;

/// <summary>
/// How long each beat of the score walk-through lasts.
///
/// A code constant rather than serialized fields because it is a FEEL constant,
/// not a tuning knob — the same reasoning as DisplaySettings.TargetFrameRate.
/// Nothing about balance reads it.
///
/// ⚠️ THERE IS ONE CLOCK, AND IT IS GameSession.ScoreThenClear. It steps through
/// the beats, raises each one, and clears the board when it's finished; every
/// widget just draws whatever beat it is handed. This used to be two clocks —
/// the session waited For(n) while ScoreTallyWidget ran n separate waits of its
/// own — and they could not stay in step: WaitForSeconds resumes on the first
/// frame AFTER its interval, so n small waits each round up to a frame boundary
/// while one long wait rounds up once. The widget always finished late, by up to
/// a frame per beat, and with a beat per tile that is a fifth of a second of
/// drift — enough for the board to clear out from under the numbers. For() is
/// gone with it. Don't reintroduce a second timer.
/// </summary>
public static class ScoreTallyTiming
{
    /// <summary>The FIRST beat. Every one after is this times StepDecay^n.</summary>
    public const float StepSeconds = 0.45f;

    /// <summary>
    /// ⚠️ EACH BEAT IS SHORTER THAN THE ONE BEFORE, so a long word speeds up and
    /// ends sooner instead of dragging. Length is the base score now, so every
    /// word has a beat per tile and the old flat rate would have made a
    /// seven-letter word a four-second wait, every time.
    ///
    /// It also BOUNDS the whole thing for free: the series converges, so
    /// FinishSeconds + StepSeconds/(1-StepDecay) — about 9.3 seconds — is the
    /// most a word can EVER take, however many beats it grows. Nothing needs a
    /// floor, a cap or a skip button.
    /// </summary>
    public const float StepDecay = 0.95f;

    /// <summary>
    /// Beat after the last step, so the final numbers are readable instead of
    /// blinking out the moment they land.
    /// </summary>
    public const float FinishSeconds = 0.35f;

    /// <summary>
    /// The played tiles lift off the board one after another, this far apart,
    /// and each takes FlightSeconds to land in the word row. The pop is the
    /// row's solid tiles going away once the count is over.
    ///
    /// ⚠️ GameSession does NOT read these. It waits for the tiles to REPORT
    /// that they've landed (Tile.IsSettled), so the word row animating them
    /// is the only thing that times a flight — one clock, still.
    /// </summary>
    public const float FlightStaggerSeconds = 0.07f;
    public const float FlightSeconds = 0.25f;
    public const float PopSeconds = 0.15f;

    /// <summary>
    /// 1 is normal; higher is faster. A speed setting in the options is one write
    /// to this and nothing else — which is why every wait goes through StepAt
    /// rather than reading StepSeconds.
    ///
    /// ⚠️ Read per beat, so changing it mid-word stretches or squeezes the beats
    /// still to come. A setting should take effect from the NEXT word.
    /// </summary>
    public static float Speed = 1f;

    /// <summary>How long to hold on beat number `index`, counting the opening as 0.</summary>
    public static float StepAt(int index) =>
        StepSeconds * Mathf.Pow(StepDecay, Mathf.Max(0, index)) / Mathf.Max(0.01f, Speed);

    /// <summary>The flight's numbers, through the same speed knob.</summary>
    public static float FlightStagger() => FlightStaggerSeconds / Mathf.Max(0.01f, Speed);
    public static float Flight() => FlightSeconds / Mathf.Max(0.01f, Speed);
    public static float Pop() => PopSeconds / Mathf.Max(0.01f, Speed);

    /// <summary>The closing hold, through the same speed knob.</summary>
    public static float Finish() => FinishSeconds / Mathf.Max(0.01f, Speed);
}
