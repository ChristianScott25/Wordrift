using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 🚧 THE RANKING (2026-10-30): how rare every bookmark, consumable, checkout,
/// tile upgrade and shop tile is, and which ones are cursed. One table for all of
/// them, so re-ranking is a one-line edit here and the whole roster can be read
/// side by side — which is how rarity is meant to be judged.
///
/// WRITTEN ON EVERY RUN, like the effect numbers in ConsumableSetup: a rank set by
/// hand in the Inspector is overwritten. It has to be — Common is the enum's
/// zero, so "never ranked" and "ranked Common" look identical and there is
/// nothing to preserve against. Change a rank HERE.
///
/// Keyed by asset FILE NAME (and a shop tile by what it spells). Anything not
/// listed is left alone, which for a brand-new asset means Common — so a new
/// item works the moment it's made, and is ranked by adding a line.
///
/// Every other setup that rewrites these assets calls Apply at the end, so
/// re-running any of them can't undo the ranking. Word Crush > Apply Rarities
/// does just this, for everything.
/// </summary>
public static class RaritySetup
{
    private const Rarity C = Rarity.Common;
    private const Rarity U = Rarity.Uncommon;
    private const Rarity R = Rarity.Rare;
    private const Rarity L = Rarity.Legendary;

    // How they were ranked: how often it fires, how big it is when it does, and
    // how well it combines with other things — and whether a future item could
    // easily outdo it (if so, it isn't Legendary). Legendary is for genuinely
    // unique abilities and is allowed to sit empty. Trash is deliberately unused:
    // it will only come from events.

    private static readonly Dictionary<string, Rarity> Bookmarks = new()
    {
        // Flat points fade as targets grow; small +Mult on narrow conditions.
        ["DejaVu"] = C, ["DogEar"] = C, ["Sequel"] = C, ["DoubleSpaced"] = C, ["Shorthand"] = C,
        // Fire often, or big but awkward to set up.
        ["VowelFanatic"] = U, ["Marginalia"] = U, ["Bookend"] = U, ["Centerpiece"] = U,
        ["GiltEdge"] = U, ["Verse"] = U, ["LookingGlass"] = U,
        // Spine is x2 on most short words; the rest are x3+ or combine with tiles you buy.
        ["Spine"] = R, ["Antiquarian"] = R, ["Columnist"] = R, ["RuledPaper"] = R, ["GoldenRatio"] = R,
    };

    private static readonly Dictionary<string, Rarity> Consumables = new()
    {
        ["Consumable_Shuffle"] = C, ["Consumable_Eraser"] = C, ["Consumable_ScrapPaper"] = C,
        ["Consumable_SparePen"] = C, ["Consumable_Highlighter"] = C, ["Consumable_LateFee"] = C,
        ["Consumable_OverdueNotice"] = C, ["Consumable_Strikethrough"] = C, ["Consumable_Bookworm"] = C,
        ["Consumable_Transpose"] = C, ["Consumable_Plagiarism"] = C,

        ["Consumable_Doubler"] = U, ["Consumable_SecondEdition"] = U, ["Consumable_CoffeeRing"] = U,
        ["Consumable_VowelShower"] = U, ["Consumable_FreeRead"] = U, ["Consumable_CheatSheet"] = U,
        // Permanent, but small: +1 point a word, or 1 tile out of ~104.
        ["Consumable_GoldLeaf"] = U, ["Consumable_Shredder"] = U,
        ["Consumable_RushOrder"] = U, ["Consumable_BannedBook"] = U, ["Consumable_Spoiler"] = U,

        ["Consumable_Bestseller"] = R, ["Consumable_InkBlot"] = R, ["Consumable_CorrectionFluid"] = R,
        ["Consumable_Ghostwriter"] = R, ["Consumable_Reprint"] = R,

        // The only thing that clears a round outright. Drops to Rare the day
        // something stranger arrives.
        ["Consumable_Epilogue"] = L,
    };

    /// <summary>Has a downside as well as an upside. Any rarity can be cursed.</summary>
    private static readonly HashSet<string> CursedConsumables = new()
    {
        "Consumable_BannedBook", "Consumable_Plagiarism", "Consumable_RushOrder", "Consumable_Spoiler",
    };

