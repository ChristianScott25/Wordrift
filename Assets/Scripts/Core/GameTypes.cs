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
    public int BookmarkBonus;        // flat points the run's bookmarks added
    public float BookmarkMultiplier; // combined multiplier they applied (1 = none fired)
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
    /// spending something — "R2   60 / 120   BAG 12". Empty for the modes with only
    /// one number to show, which is most of them.
    /// </summary>
    public string Goal;

    /// <summary>
    /// A third readout, for whatever a mode wants a standing line of text for —
    /// the run's bookmarks today. Deliberately generic: it's a slot, not a
    /// bookmark field, so the next mode that needs a line can use it too. Empty
    /// for most modes, and simply not drawn if the HUD has no label wired.
    /// </summary>
    public string Extra;
}

/// <summary>
/// What is currently selected on the board, and what the player is allowed to
/// do with it. Raised every time the selection changes, so the word preview and
/// the action buttons both read one snapshot rather than each working it out.
///
/// The two Can* flags are decisions, not raw facts: the session has already
/// asked the dictionary and the mode. A widget should obey them, never re-derive
/// them — that's what stops the button and the rule drifting apart.
/// </summary>
public struct SelectionState
{
    public string Word;      // what the selected tiles spell, lowercase
    public int TileCount;

    /// <summary>The selection is a word the session would accept.</summary>
    public bool CanSubmit;

    /// <summary>The selection can be discarded — non-empty, and within the allowance.</summary>
    public bool CanDiscard;

    /// <summary>Tiles the mode will still let you discard this round.</summary>
    public int DiscardsLeft;

    /// <summary>Nothing selected: the action buttons have nothing to act on.</summary>
    public bool IsEmpty => TileCount == 0;
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
