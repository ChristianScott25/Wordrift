using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 🚧 TEMPORARY — builds the BOOKSHOP experiment's mode asset (2026-10-28).
///
/// The Bookshop is a second shop: it starts small and the player buys upgrades
/// for the shop itself (see BookshopScreen). It's an experiment, so it lives
/// beside the ordinary shop rather than replacing it — this mode is Rogue Demo
/// in every way except that a cleared round goes to the Bookshop scene, and the
/// main menu gets a button for it, so the two can be played side by side.
///
/// THE ASSET IS A COPY, like Mode_RogueDemo_Unlimited: every field is copied
/// from the real mode and then a few are overwritten, so round targets, the bag
/// and the librarians can't drift from the game being compared against. Re-run
/// this after retuning Mode_RogueDemo.
///
/// ⚠️ EXCEPT THE BOOKSHOP'S OWN NUMBERS, which are tuned HERE, on the copy —
/// they mean nothing on the real mode. A re-sync reads them off the copy first
/// and puts them back afterwards, so re-running this never undoes that tuning.
///
/// The copy also drops the two checkouts that are about the shop (the discount
/// and the cheaper reroll): those are meant to become shop UPGRADES, which the
/// first version doesn't sell yet.
///
/// TO DELETE THE EXPERIMENT if it isn't fun:
///   1. Assets/GameData/Mode_RogueDemo_Bookshop.asset and
///      Mode_RogueDemo_Bookshop_Unlimited.asset, and this file
///   2. Assets/Scenes/Bookshop.unity (and its Build Settings entry),
///      Editor/BookshopSceneSetup.cs, Scripts/UI/BookshopScreen.cs
///   3. Scripts/Shop/BookshopTuning.cs, the "🚧 Bookshop" block on
///      RogueDemoModeConfig, the "🚧 The Bookshop's upgrades" block on RunState
///      (plus its lines in Capture and the resume constructor), and
///      RunSaveData.shopLevels
///   4. the two Bookshop buttons in MainMenuSetup, and those buttons in Main Menu.unity
///   5. the sell half: RunState.SellPrice / SellBookmark / SellConsumable (Credit
///      can stay), RogueDemoModeConfig.sellRefundPercent, and the CardTapped /
///      TapCard / Redraw hook on BookmarkRowWidget (BookmarkCard's tap can go
///      back to calling Inspector.Show itself)
/// Shop/ShopOffers.cs and Shop/ShopRoller.cs STAY — the ordinary shop uses them.
/// </summary>
public static class BookshopModeSetup
{
    private const string DataFolder = "Assets/GameData";
    private const string SourcePath = DataFolder + "/Mode_RogueDemo.asset";

    /// <summary>
    /// ⚠️ Load-bearing twice, like the unlimited mode's: MainMenuSetup builds its
    /// button against this path, and RunState.CanResume matches a save by asset NAME.
    /// </summary>
    internal const string AssetName = "Mode_RogueDemo_Bookshop";
    internal const string AssetPath = DataFolder + "/" + AssetName + ".asset";

    /// <summary>
    /// 🚧 The Bookshop with unlimited money (2026-10-28), so every section and
    /// upgrade can be tried in one visit. A copy of the BOOKSHOP copy, so it
    /// carries the Bookshop's own numbers; rebuilt every time this runs. Same
    /// name rules as the others — the save matches a mode by asset name.
    /// </summary>
    internal const string UnlimitedName = "Mode_RogueDemo_Bookshop_Unlimited";
    internal const string UnlimitedPath = DataFolder + "/" + UnlimitedName + ".asset";

    /// <summary>The scene this mode's cleared rounds go to. BookshopSceneSetup builds it.</summary>
    internal const string SceneName = "Bookshop";

    [MenuItem("Word Crush/🚧 Create Bookshop Mode")]
    public static void CreateBookshopMode()
    {
        if (Build() != null)
            Debug.Log("Now run Word Crush > 🚧 Create Bookshop Scene, then Word Crush > Set Up Main Menu.");
    }

