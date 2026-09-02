using UnityEngine;

/// <summary>
/// The ceiling every score number is held under, and the arithmetic that keeps
/// it there.
///
/// Nothing in the design stops a score exploding. Modifiers stack on a single
/// tile without limit — the shop rolls ONE target tile per visit and will
/// happily sell a fourth 3W onto it — so the word multiplier grows as 3^n, and
/// a plain int wraps a little past two billion. That is how a run ended up
/// showing a NEGATIVE score: nothing failed, the number simply went round.
///
/// Wrapping is the worst failure available here, because the score doesn't just
/// become wrong, it changes SIGN — a cleared round reads as a lost one and the
/// symptom points nowhere near the cause. So every product runs in a WIDE type
/// (long or double, neither of which can wrap at any value this game can
/// produce) and the result is then SATURATED to Max. A saturated score is still
/// wrong, but it's wrong in the direction the player expects and it never goes
/// backwards.
///
/// Clamped at every step rather than only at the end, so no intermediate can
/// overflow on its way to a total that would have been in range.
///
/// A code constant on purpose, like ScoreTallyTiming and
/// DisplaySettings.TargetFrameRate: this is a safety valve, not a tuning knob,
/// and an Inspector copy would only be a second number to get wrong. If the
/// design ever genuinely wants Balatro-scale numbers, this is the one place to
/// change — but that means widening the score's TYPE (and the save's), not
/// raising this constant.
/// </summary>
public static class ScoreLimits
{
    /// <summary>
    /// The most any points number may reach. A billion is far past anything the
    /// balance intends, and still leaves room to add two of them together
    /// inside an int — which is exactly what the running round score does.
    /// </summary>
    public const int MaxPoints = 1_000_000_000;

    /// <summary>
    /// The most the multiplier may reach. Float doesn't wrap, it goes to
    /// Infinity — and Infinity times anything is a NaN score, which displays as
    /// "NaN" and saves as garbage — so this keeps Mult a real number rather
    /// than keeping it in range.
    /// </summary>
    public const float MaxMult = 1_000_000f;

    /// <summary>
    /// Saturates a wide points value into the int the rest of the game uses.
    /// The floor is 0: a word never scores negative, whatever a bookmark
    /// subtracts.
    /// </summary>
    public static int Clamp(long value) =>
        value <= 0L ? 0 : value >= MaxPoints ? MaxPoints : (int)value;

    /// <summary>
    /// The same, for a value that went through a float multiplier. NaN counts
    /// as 0 — an unusable number, treated as no score rather than propagated.
    /// </summary>
    public static int Clamp(double value)
    {
        if (double.IsNaN(value)) return 0;
        if (value <= 0d) return 0;
        return value >= MaxPoints ? MaxPoints : (int)System.Math.Round(value);
    }

    /// <summary>Keeps a multiplier finite and non-negative.</summary>
    public static float ClampMult(float value)
    {
        if (float.IsNaN(value)) return 0f;
        return Mathf.Clamp(value, 0f, MaxMult);
    }
}
