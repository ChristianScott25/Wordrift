using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the four multiplier assets and puts them on every mode config,
/// because ScriptableObjects can't be authored from the command line.
///
/// Idempotent: the fields that define what a modifier *is* (multiplier, label,
/// colors) are refreshed every run. Its shop PRICE is different — that's a
/// balance number a human tunes — so a price is only written when the asset's
/// is still 0, the same way RogueDemoModeSetup refuses to overwrite tuned
/// round targets.
///
/// Adding a fifth kind is an Inspector job — Create -> Word Crush -> Tile
/// Modifier -> ..., set its label and color, add it to a mode's list.
/// </summary>
public static class TileModifierSetup
{
    private const string ModifierFolder = "Assets/GameData/Modifiers";

    private static readonly Color LetterBlue = new Color(0.20f, 0.52f, 0.88f, 1f);
    private static readonly Color LetterBlueDark = new Color(0.08f, 0.24f, 0.55f, 1f);
    private static readonly Color WordRed = new Color(0.88f, 0.28f, 0.26f, 1f);
    private static readonly Color WordRedDark = new Color(0.55f, 0.08f, 0.12f, 1f);

    [MenuItem("Word Crush/Create Tile Modifier Assets")]
    public static void CreateModifiers() => Build();

    internal static List<TileModifier> Build()
    {
        if (!AssetDatabase.IsValidFolder(ModifierFolder))
            AssetDatabase.CreateFolder("Assets/GameData", "Modifiers");

        var modifiers = new List<TileModifier>
        {
            Letter("DoubleLetter", 2, "2L", LetterBlue,     price: 5),
            Letter("TripleLetter", 3, "3L", LetterBlueDark, price: 9),
            Word("DoubleWord",     2, "2W", WordRed,        price: 14),
            Word("TripleWord",     3, "3W", WordRedDark,    price: 22),
        };

        int added = AttachToModes(modifiers);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Tile modifiers ready in {ModifierFolder}; {added} added to mode configs.");
        return modifiers;
    }

    private static LetterMultiplierModifier Letter(
        string name, int multiplier, string label, Color color, int price)
    {
        var asset = CreateOrLoad<LetterMultiplierModifier>(name);
        asset.multiplier = multiplier;
        Style(asset, label, color, price);
        return asset;
    }

    private static WordMultiplierModifier Word(
        string name, int multiplier, string label, Color color, int price)
    {
        var asset = CreateOrLoad<WordMultiplierModifier>(name);
        asset.multiplier = multiplier;
        Style(asset, label, color, price);
        return asset;
    }

    private static void Style(TileModifier asset, string label, Color color, int price)
    {
        asset.badgeLabel = label;
        asset.badgeColor = color;
        asset.badgeTextColor = Color.white;

        // Seed only, never re-seed: an unpriced modifier (0) gets the default
        // ladder, and anything already priced is left as tuned.
        if (asset.price == 0) asset.price = price;

        EditorUtility.SetDirty(asset);
    }

    private static T CreateOrLoad<T>(string name) where T : ScriptableObject
    {
        string path = $"{ModifierFolder}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>Adds any modifier a mode config doesn't already list.</summary>
    private static int AttachToModes(List<TileModifier> modifiers)
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ModeConfig"))
        {
            var mode = AssetDatabase.LoadAssetAtPath<ModeConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (mode == null) continue;

            bool changed = false;
            foreach (var modifier in modifiers)
            {
                if (mode.tileModifiers.Contains(modifier)) continue;
                mode.tileModifiers.Add(modifier);
                changed = true;
                added++;
            }
            if (changed) EditorUtility.SetDirty(mode);
        }
        return added;
    }
}
