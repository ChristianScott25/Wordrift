using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds (or re-styles) the score label on Assets/Prefabs/Tile.prefab.
///
/// This exists because a world-space TextMeshPro object can't be authored from
/// the command line — its serialization is large and version-specific, and
/// hand-editing prefab YAML desyncs Unity's import cache. So this does it
/// through the real API instead.
///
/// Unlike Rebuild Game Scene &amp; Assets, this is safe to re-run: it updates the
/// existing label rather than adding a second one, and touches nothing else on
/// the prefab. Once it's run, tune the label in the Inspector like anything else.
/// </summary>
public static class TileScoreLabelSetup
{
    private const string PrefabPath = "Assets/Prefabs/Tile.prefab";
    private const string LabelName = "Score";

    [MenuItem("Word Crush/Add Tile Score Label")]
    public static void AddScoreLabel()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError($"No prefab found at {PrefabPath}.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var tile = root.GetComponent<Tile>();
            if (tile == null)
            {
                Debug.LogError($"{PrefabPath} has no Tile component on its root.");
                return;
            }

            var label = FindOrCreateLabel(root);

            // Style only — Tile sets the transform from the art at runtime.
            label.text = "1";
            // Tile cancels its own fit scaling for this label, so font size lands
            // at roughly (fontSize / 10) * cellSize * fillFraction world units —
            // ~25% of the tile's height here. Tune it on the prefab afterwards.
            label.fontSize = 2.5f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.BottomRight;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = new Color(0.16f, 0.17f, 0.23f, 1f);

            var rect = label.rectTransform;
            rect.pivot = new Vector2(1f, 0f);   // bottom-right, so it sits on the corner
            rect.sizeDelta = new Vector2(1.2f, 0.9f);
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            if (!WireLabel(tile, label)) return;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"Score label ready on {PrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static TextMeshPro FindOrCreateLabel(GameObject root)
    {
        var existing = root.transform.Find(LabelName);
        if (existing != null)
        {
            return existing.GetComponent<TextMeshPro>()
                   ?? existing.gameObject.AddComponent<TextMeshPro>();
        }

        // RectTransform up front: TextMeshPro needs one, and swapping a plain
        // Transform for it after the fact is messier than creating it correctly.
        var created = new GameObject(LabelName, typeof(RectTransform), typeof(TextMeshPro));
        created.transform.SetParent(root.transform, false);
        return created.GetComponent<TextMeshPro>();
    }

    /// <summary>Assigns Tile's private scoreLabel field.</summary>
    private static bool WireLabel(Tile tile, TextMeshPro label)
    {
        var serialized = new SerializedObject(tile);
        var property = serialized.FindProperty("scoreLabel");
        if (property == null)
        {
            Debug.LogError("Tile has no 'scoreLabel' field — did the scripts compile?");
            return false;
        }

        property.objectReferenceValue = label;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
