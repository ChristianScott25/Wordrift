using System.Collections.Generic;
using TMPro;
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

    [Header("Tile look")]
    [Tooltip("Looks a spawned tile can take. List several and each tile draws one " +
             "at random by weight, so different tile types can share a board. " +
             "Leave empty to use whatever the Tile prefab was authored with.")]
    public List<TileSkin> tileSkins = new();

    [Tooltip("Typeface for the letter on every tile. Independent of the skin on " +
             "purpose — a font swap shouldn't touch the art. Empty = the prefab's font.")]
    public TMP_FontAsset letterFont;

    [Header("Special tiles")]
    [Tooltip("The pool of modifiers this mode can hand out as tile upgrades (a " +
             "future shop draws from here). Nothing spawns with these on its own.")]
    public List<TileModifier> tileModifiers = new();

    /// <summary>Creates the live rule object that runs one round of this mode.</summary>
    public abstract GameMode CreateMode();
}
