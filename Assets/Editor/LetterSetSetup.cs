using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and refreshes the tile catalog, <c>LetterSet_Scrabble.asset</c> — the
/// 26 letters plus the three kinds of tile the shop sells: multi-letter pairs,
/// the wild, and choice tiles.
///
/// It used to live as a private method inside WordCrushSetup, reachable only from
/// "Rebuild Game Scene &amp;&amp; Assets" — the one DESTRUCTIVE menu item, which
/// overwrites Game.unity wholesale. That made adding a letter cost a scene, and
/// it also meant a catalog row added by hand in the Inspector was silently wiped
/// the next time anyone rebuilt (this seeder sets the array's size and rewrites
/// every row). Split out here it is idempotent, touches no scene, and is the one
/// place a new kind of tile is born.
///
/// The convention every other seeder in this project follows applies: the fields
/// that say what a tile IS (its spelling, its base score, its spawn weight) are
/// refreshed on every run, and PRICE — a tuning knob — is seeded only when it is
/// still 0, so balancing survives a re-run.
/// </summary>
public static class LetterSetSetup
{
    private const string DataFolder = "Assets/GameData";
    private const string AssetPath = DataFolder + "/LetterSet_Scrabble.asset";

    /// <summary>
    /// Letter, base score, spawn weight. Scrabble's values, which also scale the
    /// tile bag — see LetterSet.BuildTileBag.
    /// </summary>
    private static readonly (string letter, int points, int weight)[] Letters =
    {
        ("a", 1, 9), ("b", 3, 2), ("c", 3, 2), ("d", 2, 4), ("e", 1, 12),
        ("f", 4, 2), ("g", 2, 3), ("h", 4, 2), ("i", 1, 9), ("j", 8, 1),
        ("k", 5, 1), ("l", 1, 4), ("m", 3, 2), ("n", 1, 6), ("o", 1, 8),
        ("p", 3, 2), ("q", 10, 1), ("r", 1, 6), ("s", 1, 4), ("t", 1, 6),
        ("u", 1, 4), ("v", 4, 2), ("w", 4, 2), ("x", 8, 1), ("y", 4, 2),
        ("z", 10, 1),
    };

    /// <summary>
    /// The multi-letter tiles, and what they cost. Chosen for how often the pair
    /// turns up in short words — ER, ED and IN are suffix engines that turn a
    /// three into a four; TH, SH and CH are single sounds; QU is the tile that
    /// makes the Q playable at all.
    ///
    /// Their POINTS are deliberately not written down here: they're derived below
    /// as the two letters' own base scores summed and multiplied by 1.5, rounded
    /// up. Revaluing H therefore revalues CH, SH and TH with it, and a
    /// hand-written second copy of that rule is how the catalog ends up pricing
    /// the old H.
    /// </summary>
    private static readonly (string pair, int price)[] Pairs =
    {
        ("er", 8), ("in", 8), ("ie", 8), ("ed", 10),
        ("th", 14), ("sh", 14), ("ch", 17), ("qu", 22),
    };

    /// <summary>
    /// What a WILD costs. Well above QU at $22, deliberately: a wild fits every
    /// word, sidesteps a banned letter, and resolves to whatever scores best, so
    /// it is strictly more useful than any pair. Its POINTS are 0 and can't be
    /// derived like a pair's — it has no letters to add up — so the row is
    /// written on its own rather than squeezed into the table above.
    /// </summary>
    private const int WildPrice = 35;

    /// <summary>
    /// CHOICE tiles: one square that becomes ONE of the letters it names, and
    /// nothing else — "a/e/i" is an a, an e or an i. A wild with a fence around
    /// it, which is why it can be worth points and cost a fraction of one.
    ///
    /// Grouped by THEME and by base score together, so a group's letters are
    /// worth roughly the same and its own score means something.
    ///
    /// Listed most expensive first, which is roughly — not exactly — most useful
    /// first. Flexibility is what you're buying, and the cheap common letters are
    /// where flexibility is worth most: R/S/T opens up about five times as many
    /// words as Q/Z does. J/X and Q/Z are the other end, bought for the 6 and 7
    /// points they guarantee you can always play rather than for the words they
    /// open up, which is why they're cheap despite scoring the most.
    ///
    /// ⚠️ The ORDER is part of the shop's seed, the same way StockShelves' roll
    /// order is: ShopScreen.RollTile draws uniformly over the for-sale rows in
    /// catalog order, so inserting a group changes which tile every existing seed
    /// offers. Add to the end if that ever matters; today it doesn't, because a
    /// catalog change discards every save through ModeConfig.Fingerprint anyway.
    ///
    /// ⚠️ EACH GROUP'S OPTIONS MUST BE IN ALPHABETICAL ORDER. WordValidator fills
    /// a restricted slot in the order it's given and inherits its output order
    /// from it, and ResolveSelection breaks ties by taking the first answer — so
    /// an unsorted group would resolve to a different word depending on which of
    /// the validator's two search strategies ran. Checked in ValidateChoice below
    /// rather than trusted.
    ///
    /// Their POINTS are derived, not written down: see ChoicePointsFor.
    /// </summary>
    private static readonly (string group, int price)[] Choices =
    {
        ("r/s/t", 24), ("l/n/r", 22), ("b/c/p", 20), ("a/e/i", 18), ("f/h/w", 18),
        ("k/v/y", 15), ("d/g", 12), ("j/x", 12), ("q/z", 12), ("o/u", 10),
    };

