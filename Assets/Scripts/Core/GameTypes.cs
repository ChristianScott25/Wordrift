using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain data passed around between the session, modes, and the HUD.
/// Nothing here knows about Unity scenes or specific game modes.
/// </summary>

/// <summary>
/// The two numbers a score is made of. Both START at the word's LENGTH — so many
/// letters, so many points, and a multiplier off the same curve — and the tiles
/// build on that.
///
/// It is used for two DIFFERENT moments and they are not the same numbers, which
/// is the one thing to keep straight here:
///
///   SelectionState.Preview   every tile counted  — the live readout while you drag
///   WordResult.Opening       length alone        — where the walk-through STARTS
///
/// ⚠️ There is deliberately no "the 2W factor" field any more. A word multiplier
/// now fires on its own tile's beat and multiplies whatever has piled up so far,
/// so there is no single aggregate factor that Points "has already been
/// multiplied by" — the steps are where that information lives.
/// </summary>
public struct ScorePair
{
    public int Points;
    public float Mult;

    /// <summary>The two numbers multiplied — saturated, never wrapped. See ScoreLimits.</summary>
    public int Total => ScoreLimits.Clamp((double)Points * Mult);
}

/// <summary>The outcome of one submitted chain.</summary>
public struct WordResult
{
    public string Word;
    public bool Accepted;
    public int Points;          // final points awarded (0 when rejected)

    /// <summary>
    /// Where the walk-through STARTS: the word's length, on both sides, before a
    /// single tile has been counted.
    ///
    /// ⚠️ NOT what the live preview shows. The preview has every tile in it
    /// already — see ScorePair. Reading this one as "what the word is worth"
    /// would print the letter count and call it a score.
    /// </summary>
    public ScorePair Opening;

    public int FinalPoints;     // after everything
    public float FinalMult;     // after everything

    /// <summary>
    /// What each contributor did, in order — every tile, then every bookmark,
    /// then the round, then the mode. The HUD plays these back one beat at a
    /// time and shakes whatever each one points at.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<ScoreStep> Steps;

    public int TileCount;

    /// <summary>Did anything happen at all beyond the opening numbers?</summary>
    public bool HasSteps => StepCount > 0;

    public int StepCount => Steps == null ? 0 : Steps.Count;
}

/// <summary>
/// One readout in the HUD's resource strip — "$ 25", "MOVES 12/15", "ROUND 2".
///
/// A strip of these replaced the single crammed string RogueDemoMode used to
/// build ("R2   60 / 120   BAG 12   $25"), which existed only because the HUD
/// had exactly one spare slot for it. A mode publishes as many as it has.
/// </summary>
public struct StatusChip
{
    public string Label;
    public string Value;

    /// <summary>1 = full, 0 = spent. Negative means "no bar, just the number".</summary>
    public float Fraction;

    /// <summary>Running out, so the chip can go red.</summary>
    public bool Urgent;

    public StatusChip(string label, string value, float fraction = -1f, bool urgent = false)
    {
        Label = label;
        Value = value;
        Fraction = fraction;
        Urgent = urgent;
    }
}

/// <summary>
/// Everything the HUD shows about the round in progress. The mode fills it; the
/// widgets read the field they care about.
///
/// Every readout is its OWN field or chip — deliberately, after a spell where
/// four of them shared one pre-formatted string and the HUD had to take what it
/// was given. A widget that has to parse text to find a number is a widget that
/// can't lay it out.
/// </summary>
public struct ModeStatus
{
    /// <summary>
    /// The resource strip, in display order.
    ///
    /// ⚠️ A mode's Status property is rebuilt EVERY FRAME (GameSession.Update
    /// raises StatusChanged), so this must point at a list the mode owns and
    /// refills — never a fresh one per frame.
    /// </summary>
    public IReadOnlyList<StatusChip> Chips;

    /// <summary>Where the round stands. Shown as "25 / 100" in the header.</summary>
    public int Score;
    public int Target;

    /// <summary>
    /// Which round this is. In the strip as a chip, and in the header's title
    /// bar on a round with no librarian — where "ROUND" alone says nothing.
    /// </summary>
    public int Round;

    /// <summary>Tiles left to draw, and how many the run's bag holds in total.</summary>
    public int BagRemaining;
    public int BagTotal;

