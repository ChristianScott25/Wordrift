using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the consumable assets and puts them in every Rogue Demo mode's pool,
/// because ScriptableObjects can't be authored from the command line.
///
/// Idempotent, and it splits the fields the same two ways CheckoutSetup does:
/// what a consumable IS (its name and the size of its effect) is refreshed every
/// run, while its PRICE is a balance number and is written only when the asset's
/// is still 0.
///
/// No description is written here either — a Consumable derives its own from its
/// numbers (Consumable.PowerText), so there is no second copy to go stale.
///
/// Adding another is a code + Inspector job: subclass Consumable, add a block
/// below, and re-run this. Nothing else in the game is keyed off which
/// consumables exist — the shop row rolls over this pool and the items box draws
/// whatever the run is carrying.
/// </summary>
public static class ConsumableSetup
{
    private const string ConsumableFolder = "Assets/GameData/Consumables";

    [MenuItem("Word Crush/Create Consumable Assets")]
    public static void CreateConsumables() => Build();

    internal static List<Consumable> Build()
    {
        if (!AssetDatabase.IsValidFolder(ConsumableFolder))
            AssetDatabase.CreateFolder("Assets/GameData", "Consumables");

        var consumables = new List<Consumable>();

        // 🚧 These prices are a first guess. A cleared round 1 pays about $19, so
        // these are meant to be the cheap impulse buy next to a $30 checkout —
        // something you take when the shelf has nothing permanent worth saving
        // for. Nothing here has been balanced against a full run.
        var shuffle = CreateOrLoad<ShuffleConsumable>("Consumable_Shuffle");
        Name(shuffle, "Shuffle", price: 6);
        consumables.Add(shuffle);

        var doubler = CreateOrLoad<NextWordMultiplierConsumable>("Consumable_Doubler");
        NextWord(doubler, multiplier: 2f, bonusPoints: 0, extraMoveCost: 0);
        Name(doubler, "Doubler", price: 8);
        consumables.Add(doubler);

        // 🚧 Added 2026-10-08 for testing, priced as first guesses in the same
        // band. Ink Blot costs most: a wild is $35 to own for good.
        var strikethrough = CreateOrLoad<LineClearConsumable>("Consumable_Strikethrough");
        strikethrough.horizontal = true;
        Name(strikethrough, "Strikethrough", price: 6);
        consumables.Add(strikethrough);

        var bookworm = CreateOrLoad<LineClearConsumable>("Consumable_Bookworm");
        bookworm.horizontal = false;
        Name(bookworm, "Bookworm", price: 6);
        consumables.Add(bookworm);

        var inkBlot = CreateOrLoad<WildTileConsumable>("Consumable_InkBlot");
        Name(inkBlot, "Ink Blot", price: 10);
        consumables.Add(inkBlot);

        var secondEdition = CreateOrLoad<RedrawConsumable>("Consumable_SecondEdition");
        Name(secondEdition, "Second Edition", price: 7);
        consumables.Add(secondEdition);

        // 🚧 Added 2026-10-09, priced as a first guess like the rest. Appended
        // last so no existing item's place in the pool moves.
        var cheatSheet = CreateOrLoad<BestWordConsumable>("Consumable_CheatSheet");
        Name(cheatSheet, "Cheat Sheet", price: 5);
        consumables.Add(cheatSheet);

        // ---- 🚧 2026-10-29: twenty-one more, all at once, so the Bookshop has
        // something to sell. Every number and price is a first guess. Grouped
        // by power in the comments only — rarity isn't built yet. Appended, so
        // no existing item's place in the pool moves.

        // Weak
        var eraser = CreateOrLoad<AreaClearConsumable>("Consumable_Eraser");
        eraser.radius = 0;
        Name(eraser, "Eraser", price: 3);
        consumables.Add(eraser);

        var scrapPaper = CreateOrLoad<AllowanceConsumable>("Consumable_ScrapPaper");
        Allow(scrapPaper, moves: 0, discards: 3, zeroDiscards: false);
        Name(scrapPaper, "Scrap Paper", price: 4);
        consumables.Add(scrapPaper);

        var sparePen = CreateOrLoad<AllowanceConsumable>("Consumable_SparePen");
        Allow(sparePen, moves: 1, discards: 0, zeroDiscards: false);
        Name(sparePen, "Spare Pen", price: 5);
        consumables.Add(sparePen);

        var highlighter = CreateOrLoad<NextWordMultiplierConsumable>("Consumable_Highlighter");
        NextWord(highlighter, multiplier: 1f, bonusPoints: 20, extraMoveCost: 0);
        Name(highlighter, "Highlighter", price: 4);
        consumables.Add(highlighter);

        var lateFee = CreateOrLoad<MoneyConsumable>("Consumable_LateFee");
        lateFee.flatMoney = 8;
        lateFee.moneyPerWord = 0;
        Name(lateFee, "Late Fee", price: 4);
        consumables.Add(lateFee);

        // Medium
        var goldLeaf = CreateOrLoad<GildTileConsumable>("Consumable_GoldLeaf");
        goldLeaf.modifier = AssetDatabase.LoadAssetAtPath<TileModifier>("Assets/GameData/Modifiers/DoubleLetter.asset");
        if (goldLeaf.modifier == null)
            Debug.LogError("Gold Leaf found no DoubleLetter modifier. Run Word Crush > Create Tile Modifier Assets.");
        Name(goldLeaf, "Gold Leaf", price: 9);
        consumables.Add(goldLeaf);

        var vowelShower = CreateOrLoad<RandomVowelsConsumable>("Consumable_VowelShower");
        vowelShower.count = 3;
        vowelShower.vowels = "aeiou";
        Name(vowelShower, "Vowel Shower", price: 6);
        consumables.Add(vowelShower);

        var overdueNotice = CreateOrLoad<MoneyConsumable>("Consumable_OverdueNotice");
        overdueNotice.flatMoney = 0;
        overdueNotice.moneyPerWord = 1;
        Name(overdueNotice, "Overdue Notice", price: 4);
        consumables.Add(overdueNotice);

        var freeRead = CreateOrLoad<NextWordMultiplierConsumable>("Consumable_FreeRead");
        NextWord(freeRead, multiplier: 1f, bonusPoints: 0, extraMoveCost: -1);
        Name(freeRead, "Free Read", price: 6);
        consumables.Add(freeRead);

        var transpose = CreateOrLoad<SwapTileConsumable>("Consumable_Transpose");
        Name(transpose, "Transpose", price: 5);
        consumables.Add(transpose);

        var coffeeRing = CreateOrLoad<AreaClearConsumable>("Consumable_CoffeeRing");
        coffeeRing.radius = 1;
        Name(coffeeRing, "Coffee Ring", price: 6);
        consumables.Add(coffeeRing);

        // Strong
        var bestseller = CreateOrLoad<NextWordMultiplierConsumable>("Consumable_Bestseller");
        NextWord(bestseller, multiplier: 3f, bonusPoints: 0, extraMoveCost: 0);
        Name(bestseller, "Bestseller", price: 12);
        consumables.Add(bestseller);

        var reprint = CreateOrLoad<CopyLastItemConsumable>("Consumable_Reprint");
        Name(reprint, "Reprint", price: 8);
        consumables.Add(reprint);

        var ghostwriter = CreateOrLoad<EncoreBookmarkConsumable>("Consumable_Ghostwriter");
        Name(ghostwriter, "Ghostwriter", price: 10);
        consumables.Add(ghostwriter);

        var epilogue = CreateOrLoad<FinishRoundConsumable>("Consumable_Epilogue");
        epilogue.threshold = 0.75f;
        Name(epilogue, "Epilogue", price: 12);
        consumables.Add(epilogue);

        var shredder = CreateOrLoad<ShredTileConsumable>("Consumable_Shredder");
        shredder.minimumBagSize = 40;
        Name(shredder, "Shredder", price: 8);
        consumables.Add(shredder);

        var correctionFluid = CreateOrLoad<ChooseLetterConsumable>("Consumable_CorrectionFluid");
        Name(correctionFluid, "Correction Fluid", price: 10);
        consumables.Add(correctionFluid);

        // Cursed
        var bannedBook = CreateOrLoad<NextWordMultiplierConsumable>("Consumable_BannedBook");
        NextWord(bannedBook, multiplier: 4f, bonusPoints: 0, extraMoveCost: 2);
        Name(bannedBook, "Banned Book", price: 6);
        consumables.Add(bannedBook);

        var plagiarism = CreateOrLoad<TradeBookmarkConsumable>("Consumable_Plagiarism");
        plagiarism.money = 15;
        Name(plagiarism, "Plagiarism", price: 2);
        consumables.Add(plagiarism);

        var rushOrder = CreateOrLoad<AllowanceConsumable>("Consumable_RushOrder");
        Allow(rushOrder, moves: 2, discards: 0, zeroDiscards: true);
        Name(rushOrder, "Rush Order", price: 4);
        consumables.Add(rushOrder);

        var spoiler = CreateOrLoad<AutoPlayBestWordConsumable>("Consumable_Spoiler");
        spoiler.multiplier = 2f;
        Name(spoiler, "Spoiler", price: 6);
        consumables.Add(spoiler);

        int added = AttachToModes(consumables);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Consumables ready in {ConsumableFolder}; {added} added to mode configs.");
        return consumables;
    }

