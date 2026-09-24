using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Puts the librarian's announcement in the Game scene, in the strip along the
/// bottom — below the ENTER / DISCARD row and above the seed line.
///
/// It's anchored to the BOTTOM rather than the top because everything around it
/// is: the board is framed to the camera and its lower edge moves with the
/// screen's aspect, while the buttons and the seed line never do. Top-anchoring
/// it would eventually put it through the board on some phone.
///
/// It used to sit directly under the board, at y 372. The bookmark row has that
/// band now, and the two swapping places rather than taking turns is what keeps
/// anything from jumping when a librarian round starts. The cost is that the
/// banner is less prominent than it was — worth knowing if the boss rules ever
/// stop being read.
///
/// Safe to re-run: adds the widget where it's missing and re-applies its
/// position (absolute, so a re-run can't walk it down the screen), but never
/// touches its styling once it exists.
/// </summary>
public static class RoundBannerSetup
{
    private const string PrefabPath = "Assets/Prefabs/Hud/RoundBannerWidget.prefab";
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string StatusPrefabPath = "Assets/Prefabs/Hud/StatusWidget.prefab";

    private const string LabelName = "Banner";

    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

    // BELOW the action row, which sits at y 180 and is 170 tall, and above the
    // seed line at y 24. Bottom aligned so a second line grows UP, into the gap
    // under the buttons rather than down over the seed.
    private static readonly Vector2 BannerAt = new Vector2(0f, 64f);
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

        var widget = Object.FindFirstObjectByType<RoundBannerWidget>(FindObjectsInactive.Include);
        if (widget == null)
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            placed.name = prefab.name;
            widget = placed.GetComponent<RoundBannerWidget>();
        }

        // Re-anchored every run, not just on creation. Without this, moving
        // BannerAt would change the PREFAB and leave the banner already sitting
        // in the scene exactly where it was — on top of the bookmark row.
        WordCrushSetup.Anchor(widget.gameObject, BottomCenter, BannerAt, BannerSize);

        // Writing straight to a RectTransform on a prefab INSTANCE isn't always
        // recorded as an override, and one that isn't is lost on the scene save.
        if (PrefabUtility.IsPartOfPrefabInstance(widget))
            PrefabUtility.RecordPrefabInstancePropertyModifications(widget.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Round banner ready in the Game scene, in the bottom strip below the buttons.");
    }
}
