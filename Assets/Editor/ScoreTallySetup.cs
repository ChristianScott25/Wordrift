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
    private const string PlateName = "Plate";

    // The columns, left to right (2026-10-19, off the mock-up): each number
    // centred under its caption, with the x and = in narrow slots between.
    private static readonly (string name, string caption, float xMin, float xMax)[] Columns =
    {
        (PointsName, "POINTS", 0.05f, 0.31f),
        (TimesName,  "",       0.31f, 0.39f),
        (MultName,   "MULT",   0.39f, 0.61f),
        ("Equals",   "",       0.61f, 0.69f),
        (TotalName,  "TOTAL",  0.69f, 0.95f),
    };

    private const float CaptionBottom = 0.60f;
    private const float CaptionTop = 0.92f;
    private const float NumberBottom = 0.08f;
    private const float NumberTop = 0.64f;

    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

    /// ⚠️ THESE TWO ARE WRITTEN ON EVERY RUN, unlike every other colour here.
    /// They have to agree in three places — the left-hand number, the right-hand
    /// number, and every number that floats off a tile or a card — so they live
    /// on the widget as fields and this stamps them. A colour that has to be
    /// right in three places is not hand-tuning.
    ///
    /// BRIGHT since 2026-10-19: the readout sits on a dark plate now, where the
    /// dark navy and maroon it had before disappeared. The same two also have to
    /// read on the cream page and the warm background the floating numbers
    /// cross, which is why they're mid-bright rather than pastel.
    /// PlaceInScene stamps them onto the ScorePopWidget too.
    internal static readonly Color PointsColor = new Color(0.30f, 0.58f, 1f);
    internal static readonly Color MultColor = new Color(1f, 0.33f, 0.30f);

    private static readonly Color TotalColor = Color.white;
    private static readonly Color CaptionColor = new Color(0.96f, 0.90f, 0.78f, 0.85f);

    // 🚧 The cream plate art, tinted down to a dark bar. Its black outline
    // survives the tint, which is most of what makes it read as a panel.
    private static readonly Color PlateTint = new Color(0.22f, 0.18f, 0.18f);

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

        Debug.Log($"{PrefabPath} already existed — rebuilt its plate, columns and colours.");
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

        // The one-line readout (2026-10-07) became three labelled columns on a
        // dark bar (2026-10-19, off the mock-up). The line goes, and so do the
        // beat caption and the "5 LETTERS" line retired before it.
        foreach (string retired in new[] { LineName, LengthName, StepName })
        {
            var old = shown.transform.Find(retired);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // The plate: a sibling drawn FIRST so everything else sits on it, and
        // never a raycast target — the bar is a readout, not a button.
        var plate = FindOrCreate(shown.transform, PlateName);
        WordCrushSetup.Stretch(plate);
        plate.transform.SetAsFirstSibling();
        var plateImage = plate.GetComponent<UnityEngine.UI.Image>() ?? plate.AddComponent<UnityEngine.UI.Image>();
        GameLayoutSetup.StylePlate(plateImage);
        plateImage.color = PlateTint;
        plateImage.raycastTarget = false;

        TMP_Text points = null, mult = null, total = null;
        foreach (var column in Columns)
        {
            bool isNumber = column.caption.Length > 0;

            // ⚠️ FIXED, every run: the old Times/Mult/Total/Points labels from
            // the five-label layout may still be in an older prefab under the
            // same names, at the wrong size and place. Column() re-pins them.
            var label = Column(shown.transform, column.name, column.xMin, column.xMax,
                               NumberBottom, NumberTop, isNumber ? 60 : 40);

            if (!isNumber)
            {
                label.text = column.name == TimesName ? "x" : "=";
                label.color = CaptionColor;
                label.enableAutoSizing = false;
                label.fontSize = 40;
                continue;
            }

            // Auto-sized so a big late-run score shrinks inside its column
            // rather than running into the next one.
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 60f;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var caption = Column(shown.transform, column.name + "Caption", column.xMin, column.xMax,
                                 CaptionBottom, CaptionTop, 26);
            caption.text = column.caption;
            caption.color = CaptionColor;
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 14f;
            caption.fontSizeMax = 26f;

            if (column.name == PointsName) points = label;
            else if (column.name == MultName) mult = label;
            else total = label;
        }

        WordCrushSetup.SetRef(widget, "root", shown);
        WordCrushSetup.SetRef(widget, "pointsLabel", points);
        WordCrushSetup.SetRef(widget, "multLabel", mult);
        WordCrushSetup.SetRef(widget, "totalLabel", total);

        // See the note on these constants: forced, not preserved — they have
        // to agree with the floating numbers.
        WordCrushSetup.SetColor(widget, "pointsColor", PointsColor);
        WordCrushSetup.SetColor(widget, "multColor", MultColor);
        WordCrushSetup.SetColor(widget, "totalColor", TotalColor);
    }

    /// <summary>
    /// A label pinned to a column of the bar. Position, alignment and the
    /// raycast flag every run; it is all structure here, because the bar's
    /// look is decided by these constants, not by hand-tuning.
    /// </summary>
    private static TMP_Text Column(Transform parent, string name, float xMin, float xMax,
                                   float yMin, float yMax, float size)
    {
        var existing = parent.Find(name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label == null)
        {
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            label = WordCrushSetup.MakeText(parent, name, size, TextAlignmentOptions.Center);
            label.text = "";
        }

        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.richText = false;

        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
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

        // The third place the two colours have to agree: the numbers that float
        // off tiles and cards. Stamped here because ScorePopSetup doesn't own
        // them and the tally is what decides them.
        var pops = Object.FindFirstObjectByType<ScorePopWidget>(FindObjectsInactive.Include);
        if (pops != null)
        {
            WordCrushSetup.SetColor(pops, "pointsColor", PointsColor);
            WordCrushSetup.SetColor(pops, "multColor", MultColor);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Score tally ready: POINTS x MULT is in the Game scene's HUD.");
    }
}
