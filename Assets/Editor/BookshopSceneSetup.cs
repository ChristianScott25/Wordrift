using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🚧 TEMPORARY — builds Assets/Scenes/Bookshop.unity, the Bookshop experiment's
/// scene (see BookshopScreen, and BookshopModeSetup for how to delete it all).
///
/// ⚠️ THE SCENE IS REBUILT FROM Shop.unity ON EVERY RUN, and that is the design,
/// not a shortcut. Copying the ordinary shop is what hands the Bookshop the
/// camera, the backdrop, the bookmark row, the seed line, the info box and the
/// EventSystem — every piece another setup script places in the Shop — so the
/// two shops can't drift apart while they're being compared. The ordinary
/// ShopScreen is then removed and the Bookshop's frame built in its place.
/// The cost: NOTHING HAND-TUNED IN Bookshop.unity SURVIVES A RE-RUN. Tune the
/// constants here (or the fields on BookshopScreen, then copy the number in).
///
/// The sections themselves are not built here — BookshopScreen builds them at
/// runtime, because how many there are changes as a run upgrades. This builds
/// the frame they go in: header, scroll view, description panel, REROLL and
/// CONTINUE.
/// </summary>
public static class BookshopSceneSetup
{
    private const string ShopPath = "Assets/Scenes/Shop.unity";
    internal const string ScenePath = "Assets/Scenes/" + BookshopModeSetup.SceneName + ".unity";

    private static readonly Color AccentColor = new Color(1f, 0.75f, 0.1f);

    // The buttons wear the same plate and tinting as PLAY / DISCARD
    // (WordActionsSetup.Dress), and these are its colours: green to go ahead,
    // red to sell, parchment to back out, amber to reroll.
    private static readonly Color GoColor = new Color(0.36f, 0.75f, 0.40f);
    private static readonly Color SellColor = new Color(0.82f, 0.30f, 0.28f);
    private static readonly Color BackColor = new Color(0.90f, 0.84f, 0.70f);
    private static readonly Color RerollColor = new Color(0.98f, 0.74f, 0.30f);

    // Text on the parchment popup.
    private static readonly Color Ink = new Color(0.12f, 0.13f, 0.17f);
    private static readonly Color GoldInk = new Color(0.62f, 0.40f, 0.04f);

    private static readonly Vector2 Top = new Vector2(0.5f, 1f);
    private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    // The header, from the top edge down. ⚠️ WordCrushSetup.Anchor puts the PIVOT
    // on the anchor, so each of these is the TOP EDGE of its box, not its centre.
    // Started ~130 below the top so a notch can't sit on the headline (nothing in
    // the Shop handles safe area either).
    private const float HeadlineY = -130f;      // 90 tall
    private const float DetailY = -225f;        // 50 tall
    private const float MoneyY = -280f;         // 80 tall
    private const float BookmarksY = -370f;     // the bookmark row, 100 tall
    private const float ItemsY = -480f;         // the owned-items strip, 90 tall
    private const float ItemsHeight = 90f;
    private const float OwnedY = -578f;         // 40 tall
    private const float ShelfTop = 630f;        // distance from the top edge

    // The bottom bar: its BOTTOM edge, from the bottom of the screen. The seed
    // line sits under it at 24..64.
    private const float ButtonsY = 90f;
    private const float ButtonsHeight = 130f;
    private const float ShelfBottom = ButtonsY + ButtonsHeight + 20f;
    private const float ContinueWidth = 560f;
    private const float RerollWidth = 300f;
    private const float ButtonGap = 40f;

    private const float ShelfSideInset = 40f;

    [MenuItem("Word Crush/🚧 Create Bookshop Scene")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!File.Exists(ShopPath))
        {
            Debug.LogError($"No {ShopPath} to build the Bookshop from. Run Word Crush > Create Shop Scene first.");
            return;
        }

