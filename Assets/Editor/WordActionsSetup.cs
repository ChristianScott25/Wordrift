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

    // Tints MULTIPLIED onto the off-white plate, not colours drawn over it — the
    // art is one neutral plate used by every button, so what makes PLAY green and
    // DISCARD red lives here in code rather than in two near-identical sprites.
    private static readonly Color SubmitColor = new Color(0.36f, 0.75f, 0.40f);
    private static readonly Color DiscardColor = new Color(0.82f, 0.30f, 0.28f);

    // Both buttons when they refuse. Grey rather than faded, so "you can't press
    // this" doesn't read as "this is still loading".
    private static readonly Color DisabledColor = new Color(0.60f, 0.60f, 0.62f);

    // Dark, and the SAME on every plate: the label sits on green, red and grey in
    // turn, and a colour tuned for one of those is unreadable on another.
    private static readonly Color LabelColor = new Color(0.12f, 0.13f, 0.17f);

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

        Dress(discard, DiscardColor);
        Dress(submit, SubmitColor);

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

    /// <summary>
    /// Puts the shared plate on a button and gives it its colour.
    ///
    /// ⚠️ THE COLOUR IS THE BUTTON'S, NOT THE IMAGE'S. Writing the tint straight
    /// onto the Image would be overwritten the moment Unity's own state tinting
    /// ran, and the button would flicker back to white on hover. It belongs in
    /// the ColorBlock, which is also what gives "can't press this" a look for
    /// free — the widget only has to set `interactable`.
    ///
    /// Falls back to a flat colour block when the sprite is missing, so a renamed
    /// file leaves a readable button rather than an invisible one — though
    /// LoadSprite has already said so loudly.
    /// </summary>
    private static void Dress(Button button, Color color)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        var sprite = WordCrushSetup.LoadSprite("Medium Button");

        if (sprite == null)
        {
            if (image != null) image.color = color;
            return;
        }

        // White on the Image, colour in the ColorBlock: the tint multiplies the
        // plate, so anything other than white here would double-tint it.
        WordCrushSetup.SetSprite(image, sprite, Color.white);
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = color;
        colors.selectedColor = color;
        colors.highlightedColor = Lift(color, 0.12f);
        colors.pressedColor = Lift(color, -0.12f);
        colors.disabledColor = DisabledColor;
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        var label = EnsureLabel(button);
        if (label != null) label.color = LabelColor;
    }

    /// <summary>
    /// The button's text, created if an earlier version of this script removed it.
    ///
    /// It did: for one revision the words were drawn INTO the button art, so the
    /// label was deleted to stop it printing them twice. The art is a plain plate
    /// again now and the word is text, which is what lets DISCARD keep saying how
    /// many tiles it will spend.
    /// </summary>
    private static TMP_Text EnsureLabel(Button button)
    {
        var label = LabelOf(button);
        if (label == null)
        {
            label = WordCrushSetup.MakeText(button.transform, "Label", 40,
                                            TextAlignmentOptions.Center);
            WordCrushSetup.Stretch(label.gameObject);
        }

        // ⚠️ Auto-sized, and set on EVERY run rather than only when the label is
        // new — sizing is this script's to own, and a label made before these
        // rules existed would otherwise keep overflowing its button while a
        // re-run looked like it had done nothing.
        //
        // Auto-sizing because the two buttons carry text of very different
        // lengths — "PLAY" against "DISCARD 12" over "0 LEFT" — and the band
        // decides their width, so no single font size is right for both on every
        // screen. (The TILE labels deliberately do NOT auto-size: those are
        // world-space and their transform is rescaled to cancel the tile's fit,
        // so TMP would be fitting to a rect nobody has sized.)
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 40f;

        // A little breathing room so a descender doesn't touch the plate's edge.
        label.margin = new Vector4(10f, 6f, 10f, 6f);
        return label;
    }

    private static Color Lift(Color color, float by) => new Color(
        Mathf.Clamp01(color.r + by), Mathf.Clamp01(color.g + by),
        Mathf.Clamp01(color.b + by), color.a);

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
