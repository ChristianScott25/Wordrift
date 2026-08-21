using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Assets/GameData/Mode_Overflow.asset, because a ScriptableObject
/// can't be authored from the command line.
///
/// Idempotent and narrow, like TileScoreLabelSetup: it re-points the board and
/// letter set at whatever Timed mode uses, but never overwrites the pacing
/// numbers on an asset that already exists — so re-running it can't undo
/// tuning. Safe to run any time; it does NOT touch any scene.
/// </summary>
public static class OverflowModeSetup
{
    private const string DataFolder = "Assets/GameData";
    private const string AssetPath = DataFolder + "/Mode_Overflow.asset";

    [MenuItem("Word Crush/Create Overflow Mode Asset")]
    public static void CreateOverflowMode()
    {
        var timed = AssetDatabase.LoadAssetAtPath<TimedModeConfig>($"{DataFolder}/Mode_Timed.asset");
        if (timed == null)
        {
            Debug.LogWarning($"No Mode_Timed.asset found, so {AssetPath} has no board or letter " +
                             "set. Assign Board Shape and Letter Set on it by hand.");
            Build(null, null, null);
            return;
        }

        Build(timed.boardShape, timed.letterSet, timed.tileModifiers);
    }

    /// <summary>
    /// Also called from WordCrushSetup.Rebuild, which passes the assets it just
    /// built rather than reloading them — freshly created assets aren't
    /// reliably loadable in the same run.
    /// </summary>
    internal static OverflowModeConfig Build(
        BoardShapeAsset shape, LetterSet letters, IReadOnlyList<TileModifier> modifiers)
    {
        var mode = AssetDatabase.LoadAssetAtPath<OverflowModeConfig>(AssetPath);
        bool isNew = mode == null;

        if (isNew)
        {
            mode = ScriptableObject.CreateInstance<OverflowModeConfig>();
            AssetDatabase.CreateAsset(mode, AssetPath);
        }

        mode.displayName = "Overflow";
        if (shape != null) mode.boardShape = shape;
        if (letters != null) mode.letterSet = letters;

        // Pacing is left alone on an existing asset so hand tuning survives.
        if (isNew && modifiers != null) mode.tileModifiers = new List<TileModifier>(modifiers);

        EditorUtility.SetDirty(mode);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(isNew
            ? $"Created {AssetPath}. Assign it to a Main Menu button, or to GameSession's Fallback Mode to test it directly."
            : $"{AssetPath} already existed — refreshed its board and letter set, left the pacing numbers alone.");

        Selection.activeObject = mode;
        return mode;
    }
}
