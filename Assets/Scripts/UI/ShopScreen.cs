using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The between-rounds screen of a run: what you cleared, what you earned, and
/// what you can spend it on. Its one certainty is that everything sold here
/// edits RunState.Current — never an authored asset.
///
/// THE SHELF IS FIVE SLOTS WITH FIXED ROLES — two tile upgrades, two bookmarks,
/// one checkout — and each slot is stocked once, when the shop opens. Prices are
/// what the assets say (through the run's discount); buying a row SELLS it, and
/// it stays on screen greyed out rather than vanishing, so the shelf can't shift
/// under a finger that's already moving.
///
/// Picking a row and BUYING it are two different acts, the same way selecting
/// tiles and submitting them are: Select opens a description, and only
/// ConfirmBuy spends money. That's what makes a row you can't afford still worth
/// tapping — you can read what it does before you save up for it.
/// </summary>
public class ShopScreen : MonoBehaviour
{
    /// <summary>One row of the shop: a button and the label that describes the deal.</summary>
    [Serializable]
    public class OfferRow
    {
        public Button button;
        public TMP_Text label;
    }

    [SerializeField] private TMP_Text headline;
    [SerializeField] private TMP_Text detail;

    [Tooltip("Shows the run's balance and what the last round paid.")]
    [SerializeField] private TMP_Text moneyLabel;

    [Tooltip("Lists the bookmarks and checkouts the run owns. Blank when it owns none.")]
    [SerializeField] private TMP_Text bookmarkLabel;

    [Tooltip("The buy rows, in shelf order — there must be exactly Slots of them, " +
             "because which slot a row is decides what it sells. Word Crush > " +
             "Create Shop Scene lays them out.")]
    [SerializeField] private OfferRow[] rows;

    [Tooltip("Hidden until a row is tapped. Must be a CHILD object, never this one.")]
    [SerializeField] private GameObject detailRoot;

    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailBody;
    [SerializeField] private TMP_Text detailPrice;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyLabel;

    // BACK needs no reference here — it only ever calls Back(), which the scene
    // wires straight onto the button. Nothing in this class has to look at it.

    [Tooltip("Hidden while a description is open, so the only ways out of it are " +
             "BUY and BACK.")]
    [SerializeField] private Button continueButton;

    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "Main Menu";

    private readonly List<Offer> offers = new();
    private RunState run;

    // The shelf row whose description is open, or -1 for none. The panel reads
    // the offer through this rather than holding one, so a purchase can refresh
    // the same row without the panel going stale.
    private int selected = -1;

    // The run's shop stream, taken once and kept: asking RunState again would
    // restart the sequence and re-roll the same offers every purchase.
    private Rng rng;