    internal static RogueDemoModeConfig Build()
    {
        var real = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(SourcePath);
        if (real == null)
        {
            Debug.LogError($"No {SourcePath} to copy. Run Word Crush > Create Rogue Demo Mode Asset first.");
            return null;
        }

        var copy = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(AssetPath);
        bool isNew = copy == null;

        // The copy's own Bookshop tuning, read BEFORE the copy overwrites it.
        List<BookshopSection> tunedSections = null;
        int tunedStep = 0;
        int tunedSell = 50;
        if (!isNew)
        {
            tunedSections = CloneSections(copy.bookshopSections);
            tunedStep = copy.bookshopUpgradeStep;
            tunedSell = copy.sellRefundPercent;
        }

        if (isNew)
        {
            copy = ScriptableObject.CreateInstance<RogueDemoModeConfig>();
            AssetDatabase.CreateAsset(copy, AssetPath);
        }

        EditorUtility.CopySerialized(real, copy);

        // ⚠️ CopySerialized copies m_Name too — see UnlimitedMoneyModeSetup.
        copy.name = AssetName;
        copy.displayName = "Rogue Demo — Bookshop";
        copy.shopSceneName = SceneName;

        // A copy of a test mode would carry its money rule along; this one is
        // about the shop's economy, so money has to bind.
        copy.unlimitedMoney = false;

        // Kept whenever the copy already existed — even an emptied list, which
        // is a choice (no sections at all), not a missing value.
        if (tunedSections != null)
        {
            copy.bookshopSections = tunedSections;
            copy.bookshopUpgradeStep = tunedStep;
            copy.sellRefundPercent = tunedSell;
        }
        else
        {
            // A brand-new copy gets the starting table from code, not whatever
            // the real mode happened to carry.
            copy.bookshopSections = RogueDemoModeConfig.DefaultBookshopSections();
            copy.bookshopUpgradeStep = 3;
            copy.sellRefundPercent = 50;
        }

        // The shop-shaped checkouts leave the pool. By TYPE, so a renamed
        // asset still goes.
        var kept = new List<Checkout>();
        if (copy.checkouts != null)
            foreach (var checkout in copy.checkouts)
                if (checkout != null && checkout is not ShopDiscountCheckout && checkout is not RerollDiscountCheckout)
                    kept.Add(checkout);
        copy.checkouts = kept;

        EditorUtility.SetDirty(copy);
        AssetDatabase.SaveAssets();

        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        var reloaded = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(AssetPath);
        if (reloaded == null || reloaded.name != AssetName)
            Debug.LogError($"{AssetPath} came back named '{reloaded?.name}', not '{AssetName}'. " +
                           "Saves would be resolved onto the wrong mode — fix the asset's name by hand.");

        Debug.Log(isNew
            ? $"Created {AssetPath} — Rogue Demo with the 🚧 Bookshop. Tune its Bookshop numbers on this asset."
            : $"Re-synced {AssetPath} from Mode_RogueDemo, keeping its own Bookshop numbers.");

        var bookshop = reloaded != null ? reloaded : copy;
        BuildUnlimited(bookshop);

        Selection.activeObject = bookshop;
        return bookshop;
    }

    /// <summary>
    /// The unlimited-money twin: everything copied off the Bookshop asset, then
    /// the name put back and money switched off as a limit. Nothing is tuned on
    /// it, so nothing on it is preserved — tune the Bookshop asset and re-run.
    /// </summary>
    private static void BuildUnlimited(RogueDemoModeConfig bookshop)
    {
        var twin = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(UnlimitedPath);
        bool isNew = twin == null;
        if (isNew)
        {
            twin = ScriptableObject.CreateInstance<RogueDemoModeConfig>();
            AssetDatabase.CreateAsset(twin, UnlimitedPath);
        }

        EditorUtility.CopySerialized(bookshop, twin);

        // ⚠️ CopySerialized copied the name — put it back, or both Bookshop
        // modes would claim the same save.
        twin.name = UnlimitedName;
        twin.displayName = "Rogue Demo — Bookshop (Unlimited)";
        twin.unlimitedMoney = true;

        EditorUtility.SetDirty(twin);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(UnlimitedPath, ImportAssetOptions.ForceUpdate);

        var reloaded = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(UnlimitedPath);
        if (reloaded == null || reloaded.name != UnlimitedName)
            Debug.LogError($"{UnlimitedPath} came back named '{reloaded?.name}', not '{UnlimitedName}'. " +
                           "Saves would be resolved onto the wrong mode — fix the asset's name by hand.");

        Debug.Log(isNew
            ? $"Created {UnlimitedPath} — the Bookshop with unlimited money. 🚧 TEST MODE."
            : $"Re-synced {UnlimitedPath} from the Bookshop asset.");
    }

    /// <summary>
    /// A deep copy, because CopySerialized is about to overwrite the list these
    /// objects live in and the tuning would go with it.
    /// </summary>
    private static List<BookshopSection> CloneSections(List<BookshopSection> sections)
    {
        var clone = new List<BookshopSection>();
        if (sections == null) return clone;

        foreach (var section in sections)
        {
            if (section == null) continue;
            var copy = new BookshopSection { section = section.section };
            if (section.levels != null)
                foreach (var level in section.levels)
                    if (level != null) copy.levels.Add(new BookshopLevel(level.slots, level.price));
            clone.Add(copy);
        }
        return clone;
    }
}
