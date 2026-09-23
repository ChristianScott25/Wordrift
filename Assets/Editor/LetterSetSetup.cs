using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and refreshes the tile catalog, <c>LetterSet_Scrabble.asset</c> — the
/// 26 letters plus the multi-letter tiles the shop sells.
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
    /// What a pair is worth: the letters it spells, summed, times 1.5, rounded
    /// up — because a tile you can only play where its pair fits is harder to
    /// use than the two letters loose.
    /// </summary>
    private static int PointsFor(string pair)
    {
        int sum = 0;
        foreach (char c in pair)
        {
            bool found = false;
            foreach (var (letter, points, _) in Letters)
                if (letter.Length == 1 && letter[0] == c) { sum += points; found = true; break; }

            if (!found)
                Debug.LogError($"Letter Set: '{pair}' spells '{c}', which isn't in the catalog — " +
                               "its score will be short by that letter.");
        }
        return Mathf.CeilToInt(sum * 1.5f);
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
        // serialized array duplicates its LAST element, so a wild row that
        // doesn't exist yet arrives carrying QU's price ($22) rather than 0 —
        // and the "seed only when it's 0" guard every other price uses would
        // then faithfully preserve a number nobody chose. Asking the asset first
        // is the only place where "no wild row yet" honestly reads as 0.
        int tunedWildPrice = 0;
        foreach (var entry in asset.Entries)
            if (entry != null && entry.IsWild) { tunedWildPrice = entry.price; break; }

        var so = new SerializedObject(asset);
        var entries = so.FindProperty("entries");
        // The 26 letters, the pairs, and one wild.
        entries.arraySize = Letters.Length + Pairs.Length + 1;

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

            // Seed only, never re-seed, like every other price in the project.
            var priceProperty = entry.FindPropertyRelative("price");
            if (priceProperty.intValue == 0) priceProperty.intValue = price;
        }

        // The wild, last. Weight 0 like the pairs — bought, never dealt — and
        // worth nothing: what you pay for is that it fits anywhere, so paying
        // points for it too would be charging twice.
        var wild = entries.GetArrayElementAtIndex(Letters.Length + Pairs.Length);
        wild.FindPropertyRelative("letter").stringValue = TileSpec.WildSpelling;
        wild.FindPropertyRelative("points").intValue = 0;
        wild.FindPropertyRelative("weight").intValue = 0;

        // Seeded, or whatever it was actually tuned to — never what the resize
        // happened to copy in. See tunedWildPrice above.
        wild.FindPropertyRelative("price").intValue =
            tunedWildPrice > 0 ? tunedWildPrice : WildPrice;

        so.ApplyModifiedProperties();

        // The catalog caches a lookup and a weight total on first use, and a
        // ScriptableObject stays loaded across a domain reload — so without this
        // an editor session that had already touched the letter set would keep
        // answering from the catalog as it was before this ran.
        asset.Invalidate();
        EditorUtility.SetDirty(asset);

        Debug.Log($"Word Crush: letter set has {Letters.Length} letters, " +
                  $"{Pairs.Length} multi-letter tiles and 1 wild.");
        return asset;
    }
}