    private void Start()
    {
        run = RunState.Current;
        if (run == null)
        {
            // The scene was opened on its own — nothing cleared, nowhere to go.
            Debug.LogWarning("Shop opened with no run in progress — returning to the menu.");
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        // A listener that deactivates its own GameObject stops hearing anything —
        // the same trap GameOverPanel guards against, and the shop would simply
        // disappear the first time a row was tapped.
        if (detailRoot == gameObject)
        {
            Debug.LogError("ShopScreen's 'detailRoot' must be a child object, not itself.", this);
            detailRoot = null;
        }

        // Slots have ROLES now, so a shelf that's the wrong length doesn't just
        // show fewer things — it drops whichever roles fall off the end, and the
        // checkout row is last. Loud, because the symptom (no checkouts, ever) is
        // nowhere near the cause.
        if (rows == null || rows.Length != Slots)
            Debug.LogError($"ShopScreen has {(rows == null ? 0 : rows.Length)} rows wired " +
                           $"but the shelf has {Slots} slots — run " +
                           "Word Crush > Create Shop Scene.", this);

        rng = run.StreamFor(RunState.ShopStream);

        if (headline != null) headline.text = $"ROUND {run.Round} CLEARED";
        if (detail != null) detail.text = $"NEXT TARGET   {run.Template.TargetForRound(run.Round + 1)}";

        // A visit that was interrupted comes back as it was — the same shelf at
        // the same prices, minus whatever was bought. Restocking would re-roll
        // it, and a bookmark already bought would be back on offer.
        var saved = RunState.TakePendingShop();
        if (saved != null && saved.captured) RestockFrom(saved); else StockShelves();

        WarnAboutFreeRows();
        CloseDetail();
        Refresh();
        Save();
    }

    /// <summary>
    /// A row that costs nothing is almost always an asset nobody priced: `price`
    /// defaults to 0 on every sellable asset, and the seeders only fill a 0 for
    /// the assets they know about — so a checkout or bookmark authored by hand
    /// and dropped into a mode's pool is FREE, and TrySpend(0) succeeds without
    /// complaint. Checked once per visit rather than in Refresh, which runs
    /// again after every purchase.
    /// </summary>
    private void WarnAboutFreeRows()
    {
        foreach (var offer in offers)
            if (offer.HasStock && offer.ListPrice <= 0)
                Debug.LogWarning($"Shop row '{offer.Title}' costs nothing — its asset's " +
                                 "price is still 0. Set one, or run the matching " +
                                 "Word Crush > Create ... Assets to seed the default.", this);
    }

    /// <summary>
    /// Writes the run out with the shelf as it stands. Called on arriving (the
    /// payout is already banked by then) and after every purchase, so closing the
    /// app in a shop loses nothing.
    /// </summary>
    private void Save()
    {
        if (run == null) return;

        var data = run.Capture(SaveLocation.Shop);
        var snapshot = new ShopSnapshot
        {
            captured = true,
            rngDraws = rng == null ? 0 : rng.Draws,
        };

        var index = run.TileIndex();
        foreach (var offer in offers) snapshot.offers.Add(offer.Capture(index));

        data.shopState = snapshot;
        RunSave.Write(data);
    }

    // ------------------------------------------------------------------------
    // THE STOCK
    //
    // Five slots with fixed roles. What's real here: set prices, one purchase per
    // row, and a shelf that empties as you buy it.
    //
    // 🚧 Still temporary: an upgrade lands on a RANDOM tile from your bag. The
    // roll happens up front so the button can show what you're buying — you never
    // choose it. Choosing needs a bag picker, which is its own piece of work.
    // ------------------------------------------------------------------------

    /// <summary>Tile-upgrade rows. Rolled independently, so they can coincide.</summary>
    private const int ModifierSlots = 2;

    /// <summary>Bookmark rows. Always two DIFFERENT bookmarks when two are left.</summary>
    private const int BookmarkSlots = 2;

    /// <summary>The checkout row. One per visit.</summary>
    private const int CheckoutSlots = 1;

    /// <summary>
    /// How many rows the shelf needs. Public because ShopSceneSetup lays out
    /// exactly this many buttons — the count used to be written down separately
    /// in the editor script, and two copies of it would part company the first
    /// time a slot was added.
    /// </summary>
    public const int Slots = ModifierSlots + BookmarkSlots + CheckoutSlots;

    /// <summary>
    /// One thing on the shelf. Subclassed rather than switched on, so the row
    /// rendering below never learns what kinds of stock exist.
    /// </summary>
    private abstract class Offer
    {
        /// <summary>
        /// Bought this visit. Separate from HasStock because it can't be
        /// re-derived: a sold bookmark is just one the run owns, which looks
        /// exactly like one it owned before walking in.
        /// </summary>
        public bool Sold;

        /// <summary>The row's name — "2L → E", "SPINE", "GREAT EXPECTATIONS".</summary>
        public abstract string Title { get; }

        /// <summary>What it does, in the player's words. The description panel's body.</summary>
        public abstract string Description { get; }

        /// <summary>What the asset says it costs, BEFORE the run's discount.</summary>
        public abstract int ListPrice { get; }

        /// <summary>
        /// False when this slot has nothing to sell at all — every bookmark
        /// owned, every tile full. The row hides rather than greying out: there
        /// is nothing to grey.
        /// </summary>
        public abstract bool HasStock { get; }

        /// <summary>Can still be bought right now.</summary>
        public bool Available => !Sold && HasStock;

        /// <summary>
        /// Has a row on screen at all — either something to sell, or the memory
        /// of having sold it.
        ///
        /// The second half is load-bearing: buying the third modifier for a tile
        /// makes that tile ineligible, so a sold upgrade row's HasStock flips to
        /// false the instant it's bought. Keying the row off HasStock alone would
        /// make the row you just paid for disappear rather than read SOLD.
        /// </summary>
        public bool Visible => Sold || HasStock;

        /// <summary>Applies the purchase to the run. Money has already been taken.</summary>
        public abstract void Deliver(RunState run);

        /// <summary>
        /// This row, written down. Stock is SAVED rather than re-derived from the
        /// seed: the shop's roll order changes whenever its code does, and a
        /// saved shelf must not depend on that.
        /// </summary>
        public abstract ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex);
    }

