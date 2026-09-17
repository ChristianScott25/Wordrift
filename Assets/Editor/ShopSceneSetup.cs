using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Creates the Shop scene — the screen between the rounds of a run — and
/// registers it in Build Settings, because a scene can't be authored from a CLI.
///
/// Re-runnable, and it TOPS UP an existing shop: anything the screen needs and
/// doesn't have (the money readout, the buy rows, the description panel) is
/// added, and the layout of the pieces it owns is set. It never deletes a child
/// and never touches text or colour — so the rule of thumb is: positions are the
/// generator's, styling is yours.
///
/// The one thing that crosses that line is AUTO-SIZE BOUNDS on the offer labels,
/// re-applied on every run rather than only at creation. They're a property of
/// the BOX, not of the look: this script decides how wide a row is, and the rows
/// already in the scene predate the content that outgrew them.
///
/// It DOES re-wire the buttons it owns, which it didn't used to. A persistent
/// listener stores its method by NAME, so renaming the method it points at
/// leaves a button that looks wired and does nothing when tapped — exactly what
/// happened when ShopScreen.Buy became Select. See Rewire: it clears before it
/// adds, so a re-run still ends with exactly one call per button.
///
/// Unlike the Game scene there's no second wiring pass here — everything
/// ShopScreen points at is an object in the same scene, and those survive the
/// save; it was ASSET references that didn't.
/// </summary>
public static class ShopSceneSetup
{
    internal const string ScenePath = "Assets/Scenes/Shop.unity";

    private static readonly Color BackgroundColor = new Color(0.09f, 0.16f, 0.22f);
    private static readonly Color AccentColor = new Color(1f, 0.75f, 0.1f);
    private static readonly Color OfferColor = new Color(1f, 1f, 1f, 0.14f);

    // The description panel sits over the shelf, so it has to be opaque enough to
    // hide it. Darker than the camera's background rather than lighter, so it
    // reads as something in front rather than as the page changing.
    private static readonly Color PanelColor = new Color(0.05f, 0.10f, 0.15f, 0.98f);
    private static readonly Color BackColor = new Color(1f, 1f, 1f, 0.16f);

    /// <summary>
    /// How many buy rows the shop lays out. Taken from ShopScreen rather than
    /// written down again: the slots have ROLES, and a shelf one row short drops
    /// the checkout silently. One number, one place.
    /// </summary>
    private const int OfferRows = ShopScreen.Slots;

    // The shelf, laid out from the top down. Bigger than they were (640x104) —
    // these are thumb targets on a phone, and they now carry a struck-through
    // price as well as a name.
    private const float OfferWidth = 860f;
    private const float OfferHeight = 136f;
    private const float OfferTop = 250f;
    private const float OfferPitch = 152f;

    [MenuItem("Word Crush/Create Shop Scene")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        bool existed = File.Exists(ScenePath);
        var scene = existed
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : BuildNewScene();

        var shop = Object.FindFirstObjectByType<ShopScreen>();
        if (shop == null)
        {
            Debug.LogError($"{ScenePath} has no ShopScreen — delete the scene file and re-run to regenerate it.");
            return;
        }

        int added = EnsureContents(shop);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuild();

        Debug.Log(existed
            ? $"{ScenePath} updated — {added} element(s) added, existing ones left alone."
            : $"Created {ScenePath} and added it to Build Settings.");
    }

    /// <summary>The parts that are the same every time: camera, canvas, titles, Continue.</summary>
    private static UnityEngine.SceneManagement.Scene BuildNewScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // The 2D renderer doesn't need the default light.
        var light = Object.FindFirstObjectByType<Light>();
        if (light != null) Object.DestroyImmediate(light.gameObject);

        var cam = Camera.main;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;

