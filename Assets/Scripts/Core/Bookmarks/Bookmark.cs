using UnityEngine;

/// <summary>
/// A run-owned item with a special ability — this game's version of a Balatro
/// joker. Owning one changes how words score for the rest of the run.
///
/// Like TileModifier, the asset is a read-only RECIPE. What a run actually owns
/// is a BookmarkSpec wrapping it, because two copies of the same bookmark have
/// to be able to differ once editions (holographic, negative) exist.
///
/// To add one: subclass, override OnWordScored, create the asset, add it to a
/// mode's bookmark pool. Nothing else changes.
/// </summary>
public abstract class Bookmark : ScriptableObject
{
    [Tooltip("Shown in the shop and the HUD. Keep it short — the HUD lists them on one line.")]
    public string displayName = "Bookmark";

    [Tooltip("What it does, in the player's words.")]
    [TextArea] public string description = "";

    [Tooltip("What it costs in the shop. 0 means unpriced — Word Crush > Create " +
             "Bookmark Assets fills a 0 with the default price and leaves any " +
             "number you've tuned alone.")]
    [Min(0)] public int price = 0;

    /// <summary>
    /// The bookmark's turn at a word being scored. Change ctx.Points and
    /// ctx.Mult; check ctx for whatever the effect depends on. Doing nothing is
    /// a perfectly good answer — most bookmarks won't fire on most words.
    /// </summary>
    public abstract void OnWordScored(ScoringContext ctx);
}
