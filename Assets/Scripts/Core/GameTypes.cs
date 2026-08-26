using UnityEngine;

/// <summary>
/// Plain data passed around between the session, modes, and the HUD.
/// Nothing here knows about Unity scenes or specific game modes.
/// </summary>

/// <summary>The outcome of one submitted chain.</summary>
public struct WordResult
{
    public string Word;
    public bool Accepted;
    public int Points;          // final points awarded (0 when rejected)
    public int BasePoints;      // letter values before multipliers / bonuses
    public int WordMultiplier;  // combined multiplier from tile modifiers
    public int LengthBonus;     // extra points for going past the minimum length
    public int TileCount;
}

/// <summary>
/// Whatever resource the current mode is counting down: seconds, moves, lives...
/// The HUD renders this generically, so a new mode needs no new HUD code.
/// </summary>
public struct ModeStatus
{
    public string Label;    // "TIME", "MOVES"
    public string Value;    // "0:47", "12"
    public float Fraction;  // 1 = full, 0 = spent (for a progress bar)
    public bool Urgent;     // true when running out, so the HUD can go red

    /// <summary>
    /// A second readout, for a mode that is chasing something as well as
    /// spending something — "60 / 120   BAG 12". Empty for the modes with only
    /// one number to show, which is most of them.
    /// </summary>
    public string Goal;
}

/// <summary>Everything the game-over screen needs.</summary>
public struct RoundSummary
{
    public int Score;
    public int WordsFound;
    public string BestWord;
    public int BestWordPoints;

    /// <summary>
    /// What to call this ending — "TARGET REACHED", "OUT OF MOVES". Empty leaves
    /// the game-over panel's own wording, which is what a mode that simply runs
    /// out of its resource wants.
    /// </summary>
    public string Headline;
}
