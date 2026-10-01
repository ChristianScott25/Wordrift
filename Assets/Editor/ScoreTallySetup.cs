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

    /// What QuietColor used to be. See Restyle — it is how "nobody ever touched
    /// this" is told apart from "somebody chose this".
    private static readonly Color OldQuietColor = new Color(1f, 1f, 1f, 0.75f);

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
        WordCrushSetup.Anchor(shown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 195f));

        // POINTS  x  MULT, on one line, deliberately two separate numbers.
        var points = Label(shown.transform, PointsName, 64, TextAlignmentOptions.Right,
                           new Vector2(-180f, 0f), new Vector2(300f, 80f), PointsColor, "0");
        var times = Label(shown.transform, TimesName, 44, TextAlignmentOptions.Center,
                          new Vector2(0f, -6f), new Vector2(80f, 80f), QuietColor, "x");
        var mult = Label(shown.transform, MultName, 64, TextAlignmentOptions.Left,
                         new Vector2(180f, 0f), new Vector2(300f, 80f), MultColor, "x1");

        // Where BOTH numbers come from, now that length is the base score as
        // well as the multiplier: "5 LETTERS". It had a field on the widget and
        // no label at all until 2026-09-30, so the caption had never once
        // rendered — and it explains more than it used to.
        var length = Label(shown.transform, LengthName, 24, TextAlignmentOptions.Center,
                           new Vector2(0f, -74f), new Vector2(1000f, 32f), QuietColor, "");

        // What the two of them come to.
        var total = Label(shown.transform, TotalName, 34, TextAlignmentOptions.Center,
                          new Vector2(0f, -104f), new Vector2(1000f, 44f), QuietColor, "0");

        // Named on every beat of the walk-through: "BOOKEND   x2 MULT".
        var step = Label(shown.transform, StepName, 28, TextAlignmentOptions.Center,
                         new Vector2(0f, -146f), new Vector2(1000f, 36f), QuietColor, "");

        WordCrushSetup.SetRef(widget, "root", shown);
        WordCrushSetup.SetRef(widget, "pointsLabel", points);
        WordCrushSetup.SetRef(widget, "multLabel", mult);
        WordCrushSetup.SetRef(widget, "totalLabel", total);
        WordCrushSetup.SetRef(widget, "stepLabel", step);
        WordCrushSetup.SetRef(widget, "lengthLabel", length);

        // See the note on these two constants: forced, not preserved.
        WordCrushSetup.SetColor(widget, "pointsColor", PointsColor);
        WordCrushSetup.SetColor(widget, "multColor", MultColor);

        // The two numbers' own serialized colours are overwritten by the widget
        // on its first Draw, so these only matter for what the Inspector shows.
        // Kept in step with the fields above so the two never look different.
        points.color = PointsColor;
        mult.color = MultColor;

        // The quiet three were white at 75% against a light blue-grey background,
        // which was always hard to read and matters a great deal more now that
        // the step caption names every beat of the walk-through.
        Restyle(times);
        Restyle(total);
        Restyle(step);
    }

    /// <summary>
    /// Moves a label off this script's OLD default colour and onto its current
    /// one — and leaves anything else alone.
    ///
    /// ⚠️ Colour normally survives a re-run, because it's styling worth tuning on
    /// a real screen and a generator that reset it would be infuriating. That
    /// rule is intact: this only touches a label still carrying the exact value
    /// this script itself wrote, which is how "nobody has ever looked at this"
    /// is told apart from "somebody chose this". Anything hand-picked fails the
    /// comparison and is kept.
    /// </summary>
    private static void Restyle(TMP_Text label)
    {
        if (label == null) return;

        Color was = label.color;
        bool untouched = Mathf.Approximately(was.r, OldQuietColor.r)
                      && Mathf.Approximately(was.g, OldQuietColor.g)
                      && Mathf.Approximately(was.b, OldQuietColor.b)
                      && Mathf.Approximately(was.a, OldQuietColor.a);

        if (untouched) label.color = QuietColor;
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
