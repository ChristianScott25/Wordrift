using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates the POINTS x MULT readout — Assets/Prefabs/Hud/ScoreTallyWidget.prefab
/// — and puts one in the Game scene's HUD canvas.
///
/// It lands in the strip between the word preview and the top of the board,
/// which is the only free space left in portrait. Numbers are placeholders in
/// every visual sense; what matters is that the two of them are separate and
/// both visible before you commit.
///
/// Safe to re-run: adds only what's missing, sets only layout and wiring, and
/// never rebuilds the scene.
/// </summary>
public static class ScoreTallySetup
{
    private const string PrefabPath = "Assets/Prefabs/Hud/ScoreTallyWidget.prefab";
    private const string ScenePath = "Assets/Scenes/Game.unity";

    private const string RootName = "Tally";
    private const string PointsName = "Points";
    private const string TimesName = "Times";
    private const string MultName = "Mult";
    private const string TotalName = "Total";
    private const string StepName = "Step";

    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Color PointsColor = new Color(0.42f, 0.68f, 1f);
    private static readonly Color MultColor = new Color(1f, 0.42f, 0.38f);
    private static readonly Color QuietColor = new Color(1f, 1f, 1f, 0.75f);

    [MenuItem("Word Crush/Set Up Score Tally")]
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
        PlaceInScene(prefab);
    }

    private static GameObject BuildPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            var made = WordCrushSetup.NewUI("ScoreTallyWidget", typeof(ScoreTallyWidget));
            WordCrushSetup.Anchor(made, TopCenter, new Vector2(0f, -405f), new Vector2(1000f, 185f));
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

        Debug.Log($"{PrefabPath} already existed — topped it up, left the styling alone.");
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void EnsureContents(GameObject widgetRoot)
    {
        var widget = widgetRoot.GetComponent<ScoreTallyWidget>()
                     ?? widgetRoot.AddComponent<ScoreTallyWidget>();

        // A child container, never the widget itself: the widget stays active
        // so it keeps hearing events while this is hidden.
        var shown = FindOrCreate(widgetRoot.transform, RootName);
        WordCrushSetup.Anchor(shown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 185f));

        // POINTS  x  MULT, on one line, deliberately two separate numbers.
        var points = Label(shown.transform, PointsName, 64, TextAlignmentOptions.Right,
                           new Vector2(-180f, 0f), new Vector2(300f, 80f), PointsColor, "0");
        Label(shown.transform, TimesName, 44, TextAlignmentOptions.Center,
              new Vector2(0f, -6f), new Vector2(80f, 80f), QuietColor, "x");
        var mult = Label(shown.transform, MultName, 64, TextAlignmentOptions.Left,
                         new Vector2(180f, 0f), new Vector2(300f, 80f), MultColor, "x1");

        // What the two of them come to.
        var total = Label(shown.transform, TotalName, 34, TextAlignmentOptions.Center,
                          new Vector2(0f, -82f), new Vector2(1000f, 44f), QuietColor, "0");

        // Named during the walk-through: "BOOKEND   x2 MULT".
        var step = Label(shown.transform, StepName, 28, TextAlignmentOptions.Center,
                         new Vector2(0f, -128f), new Vector2(1000f, 36f), QuietColor, "");

        WordCrushSetup.SetRef(widget, "root", shown);
        WordCrushSetup.SetRef(widget, "pointsLabel", points);
        WordCrushSetup.SetRef(widget, "multLabel", mult);
        WordCrushSetup.SetRef(widget, "totalLabel", total);
        WordCrushSetup.SetRef(widget, "stepLabel", step);
    }

    private static GameObject FindOrCreate(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var made = WordCrushSetup.NewUI(name);
        made.transform.SetParent(parent, false);
        return made;
    }

    /// <summary>
    /// Finds or makes one label. Position is ours on every run; size, colour and
    /// text are set only when the label is new, so tuning survives.
    /// </summary>
    private static TMP_Text Label(Transform parent, string name, float size,
                                  TextAlignmentOptions align, Vector2 offset,
                                  Vector2 box, Color color, string placeholder)
    {
        var existing = parent.Find(name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label == null)
        {
            var made = WordCrushSetup.MakeText(parent, name, size, align);
            made.color = color;
            made.text = placeholder;
            label = made;
        }

        WordCrushSetup.Anchor(label.gameObject, new Vector2(0.5f, 1f), offset, box);
        return label;
    }

    private static void PlaceInScene(GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError($"{ScenePath} has no Canvas — nothing to attach the tally to.");
            return;
        }

        if (Object.FindFirstObjectByType<ScoreTallyWidget>(FindObjectsInactive.Include) == null)
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            placed.name = prefab.name;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Score tally ready: POINTS x MULT is in the Game scene's HUD.");
    }
}