    private static void NextWord(NextWordMultiplierConsumable asset, float multiplier, int bonusPoints, int extraMoveCost)
    {
        asset.multiplier = multiplier;
        asset.bonusPoints = bonusPoints;
        asset.extraMoveCost = extraMoveCost;
    }

    private static void Allow(AllowanceConsumable asset, int moves, int discards, bool zeroDiscards)
    {
        asset.moves = moves;
        asset.discards = discards;
        asset.zeroDiscards = zeroDiscards;
    }

    private static void Name(Consumable asset, string displayName, int price)
    {
        asset.displayName = displayName;

        // Seed only, never re-seed: an unpriced consumable (0) gets the default,
        // and anything already priced is left as tuned.
        if (asset.price == 0) asset.price = price;

        EditorUtility.SetDirty(asset);
    }

    private static T CreateOrLoad<T>(string name) where T : ScriptableObject
    {
        string path = $"{ConsumableFolder}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>
    /// Adds any consumable a mode doesn't already list. RogueDemoModeConfig
    /// rather than ModeConfig because the pool lives on the subclass — a
    /// consumable is a run-level idea and only a mode with runs has anywhere to
    /// put one.
    /// </summary>
    private static int AttachToModes(List<Consumable> consumables)
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RogueDemoModeConfig"))
        {
            var mode = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (mode == null) continue;

            bool changed = false;
            foreach (var consumable in consumables)
            {
                if (mode.consumables.Contains(consumable)) continue;
                mode.consumables.Add(consumable);
                changed = true;
                added++;
            }
            if (changed) EditorUtility.SetDirty(mode);
        }
        return added;
    }
}
