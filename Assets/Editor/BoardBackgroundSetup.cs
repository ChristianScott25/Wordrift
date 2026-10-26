using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts a BoardBackground on the Board in the Game scene and points it at the
/// square sprite. Needed because adding a component and assigning a reference in
/// a scene can't be done from the command line.
///
/// Idempotent: it won't add a second component, and it only assigns the sprite
/// when the slot is empty, so a swapped sprite or a re-tuned colour survives.
/// The board colour, border width and draw order are all Inspector fields on the
/// component — this command never touches them after creation.
/// </summary>
public static class BoardBackgroundSetup
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string SquareSpritePath = "Assets/Sprites/White Square.png";

    // 🚧 TEMPORARY AI ART — must be replaced before any release, along with
    // everything else in that folder.
    private const string BookSpritePath = "Assets/Sprites/Temporary AI sprites/Pixel Art Open Journal Interface.png";

    // 9-slice border in sprite pixels (left, bottom, right, top — Unity's order).
    // Top and bottom are what matter: they keep the corner ornaments and the
    // ribbon out of the stretched middle, which is plain paper and straight
    // lines from y=175 down to y=1052. Sideways the art is never stretched.
    private static readonly Vector4 BookBorder = new Vector4(190, 232, 159, 175);

    [MenuItem("Word Crush/Set Up Board Background")]
    public static void SetUpFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SetUp();
    }

    internal static void SetUp()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var board = Object.FindFirstObjectByType<Board>();
        if (board == null)
        {
            Debug.LogError($"No Board in {ScenePath} — run 'Rebuild Game Scene & Assets' first.");
            return;
        }

        var background = board.GetComponent<BoardBackground>();
        bool isNew = background == null;
        if (isNew) background = board.gameObject.AddComponent<BoardBackground>();

        var serialized = new SerializedObject(background);
        var sprite = serialized.FindProperty("cellSprite");
        if (sprite == null)
        {
            Debug.LogError("BoardBackground has no 'cellSprite' field — did the scripts compile?");
            return;
        }

        // Only fill an empty slot: a sprite swapped by hand should stick.
        if (sprite.objectReferenceValue == null)
        {
            var square = AssetDatabase.LoadAllAssetsAtPath(SquareSpritePath).OfType<Sprite>().FirstOrDefault();
            if (square == null)
            {
                Debug.LogError($"No sprite found at {SquareSpritePath}.");
                return;
            }

            sprite.objectReferenceValue = square;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            serialized.Update();
            if (serialized.FindProperty("cellSprite").objectReferenceValue != square)
            {
                Debug.LogError("Failed to assign 'cellSprite' — reference did not persist.");
                return;
            }
        }

        AssignBookSprite(background);

        EditorUtility.SetDirty(background);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(isNew
            ? $"Added BoardBackground to the Board in {ScenePath}. Colour, border width and draw order are on the component."
            : $"BoardBackground already on the Board in {ScenePath} — left its settings alone.");
    }

    /// <summary>
    /// 🚧 Fills the book-sprite slot, if it's empty, with the temporary AI book —
    /// after stamping the import settings sliced drawing needs. A sprite swapped
    /// by hand sticks, same rule as the cell sprite.
    /// </summary>
    private static void AssignBookSprite(BoardBackground background)
    {
        StampBookImport(BookSpritePath);

        var serialized = new SerializedObject(background);
        var slot = serialized.FindProperty("pageSprite");
        if (slot == null || slot.objectReferenceValue != null) return;

        var book = AssetDatabase.LoadAssetAtPath<Sprite>(BookSpritePath);
        if (book == null)
        {
            Debug.LogWarning($"No sprite at {BookSpritePath} — the board keeps the flat page.");
            return;
        }

        slot.objectReferenceValue = book;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// One Single sprite, centre pivot, Full Rect mesh (a SpriteRenderer can't
    /// 9-slice a Tight mesh), the border above, bilinear and uncompressed.
    /// Bilinear, unlike the info box's point filter: this "pixel art" isn't on a
    /// real pixel grid and lands on screen at a non-whole scale, so point
    /// filtering would make some of its chunky pixels a screen pixel wider than
    /// others. Re-imports only when something had to change.
    /// </summary>
    private static void StampBookImport(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool changed = false;

        if (settings.textureType != TextureImporterType.Sprite) { settings.textureType = TextureImporterType.Sprite; changed = true; }
        if (settings.spriteMode != (int)SpriteImportMode.Single) { settings.spriteMode = (int)SpriteImportMode.Single; changed = true; }
        if (settings.spriteMeshType != SpriteMeshType.FullRect) { settings.spriteMeshType = SpriteMeshType.FullRect; changed = true; }
        if (settings.spriteAlignment != (int)SpriteAlignment.Center) { settings.spriteAlignment = (int)SpriteAlignment.Center; changed = true; }
        if (settings.spriteBorder != BookBorder) { settings.spriteBorder = BookBorder; changed = true; }
        if (settings.filterMode != FilterMode.Bilinear) { settings.filterMode = FilterMode.Bilinear; changed = true; }
        if (settings.mipmapEnabled) { settings.mipmapEnabled = false; changed = true; }
        if (changed) importer.SetTextureSettings(settings);

        var platform = importer.GetDefaultPlatformTextureSettings();
        if (platform.textureCompression != TextureImporterCompression.Uncompressed)
        {
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
            changed = true;
        }

        if (changed) importer.SaveAndReimport();
    }
}