        // A fresh copy of the Shop, every time. See the class comment.
        var shop = EditorSceneManager.OpenScene(ShopPath, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(shop, ScenePath, saveAsCopy: true))
        {
            Debug.LogError($"Couldn't write {ScenePath}.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var old = Object.FindFirstObjectByType<ShopScreen>(FindObjectsInactive.Include);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError($"{ShopPath} has no Canvas — nothing to build the Bookshop on.");
            return;
        }

        // Match WIDTH, like the Game scene: the cards are laid out against a
        // 1080-wide canvas, and matching height would shrink that on a tall
        // phone until the fourth card fell off the row. Height is what grows,
        // and the scroll view soaks it up.
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.matchWidthOrHeight = 0f;

        var screen = BuildScreen(canvas.transform);

        // The bookmark row came along with the copy, centred where the Shop's
        // header left room for it. Re-pinned to the top for this header.
        var row = Object.FindFirstObjectByType<BookmarkRowWidget>(FindObjectsInactive.Include);
        if (row != null)
        {
            var rect = (RectTransform)row.transform;
            Vector2 size = rect.sizeDelta;
            WordCrushSetup.Anchor(row.gameObject, Top, new Vector2(0f, BookmarksY), size);

            // A prefab INSTANCE: a write that isn't recorded as an override is
            // lost on save — see BookmarkRowSetup.PlaceIn, which does the same.
            if (PrefabUtility.IsPartOfPrefabInstance(row))
                PrefabUtility.RecordPrefabInstancePropertyModifications(row.transform);
        }
        else
        {
            Debug.LogWarning("The Shop has no bookmark row, so neither does the Bookshop. " +
                             "Run Word Crush > Set Up Bookmark Row, then this again.");
        }

        // Its own info box is already in the copy (the Shop has one); this tops
        // it up the same way the Shop's setup does, in case it was built before
        // the box existed.
        InspectBoxSetup.Build(canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuild();

        Selection.activeObject = screen;
        Debug.Log($"Built {ScenePath} from the Shop, and added it to Build Settings. 🚧");
    }

    private static BookshopScreen BuildScreen(Transform canvas)
    {
        var rootGo = WordCrushSetup.NewUI("BookshopScreen", typeof(BookshopScreen));
        rootGo.transform.SetParent(canvas, false);
        WordCrushSetup.Stretch(rootGo);

        // FIRST, so everything the copy brought along (bookmark row, seed line,
        // info box) draws over it rather than under it.
        rootGo.transform.SetAsFirstSibling();

        var screen = rootGo.GetComponent<BookshopScreen>();
        var root = rootGo.transform;

        // ---- Header
        var headline = Text(root, "Headline", 72f, AccentColor, Top, HeadlineY, 90f, "ROUND 1 CLEARED");
        var detail = Text(root, "Detail", 44f, Color.white, Top, DetailY, 50f, "NEXT TARGET   90");
        var money = Text(root, "Money", 64f, AccentColor, Top, MoneyY, 80f, "$0");
        var owned = Text(root, "Owned", 32f, new Color(1f, 1f, 1f, 0.8f), Top, OwnedY, 40f, "");

        // Where the bookmark row sits; shown only while the run owns none (the
        // row hides itself then).
        var noBookmarks = Text(root, "NoBookmarks", 34f, new Color(1f, 1f, 1f, 0.6f), Top,
            BookmarksY - 25f, 50f, "NO BOOKMARKS YET   0/5");

        // The items strip: built at runtime into this, left to right.
        var items = WordCrushSetup.NewUI("OwnedItems", typeof(HorizontalLayoutGroup));
        items.transform.SetParent(root, false);
        WordCrushSetup.Anchor(items, Top, new Vector2(0f, ItemsY), new Vector2(1000f, ItemsHeight));
        var itemsLayout = items.GetComponent<HorizontalLayoutGroup>();
        itemsLayout.spacing = 16f;
        itemsLayout.childAlignment = TextAnchor.MiddleLeft;
        itemsLayout.childControlWidth = true;
        itemsLayout.childControlHeight = true;
        itemsLayout.childForceExpandWidth = false;
        itemsLayout.childForceExpandHeight = true;

        // ---- The scroll view the sections are built into
        var shelf = WordCrushSetup.NewUI("Shelf", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        shelf.transform.SetParent(root, false);
        var shelfRect = (RectTransform)shelf.transform;
        shelfRect.anchorMin = Vector2.zero;
        shelfRect.anchorMax = Vector2.one;
        shelfRect.offsetMin = new Vector2(ShelfSideInset, ShelfBottom);
        shelfRect.offsetMax = new Vector2(-ShelfSideInset, -ShelfTop);

        // Invisible, but a raycast target: it's what lets a drag that starts
        // BETWEEN two sections still scroll the page.
        shelf.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

        var content = WordCrushSetup.NewUI("Sections", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(shelf.transform, false);
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 24f;
        layout.padding = new RectOffset(0, 0, 0, 40);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = shelf.GetComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = shelfRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        // ---- REROLL | CONTINUE
        float total = ContinueWidth + ButtonGap + RerollWidth;
        var cont = WordCrushSetup.MakeButton(root, "ContinueButton", "CONTINUE", Vector2.zero, GoColor, Ink);
        WordActionsSetup.Dress(cont, GoColor);
        WordCrushSetup.Anchor(cont.gameObject, Bottom,
            new Vector2(-total / 2f + ContinueWidth / 2f, ButtonsY), new Vector2(ContinueWidth, ButtonsHeight));
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(cont.onClick, screen.Continue);

        var reroll = WordCrushSetup.MakeButton(root, "RerollButton", "REROLL   $5", Vector2.zero, RerollColor, Ink);
        WordActionsSetup.Dress(reroll, RerollColor);
        WordCrushSetup.Anchor(reroll.gameObject, Bottom,
            new Vector2(total / 2f - RerollWidth / 2f, ButtonsY), new Vector2(RerollWidth, ButtonsHeight));
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(reroll.onClick, screen.Reroll);
        var rerollLabel = reroll.GetComponentInChildren<TMP_Text>();
        AutoSize(rerollLabel, 28f, 48f);

        // ---- The description panel, last so it draws over the shelf
        var panel = BuildDetailPanel(screen, root);

        // ---- Wiring
        WordCrushSetup.SetRef(screen, "headline", headline);
        WordCrushSetup.SetRef(screen, "detail", detail);
        WordCrushSetup.SetRef(screen, "moneyLabel", money);
        WordCrushSetup.SetRef(screen, "ownedLabel", owned);
        WordCrushSetup.SetRef(screen, "shelfRoot", shelf);
        WordCrushSetup.SetRef(screen, "sectionsRoot", contentRect);
        WordCrushSetup.SetRef(screen, "continueButton", cont);
        WordCrushSetup.SetRef(screen, "rerollButton", reroll);
        WordCrushSetup.SetRef(screen, "rerollLabel", rerollLabel);
        WordCrushSetup.SetRef(screen, "detailRoot", panel);
        WordCrushSetup.SetRef(screen, "ownedItemsRoot", items.GetComponent<RectTransform>());
        WordCrushSetup.SetRef(screen, "noBookmarksLabel", noBookmarks);
        WordCrushSetup.SetColor(screen, "buyColor", GoColor);
        WordCrushSetup.SetColor(screen, "sellColor", SellColor);

        // The draggable row the copy brought along. Absent is survivable — the
        // Bookshop just can't sell bookmarks — and Create() has already warned.
        var row = Object.FindFirstObjectByType<BookmarkRowWidget>(FindObjectsInactive.Include);
        if (row != null) WordCrushSetup.SetRef(screen, "bookmarkRow", row);

        // 🚧 Placeholder art, the same plates the rest of the game wears.
        WordCrushSetup.SetRef(screen, "sectionSprite", GameLayoutSetup.PlateSprite());
        WordCrushSetup.SetRef(screen, "cardSprite", InspectBoxSetup.BoxSprite());
        var item = WordCrushSetup.LoadTempArt("book_item_icon");
        if (item != null) WordCrushSetup.SetRef(screen, "itemSprite", item);

        return screen;
    }

    /// <summary>The description: title, body, price, BUY, BACK. Hidden by the screen on Start.</summary>
    private static GameObject BuildDetailPanel(BookshopScreen screen, Transform root)
    {
        var panel = WordCrushSetup.NewUI("DetailPanel", typeof(Image));
        panel.transform.SetParent(root, false);
        // The same parchment plate as the sections and the round header.
        GameLayoutSetup.StylePlate(panel.GetComponent<Image>());
        // Pinned under the header (its TOP edge where the shelf starts) rather than
        // centred: centred, a 16:9 phone put it over the items strip and a 4:3
        // screen over the money and the bookmark row — which draws later, so its
        // cards covered the panel's title. The shelf is hidden while it's open, so
        // it has that whole space. Children are placed off its centre, as before.
        WordCrushSetup.Anchor(panel, Top, new Vector2(0f, -ShelfTop), new Vector2(940f, 1000f));
        var p = panel.transform;

        var title = WordCrushSetup.MakeText(p, "DetailTitle", 72f, TextAlignmentOptions.Center);
        title.color = GoldInk;
        WordCrushSetup.Anchor(title.gameObject, Center, new Vector2(0f, 380f), new Vector2(820f, 100f));
        AutoSize(title, 40f, 72f);

        var body = WordCrushSetup.MakeText(p, "DetailBody", 42f, TextAlignmentOptions.Center);
        body.color = Ink;
        body.fontStyle = FontStyles.Normal;
        WordCrushSetup.Anchor(body.gameObject, Center, new Vector2(0f, 130f), new Vector2(820f, 380f));
        AutoSize(body, 28f, 42f);

        var price = WordCrushSetup.MakeText(p, "DetailPrice", 76f, TextAlignmentOptions.Center);
        price.color = GoldInk;
        WordCrushSetup.Anchor(price.gameObject, Center, new Vector2(0f, -140f), new Vector2(860f, 90f));
        AutoSize(price, 44f, 76f);

        var buy = WordCrushSetup.MakeButton(p, "BuyButton", "BUY", Vector2.zero, GoColor, Ink);
        WordActionsSetup.Dress(buy, GoColor);
        WordCrushSetup.Anchor(buy.gameObject, Center, new Vector2(0f, -285f), new Vector2(780f, 120f));
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(buy.onClick, screen.ConfirmBuy);

        var back = WordCrushSetup.MakeButton(p, "BackButton", "BACK", Vector2.zero, BackColor, Ink);
        WordActionsSetup.Dress(back, BackColor);
        WordCrushSetup.Anchor(back.gameObject, Center, new Vector2(0f, -405f), new Vector2(780f, 100f));
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(back.onClick, screen.Back);

        WordCrushSetup.SetRef(screen, "detailTitle", title);
        WordCrushSetup.SetRef(screen, "detailBody", body);
        WordCrushSetup.SetRef(screen, "detailPrice", price);
        WordCrushSetup.SetRef(screen, "buyButton", buy);
        WordCrushSetup.SetRef(screen, "buyLabel", buy.GetComponentInChildren<TMP_Text>());
        return panel;
    }

    private static TMP_Text Text(Transform parent, string name, float size, Color color,
                                 Vector2 anchor, float y, float height, string text)
    {
        var label = WordCrushSetup.MakeText(parent, name, size, TextAlignmentOptions.Center);
        label.color = color;
        label.text = text;
        WordCrushSetup.Anchor(label.gameObject, anchor, new Vector2(0f, y), new Vector2(1000f, height));
        AutoSize(label, size * 0.5f, size);
        return label;
    }

    private static void AutoSize(TMP_Text label, float min, float max)
    {
        if (label == null) return;
        label.enableAutoSizing = true;
        label.fontSizeMin = min;
        label.fontSizeMax = max;
    }

    /// <summary>Appends the Bookshop to Build Settings without disturbing what's there.</summary>
    private static void RegisterInBuild()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
