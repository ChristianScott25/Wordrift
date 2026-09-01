using UnityEngine;

/// <summary>
/// Plain data passed around between the session, modes, and the HUD.
/// Nothing here knows about Unity scenes or specific game modes.
/// </summary>

/// <summary>
/// The two numbers a score is made of, before anything else touches them.
/// Points is what the tiles are worth; Mult comes from the word's length. This
/// is what the HUD shows live while tiles are being selected.
/// </summary>
public struct ScorePair
{
    public int Points;
    public float Mult;

    /// <summary>
    /// The 2W/3W factor, which Points has ALREADY been multiplied by. Kept
    /// because folding it in destroys it — nothing can recover "x3 word" from
    /// Points alone — and a readout that wants to call it out will need it.
    /// Nothing reads it yet.
    /// </summary>
    public int WordMultiplier;

    public int Total => Mathf.RoundToInt(Points * Mult);
}

/// <summary>The outcome of one submitted chain.</summary>
public struct WordResult
{
    public string Word;
    public bool Accepted;
    public int Points;          // final points awarded (0 when rejected)

    /// <summary>Where the two numbers started, before any bookmark.</summary>
    public ScorePair Base;

    public int FinalPoints;     // after every bookmark
    public float FinalMult;     // after every bookmark

    /// <summary>
    /// What each bookmark did, in order. The HUD plays these back one beat at a
    /// time — and because it's empty when nothing fired, a run with no bookmarks
    /// scores instantly with nothing to sit through.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<ScoreStep> Steps;

    public int TileCount;

    /// <summary>Did anything intervene, or is this just tiles times length?</summary>
    public bool HasSteps => StepCount > 0;

    public int StepCount => Steps == null ? 0 : Steps.Count;
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

    /// <summary>
    /// What the selection is worth right now, before bookmarks — the pair of
    /// numbers shown live. Bookmarks are deliberately NOT previewed: seeing
    /// them fire after you commit is the payoff, and a preview that included
    /// them would hand you the answer.
    /// </summary>
    public ScorePair Preview;

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
