using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Assets/GameData/Mode_RogueDemo.asset, because a ScriptableObject
/// can't be authored from the command line.
///
/// Idempotent, touches no scene, re-points the board and letter set at whatever
/// Moves mode uses, and never overwrites the round numbers or bag size on an
/// asset that already exists — so re-running it can't undo tuning.
/// </summary>
public static class RogueDemoModeSetup
{
    private const string DataFolder = "Assets/GameData";
    private const string AssetPath = DataFolder + "/Mode_RogueDemo.asset";

    [MenuItem("Word Crush/Create Rogue Demo Mode Asset")]
    public static void CreateRogueDemoMode()
    {
        var moves = AssetDatabase.LoadAssetAtPath<MovesModeConfig>($"{DataFolder}/Mode_Moves.asset");
        if (moves == null)
        {
            Debug.LogWarning($"No Mode_Moves.asset found, so {AssetPath} has no board or letter " +
                             "set. Assign Board Shape and Letter Set on it by hand.");
            Build(null, null, null, null);
            return;
        }

        Build(moves.boardShape, moves.letterSet, moves.tileModifiers, moves.tileSkins);
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

        // Moves, target score and bag size are left alone on an existing asset
        // so hand tuning survives a re-run.
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
