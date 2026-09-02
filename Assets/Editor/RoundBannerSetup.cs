using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts the librarian's announcement in the Game scene, in the empty band below
/// the board and above the ENTER / DISCARD row.
///
/// It's anchored to the BOTTOM rather than the top, because that band is defined
/// by the buttons underneath it: the board is framed to the camera and its
/// bottom edge moves with the screen's aspect, while the buttons never do. Top-
/// anchoring it would eventually put it through the board on some phone.
///
/// Safe to re-run: adds the widget only where it's missing, and never touches
/// its position or styling once it exists.
/// </summary>
public static class RoundBannerSetup
{
    private const string PrefabPath = "Assets/Prefabs/Hud/RoundBannerWidget.prefab";
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string StatusPrefabPath = "Assets/Prefabs/Hud/StatusWidget.prefab";

    private const string LabelName = "Banner";

    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

    // Just above the action row, which sits at y 180 and is 170 tall. Bottom
    // aligned so a second line grows UP, away from the buttons.
    private static readonly Vector2 BannerAt = new Vector2(0f, 372f);
    private static readonly Vector2 BannerSize = new Vector2(1000f, 110f);
    private static readonly Color BannerColor = new Color(1f, 0.45f, 0.45f, 1f);

    [MenuItem("Word Crush/Set Up Round Banner")]
    public static void SetUp()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    /// <summary>Also called from WordCrushSetup.Rebuild, after the scene is wired.</summary>
    internal static void Run()
    {
        RemoveStrayStatusBanner();

        var prefab = BuildPrefab();
        if (prefab == null) return;

        PlaceInGameScene(prefab);
    }

    /// <summary>
    /// The banner briefly lived inside StatusWidget, where it could only sit
    /// under the move counter — which is where the selected word is drawn, so it
    /// was covered. Nothing reads that label any more; this clears it out so a
    /// prefab from that day doesn't keep an invisible orphan.
    /// </summary>
    private static void RemoveStrayStatusBanner()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(StatusPrefabPath) == null) return;

        var contents = PrefabUtility.LoadPrefabContents(StatusPrefabPath);
        try
        {
            var stray = contents.transform.Find(LabelName);
            if (stray == null) return;

            Object.DestroyImmediate(stray.gameObject);
            PrefabUtility.SaveAsPrefabAsset(contents, StatusPrefabPath);
            Debug.Log("Removed the status widget's old Banner label — it lives in its own widget now.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static GameObject BuildPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            var made = WordCrushSetup.NewUI("RoundBannerWidget", typeof(RoundBannerWidget));
            WordCrushSetup.Anchor(made, BottomCenter, BannerAt, BannerSize);
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
        var widget = widgetRoot.GetComponent<RoundBannerWidget>()
                     ?? widgetRoot.AddComponent<RoundBannerWidget>();

        var existing = widgetRoot.transform.Find(LabelName);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label == null)
        {
            // 30pt for the name; the power rides under it at 80% from the mode.
            var made = WordCrushSetup.MakeText(widgetRoot.transform, LabelName, 30,
                                               TextAlignmentOptions.Bottom);
            made.color = BannerColor;
            made.text = "";
            WordCrushSetup.Anchor(made.gameObject, BottomCenter, Vector2.zero, BannerSize);
            label = made;
        }

        WordCrushSetup.SetRef(widget, "label", label);
    }

    private static void PlaceInGameScene(GameObject prefab)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
        {
            Debug.LogError($"No scene at {GameScenePath}.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError($"{GameScenePath} has no Canvas — nothing to attach the banner to.");
            return;
        }

        if (Object.FindFirstObjectByType<RoundBannerWidget>(FindObjectsInactive.Include) == null)
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            placed.name = prefab.name;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Round banner ready in the Game scene, below the board.");
    }
}