    /// <summary>
    /// A tile upgrade, landing on a random bag tile rolled up front.
    ///
    /// A tile can only hold so many modifiers (ModeConfig.maxModifiersPerTile),
    /// so the roll only ever returns one with room. When the whole bag is full it
    /// returns null, which is this row's "nothing to sell".
    /// </summary>
    private class ModifierOffer : Offer
    {
        public TileModifier Modifier;
        public TileSpec Target;    // the bag tile this purchase would gild
        public int ModifierLimit;  // 0 = no limit

        // Both halves matter: no tile to gild, or a tile that's since filled up.
        // ConfirmBuy checks Available BEFORE taking money, which is what makes
        // Deliver's own guard unreachable rather than a way to be charged for
        // nothing.
        public override bool HasStock =>
            Modifier != null && Target != null && Target.CanAddModifier(ModifierLimit);

        public override int ListPrice => Modifier == null ? 0 : Modifier.price;

        public override string Title =>
            Modifier == null || Target == null
                ? ""
                : $"{Modifier.badgeLabel} → {Target.letters.ToUpperInvariant()}";

        public override string Description
        {
            get
            {
                if (Modifier == null || Target == null) return "";

                string letter = Target.letters.ToUpperInvariant();
                string effect = string.IsNullOrWhiteSpace(Modifier.description)
                    ? ""
                    : Modifier.description + "\n\n";

                // Naming the tile's CURRENT badges matters: this is the only
                // place the player can tell a fresh tile from one they've already
                // gilded twice, and the two are worth very different money.
                string carried = Target.ModifierCount == 0
                    ? "It carries nothing yet."
                    : $"It already carries {BadgeList(Target)}.";

                return $"{effect}Stamps {Modifier.badgeLabel} onto one {letter} in your bag " +
                       $"— that one tile, for the rest of the run. {carried}";
            }
        }

        private static string BadgeList(TileSpec tile)
        {
            var badges = new List<string>();
            foreach (var modifier in tile.modifiers)
                if (modifier != null) badges.Add(modifier.badgeLabel);
            return string.Join(" ", badges);
        }

        public override void Deliver(RunState run)
        {
            // Unreachable: Available has already said this tile has room, and
            // ConfirmBuy checks it before spending. Loud rather than silent,
            // because by the time we're here the money is GONE — a quiet return
            // would charge the player for nothing.
            if (!Target.AddModifier(Modifier, ModifierLimit))
                Debug.LogError($"Bought {Modifier.name} for a tile that couldn't take it — " +
                               "the player has been charged and given nothing.");
        }

