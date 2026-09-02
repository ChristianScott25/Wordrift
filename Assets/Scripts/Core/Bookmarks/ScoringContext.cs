using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One word being scored, as TWO running numbers that bookmarks get to change:
/// Points and Mult. The final score is Points x Mult.
///
/// The split is the whole scoring model, not an implementation detail. Points
/// is what the tiles are worth (their values through their own 2L/3L, times any
/// 2W/3W on the word); Mult starts from the word's LENGTH. A bookmark then
/// pushes one or the other — and because a bookmark can ADD to Mult as well as
/// multiply it, the order they run in changes the answer. That ordering is a
/// lot of where the depth will come from, which is why RunState keeps them in
/// a list and not a set.
///
/// Every change goes through AddPoints / AddMult / MultiplyMult rather than
/// touching the fields, because each one records a Step. The steps are what the
/// HUD plays back one at a time after ENTER — without them the readout can say
/// "x2" but never "BOOKEND x2".
///
/// Widen THIS when a bookmark needs a fact it can't see (tiles left on the
/// board, money, the round number) rather than widening every hook signature.
/// A class, not a struct, so everything down the chain mutates the same object.
/// </summary>
public class ScoringContext
{
    /// <summary>The word as submitted, lowercase.</summary>
    public string Word;

    /// <summary>The tiles it was spelled from, in selection order.</summary>
    public IReadOnlyList<Tile> Tiles;

    /// <summary>
    /// Words already accepted this round, NOT counting this one — so a bookmark
    /// can ask "have they spelled this before?" and get the honest answer.
    /// Read it, never add to it; it's ICollection only because that's the
    /// narrowest interface with an O(1) Contains on a HashSet.
    /// </summary>
    public ICollection<string> WordsThisRound;

    /// <summary>What the tiles are worth. Read it; change it with AddPoints.</summary>
    public int Points;

    /// <summary>
    /// The multiplier, starting from the word's length. Read it; change it with
    /// AddMult or MultiplyMult — which of the two you pick is the difference
    /// between a bookmark that commutes with its neighbours and one that doesn't.
    /// </summary>
    public float Mult;

    /// <summary>
    /// What happened, in the order it happened. Empty when no bookmark fired,
    /// which is what keeps early rounds instant — there is nothing to play back.
    /// </summary>
    public readonly List<ScoreStep> Steps = new();

    /// <summary>True when the word was played earlier this round.</summary>
    public bool IsRepeat =>
        WordsThisRound != null && Word != null && WordsThisRound.Contains(Word);

    /// <summary>
    /// Adds flat points. "DEJA VU  +10 POINTS".
    ///
    /// Saturated rather than wrapped, and floored at 0 — so a bookmark that
    /// takes points away can zero a word but never make it worth less than
    /// nothing. See ScoreLimits.
    /// </summary>
    public void AddPoints(int amount, string source)
    {
        if (amount == 0) return;
        Points = ScoreLimits.Clamp((long)Points + amount);
        Record(source, $"{Signed(amount)} POINTS", ScoreSide.Points);
    }

    /// <summary>
    /// Adds to the multiplier. The additive half of the pair — a +4 before a x3
    /// is worth far more than the same +4 after it, which is what makes slot
    /// order a decision.
    /// </summary>
    public void AddMult(float amount, string source)
    {
        if (Mathf.Approximately(amount, 0f)) return;
        Mult = ScoreLimits.ClampMult(Mult + amount);
        Record(source, $"{Signed(amount)} MULT", ScoreSide.Mult);
    }

    /// <summary>Multiplies the multiplier. The big, order-sensitive one.</summary>
    public void MultiplyMult(float factor, string source)
    {
        if (Mathf.Approximately(factor, 1f)) return;
        Mult = ScoreLimits.ClampMult(Mult * factor);
        Record(source, $"x{Trim(factor)} MULT", ScoreSide.Mult);
    }

    private void Record(string source, string detail, ScoreSide side) => Steps.Add(new ScoreStep
    {
        Source = string.IsNullOrEmpty(source) ? "?" : source.ToUpperInvariant(),
        Detail = detail,
        Side = side,
        Points = Points,
        Mult = Mult,
    });

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static string Signed(float value) =>
        value >= 0f ? $"+{Trim(value)}" : $"-{Trim(-value)}";

    /// <summary>2 rather than 2.0, but 1.5 stays 1.5.</summary>
    public static string Trim(float value) =>
        Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");

}

/// <summary>
/// One thing that happened to the score, and what the numbers read afterwards.
/// The HUD steps through these, so each entry has to stand alone as a beat:
/// who did it, what they did, and where that left the two numbers.
/// </summary>
public struct ScoreStep
{
    public string Source;   // "BOOKEND"
    public string Detail;   // "x2 MULT" — for display only, never for logic
    public ScoreSide Side;  // which number moved; what the HUD highlights
    public int Points;      // after this step
    public float Mult;      // after this step
}

/// <summary>Which half of the score a step touched.</summary>
public enum ScoreSide
{
    Points,
    Mult,
}