    /// <summary>
    /// What a pair is worth: the letters it spells, summed, times 1.5, rounded
    /// up — because a tile you can only play where its pair fits is harder to
    /// use than the two letters loose.
    /// </summary>
    private static int PointsFor(string pair)
    {
        int sum = 0;
        foreach (char c in pair) sum += PointsForLetter(c, pair);
        return Mathf.CeilToInt(sum * 1.5f);
    }

    /// <summary>
    /// What a CHOICE tile is worth: its CHEAPEST option, times 0.75, rounded
    /// DOWN — the exact mirror of a pair's x1.5 rounded up, and for the mirrored
    /// reason. A pair is harder to play than its letters loose, so it pays more;
    /// a choice tile is easier, so it pays less.
    ///
    /// The cheapest rather than the average, because the cheapest is what you're
    /// guaranteed: it can never be worth more than the worst letter you might end
    /// up playing. With groups picked to have similar values the two barely
    /// differ anyway, and this one needs no explaining.
    ///
    /// Rounding down is what makes the rule bite at all. The 1-point groups land
    /// on 0 — A/E/I and R/S/T really are worth nothing in points, and that reads
    /// honestly: what you pay for is that they always fit. Rounding up would
    /// leave every group worth 1, 2 or 3 points paying exactly what its letters
    /// do, and the discount would quietly apply to nothing but K/V/Y and Q/Z.
    /// </summary>
    private static int ChoicePointsFor(string group)
    {
        int cheapest = int.MaxValue;
        foreach (string option in group.Split(TileSpec.ChoiceSeparator))
            if (option.Length > 0)
                cheapest = Mathf.Min(cheapest, PointsForLetter(option[0], group));

        return cheapest == int.MaxValue ? 0 : Mathf.FloorToInt(cheapest * 0.75f);
    }

    /// <summary>
    /// One letter's base score out of the table above. Shared by both derivations
    /// so revaluing H revalues CH, SH, TH and F/H/W together — one number, one
    /// place, however many tiles are built on it.
    /// </summary>
    private static int PointsForLetter(char c, string owner)
    {
        foreach (var (letter, points, _) in Letters)
            if (letter.Length == 1 && letter[0] == c) return points;

        Debug.LogError($"Letter Set: '{owner}' names '{c}', which isn't in the catalog — " +
                       "its score will be wrong.");
        return 0;
    }

    /// <summary>
    /// Shouts about a choice group that can't work, at the one moment anyone is
    /// looking: two or more single letters, no repeats, in alphabetical order.
    /// Every one of these is silent at runtime — an unsorted group just resolves
    /// inconsistently — so it has to be caught here or not at all.
    /// </summary>
    private static void ValidateChoice(string group)
    {
        var options = group.Split(TileSpec.ChoiceSeparator);

        if (options.Length < 2)
            Debug.LogError($"Letter Set: choice tile '{group}' offers fewer than two " +
                           "letters, so it is just a letter with a slash in its name.");

        for (int i = 0; i < options.Length; i++)
        {
            if (options[i].Length != 1)
                Debug.LogError($"Letter Set: choice tile '{group}' has the option " +
                               $"'{options[i]}', which isn't a single letter — a choice " +
                               "tile plays as exactly one.");

            if (i > 0 && string.CompareOrdinal(options[i - 1], options[i]) >= 0)
                Debug.LogError($"Letter Set: choice tile '{group}' is not in alphabetical " +
                               "order (or repeats a letter). WordValidator searches its " +
                               "options in the order given and ResolveSelection takes the " +
                               "first answer, so this one would resolve inconsistently.");
        }
    }

