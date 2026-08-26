using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the four multiplier assets and puts them on every mode config,
/// because ScriptableObjects can't be authored from the command line.
///
/// Idempotent, and careful about what it overwrites: the fields that define
/// what a modifier *is* (multiplier, label, colors) are refreshed every run,
/// but spawnChance is only seeded when it's still zero. So tuning survives,
/// while a modifier that was never switched on gets a sensible starting value.
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
            Letter("DoubleLetter", 2, "2L", LetterBlue,     seedChance: 0.05f),
            Letter("TripleLetter", 3, "3L", LetterBlueDark, seedChance: 0.02f),
            Word("DoubleWord",     2, "2W", WordRed,        seedChance: 0.02f),
            Word("TripleWord",     3, "3W", WordRedDark,    seedChance: 0.01f),
        };

        int added = AttachToModes(modifiers);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Tile modifiers ready in {ModifierFolder}; {added} added to mode configs. " +
                  "Spawn chances are only seeded while they're zero — tune them on the assets.");
        return modifiers;
    }

    private static LetterMultiplierModifier Letter(
        string name, int multiplier, string label, Color color, float seedChance)
    {
        var asset = CreateOrLoad<LetterMultiplierModifier>(name);
        asset.multiplier = multiplier;
        Style(asset, label, color, seedChance);
        return asset;
    }

    private static WordMultiplierModifier Word(
        string name, int multiplier, string label, Color color, float seedChance)
    {
        var asset = CreateOrLoad<WordMultiplierModifier>(name);
        asset.multiplier = multiplier;
        Style(asset, label, color, seedChance);
        return asset;
    }

    private static void Style(TileModifier asset, string label, Color color, float seedChance)
    {
        asset.badgeLabel = label;
        asset.badgeColor = color;
        asset.badgeTextColor = Color.white;

        // Zero means "never switched on", so it's safe to seed. Anything else is
        // a deliberate value and gets left alone.
        if (Mathf.Approximately(asset.spawnChance, 0f)) asset.spawnChance = seedChance;

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
