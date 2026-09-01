using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts the run's seed in the bottom-left corner of both the Game scene and the
/// Shop, as one small dim line.
///
/// Two scenes rather than one because the seed is most useful exactly where you
/// stop to look at something — and the shop is where you'd copy it down.
///
/// Safe to re-run: adds the widget only where it's missing, and never touches
/// its styling once it exists.
/// </summary>
public static class SeedWidgetSetup
{
    private const string PrefabPath = "Assets/Prefabs/Hud/SeedWidget.prefab";
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string ShopScenePath = "Assets/Scenes/Shop.unity";

    private const string LabelName = "Seed";

    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Color DimColor = new Color(1f, 1f, 1f, 0.35f);

    [MenuItem("Word Crush/Set Up Seed Display")]
    public static void SetUp()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    /// <summary>Also called from WordCrushSetup.Rebuild, after the scene is wired.</summary>
    internal static void Run()
    {
        var prefab = BuildPrefab();
        if (prefab == null) return;

        // Shop first, Game last: each of these opens its scene single-mode, so
        // whichever runs last is the one left open. Finishing on Game is what
        // anyone running this expects to be looking at.
        PlaceIn(ShopScenePath, prefab);
        PlaceIn(GameScenePath, prefab);
    }

    private static GameObject BuildPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            var made = WordCrushSetup.NewUI("SeedWidget", typeof(SeedWidget));
            WordCrushSetup.Anchor(made, BottomLeft, new Vector2(30f, 24f), new Vector2(420f, 40f));
            EnsureContents(made);

            var saved = PrefabUtility.SaveAsPrefabAsset(made, PrefabPath);
            Object.DestroyImmediate(made);
            Debug.Log($"Created {PrefabPath}.");
            return saved;
        }

        var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            EnsureContents(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void EnsureContents(GameObject widgetRoot)
    {
        var widget = widgetRoot.GetComponent<SeedWidget>() ?? widgetRoot.AddComponent<SeedWidget>();

        var existing = widgetRoot.transform.Find(LabelName);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label == null)
        {
            var made = WordCrushSetup.MakeText(widgetRoot.transform, LabelName, 24,
                                               TextAlignmentOptions.BottomLeft);
            made.color = DimColor;
            made.text = "";
            label = made;
        }

        WordCrushSetup.Anchor(label.gameObject, BottomLeft, Vector2.zero, new Vector2(420f, 40f));
        WordCrushSetup.SetRef(widget, "label", label);
    }

    private static void PlaceIn(string scenePath, GameObject prefab)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            // The Shop scene only exists once Create Shop Scene has been run.
            Debug.LogWarning($"No scene at {scenePath} — skipped placing the seed display there.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError($"{scenePath} has no Canvas — nothing to attach the seed display to.");
            return;
        }

        if (Object.FindFirstObjectByType<SeedWidget>(FindObjectsInactive.Include) == null)
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            placed.name = prefab.name;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Seed display ready in {scenePath}.");
    }
}