        var canvasGo = new GameObject("Shop Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var screenGo = WordCrushSetup.NewUI("ShopScreen", typeof(ShopScreen));
        screenGo.transform.SetParent(canvasGo.transform, false);
        WordCrushSetup.Stretch(screenGo);

        var title = WordCrushSetup.MakeText(screenGo.transform, "Title", 64, TextAlignmentOptions.Center);
        WordCrushSetup.Anchor(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(1000f, 100f));
        title.text = "SHOP";
        title.color = new Color(1f, 1f, 1f, 0.5f);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        return scene;
    }

    /// <summary>
    /// Adds whatever the screen is missing and lays out what it owns. Returns
    /// how many objects it had to create, so the log can say whether a re-run
    /// actually did anything.
    /// </summary>
    private static int EnsureContents(ShopScreen shop)
    {
        var root = shop.transform;
        int added = 0;

        var headline = Ensure(root, "Headline", 100, ref added, t =>
        {
            t.text = "ROUND 1 CLEARED";
            t.color = AccentColor;
        });
        WordCrushSetup.Anchor(headline.gameObject, Center, new Vector2(0f, 700f), new Vector2(1000f, 140f));

        var detail = Ensure(root, "Detail", 52, ref added, t => t.text = "NEXT TARGET   90");
        WordCrushSetup.Anchor(detail.gameObject, Center, new Vector2(0f, 585f), new Vector2(1000f, 80f));

        var money = Ensure(root, "Money", 76, ref added, t =>
        {
            t.text = "$0";
            t.color = AccentColor;
        });
        WordCrushSetup.Anchor(money.gameObject, Center, new Vector2(0f, 470f), new Vector2(1000f, 110f));

        var bookmarks = Ensure(root, "Bookmarks", 36, ref added, t =>
        {
            t.text = "";
            t.color = new Color(1f, 1f, 1f, 0.7f);
        });
        WordCrushSetup.Anchor(bookmarks.gameObject, Center, new Vector2(0f, 370f), new Vector2(1000f, 60f));

        // The old stub's "nothing for sale yet" line is now a lie. Deleting a
        // child isn't this script's job, so it's parked under the button and —
        // only if it still says exactly that — given something true to say.
        var hint = root.Find("Hint");
        if (hint != null)
        {
            WordCrushSetup.Anchor(hint.gameObject, Center, new Vector2(0f, -690f), new Vector2(1000f, 60f));
            var hintText = hint.GetComponent<TMP_Text>();

            // Each of these was true when it was written and isn't any more.
            // Matched exactly so anything hand-edited is left alone.
            if (hintText != null &&
                (hintText.text == "nothing for sale yet" ||
                 hintText.text == "buying the same upgrade again costs more"))
                hintText.text = "one of each, and it's gone";
        }

        var buttons = new Button[OfferRows];
        var labels = new TMP_Text[OfferRows];
        for (int i = 0; i < OfferRows; i++)
        {
            string name = $"Offer{i}";
            var existing = root.Find(name);
            if (existing == null)
            {
                existing = WordCrushSetup
                    .MakeButton(root, name, "—", Vector2.zero, OfferColor, Color.white)
                    .transform;
                added++;
            }

            buttons[i] = existing.GetComponent<Button>();
            labels[i] = existing.GetComponentInChildren<TMP_Text>();

            // A row reads "LATE FEES AND OTHER STORIES     $32", which wants about
            // 1020px at the authored 56pt in an 860px button.
            AutoSize(labels[i], 30f, 56f);

            // These used to call Buy(int), which no longer exists — a tap on a
            // renamed method is a silent no-op, so this is the one place the
            // "never re-wire an existing button" rule has to be broken. Clearing
            // first is what keeps it idempotent.
            //
            // Copied out of the loop variable: a `for` loop shares one `i` across
            // every iteration, so a closure over it would wire all five rows to
            // whatever it held last.
            var button = buttons[i];
            int index = i;
            Rewire(button, () =>
                UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                    button.onClick, shop.Select, index));

            WordCrushSetup.Anchor(existing.gameObject, Center,
                new Vector2(0f, OfferTop - i * OfferPitch), new Vector2(OfferWidth, OfferHeight));
        }