    [MenuItem("Word Crush/Create Letter Set")]
    public static void CreateLetterSet()
    {
        Build();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Writes the catalog and hands the asset back, so the rebuild can go on to
    /// point a mode config at it.
    /// </summary>
    internal static LetterSet Build()
    {
        // Guarded the same way every other seeder guards its folder: this one
        // used to run only from Rebuild, which had already made GameData.
        if (!AssetDatabase.IsValidFolder(DataFolder))
            AssetDatabase.CreateFolder("Assets", "GameData");

        var asset = AssetDatabase.LoadAssetAtPath<LetterSet>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<LetterSet>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        // Read BEFORE the resize below, and this is not optional. Growing a
        // serialized array duplicates its LAST element, so a row that doesn't
        // exist yet arrives carrying the previous last row's price rather than 0
        // — and the "seed only when it's 0" guard every price uses would then
        // faithfully preserve a number nobody chose. Asking the asset first is
        // the only place where "no such row yet" honestly reads as 0.
        //
        // Keyed by what the row SPELLS rather than by its index, so a tuned price
        // follows its tile even if these tables are ever reordered. This started
        // life as one int for the wild; every kind of priced row needs it, and
        // one of them getting it and the others not is how the trap comes back.
        var tunedPrices = new Dictionary<string, int>();
        foreach (var entry in asset.Entries)
            if (entry != null && !string.IsNullOrEmpty(entry.letter) && entry.price > 0)
                tunedPrices[entry.letter.ToLowerInvariant()] = entry.price;

        var so = new SerializedObject(asset);
        var entries = so.FindProperty("entries");
        // The 26 letters, the pairs, one wild, and the choice tiles.
        entries.arraySize = Letters.Length + Pairs.Length + 1 + Choices.Length;

        for (int i = 0; i < Letters.Length; i++)
        {
            var (letter, points, weight) = Letters[i];
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("letter").stringValue = letter;
            entry.FindPropertyRelative("points").intValue = points;
            entry.FindPropertyRelative("weight").intValue = weight;

            // Nothing sells a single letter, and a price here would put one on
            // the shelf. Held at 0 rather than merely left alone.
            entry.FindPropertyRelative("price").intValue = 0;
        }

        for (int i = 0; i < Pairs.Length; i++)
        {
            var (pair, price) = Pairs[i];
            var entry = entries.GetArrayElementAtIndex(Letters.Length + i);
            entry.FindPropertyRelative("letter").stringValue = pair;
            entry.FindPropertyRelative("points").intValue = PointsFor(pair);

            // WEIGHT 0, always: a multi-letter tile is bought, never dealt. A
            // weight-0 row is listed in the catalog and left out of the bag
            // (LetterSet.BuildTileBag), which is exactly the arrangement — the
            // opening 104 tiles are untouched by any of this.
            entry.FindPropertyRelative("weight").intValue = 0;

            WritePrice(entry, pair, price, tunedPrices);
        }

        // The wild, last. Weight 0 like the pairs — bought, never dealt — and
        // worth nothing: what you pay for is that it fits anywhere, so paying
        // points for it too would be charging twice.
        var wild = entries.GetArrayElementAtIndex(Letters.Length + Pairs.Length);
        wild.FindPropertyRelative("letter").stringValue = TileSpec.WildSpelling;
        wild.FindPropertyRelative("points").intValue = 0;
        wild.FindPropertyRelative("weight").intValue = 0;

        WritePrice(wild, TileSpec.WildSpelling, WildPrice, tunedPrices);

        // The choice tiles, after the wild so that adding one can't move the
        // wild's index. Weight 0 like everything else the shop sells.
        for (int i = 0; i < Choices.Length; i++)
        {
            var (group, price) = Choices[i];
            ValidateChoice(group);

            var entry = entries.GetArrayElementAtIndex(
                Letters.Length + Pairs.Length + 1 + i);
            entry.FindPropertyRelative("letter").stringValue = group;
            entry.FindPropertyRelative("points").intValue = ChoicePointsFor(group);
            entry.FindPropertyRelative("weight").intValue = 0;
            WritePrice(entry, group, price, tunedPrices);
        }

        so.ApplyModifiedProperties();

        // The catalog caches a lookup and a weight total on first use, and a
        // ScriptableObject stays loaded across a domain reload — so without this
        // an editor session that had already touched the letter set would keep
        // answering from the catalog as it was before this ran.
        asset.Invalidate();
        EditorUtility.SetDirty(asset);

        Debug.Log($"Word Crush: letter set has {Letters.Length} letters, " +
                  $"{Pairs.Length} multi-letter tiles, {Choices.Length} choice tiles " +
                  "and 1 wild.");
        return asset;
    }

    /// <summary>
    /// Seeds a row's price, or keeps whatever it was tuned to — never whatever
    /// the array resize happened to copy in. The one place a price is written, so
    /// the "seed only, never re-seed" rule can't be half-applied.
    /// </summary>
    private static void WritePrice(SerializedProperty entry, string key, int seed,
                                   Dictionary<string, int> tuned) =>
        entry.FindPropertyRelative("price").intValue =
            tuned.TryGetValue(key, out int tunedPrice) ? tunedPrice : seed;
}
