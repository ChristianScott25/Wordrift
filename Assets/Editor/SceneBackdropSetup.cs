using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts a SceneBackdrop on the main camera of the Main Menu, the Shop and the
/// Game scene, and points it at the 🚧 temporary library picture.
///
/// Safe to re-run: adds the component only where it's missing, and fills the
/// sprite slot only when it's empty, so a picture swapped by hand sticks. The
/// texture's import settings ARE written every run, like the info box's —
/// they're what the code assumes, not a look to tune.
/// </summary>
public static class SceneBackdropSetup
{
    // 🚧 TEMPORARY AI ART — must be replaced before any release.
    private const string BackdropPath = "Assets/Sprites/Temporary AI sprites/Cozy Twilight Pixel Library.png";

    // Game last: each scene opens single-mode, so the last one is left open.
    private static readonly string[] Scenes =
    {
        "Assets/Scenes/Main Menu.unity",
        "Assets/Scenes/Shop.unity",
        "Assets/Scenes/Game.unity",
    };

    [MenuItem("Word Crush/Set Up Scene Backdrops")]
    public static void SetUpFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SetUp();
    }

    internal static void SetUp()
    {
        StampImport(BackdropPath);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);
        if (sprite == null)
        {
            Debug.LogError($"No sprite at {BackdropPath}.");
            return;
        }

        foreach (var path in Scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning($"No scene at {path} — skipped.");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError($"No camera in {path} — skipped.");
                continue;
            }

            var backdrop = camera.GetComponent<SceneBackdrop>();
            if (backdrop == null) backdrop = camera.gameObject.AddComponent<SceneBackdrop>();

            var serialized = new SerializedObject(backdrop);
            var slot = serialized.FindProperty("sprite");
            if (slot.objectReferenceValue == null)
            {
                slot.objectReferenceValue = sprite;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(backdrop);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Backdrop set up on '{camera.name}' in {path}.");
        }
    }

    /// <summary>
    /// One Single sprite, centre pivot, point filtered, no mipmaps, uncompressed.
    /// Point, unlike the book: this one is UPSCALED (1536 tall onto a ~2500-pixel
    /// screen), where bilinear visibly smears the chunky pixels, and they're big
    /// enough that point filtering's slightly uneven widths don't show.
    /// </summary>
    private static void StampImport(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            Debug.LogError($"No texture at {path}.");
            return;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool changed = false;

        if (settings.textureType != TextureImporterType.Sprite) { settings.textureType = TextureImporterType.Sprite; changed = true; }
        if (settings.spriteMode != (int)SpriteImportMode.Single) { settings.spriteMode = (int)SpriteImportMode.Single; changed = true; }
        if (settings.spriteAlignment != (int)SpriteAlignment.Center) { settings.spriteAlignment = (int)SpriteAlignment.Center; changed = true; }
        if (settings.filterMode != FilterMode.Point) { settings.filterMode = FilterMode.Point; changed = true; }
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
