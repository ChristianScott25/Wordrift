using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lays the Game scene's HUD out in BANDS — Word Crush/Set Up Game Layout.
///
/// The screen became a vertical stack: round header, resource strip, score,
/// word row, board, buttons. This puts a GameLayout on the canvas to resolve
/// that stack, creates the widgets that had no home before (the tile bag, the
/// consumables area, the info/settings buttons) and points every widget at the
/// band it belongs to.
///
/// 🚧 EVERYTHING IT DRAWS IS A PLACEHOLDER — flat colour blocks and real text.
/// The point is to judge the LAYOUT at its real size on a real screen, not the
/// art, which doesn't exist yet.
///
/// Safe to re-run. It adds only what's missing and sets only what it owns
/// (structure and wiring), so colours, font sizes and the band weights survive
/// a second run. It never rebuilds the scene.
/// </summary>
public static class GameLayoutSetup
{
    private const string ScenePath = "Assets/Scenes/Game.unity";

    // Retired when the bands arrived. Their jobs moved: the score and the
    // librarian's rule into the round header, the current word into the word
    // row, the word result into the score tally. Left behind in the scene they
    // would draw over the bands, so they are removed by name — which also
    // catches an instance whose script asset has since gone.
    private static readonly string[] Retired =
    {
        "ScoreWidget", "WordResultWidget",
    };

    private static readonly Color Panel = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color Faint = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color InfoColor = new Color(0.22f, 0.26f, 0.34f);

    [MenuItem("Word Crush/Set Up Game Layout")]
    public static void SetUp()
    {
        // Only the menu path asks about unsaved scenes; Rebuild has already
        // asked once and must not prompt again halfway through.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!Run()) return;

        // Only on the menu path — Rebuild shows its own, and two dialogs in a row
        // is how you train someone to click through without reading.
        //
        // It is here because the failure this replaces was SILENT: an exception
        // partway through leaves the scene unsaved and looks, from the Game view,
        // exactly like a layout that was built wrong. No dialog now means it
        // didn't finish, and the Console says why.
        EditorUtility.DisplayDialog("Word Crush",
            "Game layout is set up.\n\n" +
            "Seven bands, placeholder art. Press Play, then check it at 19.5:9, " +
            "16:9 and 4:3 in the Game view.", "OK");
    }

    /// <summary>
    /// Also called from WordCrushSetup.Rebuild, last, after every widget exists.
    /// Returns false if it gave up before saving the scene.
    /// </summary>
    internal static bool Run()
    {
        // ⚠️ FIRST, AND BEFORE WE OPEN THE SCENE OURSELVES. BookmarkRowSetup
        // opens the Game scene itself (EditorSceneManager.OpenScene, Single),
        // which RELOADS IT FROM DISK and silently discards every unsaved change
        // made before it. Running it at the end — which is where it was, on the
        // reasoning that the row wants pinning after the board is placed — threw
        // away this whole script's work and then saved the row on top, so the
        // menu item reported success and the scene came back with nothing in it
        // but a moved bookmark row.
        //
        // There was never a reason for it to run late: the row re-pins itself
        // against the board's real top edge at RUNTIME. All the setup does is
        // give it a starting position.
        //
        // The same applies to any other setup called from here. Call it before
        // the OpenScene below, or not at all.
        BookmarkRowSetup.Run();

        // Same rule, same reason: it re-anchors PLAY and DISCARD as fractions of
        // whatever width their band gives them, which they need now that they no
        // longer float over the board at a fixed 1040 wide.
        WordActionsSetup.Run();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        var session = Object.FindFirstObjectByType<GameSession>();
        var board = Object.FindFirstObjectByType<Board>();

        if (canvas == null || session == null || board == null)
        {
            Debug.LogError($"{ScenePath} is missing its Canvas, GameSession or Board — " +
                           "there is nothing to lay out.");
            return false;
        }

        ConfigureCanvas(canvas);
        RemoveRetired(canvas);

        var layout = EnsureLayout(canvas, session, board);
        BuildHeader(canvas);
        BuildStrip(canvas);
        BuildBag(canvas);
        BuildConsumables(canvas);
        BuildSystemButtons(canvas);
        BuildWordRow(canvas, board);

        WordCrushSetup.SetRef(session, "layout", layout);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!SurvivedTheSave()) return false;