    private static readonly Dictionary<string, Rarity> Checkouts = new()
    {
        ["Checkout_PayoutBonus"] = C, ["Checkout_RerollDiscount"] = C,
        ["Checkout_ExtraDiscards"] = U, ["Checkout_ShopDiscount"] = U,
        ["Checkout_ExtraMoves"] = R, ["Checkout_Interest"] = R,
    };

    private static readonly Dictionary<string, Rarity> Modifiers = new()
    {
        ["DoubleLetter"] = C, ["TripleLetter"] = U, ["DoubleWord"] = U, ["TripleWord"] = R,
    };

    /// <summary>
    /// Shop tiles, by what they spell. Plain letters aren't sold, so they aren't
    /// listed and stay Common — which nothing shows (LetterSet.RarityOf).
    /// </summary>
    private static readonly Dictionary<string, Rarity> Tiles = new()
    {
        ["er"] = C, ["in"] = C, ["ie"] = C, ["ed"] = C, ["th"] = C, ["sh"] = C, ["ch"] = C,
        // QU solves the Q; choice tiles bend to fit the word.
        ["qu"] = U, ["b/c/p"] = U, ["l/n/r"] = U, ["f/h/w"] = U, ["k/v/y"] = U,
        ["d/g"] = U, ["o/u"] = U, ["j/x"] = U, ["q/z"] = U,
        // Fit almost any word.
        ["r/s/t"] = R, ["a/e/i"] = R, [TileSpec.WildSpelling] = R,
    };

    [MenuItem("Word Crush/Apply Rarities")]
    public static void ApplyAll()
    {
        int n = 0;
        n += Apply<Bookmark>();
        n += Apply<Consumable>();
        n += Apply<Checkout>();
        n += Apply<TileModifier>();
        foreach (var guid in AssetDatabase.FindAssets("t:LetterSet"))
            n += ApplyTiles(AssetDatabase.LoadAssetAtPath<LetterSet>(AssetDatabase.GUIDToAssetPath(guid)));
        AssetDatabase.SaveAssets();
        Debug.Log($"Word Crush: ranked {n} things by rarity.");
    }

    /// <summary>
    /// Ranks every asset of one kind in the project. Called by that kind's own
    /// setup after it writes the assets. Returns how many it ranked.
    /// </summary>
    internal static int Apply<T>() where T : Object
    {
        int ranked = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && Rank(asset)) ranked++;
        }
        return ranked;
    }

    /// <summary>Ranks one asset if the table lists it. True when it was listed.</summary>
    private static bool Rank(Object asset)
    {
        bool changed;
        switch (asset)
        {
            case Bookmark b when Bookmarks.TryGetValue(b.name, out var rank):
                changed = Set(ref b.rarity, rank) | Set(ref b.cursed, false);
                break;
            case Consumable c when Consumables.TryGetValue(c.name, out var rank):
                changed = Set(ref c.rarity, rank) | Set(ref c.cursed, CursedConsumables.Contains(c.name));
                break;
            case Checkout k when Checkouts.TryGetValue(k.name, out var rank):
                changed = Set(ref k.rarity, rank) | Set(ref k.cursed, false);
                break;
            case TileModifier m when Modifiers.TryGetValue(m.name, out var rank):
                changed = Set(ref m.rarity, rank);
                break;
            default:
                return false;
        }

        if (changed) EditorUtility.SetDirty(asset);
        return true;
    }

    /// <summary>Writes a field only if it differs, so an unchanged asset isn't dirtied.</summary>
    private static bool Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        return true;
    }

    /// <summary>
    /// Ranks a letter set's shop rows. Called by LetterSetSetup, which rewrites
    /// every row and would otherwise leave each rank wherever the resize put it.
    /// </summary>
    internal static int ApplyTiles(LetterSet set)
    {
        if (set == null) return 0;

        int ranked = 0;
        bool changed = false;
        foreach (var entry in set.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.letter)) continue;

            // Unlike the assets, an unlisted ROW is reset to Common rather than
            // left alone: LetterSetSetup grows the array by copying its last row,
            // so a new row arrives wearing its neighbour's rank.
            if (Tiles.TryGetValue(entry.letter.ToLowerInvariant(), out var rank)) ranked++;
            else rank = Rarity.Common;
            if (entry.rarity == rank) continue;
            entry.rarity = rank;
            changed = true;
        }
        if (changed) EditorUtility.SetDirty(set);
        return ranked;
    }
}
