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

        // 🚧 Both prices are a first guess. A cleared round 1 pays about $19, so
        // these are meant to be the cheap impulse buy next to a $30 checkout —
        // something you take when the shelf has nothing permanent worth saving
        // for. Nothing here has been balanced against a full run.
        var shuffle = CreateOrLoad<ShuffleConsumable>("Consumable_Shuffle");
        Name(shuffle, "Shuffle", price: 6);
        consumables.Add(shuffle);

        var doubler = CreateOrLoad<NextWordMultiplierConsumable>("Consumable_Doubler");
        doubler.multiplier = 2f;
        Name(doubler, "Doubler", price: 8);
        consumables.Add(doubler);

        int added = AttachToModes(consumables);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Consumables ready in {ConsumableFolder}; {added} added to mode configs.");
        return consumables;
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