        Debug.Log("Game layout ready: seven bands, placeholder art. " +
                  "Check it at 19.5:9, 16:9 and 4:3 in the Game view.");
        return true;
    }

    /// <summary>
    /// Re-reads the scene FROM DISK and checks the two things that prove this
    /// ran: the canvas has a GameLayout, and the session points at it.
    ///
    /// ⚠️ This exists because the failure it catches reported SUCCESS. Another
    /// setup script called from here re-opened the Game scene mid-run, which
    /// reloads it from disk and silently drops every unsaved change — so the
    /// work vanished, the row that ran afterwards saved fine, and the dialog
    /// said it had worked. Checking in memory would not have caught it; only
    /// asking the file does. Same discipline as WordCrushSetup.SetRef reading
    /// its own assignment back.
    /// </summary>
    private static bool SurvivedTheSave()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        var session = Object.FindFirstObjectByType<GameSession>();
        var layout = canvas == null ? null : canvas.GetComponent<GameLayout>();

        bool wired = session != null &&
                     new SerializedObject(session).FindProperty("layout")
                         ?.objectReferenceValue != null;

        if (layout != null && wired) return true;

        Debug.LogError(
            "Set Up Game Layout did not stick. The scene on disk has " +
            (layout == null ? "no GameLayout on the canvas" : "a GameLayout") +
            " and GameSession's layout reference is " + (wired ? "set" : "empty") +
            ". Something re-opened the scene during the run and discarded the " +
            "changes — check any setup script called from GameLayoutSetup.Run.");
        return false;
    }

    // ----------------------------------------------------------------- canvas

    /// <summary>
    /// ⚠️ MATCH WIDTH, NOT 0.5. With the scaler balanced between width and
    /// height, the logical canvas WIDTH shrinks on a tall phone — 1080 becomes
    /// about 979 on a 19.5:9 screen — so a layout built against 1080 overflows on
    /// exactly the devices this game is for. Matching width pins it at 1080 and
    /// lets the extra aspect become extra height, which is what a band stack
    /// wants: the bands share whatever height there is.
    /// </summary>
    private static void ConfigureCanvas(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
    }

    private static void RemoveRetired(Canvas canvas)
    {
        foreach (string name in Retired)
        {
            var found = canvas.transform.Find(name);
            if (found == null) continue;

            DestroyInstance(found.gameObject);
            Debug.Log($"Removed the retired {name} from the HUD.");
        }
    }

    private static GameLayout EnsureLayout(Canvas canvas, GameSession session, Board board)
    {
        var layout = canvas.GetComponent<GameLayout>() ?? canvas.gameObject.AddComponent<GameLayout>();

        // Both are scene objects, so only this can wire them — and the band
        // weights are deliberately NOT written here, so tuning survives a re-run.
        WordCrushSetup.SetRef(layout, "board", board);
        WordCrushSetup.SetRef(layout, "sceneCamera", Camera.main);
        return layout;
    }

    // ---------------------------------------------------------------- widgets

    /// <summary>
    /// The round header: title bar, portrait, rule, score over target.
    ///
    /// Anchors inside the widget are fractional so the box reflows with its band
    /// — the band's own height changes with the screen, and a header pinned in
    /// pixels would overflow it on a short one.
    /// </summary>
    private static void BuildHeader(Canvas canvas)
    {
        var root = Reuse<RoundBannerWidget>(canvas, "RoundHeader");
        var widget = root.GetComponent<RoundBannerWidget>();

        Backdrop(root);

        var title = Slot(root.transform, "Title", 0f, 0.76f, 1f, 1f);
        var text = Text(title, 40, TextAlignmentOptions.Center);

        var avatar = Slot(root.transform, "Avatar", 0.03f, 0.08f, 0.32f, 0.72f);
        var portrait = Image(avatar, new Color(1f, 1f, 1f, 0.12f));

        var power = Slot(root.transform, "Power", 0.35f, 0.42f, 0.98f, 0.74f);
        var powerText = Text(power, 26, TextAlignmentOptions.TopLeft);
        powerText.color = Faint;

        var score = Slot(root.transform, "Score", 0.35f, 0.06f, 0.98f, 0.4f);
        var scoreText = Text(score, 54, TextAlignmentOptions.Left);

        WordCrushSetup.SetRef(widget, "titleLabel", text);
        WordCrushSetup.SetRef(widget, "powerLabel", powerText);
        WordCrushSetup.SetRef(widget, "scoreLabel", scoreText);
        WordCrushSetup.SetRef(widget, "avatar", portrait);
    }

    /// <summary>
    /// The resource strip. It has no children to build — the chip COUNT is the
    /// mode's to decide, so the widget makes its own at runtime, exactly as the
    /// bookmark row makes its cards.
    /// </summary>
    private static void BuildStrip(Canvas canvas)
    {
        var root = Reuse<StatusWidget>(canvas, "ResourceStrip");
        var widget = root.GetComponent<StatusWidget>();
        WordCrushSetup.SetRef(widget, "chipSprite", Square());
    }

    private static void BuildBag(Canvas canvas)
    {
        var root = Reuse<BagButtonWidget>(canvas, "TileBag");
        var widget = root.GetComponent<BagButtonWidget>();

        Backdrop(root);
        var button = root.GetComponent<Button>() ?? root.AddComponent<Button>();

        var caption = Slot(root.transform, "Caption", 0f, 0.58f, 1f, 0.95f);
        var captionText = Text(caption, 24, TextAlignmentOptions.Center);
        captionText.color = Faint;

        var value = Slot(root.transform, "Value", 0f, 0.08f, 1f, 0.58f);
        var valueText = Text(value, 40, TextAlignmentOptions.Center);

        WordCrushSetup.SetRef(widget, "button", button);
        WordCrushSetup.SetRef(widget, "captionLabel", captionText);
        WordCrushSetup.SetRef(widget, "valueLabel", valueText);
    }

    private static void BuildConsumables(Canvas canvas)
    {
        var root = Reuse<ConsumablesAreaWidget>(canvas, "Consumables");
        var widget = root.GetComponent<ConsumablesAreaWidget>();

        var caption = Slot(root.transform, "Caption", 0f, 0.74f, 1f, 1f);
        var captionText = Text(caption, 22, TextAlignmentOptions.Center);
        captionText.color = Faint;
        if (string.IsNullOrEmpty(captionText.text)) captionText.text = "ITEMS";

        WordCrushSetup.SetRef(widget, "captionLabel", captionText);
        WordCrushSetup.SetRef(widget, "slotSprite", Square());
    }

    private static void BuildSystemButtons(Canvas canvas)
    {
        var root = Reuse<SystemButtonsWidget>(canvas, "SystemButtons");
        var widget = root.GetComponent<SystemButtonsWidget>();

        var info = FindOrMakeButton(root.transform, "InfoButton", "i", 0f, 0.46f);
        // 🚧 "SET", not a gear glyph: the default TMP font has no U+2699, and a
        // missing character renders as a box, which reads as broken rather than
        // as a placeholder. Swap it for an icon when there is art.
        var settings = FindOrMakeButton(root.transform, "SettingsButton", "SET", 0.54f, 1f);

        WordCrushSetup.SetRef(widget, "infoButton", info);
        WordCrushSetup.SetRef(widget, "settingsButton", settings);
    }

    /// <summary>
    /// The word row. Its TILES are world-space sprites the widget spawns off the
    /// board, so the only thing here is the message label that replaces them when
    /// the selection won't score.
    /// </summary>
    private static void BuildWordRow(Canvas canvas, Board board)
    {
        var root = Reuse<CurrentWordWidget>(canvas, "WordRow");
        var widget = root.GetComponent<CurrentWordWidget>();

        var message = Slot(root.transform, "Message", 0f, 0f, 1f, 1f);
        var text = Text(message, 44, TextAlignmentOptions.Center);

        WordCrushSetup.SetRef(widget, "messageLabel", text);
        WordCrushSetup.SetRef(widget, "board", board);
    }

    // ----------------------------------------------------------------- pieces

    /// <summary>
    /// The widget's object, made if it isn't there.
    ///
    /// Found by COMPONENT rather than by name, so a widget the user renamed is
    /// still found and isn't quietly duplicated. Inactive ones count — a widget
    /// switched off for a test should be topped up, not cloned.
    ///
    /// ⚠️ A PREFAB INSTANCE IS REPLACED, NOT TOPPED UP. Three of these widgets
    /// kept their class name but changed job — StatusWidget became the chip
    /// strip, RoundBannerWidget the round header, CurrentWordWidget the word row
    /// — so the scene still holds instances of prefabs built for the OLD job,
    /// carrying children like an 80pt "Value" label anchored top-left. Topping
    /// one up would leave those sitting on top of the new layout. Rebuilding
    /// gives a clean object, and because the replacement is a PLAIN object, every
    /// later run takes the top-up path and hand-tuning survives from then on.
    /// </summary>
    private static GameObject Reuse<T>(Canvas canvas, string name) where T : Component
    {
        var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (existing != null)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(existing.gameObject))
                return existing.gameObject;

            DestroyInstance(existing.gameObject);
            Debug.Log($"Replaced the old {typeof(T).Name} prefab instance — it was built " +
                      "for the pre-band layout.");
        }

        var made = WordCrushSetup.NewUI(name, typeof(T));
        made.transform.SetParent(canvas.transform, false);
        return made;
    }

    /// <summary>
    /// Removes a scene object, UNPACKING it first if it came from a prefab.
    ///
    /// ⚠️ Unity is fussy about destroying anything inside a prefab instance, and
    /// the difference between a root and a child is not always what you expect.
    /// Unpacking turns it into a plain object first, where DestroyImmediate is
    /// simply allowed — and an exception here would abort the whole run before
    /// the scene is saved, which looks exactly like the menu item never having
    /// been clicked.
    /// </summary>
    private static void DestroyInstance(GameObject go)
    {
        if (go == null) return;

        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                                               InteractionMode.AutomatedAction);

        Object.DestroyImmediate(go);
    }

    /// <summary>A flat panel behind a box, so it reads as a box at all. 🚧 Placeholder.</summary>
    private static void Backdrop(GameObject root)
    {
        var image = root.GetComponent<Image>();
        if (image != null) return;

        image = root.AddComponent<Image>();
        image.sprite = Square();
        image.color = Panel;
    }

    /// <summary>
    /// A child pinned to a fractional rectangle of its parent.
    ///
    /// Fractions, not pixels: every band's height depends on the screen, so a
    /// child measured in pixels would sit right on one device and hang off the
    /// next. Position is ours on every run; nothing else here is.
    /// </summary>
    private static GameObject Slot(Transform parent, string name,
                                   float xMin, float yMin, float xMax, float yMax)
    {
        var existing = parent.Find(name);
        var go = existing != null ? existing.gameObject : WordCrushSetup.NewUI(name);
        if (existing == null) go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }

    /// <summary>
    /// The text on a slot. Size and alignment are set only when the label is
    /// NEW — they're exactly the sort of thing worth hand-tuning once this is on
    /// a real screen, and a re-run that reset them would be infuriating.
    /// </summary>
    private static TMP_Text Text(GameObject slot, float size, TextAlignmentOptions align)
    {
        var existing = slot.GetComponent<TMP_Text>();
        if (existing != null) return existing;

        var text = slot.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

    private static Image Image(GameObject slot, Color color)
    {
        var existing = slot.GetComponent<Image>();
        if (existing != null) return existing;

        var image = slot.AddComponent<Image>();
        image.sprite = Square();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// The flat square every placeholder here is drawn with.
    ///
    /// Sharp corners and no outline, which is why it and not Tile - white: a
    /// rounded sprite scallops wherever two of these meet.
    /// </summary>
    private static Sprite Square() =>
        AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/White Square.png");

    private static Button FindOrMakeButton(Transform parent, string name, string label,
                                           float xMin, float xMax)
    {
        var slot = Slot(parent, name, xMin, 0.1f, xMax, 0.9f);

        var button = slot.GetComponent<Button>();
        if (button != null) return button;

        var image = slot.AddComponent<Image>();
        image.sprite = Square();
        image.color = InfoColor;

        button = slot.AddComponent<Button>();

        var text = WordCrushSetup.MakeText(slot.transform, "Label", 46, TextAlignmentOptions.Center);
        text.text = label;
        WordCrushSetup.Stretch(text.gameObject);
        return button;
    }
}
