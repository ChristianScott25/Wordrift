using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 🚧 TEMPORARY — THE BOOKSHOP EXPERIMENT (2026-10-28). An alternative to
/// ShopScreen, picked by the mode (RogueDemoModeConfig.shopSceneName =
/// "Bookshop"); only Mode_RogueDemo_Bookshop uses it. If it isn't fun, deleting it
/// is the list at the top of Editor/BookshopModeSetup.cs.
///
/// THE SHOP IS PART OF THE BUILD. It starts small — two bookmarks and one item —
/// and the player buys UPGRADES for the shop itself: more slots in a section, or
/// whole new sections (tiles, checkouts). Upgrades last the whole run and live on
/// RunState (ShopLevel), and every one bought makes every other one a little
/// dearer (RogueDemoModeConfig.bookshopUpgradeStep), so a run has to choose what
/// kind of shop it builds.
///
/// It sells exactly what the ordinary shop sells, through the same ShopOffer
/// classes and ShopRoller rolls — only the layout and the upgrades are new. No
/// tile upgrades (2L, 3W) in this first version, his call.
///
/// The sections are BUILT AT RUNTIME, because how many there are and how many
/// cards each holds changes as the run upgrades. The scene (BookshopSceneSetup)
/// supplies the frame: the header, a scroll view to build into, the description
/// panel, and REROLL / CONTINUE.
///
/// Rules carried over from ShopScreen, unchanged:
///   - picking and buying are two acts: a tap opens the description, BUY spends;
///   - a sold card stays on screen greyed out, an unaffordable one stays tappable;
///   - the shelf only changes when the player asks (buys, upgrades, rerolls);
///   - the visit is saved after every change, stock and stream position included.
/// </summary>
public class BookshopScreen : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text headline;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private TMP_Text moneyLabel;

    [Tooltip("Checkouts owned, bag size, items carried — see ShopRoller.OwnedLine.")]
    [SerializeField] private TMP_Text ownedLabel;

    [Header("Shelf")]
    [Tooltip("The scroll view. Hidden whole while a description is open.")]
    [SerializeField] private GameObject shelfRoot;

    [Tooltip("The scroll view's content, which the sections are built into. Needs " +
             "a VerticalLayoutGroup and a ContentSizeFitter so it grows as they do.")]
    [SerializeField] private RectTransform sectionsRoot;

    [Header("What the run owns (with SELL)")]
    [Tooltip("The draggable bookmark row the scene copied from the Shop. Its taps " +
             "are pointed at the SELL panel while this screen is up; drags still " +
             "reorder.")]
    [SerializeField] private BookmarkRowWidget bookmarkRow;

    [Tooltip("Where the items strip is built — needs a HorizontalLayoutGroup.")]
    [SerializeField] private RectTransform ownedItemsRoot;

    [Tooltip("Shown where the bookmark row would be while the run owns none — the " +
             "row hides itself then, and an empty band reads as a layout bug.")]
    [SerializeField] private TMP_Text noBookmarksLabel;

    [Tooltip("The description's main button is BUY or SELL; these are its tints. " +
             "Same green and red as PLAY and DISCARD.")]
    [SerializeField] private Color buyColor = new Color(0.36f, 0.75f, 0.40f);
    [SerializeField] private Color sellColor = new Color(0.82f, 0.30f, 0.28f);

    [Header("🚧 Look (placeholder art)")]
    [SerializeField] private Sprite sectionSprite;
    [SerializeField] private Sprite cardSprite;

    [Tooltip("Drawn on every item card. Items have no art of their own yet.")]
    [SerializeField] private Sprite itemSprite;

    [Tooltip("How chunky the plates' 9-sliced corners are. Bigger = chunkier, the " +
             "same knob the info box has.")]
    [SerializeField] private float borderScale = 2.5f;

    [SerializeField] private Color inkColor = new Color(0.12f, 0.13f, 0.17f);
    [SerializeField] private Color quietInkColor = new Color(0.12f, 0.13f, 0.17f, 0.65f);
    [SerializeField] private Color upgradeColor = new Color(0.30f, 0.20f, 0.13f);
    [SerializeField] private Color upgradeTextColor = new Color(1f, 0.85f, 0.45f);

    [Tooltip("One card. A section wraps onto a new row after Columns cards.")]
    [SerializeField] private Vector2 cardSize = new Vector2(210f, 260f);

    [SerializeField] private float cardSpacing = 18f;
    [SerializeField, Min(1)] private int columns = 4;

    [Header("Description")]
    [Tooltip("Hidden until something is tapped. Must be a CHILD object, never this one.")]
    [SerializeField] private GameObject detailRoot;

    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailBody;
    [SerializeField] private TMP_Text detailPrice;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyLabel;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollLabel;

    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "Main Menu";

    /// <summary>
    /// Every section, in value order — which is draw order, roll order and save
    /// order at once (see ShopSection). Taken from the enum rather than written
    /// out, so a section added there can't be forgotten here.
    /// </summary>
    private static readonly ShopSection[] Sections =
        (ShopSection[])Enum.GetValues(typeof(ShopSection));

    /// <summary>This visit's stock, one list per section, indexed by ShopSection.</summary>
    private readonly List<ShopOffer>[] stock = new List<ShopOffer>[Sections.Length];

    private RunState run;

    // The run's shop stream, taken once and kept — asking again restarts it.
    private Rng rng;
    private ShopRoller roller;

    // Rerolls bought THIS VISIT, saved like the ordinary shop's.
    private int rerolls;

    // What the description panel is showing, if anything. Read through these
    // rather than holding the offer, so a redraw can't leave the panel stale.
    private enum Picked { None, Offer, Upgrade, OwnedBookmark, OwnedConsumable }
    private Picked picked = Picked.None;
    private ShopSection pickedSection;
    private int pickedSlot;              // a shelf slot, or a carried item's slot
    private BookmarkSpec pickedBookmark; // by reference: a drag can reorder the row under it

    private void OnEnable() => RunState.Changed += OnRunChanged;

    private void OnDisable()
    {
        RunState.Changed -= OnRunChanged;

        // Only if it's still ours — the row is a separate object and may have
        // been handed to something else.
        if (bookmarkRow != null && bookmarkRow.CardTapped == OnBookmarkTapped)
            bookmarkRow.CardTapped = null;
    }

    /// <summary>A bookmark dragged in the row — its order is part of the run, so save.</summary>
    private void OnRunChanged()
    {
        if (run == null) return;
        Redraw();
        Save();
    }

    private void Start()
    {
        run = RunState.Current;
        if (run == null)
        {
            Debug.LogWarning("Bookshop opened with no run in progress — returning to the menu.");
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        if (detailRoot == gameObject)
        {
            Debug.LogError("BookshopScreen's 'detailRoot' must be a child object, not itself.", this);
            detailRoot = null;
        }

        if (sectionsRoot == null)
            Debug.LogError("BookshopScreen has no sections root wired — run " +
                           "Word Crush > 🚧 Create Bookshop Scene.", this);

        for (int i = 0; i < stock.Length; i++) stock[i] = new List<ShopOffer>();

        rng = run.StreamFor(RunState.ShopStream);
        roller = new ShopRoller(run, rng);

        // A tap on an owned bookmark means SELL here, not "describe it".
        if (bookmarkRow != null) bookmarkRow.CardTapped = OnBookmarkTapped;

        if (headline != null) headline.text = $"ROUND {run.Round} CLEARED";
        if (detail != null) detail.text = $"NEXT TARGET   {run.Template.TargetForRound(run.Round + 1)}";

        var saved = RunState.TakePendingShop();
        if (saved != null && saved.captured) RestockFrom(saved); else StockAll();

        WarnAboutFreeRows();
        CloseDetail();
        Save();
    }

    // ------------------------------------------------------------------------
    // THE STOCK

    private static string KindOf(ShopSection section) => section switch
    {
        ShopSection.Bookmarks => ShopOfferData.Bookmark,
        ShopSection.Consumables => ShopOfferData.Consumable,
        ShopSection.Tiles => ShopOfferData.Tile,
        ShopSection.Checkouts => ShopOfferData.Checkout,
        _ => "",
    };

    private List<ShopOffer> StockOf(ShopSection section) => stock[(int)section];

    /// <summary>
    /// A fresh shelf: every open section rolled from empty. ⚠️ THE ROLL ORDER IS
    /// PART OF THE SEED — sections in ShopSection order, slots left to right.
    /// </summary>
    private void StockAll()
    {
        foreach (var section in Sections)
        {
            StockOf(section).Clear();
            FillSection(section);
        }
    }

    /// <summary>
    /// Tops a section up to however many slots it has now — the whole of what an
    /// upgrade does to the shelf. A brand-new slot is rolled at once, so the
    /// player SEES what they just bought; only the new slots draw, so nothing
    /// already on the shelf moves.
    /// </summary>
    private void FillSection(ShopSection section)
    {
        var offers = StockOf(section);
        int want = run.ShopSlots(section);

        // Only if the ladder was authored to SHRINK a section. Trimmed rather
        // than kept, so what's saved always matches what the level says.
        while (offers.Count > want) offers.RemoveAt(offers.Count - 1);
        while (offers.Count < want) offers.Add(RollOne(section, offers));
    }

    /// <summary>
    /// One new card for a section, kept DIFFERENT from what the section already
    /// shows where the pool allows (see the exclude lists on ShopRoller). One
    /// draw at most, whatever is excluded, so the stream stays predictable.
    /// </summary>
    private ShopOffer RollOne(ShopSection section, List<ShopOffer> beside)
    {
        switch (section)
        {
            case ShopSection.Bookmarks:
            {
                var shown = new List<Bookmark>();
                foreach (var offer in beside)
                    if (offer is BookmarkOffer b && b.Bookmark != null) shown.Add(b.Bookmark);
                var rolled = roller.RollBookmarks(1, shown);
                return new BookmarkOffer { Bookmark = rolled.Count > 0 ? rolled[0] : null };
            }

            case ShopSection.Consumables:
            {
                var shown = new List<Consumable>();
                foreach (var offer in beside)
                    if (offer is ConsumableOffer c && c.Consumable != null) shown.Add(c.Consumable);
                return new ConsumableOffer { Consumable = roller.RollConsumable(shown) };
            }

            case ShopSection.Tiles:
            {
                var shown = new List<LetterSet.Entry>();
                foreach (var offer in beside)
                    if (offer is TileOffer t && t.Entry != null) shown.Add(t.Entry);
                return new TileOffer { Entry = roller.RollTile(shown) };
            }

            case ShopSection.Checkouts:
            {
                var shown = new List<Checkout>();
                foreach (var offer in beside)
                    if (offer is CheckoutOffer c && c.Checkout != null) shown.Add(c.Checkout);
                return new CheckoutOffer { Checkout = roller.RollCheckout(shown) };
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// Rebuilds the visit exactly as it was saved and winds the stream past the
    /// rolls already made. The saved rows are one flat list in section order;
    /// each section takes as many as its level gives it slots.
    ///
    /// Anything that doesn't line up — a row of the wrong kind, too few or too
    /// many — re-rolls the whole visit rather than restoring a shelf that's
    /// quietly wrong. The levels themselves come off RunState, which the resume
    /// has already rebuilt.
    /// </summary>
    private void RestockFrom(ShopSnapshot saved)
    {
        rng.Skip(saved.rngDraws);
        rerolls = Mathf.Max(0, saved.rerolls);

        int next = 0;
        bool fits = true;

        foreach (var section in Sections)
        {
            var offers = StockOf(section);
            offers.Clear();

            int want = run.ShopSlots(section);
            for (int i = 0; i < want && fits; i++)
            {
                if (next >= saved.offers.Count) { fits = false; break; }

                var entry = saved.offers[next++];

                // Kind checked BEFORE restoring, so a mismatched row can't make
                // Restore draw from the stream on the way to being thrown away.
                var offer = entry.kind == KindOf(section) ? roller.Restore(entry) : null;
                if (offer == null) { fits = false; break; }
                offers.Add(offer);
            }
        }

        if (fits && next == saved.offers.Count) return;

        Debug.LogWarning($"Saved Bookshop shelf ({saved.offers.Count} rows) doesn't match " +
                         "the shop's sections as they stand — re-rolling the visit rather " +
                         "than restoring a shelf that can't line up.");
        StockAll();
    }

    private void WarnAboutFreeRows()
    {
        foreach (var section in Sections)
            ShopRoller.WarnAboutFreeRows(StockOf(section), this);
    }

    /// <summary>Writes the run out with the shelf as it stands. Same shape as the ordinary shop's save.</summary>
    private void Save()
    {
        if (run == null) return;

        var data = run.Capture(SaveLocation.Shop);
        var snapshot = new ShopSnapshot
        {
            captured = true,
            rngDraws = rng == null ? 0 : rng.Draws,
            rerolls = rerolls,
        };

        var index = run.TileIndex();
        foreach (var section in Sections)
            foreach (var offer in StockOf(section))
                snapshot.offers.Add(offer.Capture(index));

        data.shopState = snapshot;
        RunSave.Write(data);
    }

    // ------------------------------------------------------------------------
    // NAMES

    private static string NameOf(ShopSection section) => section switch
    {
        ShopSection.Bookmarks => "BOOKMARKS",
        ShopSection.Consumables => "CONSUMABLES",
        ShopSection.Tiles => "TILES",
        ShopSection.Checkouts => "CHECKOUTS",
        _ => section.ToString().ToUpperInvariant(),
    };

    /// <summary>What one slot of a section holds, for the upgrade descriptions.</summary>
    private static string NounOf(ShopSection section, int count)
    {
        string one = section switch
        {
            ShopSection.Bookmarks => "bookmark",
            ShopSection.Consumables => "item",
            ShopSection.Tiles => "new tile",
            ShopSection.Checkouts => "checkout",
            _ => "thing",
        };
        return count == 1 ? one : one + "s";
    }

    /// <summary>The section's slot count one level up, or its current count at the top.</summary>
    private int NextSlots(ShopSection section) =>
        run.Template.BookshopSlots(section, run.ShopLevel(section) + 1);

    // ------------------------------------------------------------------------
    // DRAWING

    /// <summary>
    /// Redraws everything from the stock and the run. Rebuilds the sections from
    /// scratch rather than patching them — a purchase is a rare event and this
    /// is a few dozen objects, and one path that draws everything can't leave a
    /// card describing a shelf that has since changed.
    /// </summary>
    private void Redraw()
    {
        if (moneyLabel != null)
        {
            moneyLabel.text = run.LastPayout > 0
                ? $"{run.MoneyText}   <size=60%>+${run.LastPayout} EARNED</size>"
                : run.MoneyText;
        }

        if (ownedLabel != null) ownedLabel.text = ShopRoller.OwnedLine(run);

        // What the run owns sits outside the shelf and stays on screen while a
        // description is open, so it's redrawn before the guard below.
        if (noBookmarksLabel != null)
        {
            noBookmarksLabel.gameObject.SetActive(run.Bookmarks.Count == 0);
            int cap = run.Template.maxBookmarks;
            noBookmarksLabel.text = cap > 0 ? $"NO BOOKMARKS YET   0/{cap}" : "NO BOOKMARKS YET";
        }
        BuildOwnedItems();

        // A description is open: it owns the screen until it closes, and closing
        // calls back here.
        if (picked != Picked.None) return;

        RefreshReroll();
        if (sectionsRoot == null) return;

        // Switched off BEFORE Destroy: Destroy waits for the end of the frame,
        // and until then the old sections would still be laid out beside the new.
        for (int i = sectionsRoot.childCount - 1; i >= 0; i--)
        {
            var child = sectionsRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        var locked = new List<ShopSection>();
        foreach (var section in Sections)
        {
            if (run.ShopSlots(section) > 0) BuildSection(section);
            else if (run.ShopUpgradePrice(section) > 0) locked.Add(section);
            // A section with no slots and no way to open it isn't in this mode.
        }

        if (locked.Count > 0) BuildAddSection(locked);
    }

    private void BuildSection(ShopSection section)
    {
        var panel = Panel(sectionsRoot, NameOf(section));

        // Header: the name, and the upgrade button (or MAX).
        var header = Row(panel, "Header", 84f);
        var title = Label(header, "Title", NameOf(section), 46f, TextAlignmentOptions.Left, inkColor);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        int price = run.ShopUpgradePrice(section);
        int gain = NextSlots(section) - run.ShopSlots(section);
        string upgradeText = price <= 0
            ? "MAX"
            : $"+{Mathf.Max(1, gain)} SLOT{(gain > 1 ? "S" : "")}   ${price}";

        var upgrade = SmallButton(header, "Upgrade", upgradeText, 330f);
        upgrade.interactable = price > 0;
        if (price > 0) upgrade.onClick.AddListener(() => SelectUpgrade(section));
        SetAlpha(upgrade, price > 0 && !run.CanAfford(price) ? 0.6f : 1f);

        // The cards, wrapping onto new rows after `columns`.
        var gridGo = new GameObject("Cards", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(panel, false);
        var grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = cardSize;
        grid.spacing = new Vector2(cardSpacing, cardSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.UpperCenter;

        var offers = StockOf(section);
        int shown = 0;
        for (int i = 0; i < offers.Count; i++)
        {
            // Nothing was ever rolled for this slot (every bookmark owned, say):
            // no card, the same way the ordinary shop hides the row.
            if (offers[i] == null || !offers[i].Visible) continue;
            BuildCard(gridGo.transform, section, i, offers[i]);
            shown++;
        }

        if (shown == 0)
            Label(panel, "Empty", "NOTHING LEFT TO SELL HERE", 30f, TextAlignmentOptions.Center, quietInkColor)
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
    }

    private void BuildCard(Transform parent, ShopSection section, int slot, ShopOffer offer)
    {
        var go = new GameObject($"Card {slot}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        Dress(image, cardSprite);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        // Unaffordable cards stay tappable ON PURPOSE — reading what something
        // does is how you decide to save for it. Only a sold card is dead.
        button.interactable = !offer.Sold;
        button.onClick.AddListener(() => SelectOffer(section, slot));

        int price = run.PriceOf(offer.ListPrice);
        bool affordable = run.CanAfford(price);
        go.GetComponent<CanvasGroup>().alpha = offer.Sold ? 0.35f : 1f;

        // What goes in the picture area: a bookmark's card art, the item icon, or
        // a tile's letters drawn big. A checkout has no art, so its name fills it.
        Sprite art = offer switch
        {
            BookmarkOffer b => b.Bookmark != null ? b.Bookmark.cardArt : null,
            ConsumableOffer => itemSprite,
            _ => null,
        };

        bool tile = offer is TileOffer;
        bool hasPicture = art != null || tile;

        if (art != null)
        {
            var pic = new GameObject("Art", typeof(RectTransform), typeof(Image));
            pic.transform.SetParent(go.transform, false);
            var picImage = pic.GetComponent<Image>();
            picImage.sprite = art;
            picImage.preserveAspect = true;
            picImage.raycastTarget = false;
            Place(pic, new Vector2(0f, 0.48f), new Vector2(1f, 1f), new Vector2(22f, 0f), new Vector2(-22f, -20f));
        }
        else if (tile && offer is TileOffer t && t.Entry != null)
        {
            string letters = t.Entry.IsWild ? TileSpec.WildSpelling : t.Entry.letter.ToUpperInvariant();
            var big = Label(go.transform, "Letters", letters, 80f, TextAlignmentOptions.Center, inkColor);
            big.enableAutoSizing = true;
            big.fontSizeMin = 30f;
            big.fontSizeMax = 80f;
            Place(big.gameObject, new Vector2(0f, 0.48f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-18f, -16f));
        }

        string name = RarityStyle.Name(tile ? $"{((TileOffer)offer).Entry?.points ?? 0} PTS" : offer.Title,
                                       offer.Rarity);
        var nameLabel = Label(go.transform, "Name", name, 30f, TextAlignmentOptions.Center, inkColor);
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMin = 16f;
        nameLabel.fontSizeMax = 30f;
        Place(nameLabel.gameObject,
            new Vector2(0f, 0.2f), new Vector2(1f, hasPicture ? 0.48f : 1f),
            new Vector2(14f, 0f), new Vector2(-14f, hasPicture ? 0f : -18f));

        var priceLabel = Label(go.transform, "Price",
            offer.Sold ? "SOLD" : ShopRoller.PriceText(offer.ListPrice, price),
            32f, TextAlignmentOptions.Center, inkColor);
        priceLabel.alpha = offer.Sold || affordable ? 1f : 0.55f;
        Place(priceLabel.gameObject, new Vector2(0f, 0f), new Vector2(1f, 0.2f),
            new Vector2(10f, 12f), new Vector2(-10f, 0f));
    }

    /// <summary>
    /// The "ADD A SECTION" card: one button per locked section, each naming what
    /// it opens and what it costs.
    /// </summary>
    private void BuildAddSection(List<ShopSection> locked)
    {
        var panel = Panel(sectionsRoot, "Add A Section");

        Label(panel, "Title", "ADD A SECTION", 46f, TextAlignmentOptions.Center, inkColor)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        foreach (var section in locked)
        {
            int price = run.ShopUpgradePrice(section);
            int slots = NextSlots(section);
            var button = SmallButton(panel,
                $"Open {section}",
                $"OPEN {NameOf(section)}   ${price}\n<size=70%>{slots} {NounOf(section, slots).ToUpperInvariant()} EVERY VISIT</size>",
                0f);
            button.gameObject.GetComponent<LayoutElement>().preferredHeight = 120f;
            button.onClick.AddListener(() => SelectUpgrade(section));
            SetAlpha(button, run.CanAfford(price) ? 1f : 0.6f);
        }
    }

    /// <summary>
    /// The items strip: "ITEMS 1/2", then one small card per slot the run may
    /// fill — the empty ones faint, so the cap is visible. Tapping a filled one
    /// opens SELL. Rebuilt whole, like the sections.
    /// </summary>
    private void BuildOwnedItems()
    {
        if (ownedItemsRoot == null) return;

        for (int i = ownedItemsRoot.childCount - 1; i >= 0; i--)
        {
            var child = ownedItemsRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        var items = run.Consumables;
        int cap = run.Template.maxConsumables;

        // 0 = no cap: show what's carried, and one empty slot so the strip
        // never reads as missing.
        int slots = cap > 0 ? Mathf.Max(cap, items.Count) : items.Count + 1;

        var title = Label(ownedItemsRoot, "Title",
            cap > 0 ? $"ITEMS  {items.Count}/{cap}" : $"ITEMS  {items.Count}",
            34f, TextAlignmentOptions.Left, Color.white);
        title.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;

        for (int i = 0; i < slots; i++)
        {
            var item = i < items.Count ? items[i] : null;

            var go = new GameObject($"Item {i}", typeof(RectTransform), typeof(Image), typeof(Button),
                                    typeof(LayoutElement), typeof(CanvasGroup));
            go.transform.SetParent(ownedItemsRoot, false);
            go.GetComponent<LayoutElement>().preferredWidth = 300f;

            var image = go.GetComponent<Image>();
            Dress(image, cardSprite);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            if (item == null)
            {
                button.interactable = false;
                go.GetComponent<CanvasGroup>().alpha = 0.4f;
                var empty = Label(go.transform, "Empty", "EMPTY", 26f, TextAlignmentOptions.Center, quietInkColor);
                Place(empty.gameObject, Vector2.zero, Vector2.one, new Vector2(12f, 6f), new Vector2(-12f, -6f));
                continue;
            }

            int slot = i;
            button.onClick.AddListener(() => SelectOwnedConsumable(slot));

            if (itemSprite != null)
            {
                var pic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                pic.transform.SetParent(go.transform, false);
                var picImage = pic.GetComponent<Image>();
                picImage.sprite = itemSprite;
                picImage.preserveAspect = true;
                picImage.raycastTarget = false;
                Place(pic, new Vector2(0f, 0f), new Vector2(0.3f, 1f), new Vector2(12f, 10f), new Vector2(0f, -10f));
            }

            var name = Label(go.transform, "Name", RarityStyle.Name(item.Title.ToUpperInvariant(), item.rarity), 26f,
                             TextAlignmentOptions.Center, inkColor);
            name.enableAutoSizing = true;
            name.fontSizeMin = 14f;
            name.fontSizeMax = 26f;
            Place(name.gameObject, new Vector2(itemSprite != null ? 0.3f : 0f, 0f), Vector2.one,
                  new Vector2(6f, 6f), new Vector2(-12f, -6f));
        }
    }

    // ---- Building blocks ---------------------------------------------------

    /// <summary>A section plate that stacks its children top to bottom and grows to fit them.</summary>
    private Transform Panel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        Dress(go.GetComponent<Image>(), sectionSprite);

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 30);
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        return go.transform;
    }

    /// <summary>A fixed-height strip that lays its children out left to right.</summary>
    private Transform Row(Transform parent, string name, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;

        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
        return go.transform;
    }

    /// <summary>A dark button with gold text — the upgrade buttons. Width 0 = fill the row.</summary>
    private Button SmallButton(Transform parent, string name, string text, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        Dress(image, cardSprite);
        image.color = upgradeColor;

        var element = go.GetComponent<LayoutElement>();
        if (width > 0f) element.preferredWidth = width;
        else element.flexibleWidth = 1f;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var label = Label(go.transform, "Label", text, 34f, TextAlignmentOptions.Center, upgradeTextColor);
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 34f;
        Place(label.gameObject, Vector2.zero, Vector2.one, new Vector2(16f, 8f), new Vector2(-16f, -8f));
        return button;
    }

    private static void SetAlpha(Button button, float alpha)
    {
        var group = button.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = alpha;
    }

    /// <summary>
    /// A label. ⚠️ NOT a raycast target: a label over a card would otherwise eat
    /// the tap meant for the card's button.
    /// </summary>
    private static TextMeshProUGUI Label(Transform parent, string name, string text, float size,
                                         TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = align;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    /// <summary>A plate: 9-sliced, with the same corner knob as the info box.</summary>
    private void Dress(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.color = Color.white;
        if (sprite == null) return;
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, borderScale);
    }

    private static void Place(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    // ------------------------------------------------------------------------
    // REROLL

    private int RerollPrice => ShopRoller.RerollPrice(run, rerolls);

    /// <summary>Wired to REROLL. Restocks every open section; upgrades are untouched.</summary>
    public void Reroll()
    {
        if (run == null || picked != Picked.None) return;

        int price = RerollPrice;
        if (price <= 0 || !run.TrySpend(price)) return;

        rerolls++;
        StockAll();
        WarnAboutFreeRows();
        Redraw();
        Save();
    }

    private void RefreshReroll()
    {
        if (rerollButton == null) return;

        int price = RerollPrice;
        rerollButton.gameObject.SetActive(price > 0);
        if (price <= 0) return;

        bool affordable = run.CanAfford(price);
        rerollButton.interactable = affordable;
        if (rerollLabel != null)
        {
            rerollLabel.text = $"REROLL   ${price}";
            rerollLabel.alpha = affordable ? 1f : 0.55f;
        }
    }

    // ------------------------------------------------------------------------
    // PICKING, THEN BUYING

    private void SelectOffer(ShopSection section, int slot)
    {
        var offers = StockOf(section);
        if (slot < 0 || slot >= offers.Count || offers[slot] == null || !offers[slot].Visible) return;

        picked = Picked.Offer;
        pickedSection = section;
        pickedSlot = slot;
        ShowDetail();
    }

    private void SelectUpgrade(ShopSection section)
    {
        if (run.ShopUpgradePrice(section) <= 0) return;

        picked = Picked.Upgrade;
        pickedSection = section;
        ShowDetail();
    }

    /// <summary>The bookmark row's tap hook (see Start). Opens SELL for that bookmark.</summary>
    private void OnBookmarkTapped(BookmarkSpec spec, RectTransform rect)
    {
        // One panel at a time — a tap on the row while another is open is ignored.
        if (run == null || spec == null || picked != Picked.None) return;

        picked = Picked.OwnedBookmark;
        pickedBookmark = spec;
        ShowDetail();
    }

    private void SelectOwnedConsumable(int slot)
    {
        if (run == null || picked != Picked.None) return;
        if (slot < 0 || slot >= run.Consumables.Count || run.Consumables[slot] == null) return;

        picked = Picked.OwnedConsumable;
        pickedSlot = slot;
        ShowDetail();
    }

    private ShopOffer PickedOffer
    {
        get
        {
            if (picked != Picked.Offer) return null;
            var offers = StockOf(pickedSection);
            return pickedSlot >= 0 && pickedSlot < offers.Count ? offers[pickedSlot] : null;
        }
    }

    private void ShowDetail()
    {
        if (detailRoot == null)
        {
            Debug.LogError("BookshopScreen has no description panel wired — run " +
                           "Word Crush > 🚧 Create Bookshop Scene.", this);
            picked = Picked.None;
            return;
        }

        if (picked == Picked.Offer)
        {
            var offer = PickedOffer;
            if (offer == null) { CloseDetail(); return; }

            int price = run.PriceOf(offer.ListPrice);
            string blocked = offer.Blocked(run);
            bool affordable = run.CanAfford(price);

            SetDetail(offer.Title, RarityStyle.WithLine(offer.Description, offer.Rarity, offer.Cursed),
                ShopRoller.PriceText(offer.ListPrice, price),
                offer.Available && affordable && blocked == null,
                !offer.Available ? "SOLD" : blocked ?? (affordable ? "BUY" : "NOT ENOUGH"), buyColor);
        }
        else if (picked == Picked.OwnedBookmark)
        {
            var bookmark = pickedBookmark?.bookmark;
            if (bookmark == null || !run.Bookmarks.Contains(pickedBookmark)) { CloseDetail(); return; }

            SetDetail(bookmark.Title.ToUpperInvariant(),
                RarityStyle.WithLine(bookmark.description, bookmark.rarity, bookmark.cursed),
                $"SELL FOR ${run.SellPrice(bookmark.price)}", true, "SELL", sellColor);
        }
        else if (picked == Picked.OwnedConsumable)
        {
            var item = pickedSlot >= 0 && pickedSlot < run.Consumables.Count ? run.Consumables[pickedSlot] : null;
            if (item == null) { CloseDetail(); return; }

            SetDetail(item.Title.ToUpperInvariant(), RarityStyle.WithLine(item.Power, item.rarity, item.cursed),
                $"SELL FOR ${run.SellPrice(item.price)}", true, "SELL", sellColor);
        }
        else
        {
            int price = run.ShopUpgradePrice(pickedSection);
            int now = run.ShopSlots(pickedSection);
            int then = NextSlots(pickedSection);
            string name = NameOf(pickedSection);
            bool affordable = run.CanAfford(price);

            string title = now == 0 ? $"OPEN {name}" : $"MORE {name}";
            string body = now == 0
                ? $"Opens the {name.ToLowerInvariant()} section. Every visit from now on " +
                  $"offers {then} {NounOf(pickedSection, then)} — starting right now."
                : $"{name[0]}{name.Substring(1).ToLowerInvariant()} go from {now} to {then} " +
                  "on every visit from now on. " +
                  (then - now == 1 ? "The new one fills in right now." : "The new ones fill in right now.");

            int step = run.Template.bookshopUpgradeStep;
            if (step > 0)
                body += $"\n\nEvery upgrade you buy makes every OTHER upgrade ${step} dearer.";

            SetDetail(title, body, $"${price}", price > 0 && affordable,
                price <= 0 ? "MAX" : affordable ? "BUY" : "NOT ENOUGH", buyColor);
        }

        SetShelfVisible(false);
        detailRoot.SetActive(true);
    }

    private void SetDetail(string title, string body, string price, bool canBuy, string buyText, Color tint)
    {
        if (detailTitle != null) detailTitle.text = title;
        if (detailBody != null) detailBody.text = body;
        if (detailPrice != null) detailPrice.text = price;
        if (buyLabel != null) buyLabel.text = buyText;

        if (buyButton != null)
        {
            buyButton.interactable = canBuy;

            // The tint lives in the ColorBlock, not on the Image — the same rule
            // WordActionsSetup.Dress follows for PLAY and DISCARD, or Unity's own
            // state tinting would paint over it on the next hover.
            var colors = buyButton.colors;
            colors.normalColor = tint;
            colors.selectedColor = tint;
            colors.highlightedColor = Lift(tint, 0.12f);
            colors.pressedColor = Lift(tint, -0.12f);
            buyButton.colors = colors;
        }
    }

    private static Color Lift(Color color, float by) => new Color(
        Mathf.Clamp01(color.r + by), Mathf.Clamp01(color.g + by), Mathf.Clamp01(color.b + by), color.a);

    /// <summary>Wired to BUY. The only thing that spends, for cards and upgrades alike.</summary>
    public void ConfirmBuy()
    {
        if (run == null) return;

        if (picked == Picked.Offer)
        {
            var offer = PickedOffer;
            if (offer == null || !offer.Available) return;

            // BEFORE TrySpend: a refusal after it would charge for nothing.
            if (offer.Blocked(run) != null) return;
            if (!run.TrySpend(run.PriceOf(offer.ListPrice))) return;

            offer.Deliver(run);
            offer.Sold = true;

            // RunState.AddBookmark raises nothing, so the row has to be told —
            // otherwise the bookmark you just bought isn't on it until you leave.
            if (offer is BookmarkOffer && bookmarkRow != null) bookmarkRow.Redraw();
        }
        else if (picked == Picked.OwnedBookmark)
        {
            // Raises RunState.Changed, which redraws the row and saves.
            if (!run.SellBookmark(pickedBookmark)) return;
        }
        else if (picked == Picked.OwnedConsumable)
        {
            if (!run.SellConsumable(pickedSlot)) return;
        }
        else if (picked == Picked.Upgrade)
        {
            if (!run.TryUpgradeShop(pickedSection)) return;

            // The new slot(s) appear at once — that's the payoff of the purchase.
            FillSection(pickedSection);
            ShopRoller.WarnAboutFreeRows(StockOf(pickedSection), this);
        }
        else return;

        CloseDetail();
        Save();
    }

    /// <summary>Wired to BACK.</summary>
    public void Back() => CloseDetail();

    private void CloseDetail()
    {
        picked = Picked.None;
        pickedBookmark = null;
        if (detailRoot != null) detailRoot.SetActive(false);
        SetShelfVisible(true);
        Redraw();
    }

    /// <summary>
    /// Shows or hides the shelf wholesale. While a description is open BUY and
    /// BACK are the only ways out — CONTINUE and REROLL hide with the shelf, so a
    /// tap meant for BACK can't start the next round or spend money.
    /// </summary>
    private void SetShelfVisible(bool visible)
    {
        if (shelfRoot != null) shelfRoot.SetActive(visible);
        if (continueButton != null) continueButton.gameObject.SetActive(visible);
        if (rerollButton != null) rerollButton.gameObject.SetActive(visible);
    }

    /// <summary>Wired to CONTINUE. Starts the next round of the run.</summary>
    public void Continue()
    {
        if (run == null)
        {
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        run.AdvanceRound();
        ModeSelection.Select(run.Template);
        SceneManager.LoadScene(gameSceneName);
    }
}
