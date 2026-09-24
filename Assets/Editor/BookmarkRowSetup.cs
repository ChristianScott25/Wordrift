using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the bookmark row — Assets/Prefabs/Hud/BookmarkRowWidget.prefab — and
/// puts one in BOTH the Game scene and the Shop.
///
/// It goes in both because buying a bookmark and deciding where it sits are the
/// same thought, and the shop is where the first half happens. The prefab is
/// identical in the two scenes; only the Game scene wires `board`, which is what
/// makes the row track the board's bottom edge instead of a fixed height.
///
/// Safe to re-run. It adds what's missing, re-applies the layout (ABSOLUTE
/// positions, so a re-run can't walk the row down the screen) and leaves colours
/// and text alone once they exist.
///
/// ⚠️ It does NOT own the card SIZE. BookmarkRowWidget divides the row into
/// ModeConfig.maxBookmarks slots at runtime and sizes the cards to fit, so the
/// cap can be turned in the Inspector without coming back here. The size set on
/// the template below is only what it looks like sitting in the prefab.
/// </summary>
public static class BookmarkRowSetup
{
    private const string PrefabPath = "Assets/Prefabs/Hud/BookmarkRowWidget.prefab";
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string ShopScenePath = "Assets/Scenes/Shop.unity";
    private const string SkinPath = "Assets/GameData/Skins/TileSkin_White.asset";

    private const string RowName = "Row";
    private const string CardName = "CardTemplate";
    private const string CardLabelName = "Name";

    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    private static readonly Vector2 RowSize = new Vector2(1000f, 110f);

    // GAME SCENE: the band directly under the board, bottom-anchored. It used to
    // be the round banner's — the banner has moved below the action buttons, so
    // each has a permanent slot and nothing jumps when a librarian round starts.
    //
    // Only the STARTING position: the widget re-pins itself under the board's
    // real bottom edge at runtime, because the board is camera-framed and how
    // much room sits below it changes with the shape of the screen. This is
    // where it lands on a 16:9 editor Game view, where the board is big enough
    // that the pin bottoms out against its floor.
    private static readonly Vector2 RowAtInGame = new Vector2(0f, 372f);

    // SHOP: centre-anchored like everything else in that scene, in the band
    // between the owned line and the top shelf row.
    //
    // ⚠️ That band only exists because ShopSceneSetup's header constants were
    // pulled up to make it. OwnedY is 470 with a height of 44 (bottom edge 448)
    // and the top shelf row's upper edge is at 318, so this has 12px of air at
    // each end. Move either and check the other.
    private static readonly Vector2 RowAtInShop = new Vector2(0f, 385f);

    // One card as it sits in the prefab. The real size is computed at runtime.
    private static readonly Vector2 CardSize = new Vector2(190f, 100f);

    private static readonly Color CardColor = new Color(0.94f, 0.90f, 0.78f);
    private static readonly Color CardTextColor = new Color(0.16f, 0.17f, 0.23f);

    [MenuItem("Word Crush/Set Up Bookmark Row")]
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