        var continueButton = root.Find("ContinueButton");
        if (continueButton == null)
        {
            var made = WordCrushSetup.MakeButton(root, "ContinueButton", "CONTINUE",
                Vector2.zero, AccentColor, Color.black);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(made.onClick, shop.Continue);
            continueButton = made.transform;
            added++;
        }
        WordCrushSetup.Anchor(continueButton.gameObject, Center, new Vector2(0f, -570f), new Vector2(620f, 150f));

        var panel = EnsureDetailPanel(shop, root, ref added);

        WordCrushSetup.SetRef(shop, "headline", headline);
        WordCrushSetup.SetRef(shop, "detail", detail);
        WordCrushSetup.SetRef(shop, "moneyLabel", money);
        WordCrushSetup.SetRef(shop, "bookmarkLabel", bookmarks);
        WordCrushSetup.SetRef(shop, "continueButton", continueButton.GetComponent<Button>());
        WordCrushSetup.SetRef(shop, "detailRoot", panel.gameObject);
        WireRows(shop, buttons, labels);

        return added;
    }

    /// <summary>
    /// Replaces a button's persistent listeners with exactly one. Removing from
    /// the end backwards because each removal reindexes the rest — going forwards
    /// leaves every other one behind.
    /// </summary>
    private static void Rewire(Button button, System.Action addListener)
    {
        if (button == null) return;

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);

        addListener();
    }

    /// <summary>
    /// The description a tapped row opens: what it is, what it does, what it
    /// costs, and BUY / BACK. Built as one parent so ShopScreen can show and hide
    /// the lot with a single SetActive — it is left ACTIVE here, because the
    /// screen hides it itself on Start and an inactive object is easy to lose in
    /// the hierarchy while it's being styled.
    /// </summary>
    private static Transform EnsureDetailPanel(ShopScreen shop, Transform root, ref int added)
    {
        var panel = root.Find("DetailPanel");
        if (panel == null)
        {
            var go = WordCrushSetup.NewUI("DetailPanel", typeof(Image));
            go.transform.SetParent(root, false);
            go.GetComponent<Image>().color = PanelColor;
            panel = go.transform;
            added++;
        }
        // Sits BELOW the money readout (which spans y 415..525) rather than over
        // it: the balance is exactly the number you need while deciding whether
        // to buy, so covering it would be the one thing this panel must not do.
        // It covers every offer row and the Continue button, which is the point.
        WordCrushSetup.Anchor(panel.gameObject, Center, new Vector2(0f, -60f), new Vector2(940f, 900f));

        // Laid out top-down inside the panel's 900, with real gaps between the
        // boxes. An earlier pass had the price box overlapping the body's, which
        // only looked fine because both texts happen to centre inside theirs.
        // COLOUR is set once, at creation — that's yours to change. The AUTO-SIZE
        // BOUNDS are re-applied every run, because they belong to the box this
        // script draws rather than to the look: "LATE FEES AND OTHER STORIES"
        // needs about 1010px at 72pt in an 860px box, so a fixed size either
        // clips the long titles or wastes the short ones.
        var title = EnsureIn(panel, "DetailTitle", 72, ref added, t => t.color = AccentColor);
        WordCrushSetup.Anchor(title.gameObject, Center, new Vector2(0f, 350f), new Vector2(860f, 100f));
        AutoSize(title, 40f, 72f);

        // Same again: a bookmark's description is one line and a tile upgrade's
        // is five, so the text has to give rather than overflow the box.
        var body = EnsureIn(panel, "DetailBody", 42, ref added, t =>
        {
            t.color = new Color(1f, 1f, 1f, 0.9f);
            t.fontStyle = TMPro.FontStyles.Normal;
        });
        WordCrushSetup.Anchor(body.gameObject, Center, new Vector2(0f, 100f), new Vector2(820f, 340f));
        AutoSize(body, 28f, 42f);

        var price = EnsureIn(panel, "DetailPrice", 76, ref added, t => t.color = AccentColor);
        WordCrushSetup.Anchor(price.gameObject, Center, new Vector2(0f, -140f), new Vector2(860f, 90f));
        AutoSize(price, 44f, 76f);

        var buy = panel.Find("BuyButton");
        if (buy == null)
        {
            buy = WordCrushSetup.MakeButton(panel, "BuyButton", "BUY",
                Vector2.zero, AccentColor, Color.black).transform;
            added++;
        }
        WordCrushSetup.Anchor(buy.gameObject, Center, new Vector2(0f, -265f), new Vector2(820f, 120f));
        Rewire(buy.GetComponent<Button>(), () =>
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                buy.GetComponent<Button>().onClick, shop.ConfirmBuy));

        var back = panel.Find("BackButton");
        if (back == null)
        {
            back = WordCrushSetup.MakeButton(panel, "BackButton", "BACK",
                Vector2.zero, BackColor, Color.white).transform;
            added++;
        }
        WordCrushSetup.Anchor(back.gameObject, Center, new Vector2(0f, -385f), new Vector2(820f, 100f));
        Rewire(back.GetComponent<Button>(), () =>
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                back.GetComponent<Button>().onClick, shop.Back));

        WordCrushSetup.SetRef(shop, "detailTitle", title);
        WordCrushSetup.SetRef(shop, "detailBody", body);
        WordCrushSetup.SetRef(shop, "detailPrice", price);
        WordCrushSetup.SetRef(shop, "buyButton", buy.GetComponent<Button>());
        WordCrushSetup.SetRef(shop, "buyLabel", buy.GetComponentInChildren<TMP_Text>());

        return panel;
    }

    /// <summary>Ensure, but parented somewhere other than the screen's root.</summary>
    private static TMP_Text EnsureIn(Transform parent, string name, float size,
                                     ref int added, System.Action<TMP_Text> style)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<TMP_Text>();

        var text = WordCrushSetup.MakeText(parent, name, size, TextAlignmentOptions.Center);
        style(text);
        added++;
        return text;
    }

    private static Vector2 Center => new Vector2(0.5f, 0.5f);

    /// <summary>
    /// Lets a label shrink to fit its box. Re-applied on EVERY run, unlike the
    /// colours and fonts around it, because these bounds describe the box this
    /// script sized rather than the look someone chose — and because the labels
    /// that need them were created by an earlier version of this script, so
    /// styling-on-creation-only would never reach them.
    /// </summary>
    private static void AutoSize(TMP_Text label, float min, float max)
    {
        if (label == null) return;
        label.enableAutoSizing = true;
        label.fontSizeMin = min;
        label.fontSizeMax = max;
    }

    /// <summary>Finds a text child by name, or makes one and styles it the first time.</summary>
    private static TMP_Text Ensure(Transform root, string name, float size,
                                   ref int added, System.Action<TMP_Text> style)
    {
        var existing = root.Find(name);
        if (existing != null) return existing.GetComponent<TMP_Text>();

        var text = WordCrushSetup.MakeText(root, name, size, TextAlignmentOptions.Center);
        style(text);
        added++;
        return text;
    }

    /// <summary>
    /// Fills ShopScreen.rows — an array of a nested serializable pair, which
    /// SetRef can't reach, so this walks the SerializedProperty itself.
    /// </summary>
    private static void WireRows(ShopScreen shop, Button[] buttons, TMP_Text[] labels)
    {
        var so = new SerializedObject(shop);
        var rows = so.FindProperty("rows");
        if (rows == null)
        {
            Debug.LogError("No serialized field 'rows' on ShopScreen.", shop);
            return;
        }

        rows.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++)
        {
            var element = rows.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("button").objectReferenceValue = buttons[i];
            element.FindPropertyRelative("label").objectReferenceValue = labels[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shop);
    }

    /// <summary>
    /// Appends the Shop scene to Build Settings without disturbing what's
    /// already registered — WordCrushSetup.RegisterScenes owns the ordering.
    /// </summary>
    private static void RegisterInBuild()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
