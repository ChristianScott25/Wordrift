using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A special property attached to a tile — double letter, triple word, and
/// whatever comes later. ScoreCalculator walks these when scoring a chain,
/// and Tile uses the visuals to make them readable on the board.
///
/// To add one: subclass, override the bits you care about, create the asset.
/// </summary>
public abstract class TileModifier : ScriptableObject
{
    [Header("Badge")]
    [Tooltip("Short label drawn in the tile's top-left corner. Two characters is " +
             "the design target: 2L, 3W.")]
    public string badgeLabel = "2L";

    [Tooltip("Color of the circle behind the label. This is the only thing that " +
             "marks the tile — the body keeps its skin color, so several " +
             "multipliers on screen stay calm.")]
    public Color badgeColor = new Color(0.20f, 0.52f, 0.88f, 1f);

    [Tooltip("Color of the label text on that circle.")]
    public Color badgeTextColor = Color.white;

    [Header("Shop")]
    [Tooltip("What one of these costs in the shop. 0 means unpriced — Word Crush > " +
             "Create Tile Modifier Assets fills a 0 with the default ladder and " +
             "leaves any number you've tuned alone.")]
    [Min(0)] public int price = 0;

    // The circle sprite itself lives on TileSkin, not here — so swapping the
    // badge treatment is one field on one asset rather than one per modifier.
    //
    // There is deliberately NO spawn chance: a modifier reaches a tile by being
    // on its TileSpec (an upgrade the run applied), never by a random roll at
    // spawn. Which modifiers a mode OFFERS is ModeConfig.tileModifiers' job.

    /// <summary>
    /// Applied to this tile's own letter value. Takes and returns a long because
    /// a tile can carry any number of these and they compound — 2L twice is x4,
    /// thirty times is more than an int holds. See ScoreLimits.
    /// </summary>
    public virtual long ModifyLetterScore(long points) => points;

    /// <summary>Multiplies the score of the whole word this tile is part of.</summary>
    public virtual int WordMultiplier => 1;

    /// <summary>
    /// Runs a letter's value through a tile's modifiers, in order. Shared so the
    /// number printed on a tile and the number ScoreCalculator adds up can't drift
    /// apart — change how letter modifiers stack here and both follow.
    ///
    /// Saturated after EVERY modifier, not once at the end: the clamp is what
    /// guarantees the next multiply has a bounded input, so no product on the
    /// way through can overflow even a long.
    /// </summary>
    public static long ApplyLetterModifiers(long points, IReadOnlyList<TileModifier> modifiers)
    {
        if (modifiers == null) return ScoreLimits.Clamp(points);
        for (int i = 0; i < modifiers.Count; i++)
            if (modifiers[i] != null)
                points = ScoreLimits.Clamp(modifiers[i].ModifyLetterScore(points));
        return ScoreLimits.Clamp(points);
    }
}
