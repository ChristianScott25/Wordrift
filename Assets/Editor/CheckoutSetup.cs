using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the checkout assets and puts them in every Rogue Demo mode's pool,
/// because ScriptableObjects can't be authored from the command line.
///
/// Idempotent, and it splits the fields the same two ways BookmarkSetup does:
/// what a checkout IS (its title and the size of its effect) is refreshed every
/// run, while its PRICE is a balance number, so a price is written only when the
/// asset's is still 0.
///
/// The ASSET FILE is named for the power and the TITLE is the book joke, on
/// purpose: a save names the file, so retitling one can never invalidate a run.
///
/// Adding another is a code + Inspector job — subclass Checkout, add a block
/// below, and re-run this.
/// </summary>
public static class CheckoutSetup
{
    private const string CheckoutFolder = "Assets/GameData/Checkouts";

    [MenuItem("Word Crush/Create Checkout Assets")]
    public static void CreateCheckouts() => Build();

    internal static List<Checkout> Build()
    {
        if (!AssetDatabase.IsValidFolder(CheckoutFolder))
            AssetDatabase.CreateFolder("Assets/GameData", "Checkouts");

        var checkouts = new List<Checkout>();

        var discount = CreateOrLoad<ShopDiscountCheckout>("Checkout_ShopDiscount");
        discount.discountPercent = 20;
        Name(discount, "Sense and Frugality", price: 30);
        checkouts.Add(discount);

        var discards = CreateOrLoad<ExtraDiscardsCheckout>("Checkout_ExtraDiscards");
        discards.extraDiscards = 2;
        Name(discards, "Second Thoughts", price: 25);
        checkouts.Add(discards);

        var payout = CreateOrLoad<PayoutBonusCheckout>("Checkout_PayoutBonus");
        payout.bonusPercent = 10;
        Name(payout, "Late Fees and Other Stories", price: 20);
        checkouts.Add(payout);

        var moves = CreateOrLoad<ExtraMovesCheckout>("Checkout_ExtraMoves");
        moves.extraMoves = 1;
        Name(moves, "One More Chapter", price: 35);
        checkouts.Add(moves);

        var interest = CreateOrLoad<InterestCheckout>("Checkout_Interest");
        interest.perTenHeld = 1;
        Name(interest, "Great Expectations", price: 40);
        checkouts.Add(interest);

        var reroll = CreateOrLoad<RerollDiscountCheckout>("Checkout_RerollDiscount");
        reroll.discountOff = 2;
        Name(reroll, "A Tale of Two Shelves", price: 20);
        checkouts.Add(reroll);

        int added = AttachToModes(checkouts);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Checkouts ready in {CheckoutFolder}; {added} added to mode configs.");
        return checkouts;
    }

    /// <summary>
    /// No description is written here: a Checkout derives its own from its
    /// numbers (Checkout.PowerText), so there is no second copy to go stale. The
    /// powerOverride field is for a human who wants different wording, and this
    /// must never touch it.
    /// </summary>
    private static void Name(Checkout asset, string displayName, int price)
    {
        asset.displayName = displayName;

        // Seed only, never re-seed: an unpriced checkout (0) gets the default,
        // and anything already priced is left as tuned.
        if (asset.price == 0) asset.price = price;

        EditorUtility.SetDirty(asset);
    }

    private static T CreateOrLoad<T>(string name) where T : ScriptableObject
    {
        string path = $"{CheckoutFolder}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>
    /// Adds any checkout a mode doesn't already list. RogueDemoModeConfig rather
    /// than ModeConfig because the pool lives on the subclass — checkouts are a
    /// run-level idea and only a mode with runs has anywhere to put them.
    /// </summary>
    private static int AttachToModes(List<Checkout> checkouts)
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RogueDemoModeConfig"))
        {
            var mode = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (mode == null) continue;

            bool changed = false;
            foreach (var checkout in checkouts)
            {
                if (mode.checkouts.Contains(checkout)) continue;
                mode.checkouts.Add(checkout);
                changed = true;
                added++;
            }
            if (changed) EditorUtility.SetDirty(mode);
        }
        return added;
    }
}
