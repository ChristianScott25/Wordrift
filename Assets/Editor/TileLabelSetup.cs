using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors the two text labels on Assets/Prefabs/Tile.prefab — the letter and
/// the score — and gives the prefab a default tile sprite to fall back on.
///
/// This exists because world-space TextMeshPro objects can't be authored from
/// the command line: their serialization is large and version-specific, and
/// hand-editing prefab YAML desyncs Unity's import cache. So this does it
/// through the real API instead.
///
/// Unlike Rebuild Game Scene &amp; Assets, it's safe to re-run: it updates the
/// existing labels rather than adding more, and touches nothing else on the
/// prefab. Once it's run, tune the labels in the Inspector like anything else —
/// but note that Tile.LayOutLabels drives their position and scale at runtime,
/// so style them (font, size, color) and leave the transforms alone.
/// </summary>
public static class TileLabelSetup
{
    private const string PrefabPath = "Assets/Prefabs/Tile.prefab";
    private const string DefaultTileSprite = "Assets/Sprites/Tile - white.png";

    private const string LetterName = "Letter";
    private const string ScoreName = "Score";

    private static readonly Color InkColor = new Color(0.16f, 0.17f, 0.23f, 1f);

    [MenuItem("Word Crush/Set Up Tile Prefab")]
    public static void SetUpTilePrefab()
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

            // A default body so the prefab reads correctly on its own, and so a
            // mode with no skins assigned still draws something sensible.
            var renderer = root.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite == null)
            {
                var sprite = LoadSprite(DefaultTileSprite);
                if (sprite != null) renderer.sprite = sprite;
                else Debug.LogWarning($"No sprite found at {DefaultTileSprite}.");
            }

            // The badge used to sort *behind* the body, which was survivable while
            // the letter art had transparency and is not now that the body is a
            // solid tile. Modifier tiles are dormant (spawnChance 0), so this has
            // never been visible either way.
            var badge = root.transform.Find("Badge");
            if (badge != null && renderer != null &&
                badge.TryGetComponent<SpriteRenderer>(out var badgeRenderer))
            {
                badgeRenderer.sortingLayerID = renderer.sortingLayerID;
                badgeRenderer.sortingOrder = renderer.sortingOrder + 3;
            }

            // The letter: big and centred. Tile overwrites .text per spawn.
            var letter = FindOrCreateLabel(root, LetterName);
            StyleLabel(letter, fontSize: 6f, align: TextAlignmentOptions.Center,
                       pivot: new Vector2(0.5f, 0.5f), size: new Vector2(2f, 2f));
            letter.text = "A";

            // The score: small, tucked into the bottom-right corner.
            var score = FindOrCreateLabel(root, ScoreName);
            StyleLabel(score, fontSize: 2.5f, align: TextAlignmentOptions.BottomRight,
                       pivot: new Vector2(1f, 0f), size: new Vector2(1.2f, 0.9f));
            score.text = "1";

            if (!WireLabel(tile, "letterLabel", letter)) return;
            if (!WireLabel(tile, "scoreLabel", score)) return;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"Tile prefab ready: letter + score labels wired on {PrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Sprites imported as "Multiple" live as sub-assets, so load them all.</summary>
    private static Sprite LoadSprite(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    private static void StyleLabel(TextMeshPro label, float fontSize,
                                   TextAlignmentOptions align, Vector2 pivot, Vector2 size)
    {
        // Tile cancels its own fit scaling for these labels, so a font size of N
        // lands at roughly (N / 10) * cellSize * fillFraction world units —
        // about 60% of the tile's height for the letter, 25% for the score.
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = InkColor;

        // Position and scale are set from the tile art at runtime; only the
        // pivot and the box matter here.
        var rect = label.rectTransform;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static TextMeshPro FindOrCreateLabel(GameObject root, string name)
    {
        var existing = root.transform.Find(name);
        if (existing != null)
        {
            return existing.GetComponent<TextMeshPro>()
                   ?? existing.gameObject.AddComponent<TextMeshPro>();
        }

        // RectTransform up front: TextMeshPro needs one, and swapping a plain
        // Transform for it after the fact is messier than creating it correctly.
        var created = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
        created.transform.SetParent(root.transform, false);
        return created.GetComponent<TextMeshPro>();
    }

    /// <summary>Assigns one of Tile's private label fields.</summary>
    private static bool WireLabel(Tile tile, string field, TextMeshPro label)
    {
        var serialized = new SerializedObject(tile);
        var property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"Tile has no '{field}' field — did the scripts compile?");
            return false;
        }

        property.objectReferenceValue = label;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
