using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One asset = one playable mode. Holds everything that mode needs (board,
/// alphabet, scoring rules) and knows how to create its rule object.
///
/// "Timed Mode Hard" is a duplicate of this asset with different numbers —
/// not a new class. Subclass only when the *rules* differ, not the values.
/// </summary>
public abstract class ModeConfig : ScriptableObject
{
    [Header("Presentation")]
    public string displayName = "Mode";

    [Header("Board")]
    public BoardShapeAsset boardShape;
    public LetterSet letterSet;

    [Header("Word rules")]
    [Min(2)] public int minWordLength = 3;

    [Header("Scoring")]
    [Tooltip("Bonus points per letter beyond the minimum word length. 0 = off.")]
    public int lengthBonusPerExtraLetter = 0;

    [Tooltip("Multiplies the whole word score. Useful for harder variants.")]
    public float scoreMultiplier = 1f;

    [Header("Special tiles")]
    [Tooltip("Modifiers that can appear on spawned tiles. Each one's spawn chance lives on its own asset. Leave empty for plain tiles.")]
    public List<TileModifier> tileModifiers = new();

    /// <summary>Creates the live rule object that runs one round of this mode.</summary>
    public abstract GameMode CreateMode();
}