        // Shop first, Game last: each of these opens its scene single-mode, so
        // whichever runs last is the one left open, and Game is the one anyone
        // running this expects to be looking at.
        PlaceIn(ShopScenePath, prefab, Center, RowAtInShop, wireBoard: false);
        PlaceIn(GameScenePath, prefab, BottomCenter, RowAtInGame, wireBoard: true);
    }

    // ---------------------------------------------------------------- prefab

    private static GameObject BuildPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (existing == null)
        {
            var made = WordCrushSetup.NewUI("BookmarkRowWidget", typeof(BookmarkRowWidget));
            WordCrushSetup.Anchor(made, BottomCenter, RowAtInGame, RowSize);
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

    private static void EnsureContents(GameObject widgetRoot)
    {
        var widget = widgetRoot.GetComponent<BookmarkRowWidget>()
                     ?? widgetRoot.AddComponent<BookmarkRowWidget>();

        // A child container, never the widget itself: the widget has to stay
        // active to keep hearing RoundStarted and RunState.Changed, so it hides
        // this instead when the run owns no bookmarks.
        var row = FindOrCreate(widgetRoot.transform, RowName);
        WordCrushSetup.Anchor(row, Center, Vector2.zero, RowSize);

        var card = EnsureCard(row.transform);

        // The RectTransform, NOT the GameObject. SetRef takes an Object and
        // hands it to objectReferenceValue, which silently refuses a type it
        // can't hold — and the widget's field is a RectTransform because it
        // measures the row and parents the cards to it. (SetRef's read-back
        // check would catch this, but only once somebody ran the item.)
        WordCrushSetup.SetRef(widget, "row", row.GetComponent<RectTransform>());
        WordCrushSetup.SetRef(widget, "cardTemplate", card);

        // The cards borrow the board's own tile art, so a bookmark reads as
        // something out of this game. Missing is survivable — the widget falls
        // back to whatever sprite the template was authored with — so this warns
        // rather than erroring.
        var skin = AssetDatabase.LoadAssetAtPath<TileSkin>(SkinPath);
        if (skin == null)
            Debug.LogWarning($"No tile skin at {SkinPath} — the cards will use the " +
                             "template's own sprite. Run Word Crush > Create Tile Skin Asset.");
        else
            WordCrushSetup.SetRef(widget, "cardSkin", skin);
    }

    /// <summary>
    /// The card to clone. Left switched OFF: it's a recipe, not a card, and one
    /// left visible looks exactly like a bookmark nobody owns. (The widget
    /// switches it off in Awake too — this is so it looks right in the prefab.)
    /// </summary>
    private static BookmarkCard EnsureCard(Transform parent)
    {
        var existing = parent.Find(CardName);
        BookmarkCard card = existing != null ? existing.GetComponent<BookmarkCard>() : null;
        Image body;

        if (card == null)
        {
            var made = WordCrushSetup.NewUI(CardName, typeof(Image), typeof(BookmarkCard));
            made.transform.SetParent(parent, false);
            card = made.GetComponent<BookmarkCard>();

            body = made.GetComponent<Image>();
            body.color = CardColor;
        }
        else
        {
            body = card.GetComponent<Image>() ?? card.gameObject.AddComponent<Image>();
        }

        // Position and size are ours; colour is not, once it exists.
        WordCrushSetup.Anchor(card.gameObject, Center, Vector2.zero, CardSize);

        // Re-applied every run, not just on creation. This is the one property
        // the card cannot work without: it's what lets the EventSystem see the
        // drag AND what keeps ChainController's press guard from poking a tile
        // through it.
        body.raycastTarget = true;

        var label = EnsureCardLabel(card.transform);

        WordCrushSetup.SetRef(card, "body", body);
        WordCrushSetup.SetRef(card, "label", label);

        card.gameObject.SetActive(false);
        return card;
    }

    private static TMP_Text EnsureCardLabel(Transform parent)
    {
        var existing = parent.Find(CardLabelName);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label == null)
        {
            var made = WordCrushSetup.MakeText(parent, CardLabelName, 24, TextAlignmentOptions.Center);
            made.color = CardTextColor;
            made.text = "BOOKMARK";
            label = made;
        }

        // Stretched with an inset rather than given a size: the card is resized
        // at runtime to divide the row, and a fixed label would not follow it.
        WordCrushSetup.Stretch(label.gameObject);
        var rect = (RectTransform)label.transform;
        rect.offsetMin = new Vector2(10f, 8f);
        rect.offsetMax = new Vector2(-10f, -8f);

        // Names run from "SPINE" to "VOWEL FANATIC" and the card is about 190px
        // wide, so the long ones have to wrap and shrink. Re-applied every run,
        // like the shop's rows: these are bounds, not styling.
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 26f;
        label.textWrappingMode = TextWrappingModes.Normal;

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

    // ----------------------------------------------------------------- scene

    private static void PlaceIn(string scenePath, GameObject prefab,
                                Vector2 anchor, Vector2 at, bool wireBoard)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            Debug.LogWarning($"No scene at {scenePath} — skipped. " +
                             "Run Word Crush > Create Shop Scene if this is the shop.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError($"{scenePath} has no Canvas — nothing to attach the bookmark row to.");
            return;
        }

        var widget = Object.FindFirstObjectByType<BookmarkRowWidget>(FindObjectsInactive.Include);
        if (widget == null)
        {
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            placed.name = prefab.name;
            widget = placed.GetComponent<BookmarkRowWidget>();
        }

        // Absolute, every run. A relative nudge would walk the row down the
        // screen a little further each time the item was used. The two scenes
        // anchor differently on purpose: the Game scene's HUD hangs off the
        // canvas edges, the Shop's off its centre.
        WordCrushSetup.Anchor(widget.gameObject, anchor, at, RowSize);

        // Writing straight to a RectTransform on a prefab INSTANCE isn't always
        // recorded as an override, and one that isn't recorded is lost on the
        // scene save — the row would silently snap back to the prefab's position.
        // SetRef does this for the references it writes; this is the same for the
        // layout. (Harmless on an object that isn't a prefab instance.)
        if (PrefabUtility.IsPartOfPrefabInstance(widget))
            PrefabUtility.RecordPrefabInstancePropertyModifications(widget.transform);

        if (wireBoard)
        {
            // The one thing that can only be done here: a prefab can't hold a
            // reference to a scene object. Without it the row sits at RowAt and
            // is only correct on the aspect RowAt was measured at.
            var board = Object.FindFirstObjectByType<Board>(FindObjectsInactive.Include);
            if (board == null)
                Debug.LogWarning($"{scenePath} has no Board — the bookmark row will sit at a " +
                                 "fixed height instead of tracking the board's bottom edge.");
            else
                WordCrushSetup.SetRef(widget, "board", board);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Bookmark row ready in {scenePath}.");
    }
}