        public override ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex) => new ShopOfferData
        {
            kind = ShopOfferData.Modifier,
            assetName = Modifier == null ? "" : Modifier.name,
            targetTile = Target != null && tileIndex.TryGetValue(Target, out int i) ? i : -1,
            sold = Sold,
        };
    }

    /// <summary>
    /// A bookmark the run doesn't own. When there are none left unowned the slot
    /// simply has nothing in it and the row disappears.
    /// </summary>
    private class BookmarkOffer : Offer
    {
        public Bookmark Bookmark;

        public override bool HasStock => Bookmark != null;
        public override int ListPrice => Bookmark == null ? 0 : Bookmark.price;
        public override string Title =>
            Bookmark == null ? "" : Bookmark.displayName.ToUpperInvariant();
        public override string Description => Bookmark == null ? "" : Bookmark.description;

        public override void Deliver(RunState run)
        {
            // Unreachable in a healthy run — the shelf only ever stocks bookmarks
            // the run doesn't own. Loud rather than silent for the same reason
            // ModifierOffer is: by the time Deliver runs the money is GONE, so a
            // quiet refusal charges the player for nothing. The way it happens is
            // two bookmark assets sharing a file name in different folders: the
            // save records the name, FindByName resolves both rows to the first
            // match, and the second purchase buys what you already own.
            if (!run.AddBookmark(Bookmark))
                Debug.LogError($"Bought bookmark '{Bookmark.name}' that the run already owns — " +
                               "the player has been charged and given nothing.");
        }

        public override ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex) => new ShopOfferData
        {
            kind = ShopOfferData.Bookmark,

            // Empty here is the whole point: it means the run already owns every
            // bookmark, and the row must stay gone after a resume.
            assetName = Bookmark == null ? "" : Bookmark.name,
            sold = Sold,
        };
    }

    /// <summary>
    /// A checkout the run doesn't own — a permanent perk that takes effect the
    /// moment it's bought, including on the prices of the rows beside it.
    /// </summary>
    private class CheckoutOffer : Offer
    {
        public Checkout Checkout;

        public override bool HasStock => Checkout != null;
        public override int ListPrice => Checkout == null ? 0 : Checkout.price;
        public override string Title => Checkout == null ? "" : Checkout.Title.ToUpperInvariant();
        public override string Description =>
            Checkout == null
                ? ""
                : $"{Checkout.Power}\n\nYours for the rest of the run.";

        public override void Deliver(RunState run)
        {
            // See BookmarkOffer.Deliver — same unreachable case, same reason it
            // shouts instead of shrugging.
            if (!run.AddCheckout(Checkout))
                Debug.LogError($"Bought checkout '{Checkout.name}' that the run already owns — " +
                               "the player has been charged and given nothing.");
        }

        public override ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex) => new ShopOfferData
        {
            kind = ShopOfferData.Checkout,
            assetName = Checkout == null ? "" : Checkout.name,
            sold = Sold,
        };
    }

    /// <summary>
    /// Fills the five slots. THE ROLL ORDER IS PART OF THE SEED — every draw
    /// below comes off one stream in sequence, so reordering these lines changes
    /// what every existing seed stocks. Add to the end, don't reshuffle.
    ///
    /// Slots keep their roles even when a pool is empty: an offer with nothing in
    /// it still takes its place in the list, so the checkout is always row 5 and
    /// a shelf that loses its bookmarks doesn't slide everything up a row.
    /// </summary>
    private void StockShelves()
    {
        offers.Clear();

        // Targets are drawn without replacement ACROSS the upgrade rows. Two rows
        // naming the same tile looks harmless until that tile is one modifier
        // short of the limit: buying the first fills it, which flips the second
        // row's HasStock false while it is still unsold — so an untouched row
        // would vanish mid-visit, which is the one thing the shelf promises it
        // never does. Costs no extra draw, so the stream position is unchanged.
        int limit = run.Template.maxModifiersPerTile;
        var taken = new List<TileSpec>(ModifierSlots);
        for (int i = 0; i < ModifierSlots; i++)
        {
            var modifier = RollModifier();
            var target = RollTarget(taken);
            if (target != null) taken.Add(target);

            offers.Add(new ModifierOffer
            {
                Modifier = modifier,
                Target = target,
                ModifierLimit = limit,
            });
        }

        // Drawn together rather than one at a time, so the two rows can't offer
        // the same bookmark — which they otherwise would, often, against a pool
        // this small.
        var bookmarks = RollBookmarks(BookmarkSlots);
        for (int i = 0; i < BookmarkSlots; i++)
            offers.Add(new BookmarkOffer { Bookmark = i < bookmarks.Count ? bookmarks[i] : null });

        for (int i = 0; i < CheckoutSlots; i++)
            offers.Add(new CheckoutOffer { Checkout = RollCheckout() });
    }

    /// <summary>
    /// Rebuilds the shelf exactly as it was saved, and winds the shop's stream
    /// forward past the rolls it had already made — a restored stream restarts
    /// from the seed, so without the skip the next roll would repeat one the
    /// player has already seen.
    ///
    /// An offer naming an asset that's since vanished comes back EMPTY rather
    /// than being dropped from the list: dropping it would slide every row below
    /// it up a slot, and the slots have roles.
    /// </summary>
    private void RestockFrom(ShopSnapshot saved)
    {
        offers.Clear();
        rng.Skip(saved.rngDraws);

        // A shelf of the wrong length can't be restored faithfully — the slots
        // are positional, so there is no honest way to decide which role the
        // missing one was. Re-rolling loses the visit's stock, which is a bad
        // day; restoring it short would write the shorter shelf back out on the
        // next Save and lose that slot for the rest of the run, which is a bug
        // nobody would trace. This fires when Slots changes without a save
        // version bump — see RunSave.Version.
        if (saved.offers.Count != Slots)
        {
            Debug.LogWarning($"Saved shop shelf has {saved.offers.Count} rows but this " +
                             $"shop has {Slots} slots — re-rolling the visit rather than " +
                             "restoring a shelf that can't line up.");
            StockShelves();
            return;
        }

        foreach (var entry in saved.offers)
        {
            switch (entry.kind)
            {
                case ShopOfferData.Bookmark:
                    offers.Add(new BookmarkOffer
                    {
                        // Resolved whether or not the run owns it — a SOLD row has
                        // to keep saying which bookmark it sold. Null is allowed
                        // and means the slot was empty, which Refresh hides.
                        Bookmark = FindByName(run.Template.bookmarks, entry.assetName),
                        Sold = entry.sold,
                    });
                    break;

                case ShopOfferData.Checkout:
                    offers.Add(new CheckoutOffer
                    {
                        Checkout = FindByName(run.Template.checkouts, entry.assetName),
                        Sold = entry.sold,
                    });
                    break;

                default:
                    var target = run.TileAt(entry.targetTile);

                    // A saved target that's since filled up (it can't within one
                    // visit, but a save is not one visit) is re-rolled rather than
                    // restored — otherwise the row would offer a purchase Deliver
                    // would refuse. A SOLD row keeps its target either way, since
                    // the tile it names is the one it landed on.
                    if (!entry.sold && target != null &&
                        !target.CanAddModifier(run.Template.maxModifiersPerTile))
                        target = null;

                    offers.Add(new ModifierOffer
                    {
                        Modifier = FindByName(run.Template.tileModifiers, entry.assetName),
                        Target = target ?? (entry.sold ? null : RollTarget()),
                        ModifierLimit = run.Template.maxModifiersPerTile,
                        Sold = entry.sold,
                    });
                    break;
            }
        }
    }

    private static T FindByName<T>(List<T> pool, string assetName) where T : UnityEngine.Object
    {
        if (pool == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (var asset in pool)
            if (asset != null && asset.name == assetName) return asset;
        return null;
    }

    /// <summary>
    /// One of the mode's modifiers, at random. Rolled rather than taken in list
    /// order, so which upgrades a visit offers is part of the seed rather than
    /// part of how the Inspector list happens to be sorted.
    /// </summary>
    private TileModifier RollModifier()
    {
        var pool = run.Template.tileModifiers;
        if (pool == null) return null;

        var available = new List<TileModifier>();
        foreach (var modifier in pool)
            if (modifier != null) available.Add(modifier);

        return available.Count == 0 ? null : available[rng.Range(0, available.Count)];
    }

    /// <summary>
    /// A random tile out of the run's bag that still has room for a modifier —
    /// what this purchase would land on. Null when every tile is full, which
    /// takes the row off the shelf.
    ///
    /// Deliberately rolls over the ELIGIBLE tiles rather than rolling over the
    /// whole bag and retrying: a retry loop's draw count depends on how full the
    /// bag is, and the shop's stream position is saved.
    /// </summary>
    /// <param name="taken">
    /// Tiles another row on this shelf is already offering, or null for none.
    /// Excluded so two rows can't sell upgrades for the same tile — see
    /// StockShelves. Excluding shrinks the list a single draw indexes into; it
    /// never changes how many draws happen.
    /// </param>
    private TileSpec RollTarget(List<TileSpec> taken = null)
    {
        int limit = run.Template.maxModifiersPerTile;

        var available = new List<TileSpec>();
        foreach (var tile in run.TileBag)
            if (tile != null && tile.CanAddModifier(limit) &&
                (taken == null || !taken.Contains(tile)))
                available.Add(tile);

        return available.Count == 0 ? null : available[rng.Range(0, available.Count)];
    }

    /// <summary>
    /// Up to `count` DIFFERENT bookmarks the run doesn't own — fewer, or none, when
    /// it owns nearly all of them. A short list is a normal answer, not an error.
    /// </summary>
    private List<Bookmark> RollBookmarks(int count)
    {
        var picked = new List<Bookmark>();

        var available = new List<Bookmark>();
        var pool = run.Template.bookmarks;
        if (pool != null)
            foreach (var bookmark in pool)
                if (bookmark != null && !run.Owns(bookmark)) available.Add(bookmark);

        // Draws without replacement off the one stream, so two rows can't collide
        // and the number of draws depends only on how many are left — not on how
        // unlucky the rolls were.
        while (picked.Count < count && available.Count > 0)
        {
            int i = rng.Range(0, available.Count);
            picked.Add(available[i]);
            available.RemoveAt(i);
        }

        return picked;
    }

    /// <summary>
    /// A random checkout the run doesn't own, or null when it owns every one the
    /// mode offers. Null is a normal answer — the shop just has none today.
    /// </summary>
    private Checkout RollCheckout()
    {
        var pool = run.Template.checkouts;
        if (pool == null) return null;

        var available = new List<Checkout>();
        foreach (var checkout in pool)
            if (checkout != null && !run.Owns(checkout)) available.Add(checkout);

        return available.Count == 0 ? null : available[rng.Range(0, available.Count)];
    }

    // ---- Picking, then buying ---------------------------------------------

    /// <summary>
    /// Wired to a shelf button, one per row index. Opens the description — it
    /// never spends money, which is what lets a row you can't afford still be
    /// worth tapping.
    /// </summary>
    public void Select(int index)
    {
        if (run == null || index < 0 || index >= offers.Count) return;
        if (!offers[index].Visible) return;

        if (detailRoot == null)
        {
            Debug.LogError("ShopScreen has no detail panel wired — run " +
                           "Word Crush > Create Shop Scene to build it.", this);
            return;
        }

        selected = index;
        ShowDetail();
    }

    /// <summary>Wired to the description's BUY button.</summary>
    public void ConfirmBuy()
    {
        if (run == null || selected < 0 || selected >= offers.Count) return;

        var offer = offers[selected];
        if (!offer.Available) return;

        // The button is disabled when you can't afford it; this is the real
        // guard, since nothing else may take money.
        if (!run.TrySpend(run.PriceOf(offer.ListPrice))) return;

        offer.Deliver(run);
        offer.Sold = true;

        // Back to the shelf rather than lingering on a description of something
        // you now own — and the shelf is where the purchase is visible, both as
        // the SOLD row and, for a discount, as every other row getting cheaper.
        CloseDetail();
        Refresh();
        Save();
    }

    /// <summary>
    /// Wired to the description's BACK button.
    ///
    /// Refresh after, not just CloseDetail: closing puts every row back on screen
    /// wholesale, and it's Refresh that decides which ones deserve to be there.
    /// Without it, backing out would reveal the empty slots.
    /// </summary>
    public void Back()
    {
        CloseDetail();
        Refresh();
    }

    /// <summary>
    /// Shows the description and hides everything behind it — the shelf AND
    /// Continue, so BUY and BACK are the only ways out. Leaving Continue live
    /// would let a tap meant for BACK start the next round.
    /// </summary>
    private void ShowDetail()
    {
        var offer = offers[selected];
        int price = run.PriceOf(offer.ListPrice);

        if (detailTitle != null) detailTitle.text = offer.Title;
        if (detailBody != null) detailBody.text = offer.Description;
        if (detailPrice != null) detailPrice.text = PriceText(offer.ListPrice, price);

        bool affordable = run.CanAfford(price);
        if (buyButton != null) buyButton.interactable = offer.Available && affordable;
        if (buyLabel != null)
            buyLabel.text = !offer.Available ? "SOLD" : affordable ? "BUY" : "NOT ENOUGH";

        SetShelfVisible(false);
        detailRoot.SetActive(true);
    }

    private void CloseDetail()
    {
        selected = -1;
        if (detailRoot != null) detailRoot.SetActive(false);
        SetShelfVisible(true);
    }

    /// <summary>
    /// Shows or hides the shelf wholesale. Refresh decides which rows are really
    /// visible, so this only ever hides — showing hands the decision back.
    /// </summary>
    private void SetShelfVisible(bool visible)
    {
        if (continueButton != null) continueButton.gameObject.SetActive(visible);

        if (rows == null) return;
        foreach (var row in rows)
            if (row?.button != null) row.button.gameObject.SetActive(visible);
    }

    /// <summary>
    /// "$22" normally, "<s>$22</s> $18" when a checkout is taking something off —
    /// the discount is only believable if the player can see what it used to cost.
    /// </summary>
    private static string PriceText(int listPrice, int price) =>
        price == listPrice ? $"${price}" : $"<alpha=#60><s>${listPrice}</s><alpha=#FF> ${price}";

    private void Refresh()
    {
        if (moneyLabel != null)
        {
            moneyLabel.text = run.LastPayout > 0
                ? $"${run.Money}   <size=60%>+${run.LastPayout} EARNED</size>"
                : $"${run.Money}";
        }

        if (bookmarkLabel != null) bookmarkLabel.text = BuildOwnedLine();

        // A description is open over the top; it owns what's visible until it
        // closes, and it closes through CloseDetail, which calls back here.
        if (selected >= 0) return;

        if (rows == null) return;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null) continue;

            // Nothing was ever rolled for this slot — every bookmark owned, every
            // tile full. There is no deal to grey out, so the row goes.
            bool visible = i < offers.Count && offers[i].Visible;
            if (row.button != null) row.button.gameObject.SetActive(visible);
            if (!visible) continue;

            var offer = offers[i];
            int price = run.PriceOf(offer.ListPrice);
            bool affordable = run.CanAfford(price);

            if (row.label != null)
            {
                row.label.text = offer.Sold
                    ? $"{offer.Title}     SOLD"
                    : $"{offer.Title}     {PriceText(offer.ListPrice, price)}";

                // Sold is dimmer than merely unaffordable: one is over, the other
                // is a target to save for.
                row.label.alpha = offer.Sold ? 0.3f : affordable ? 1f : 0.55f;
            }

            // Unaffordable rows stay tappable ON PURPOSE — reading what something
            // does is how you decide whether to save for it. Only a sold row is
            // dead, because there is nothing left to learn about it.
            if (row.button != null) row.button.interactable = !offer.Sold;
        }
    }

    /// <summary>
    /// What the run is carrying, on one line. Bookmarks and checkouts share it
    /// because they're both "things you own that keep working" — and because the
    /// shop has one line for them.
    /// </summary>
    private string BuildOwnedLine()
    {
        var names = new List<string>();
        foreach (var owned in run.Bookmarks) names.Add(owned.Name.ToUpperInvariant());
        foreach (var checkout in run.Checkouts) names.Add(checkout.Title.ToUpperInvariant());

        return names.Count == 0 ? "" : $"OWNED   {string.Join("  ·  ", names)}";
    }

    /// <summary>Wired to the Continue button. Starts the next round of the run.</summary>
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
