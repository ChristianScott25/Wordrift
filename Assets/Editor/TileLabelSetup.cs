using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors the three text labels on Assets/Prefabs/Tile.prefab — the letter, the
/// score, and the multiplier badge — plus the selection box behind the tile, and
/// gives the prefab a default tile sprite to fall back on.
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

    // A sharp square, so the overhang reads as a box rather than following the
    // tile's rounded corners. Swap this on the prefab for "Tile - white" if a
    // rounded border is wanted instead — Tile sizes whatever it finds.
    private const string SelectionBoxSprite = "Assets/Sprites/White Square.png";

    private const string LetterName = "Letter";
    private const string ScoreName = "Score";
    private const string BadgeLabelName = "BadgeLabel";
    private const string SelectionBoxName = "SelectionBox";

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

            // The badge circle used to sort *behind* the body, which was survivable
            // while the letter art had transparency and is not now that the body is
            // a solid tile. Tile re-applies this at runtime; setting it here keeps
            // the prefab honest when you look at it in isolation.
            var badge = root.transform.Find("Badge");
            if (badge != null && renderer != null &&
                badge.TryGetComponent<SpriteRenderer>(out var badgeRenderer))
            {
                badgeRenderer.sortingLayerID = renderer.sortingLayerID;
                badgeRenderer.sortingOrder = renderer.sortingOrder + 3;
                badgeRenderer.enabled = false;   // only a modifier turns it on
            }

            // The selection box: a square behind the body, switched on while the
            // tile is selected. Tile sizes and sorts it at runtime from the body
            // sprite's bounds, so only the sprite matters here.
            var box = FindOrCreateSelectionBox(root);
            if (box.sprite == null)
            {
                var boxSprite = LoadSprite(SelectionBoxSprite);
                if (boxSprite != null) box.sprite = boxSprite;
                else Debug.LogWarning($"No sprite found at {SelectionBoxSprite}.");
            }
            if (renderer != null)
            {
                box.sortingLayerID = renderer.sortingLayerID;
                box.sortingOrder = renderer.sortingOrder - 1;
            }
            box.enabled = false;   // only a selection turns it on

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

            // The multiplier badge: two characters inside the circle. Smaller than
            // the score on purpose — "2L" has to fit across a circle that's only
            // ~38% of the tile wide, and the outline eats into that.
            var badgeText = FindOrCreateLabel(root, BadgeLabelName);
            StyleLabel(badgeText, fontSize: 1.6f, align: TextAlignmentOptions.Center,
                       pivot: new Vector2(0.5f, 0.5f), size: new Vector2(2f, 2f));
            badgeText.text = "2L";
            badgeText.color = Color.white;
            badgeText.enabled = false;   // only a modifier turns it on

            if (!WireRef(tile, "letterLabel", letter)) return;
            if (!WireRef(tile, "scoreLabel", score)) return;
            if (!WireRef(tile, "badgeLabel", badgeText)) return;
            if (!WireRef(tile, "selectionBox", box)) return;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"Tile prefab ready: letter, score and badge labels plus the " +
                      $"selection box wired on {PrefabPath}.");
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

    private static SpriteRenderer FindOrCreateSelectionBox(GameObject root)
    {
        var existing = root.transform.Find(SelectionBoxName);
        if (existing != null)
        {
            return existing.GetComponent<SpriteRenderer>()
                   ?? existing.gameObject.AddComponent<SpriteRenderer>();
        }

        var created = new GameObject(SelectionBoxName, typeof(SpriteRenderer));
        created.transform.SetParent(root.transform, false);
        return created.GetComponent<SpriteRenderer>();
    }

    /// <summary>Assigns one of Tile's private serialized reference fields.</summary>
    private static bool WireRef(Tile tile, string field, Object value)
    {
        var serialized = new SerializedObject(tile);
        var property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"Tile has no '{field}' field — did the scripts compile?");
            return false;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
