using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a chain of tiles into points, in stages:
///
///   base letter values
///     -> per-tile modifiers   (double letter)
///     -> word multipliers     (triple word)
///     -> length bonus         (longer words pay more)
///     -> the run's bookmarks, in slot order  (they mutate a ScoringContext)
///     -> mode score multiplier
///
/// The first stages are fixed rules about tiles. The bookmark stage is the
/// OPEN one: anything that wants to intervene in scoring does it there, by
/// changing the running Points and Mult, rather than by growing this method.
/// Modes with no bookmarks pass none and the stage is a no-op.
/// </summary>
public class ScoreCalculator
{
    private readonly ModeConfig config;

    public ScoreCalculator(ModeConfig config) => this.config = config;

    /// <param name="wordsThisRound">
    /// What's already been played this round, so a bookmark can spot a repeat.
    /// Must NOT contain the word being scored yet.
    /// </param>
    /// <param name="bookmarks">The run's bookmarks in slot order; null for a mode without a run.</param>
    public WordResult Evaluate(IReadOnlyList<Tile> chain, string word,
                               ICollection<string> wordsThisRound = null,
                               IReadOnlyList<BookmarkSpec> bookmarks = null)
    {
        int basePoints = 0;
        int wordMultiplier = 1;

        foreach (var tile in chain)
        {
            // The tile's corner shows the BASE letter value; the badge is what
            // tells the player it gets multiplied. This is where that actually
            // happens, and it's the only place letter modifiers are applied.
            // The tile carries its own base worth (TileSpec.baseScore), so a
            // specific tile's value can differ from its letter's usual one.
            basePoints += TileModifier.ApplyLetterModifiers(
                tile.LetterPoints, tile.Modifiers);

            foreach (var modifier in tile.Modifiers)
                if (modifier != null) wordMultiplier *= modifier.WordMultiplier;
        }

        int extraLetters = Mathf.Max(0, chain.Count - config.minWordLength);
        int lengthBonus = extraLetters * config.lengthBonusPerExtraLetter;

        int beforeBookmarks = basePoints * wordMultiplier + lengthBonus;

        var ctx = new ScoringContext
        {
            Word = word,
            Tiles = chain,
            WordsThisRound = wordsThisRound,
            Points = beforeBookmarks,
            Mult = 1f,
        };

        if (bookmarks != null)
            for (int i = 0; i < bookmarks.Count; i++)
                bookmarks[i]?.Apply(ctx);   // slot order is the call order

        int total = Mathf.RoundToInt(ctx.Points * ctx.Mult * config.scoreMultiplier);

        return new WordResult
        {
            Word = word,
            Accepted = true,
            Points = total,
            BasePoints = basePoints,
            WordMultiplier = wordMultiplier,
            LengthBonus = lengthBonus,
            BookmarkBonus = ctx.Points - beforeBookmarks,
            BookmarkMultiplier = ctx.Mult,
            TileCount = chain.Count,
        };
    }

    public static WordResult Rejected(string word, int tileCount) => new WordResult
    {
        Word = word,
        Accepted = false,
        Points = 0,
        WordMultiplier = 1,
        BookmarkMultiplier = 1f,
        TileCount = tileCount,
    };
}
