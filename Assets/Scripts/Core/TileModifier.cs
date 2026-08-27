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

    // The circle sprite itself lives on TileSkin, not here — so swapping the
    // badge treatment is one field on one asset rather than one per modifier.
    //
    // There is deliberately NO spawn chance: a modifier reaches a tile by being
    // on its TileSpec (an upgrade the run applied), never by a random roll at
    // spawn. Which modifiers a mode OFFERS is ModeConfig.tileModifiers' job.

    /// <summary>Applied to this tile's own letter value.</summary>
    public virtual int ModifyLetterScore(int points) => points;

    /// <summary>Multiplies the score of the whole word this tile is part of.</summary>
    public virtual int WordMultiplier => 1;

    /// <summary>
    /// Runs a letter's value through a tile's modifiers, in order. Shared so the
    /// number printed on a tile and the number ScoreCalculator adds up can't drift
    /// apart — change how letter modifiers stack here and both follow.
    /// </summary>
    public static int ApplyLetterModifiers(int points, IReadOnlyList<TileModifier> modifiers)
    {
        if (modifiers == null) return points;
        for (int i = 0; i < modifiers.Count; i++)
            if (modifiers[i] != null) points = modifiers[i].ModifyLetterScore(points);
        return points;
    }
}
