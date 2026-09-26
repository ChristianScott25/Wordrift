using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the ENTER / DISCARD buttons — Assets/Prefabs/Hud/WordActionsWidget.prefab
/// — and puts one in the Game scene's HUD canvas.
///
/// Two jobs in one menu item because they're useless apart: the prefab can't
/// carry a reference to the scene's GameSession, so placing it is also what
/// wires it.
///
/// Safe to re-run. It adds only what's missing and sets only what it owns
/// (layout and wiring), so button colours, label sizes and any hand-tuning
/// survive. It never rebuilds the scene.
/// </summary>
public static class WordActionsSetup
{
    private const string PrefabPath = "Assets/Prefabs/Hud/WordActionsWidget.prefab";
    private const string ScenePath = "Assets/Scenes/Game.unity";

    private const string RootName = "Actions";
    private const string SubmitName = "EnterButton";
    private const string DiscardName = "DiscardButton";

    private static readonly Color SubmitColor = new Color(0.18f, 0.6f, 0.32f);
    private static readonly Color DiscardColor = new Color(0.42f, 0.22f, 0.26f);

    // Bottom of the 1080x1920 canvas. Everything else in this HUD hangs off the
    // top, and the board's lower edge sits about 310px above the canvas bottom,
    // so this strip is clear of both.
    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

    [MenuItem("Word Crush/Set Up Word Actions")]
    public static void SetUp()
    {
        // Only the menu path asks about unsaved scenes; Rebuild has already
        // asked once and must not prompt again halfway through.
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

    // ---------------------------------------------------------------- prefab

    private static GameObject BuildPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (existing == null)
        {
            var made = WordCrushSetup.NewUI("WordActionsWidget", typeof(WordActionsWidget));
            WordCrushSetup.Anchor(made, BottomCenter, new Vector2(0f, 180f), new Vector2(1040f, 170f));
            EnsureContents(made);

            var saved = PrefabUtility.SaveAsPrefabAsset(made, PrefabPath);
            Object.DestroyImmediate(made);
            Debug.Log($"Created {PrefabPath}.");
            return saved;
        }

        // Top up in place, so nothing already tuned on it is lost.
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

    /// <summary>
    /// Adds whatever children are missing and re-does the layout and wiring.
    /// Deliberately does NOT touch label text or button colours on things that
    /// already exist — those are the parts worth hand-tuning.
    /// </summary>
    private static void EnsureContents(GameObject widgetRoot)
    {
        var widget = widgetRoot.GetComponent<WordActionsWidget>()
                     ?? widgetRoot.AddComponent<WordActionsWidget>();

        // ⚠️ EVERYTHING HERE IS SIZED IN FRACTIONS OF THE PARENT, NOT PIXELS.
        // The widget used to be a fixed 1040x170 strip floating over the board,
        // so its two buttons could sit at a hardcoded +/-265 and be 490 wide. It
        // lives in a BAND now and GameLayout gives it whatever width the band
        // and its share of it work out to — roughly 700 on a phone. Absolute
        // offsets against that overflowed the root in both directions: DISCARD
        // slid left under the info/settings buttons, and PLAY ran off the screen.
        var shown = FindOrCreate(widgetRoot.transform, RootName);
        WordCrushSetup.Stretch(shown);

        var discard = FindOrCreateButton(shown.transform, DiscardName, "DISCARD",
                                         0f, 0.48f, DiscardColor);
        var submit = FindOrCreateButton(shown.transform, SubmitName, "PLAY",
                                        0.52f, 1f, SubmitColor);

        WordCrushSetup.SetRef(widget, "discardButton", discard);
        WordCrushSetup.SetRef(widget, "discardLabel", LabelOf(discard));
        WordCrushSetup.SetRef(widget, "submitButton", submit);
        WordCrushSetup.SetRef(widget, "submitLabel", LabelOf(submit));
    }

    private static GameObject FindOrCreate(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var made = WordCrushSetup.NewUI(name);
        made.transform.SetParent(parent, false);
        return made;
    }

    private static Button FindOrCreateButton(Transform parent, string name, string label,
                                             float xMin, float xMax, Color background)
    {
        var existing = parent.Find(name);
        Button button = existing != null ? existing.GetComponent<Button>() : null;

        if (button == null)
            button = WordCrushSetup.MakeButton(parent, name, label, Vector2.zero,
                                               background, Color.white);

        // Position is ours; colour and text are not, once they exist.
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, 0f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // The label fills the button, for the same reason — a fixed-size label on
        // a button that changed width clips its own text.
        var text = LabelOf(button);
        if (text != null) WordCrushSetup.Stretch(text.gameObject);
        return button;
    }

    private static TMP_Text LabelOf(Button button) =>
        button == null ? null : button.GetComponentInChildren<TMP_Text>(true);

    // ----------------------------------------------------------------- scene

    private static void PlaceInScene(GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        var session = Object.FindFirstObjectByType<GameSession>();
        if (canvas == null || session == null)
        {
            Debug.LogError($"{ScenePath} has no Canvas or no GameSession — nothing to attach the buttons to.");
            return;
        }

        var widget = Object.FindFirstObjectByType<WordActionsWidget>(FindObjectsInactive.Include);
        if (widget == null)
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            placed.name = prefab.name;
            widget = placed.GetComponent<WordActionsWidget>();
        }

        // The one thing that can only be done here: a prefab can't hold a
        // reference to a scene object.
        WordCrushSetup.SetRef(widget, "session", session);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Word actions ready: ENTER and DISCARD are in the Game scene's HUD.");
    }
}
