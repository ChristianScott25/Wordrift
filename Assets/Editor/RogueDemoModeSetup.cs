using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Assets/GameData/Mode_RogueDemo.asset, because a ScriptableObject
/// can't be authored from the command line.
///
/// Idempotent, touches no scene, re-points the board and letter set at the
/// generated assets, and never overwrites the round numbers or tile-bag size on
/// an asset that already exists — so re-running it can't undo tuning.
/// </summary>
public static class RogueDemoModeSetup
{
    private const string DataFolder = "Assets/GameData";
    private const string AssetPath = DataFolder + "/Mode_RogueDemo.asset";

    [MenuItem("Word Crush/Create Rogue Demo Mode Asset")]
    public static void CreateRogueDemoMode()
    {
        // Loaded by path rather than copied off another mode: this is the only
        // mode there is now, so there's nothing to copy from.
        var shape = AssetDatabase.LoadAssetAtPath<BoardShapeAsset>($"{DataFolder}/Board_5x5.asset");
        var letters = AssetDatabase.LoadAssetAtPath<LetterSet>($"{DataFolder}/LetterSet_Scrabble.asset");

        if (shape == null || letters == null)
            Debug.LogWarning($"Missing Board_5x5 or LetterSet_Scrabble, so {AssetPath} may have no " +
                             "board or letter set. Assign them by hand, or run Rebuild Game Scene.");

        Build(shape, letters, LoadAll<TileModifier>(), LoadAll<TileSkin>());
    }

    /// <summary>
    /// Every asset of a kind already in the project. Used only to stock a
    /// brand-new mode asset — the modifier, skin and bookmark setups each
    /// attach themselves to every ModeConfig, so this is a convenience, not
    /// the wiring anything depends on.
    /// </summary>
    private static List<T> LoadAll<T>() where T : Object
    {
        var found = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) found.Add(asset);
        }
        return found;
    }

    /// <summary>
    /// Also called from WordCrushSetup.Rebuild, which passes the assets it just
    /// built rather than reloading them — freshly created assets aren't reliably
    /// loadable in the same run. Skins come through as null there because
    /// TileSkinSetup runs afterwards and adds the default skin to every config.
    /// </summary>
    internal static RogueDemoModeConfig Build(
        BoardShapeAsset shape, LetterSet letters,
        IReadOnlyList<TileModifier> modifiers, IReadOnlyList<TileSkin> skins)
    {
        var mode = AssetDatabase.LoadAssetAtPath<RogueDemoModeConfig>(AssetPath);
        bool isNew = mode == null;

        if (isNew)
        {
            mode = ScriptableObject.CreateInstance<RogueDemoModeConfig>();
            AssetDatabase.CreateAsset(mode, AssetPath);
        }

        mode.displayName = "Rogue Demo";
        if (shape != null) mode.boardShape = shape;
        if (letters != null) mode.letterSet = letters;

        // Moves, target score and tile-bag size are left alone on an existing
        // asset so hand tuning survives a re-run.
        if (isNew)
        {
            if (modifiers != null) mode.tileModifiers = new List<TileModifier>(modifiers);
            if (skins != null) mode.tileSkins = new List<TileSkin>(skins);
        }

        EditorUtility.SetDirty(mode);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(isNew
            ? $"Created {AssetPath}. Drop it into GameSession's Fallback Mode in the Game scene to play it, or wire it to a Main Menu button."
            : $"{AssetPath} already existed — refreshed its board and letter set, left the round numbers alone.");

        Selection.activeObject = mode;
        return mode;
    }
}
