using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a chain of tiles into points, in stages:
///
///   base letter values
///     -> per-tile modifiers   (double letter)
///     -> word multipliers     (triple word)
///     -> length bonus         (longer words pay more)
///     -> mode score multiplier
///
/// New scoring ideas slot into this pipeline instead of being scattered
/// through the session.
/// </summary>
public class ScoreCalculator
{
    private readonly LetterSet letters;
    private readonly ModeConfig config;

    public ScoreCalculator(LetterSet letters, ModeConfig config)
    {
        this.letters = letters;
        this.config = config;
    }

    public WordResult Evaluate(IReadOnlyList<Tile> chain, string word)
    {
        int basePoints = 0;
        int wordMultiplier = 1;

        foreach (var tile in chain)
        {
            int points = letters.PointsFor(tile.Letter);
            foreach (var modifier in tile.Modifiers)
            {
                points = modifier.ModifyLetterScore(points);
                wordMultiplier *= modifier.WordMultiplier;
            }
            basePoints += points;
        }

        int extraLetters = Mathf.Max(0, chain.Count - config.minWordLength);
        int lengthBonus = extraLetters * config.lengthBonusPerExtraLetter;

        int total = Mathf.RoundToInt((basePoints * wordMultiplier + lengthBonus) * config.scoreMultiplier);

        return new WordResult
        {
            Word = word,
            Accepted = true,
            Points = total,
            BasePoints = basePoints,
            WordMultiplier = wordMultiplier,
            LengthBonus = lengthBonus,
            TileCount = chain.Count,
        };
    }

    public static WordResult Rejected(string word, int tileCount) => new WordResult
    {
        Word = word,
        Accepted = false,
        Points = 0,
        WordMultiplier = 1,
        TileCount = tileCount,
    };
}