    /// <summary>
    /// This round's librarian, split into the two things the header draws in
    /// separate places — the name in its title bar, the rule in the body under
    /// it. Both empty on an ordinary round, which is what puts the header's
    /// placeholder up instead. They used to be one string glued together with
    /// size markup, which only worked while a single label drew both.
    /// </summary>
    public string LibrarianName;
    public string LibrarianPower;

    public bool HasLibrarian => !string.IsNullOrEmpty(LibrarianName);

    /// <summary>
    /// What the player has armed to fire on the next word — "DOUBLER", or
    /// "DOUBLER x2" for two of them. Empty when nothing is.
    ///
    /// ⚠️ The mode must hand over a string it built when the arming CHANGED, not
    /// one it composes here: Status is rebuilt every frame, so a $"..." in the
    /// property would allocate sixty strings a second.
    /// </summary>
    public string ArmedText;

    /// <summary>Something is waiting to score the next word.</summary>
    public bool HasArmed => !string.IsNullOrEmpty(ArmedText);
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

    /// <summary>
    /// The selected tiles themselves, in chain order — what the word row draws
    /// copies of, so it can show the same faces, score corners and badges the
    /// board is showing rather than an imitation built from Word alone.
    ///
    /// ⚠️ Borrowed, not given: this points at the session's live chain list and
    /// is only valid for the duration of the callback. A widget that wants to
    /// keep it must copy what it needs. Raised on EVERY frame of a drag, which
    /// is also why nothing here allocates.
    /// </summary>
    public IReadOnlyList<Tile> Tiles;

    /// <summary>The selection is a word the session would accept.</summary>
    public bool CanSubmit;

    /// <summary>
    /// Why a word the dictionary knows still can't be played — a librarian's
    /// rule, in the player's words. Empty when there's nothing to explain, which
    /// includes every plain non-word: "that isn't a word" needs no caption, and
    /// captioning it would bury the one message that's actually informative.
    /// </summary>
    public string RefusedReason;

    /// <summary>The selection can be discarded — non-empty, and within the allowance.</summary>
    public bool CanDiscard;

    /// <summary>Tiles the mode will still let you discard this round.</summary>
    public int DiscardsLeft;

    /// <summary>
    /// What the selection is worth right now: the word's length plus every tile,
    /// through its own badges. Bookmarks, the librarian and armed items are
    /// deliberately NOT previewed — seeing them fire after you commit is the
    /// payoff, and a preview that included them would hand you the answer.
    ///
    /// ⚠️ This is a different number from WordResult.Opening, which is the
    /// length ALONE. The walk-through starts there and climbs back up to this.
    /// </summary>
    public ScorePair Preview;

    /// <summary>Nothing selected: the action buttons have nothing to act on.</summary>
    public bool IsEmpty => TileCount == 0;
}

/// <summary>
/// A word, and enough of the round around it to judge whether it may be played.
/// Asked of the mode — and through it of the round's librarian — every time the
/// selection changes, and once more at the moment of submitting.
///
/// Widen this rather than the hook that takes it: a librarian that needs a fact
/// it can't see here gets a new field, and every existing one keeps compiling.
/// Same bargain as ScoringContext, for the same reason.
/// </summary>
public struct WordCheck
{
    /// <summary>The word the selection spells, lowercase. Never null in practice.</summary>
    public string Word;

    /// <summary>The tiles it's spelled from, in selection order.</summary>
    public IReadOnlyList<Tile> Tiles;

    /// <summary>
    /// Words already accepted this round, NOT counting this one. Read it, never
    /// add to it — this is the session's own set, handed over unwrapped.
    /// </summary>
    public IReadOnlyCollection<string> WordsThisRound;

    /// <summary>
    /// Whatever the round's rule decided for itself when the round began — the
    /// banned letter, today. Empty for a rule that chooses nothing, which is
    /// most of them. Stamped by the MODE on its way through, so the session
    /// never learns that librarians exist.
    /// </summary>
    public string Note;

    /// <summary>
    /// Letters in the word — NOT the tile count, which is a different number
    /// once a multi-letter tile is on the board. This is what every length rule
    /// asks about, and letters is what they all mean.
    /// </summary>
    public int Length => Word == null ? 0 : Word.Length;
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
