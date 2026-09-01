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
    [Tooltip("The base MULTIPLIER by word length — the right-hand number. Entry 0 " +
             "is a word of Min Word Length, and each entry after it is one letter " +
             "longer. This is the whole reason to reach for a longer word, so it's " +
             "the first curve to tune.")]
    public float[] lengthMultipliers = { 1f, 1.5f, 2f };

    [Tooltip("Once a word outruns the list above, every further letter adds this " +
             "much to the multiplier.")]
    [Min(0f)] public float multiplierPerExtraLetter = 0.5f;

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
    [Tooltip("The pool of modifiers this mode can hand out as tile upgrades (the " +
             "shop draws from here). Nothing spawns with these on its own.")]
    public List<TileModifier> tileModifiers = new();

    [Header("Bookmarks")]
    [Tooltip("The pool of bookmarks this mode's shop can offer. A run can own each " +
             "at most once, and an empty list simply means no bookmark is for sale.")]
    public List<Bookmark> bookmarks = new();

    /// <summary>
    /// The base multiplier a word of this many tiles is worth. Below the minimum
    /// word length it just reads as the first entry — nothing can be submitted
    /// there anyway, and the live preview still needs a number to show.
    /// </summary>
    public float LengthMultiplier(int tileCount)
    {
        int index = Mathf.Max(0, tileCount - minWordLength);

        if (lengthMultipliers == null || lengthMultipliers.Length == 0)
            return 1f + index * multiplierPerExtraLetter;

        if (index < lengthMultipliers.Length) return lengthMultipliers[index];

        float last = lengthMultipliers[lengthMultipliers.Length - 1];
        return last + (index - (lengthMultipliers.Length - 1)) * multiplierPerExtraLetter;
    }

    /// <summary>Creates the live rule object that runs one round of this mode.</summary>
    public abstract GameMode CreateMode();
}
