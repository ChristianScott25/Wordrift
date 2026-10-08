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
    private const string LengthName = "Length";
    private const string LineName = "Line";

    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

    /// ⚠️ THESE TWO ARE WRITTEN ON EVERY RUN, unlike every other colour here.
    /// They have to agree in three places — the left-hand number, the right-hand
    /// number, and every number that floats off a tile or a card — so they live
    /// on the widget as fields and this stamps them. A colour that has to be
    /// right in three places is not hand-tuning.
    ///
    /// Dark because the scene's camera clears to a light blue-grey
    /// (0.557, 0.655, 0.804) and the pale versions these replaced were washed out
    /// against it.
    private static readonly Color PointsColor = new Color(0.13f, 0.26f, 0.55f);
    private static readonly Color MultColor = new Color(0.60f, 0.13f, 0.16f);

    private static readonly Color QuietColor = new Color(0.12f, 0.13f, 0.17f, 0.75f);

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
        // so it keeps hearing events while this is hidden. STRETCHED to the
        // band since 2026-10-07 — it was a fixed 1000x195 box centred on the
        // widget, taller than the band it sat in.
        var shown = FindOrCreate(widgetRoot.transform, RootName);
        WordCrushSetup.Stretch(shown);

        // The five-label layout — POINTS, x, MULT, then "5 LETTERS" and the total
        // on lines of their own — became ONE line, "3 x 1 = 3" (his call,
        // 2026-10-07). The old labels go, so a re-run can't leave them drawn
        // under the new one. The beat caption under it ("BOOKEND   x2 MULT")
        // went too (2026-10-08, his call) — the floating numbers already say it.
        foreach (string retired in new[] { PointsName, TimesName, MultName, TotalName, LengthName, StepName })
        {
            var old = shown.transform.Find(retired);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // POINTS x MULT = TOTAL, filling the band. Auto-sized so a big
        // late-run score shrinks to fit rather than running off the sides.
        var line = Band(shown.transform, LineName, 0f, 1f, 64, Color.white);
        line.enableAutoSizing = true;
        line.fontSizeMin = 28f;
        line.fontSizeMax = 64f;
        line.richText = true;
        line.textWrappingMode = TextWrappingModes.NoWrap;

        WordCrushSetup.SetRef(widget, "root", shown);
        WordCrushSetup.SetRef(widget, "lineLabel", line);

        // See the note on these constants: forced, not preserved — they have
        // to agree with the floating numbers, and the line is built from them.
        WordCrushSetup.SetColor(widget, "pointsColor", PointsColor);
        WordCrushSetup.SetColor(widget, "multColor", MultColor);
        WordCrushSetup.SetColor(widget, "quietColor", QuietColor);
    }

    /// <summary>
    /// A label stretched across the container between two heights (fractions
    /// of it). Position every run; size and colour only when new.
    /// </summary>
    private static TMP_Text Band(Transform parent, string name, float yMin, float yMax,
                                 float size, Color color)
    {
        var existing = parent.Find(name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label == null)
        {
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            label = WordCrushSetup.MakeText(parent, name, size, TextAlignmentOptions.Center);
            label.text = "";
            label.color = color;   // white on the line: its colours are all inline tags
        }

        label.alignment = TextAlignmentOptions.Center;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, yMin);
        rect.anchorMax = new Vector2(1f, yMax);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return label;
    }

    private static GameObject FindOrCreate(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var made = WordCrushSetup.NewUI(name);
        made.transform.SetParent(parent, false);
        return made;
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
