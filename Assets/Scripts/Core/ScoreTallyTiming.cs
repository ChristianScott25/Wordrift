/// <summary>
/// How long the score walk-through takes.
///
/// A code constant rather than serialized fields because TWO objects have to
/// agree on it exactly: GameSession WAITS this long before clearing the board,
/// and ScoreTallyWidget ANIMATES for this long. Two Inspector copies would
/// silently drift the first time someone tuned one of them, and the symptom —
/// the board clearing out from under the numbers — wouldn't obviously point
/// back at the cause. Same reasoning as DisplaySettings.TargetFrameRate.
/// </summary>
public static class ScoreTallyTiming
{
    /// <summary>Held on each bookmark as it fires.</summary>
    public const float StepSeconds = 0.45f;

    /// <summary>
    /// Beat after the last step. Paid even when nothing fired — which is every
    /// word until the run buys its first bookmark — so the final numbers are
    /// readable instead of blinking out.
    /// </summary>
    public const float FinishSeconds = 0.35f;

    public static float For(int stepCount) => FinishSeconds + stepCount * StepSeconds;
}
