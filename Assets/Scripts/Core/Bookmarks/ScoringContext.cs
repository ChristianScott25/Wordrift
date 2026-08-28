using System.Collections.Generic;

/// <summary>
/// One word being scored, as a running total that bookmarks get to change.
///
/// The reason it exists: ScoreCalculator used to be a closed function with a
/// fixed order, which left nowhere for a relic to intervene. Now the tile stages
/// build this up, every bookmark gets a turn at it IN SLOT ORDER, and the final
/// number is Points x Mult. Two bookmarks in the other order can produce a
/// different score once one of them adds to Mult rather than multiplying it —
/// that ordering is a lot of where the depth will come from.
///
/// Widen THIS when a bookmark needs a fact it can't see (tiles left on the
/// board, money, the round number) rather than widening every hook signature.
/// A class, not a struct, so everything down the chain mutates the same object.
/// </summary>
public class ScoringContext
{
    /// <summary>The word as submitted, lowercase.</summary>
    public string Word;

    /// <summary>The tiles it was spelled from, in drag order.</summary>
    public IReadOnlyList<Tile> Tiles;

    /// <summary>
    /// Words already accepted this round, NOT counting this one — so a bookmark
    /// can ask "have they spelled this before?" and get the honest answer.
    /// Read it, never add to it; it's ICollection only because that's the
    /// narrowest interface with an O(1) Contains on a HashSet.
    /// </summary>
    public ICollection<string> WordsThisRound;

    /// <summary>
    /// The running score: letter values through their tile modifiers, times the
    /// tiles' word multipliers, plus the length bonus. Add flat points here.
    /// </summary>
    public int Points;

    /// <summary>
    /// The running multiplier, starting at 1. Multiply it for a "double the
    /// word" effect; later effects may add to it instead, which is exactly when
    /// slot order starts to matter.
    /// </summary>
    public float Mult = 1f;

    /// <summary>True when the word was played earlier this round.</summary>
    public bool IsRepeat =>
        WordsThisRound != null && Word != null && WordsThisRound.Contains(Word);
}
