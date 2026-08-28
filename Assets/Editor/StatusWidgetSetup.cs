using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the status widget its second and third lines — the goal readout and
/// the mode's extra line (the run's bookmarks today).
///
/// It edits the PREFAB, not the scene, which is what makes it safe: the Game
/// scene holds an instance, so it picks the new labels up on its own and the
/// destructive Rebuild Game Scene is never needed. Idempotent — a label that
/// already exists is left exactly as it is, position included, so nothing you
/// tune in the Inspector gets undone.
///
/// Without this the widget still works: StatusWidget drops an unwired line
/// rather than failing, and the goal keeps riding along with the name label.
/// </summary>
public static class StatusWidgetSetup
{
    private const string PrefabPath = "Assets/Prefabs/Hud/StatusWidget.prefab";

    [MenuItem("Word Crush/Set Up Status Widget")]
    public static void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"No status widget prefab at {PrefabPath}.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var widget = root.GetComponent<StatusWidget>();
            if (widget == null)
            {
                Debug.LogError("The status widget prefab has no StatusWidget component.");
                return;
            }

            // The value label ends around y = -146; these sit under it. Narrow
            // enough (600 of the 1080-wide canvas) to clear the score readout
            // in the opposite corner.
            var goal = Ensure(root.transform, "Goal", 30, new Vector2(0f, -150f), new Vector2(600f, 44f),
                              new Color(1f, 1f, 1f, 0.75f));
            var extra = Ensure(root.transform, "Extra", 26, new Vector2(0f, -196f), new Vector2(600f, 40f),
                               new Color(1f, 0.75f, 0.1f, 0.9f));

            WordCrushSetup.SetRef(widget, "goalLabel", goal);
            WordCrushSetup.SetRef(widget, "extraLabel", extra);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Status widget now has its goal and extra lines. " +
                      "The Game scene's instance follows the prefab — nothing to re-wire.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Finds a label by name, or authors one the first time.</summary>
    private static TMP_Text Ensure(Transform parent, string name, float size,
                                   Vector2 offset, Vector2 area, Color color)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<TMP_Text>();

        var text = WordCrushSetup.MakeText(parent, name, size, TextAlignmentOptions.TopLeft);
        WordCrushSetup.Anchor(text.gameObject, new Vector2(0f, 1f), offset, area);
        text.color = color;
        return text;
    }
}
