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
/// THE SHELF IS SIX SLOTS WITH FIXED ROLES — two tile upgrades, two bookmarks,
/// one checkout, one new tile for the bag — stocked when the shop opens and then
/// only ever by a REROLL the player pays for. Prices are
/// what the assets say (through the run's discount); buying a row SELLS it, and
/// it stays on screen greyed out rather than vanishing, so the shelf can't shift
/// under a finger that's already moving.
///
/// A reroll is the one thing that replaces a row the player didn't buy, and it
/// doesn't break that promise: the shelf never changes on its own, only when
/// they ask it to and pay for it.
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

    [Tooltip("Re-stocks the whole shelf for a price that climbs each time. " +
             "Hidden while a description is open for the same reason Continue " +
             "is — a tap meant for BACK must not be able to spend money.")]
    [SerializeField] private Button rerollButton;

    [SerializeField] private TMP_Text rerollLabel;

    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "Main Menu";

    private readonly List<ShopOffer> offers = new();
    private RunState run;

    // The shelf row whose description is open, or -1 for none. The panel reads
    // the offer through this rather than holding one, so a purchase can refresh
    // the same row without the panel going stale.
    private int selected = -1;

    // The run's shop stream, taken once and kept: asking RunState again would
    // restart the sequence and re-roll the same offers every purchase.
    private Rng rng;

    // Every roll the shelf makes, off that one stream. Built once beside it.
    private ShopRoller roller;

    // Rerolls bought THIS VISIT — what the next one costs. Per-visit rather than
    // per-run, which needs no code to enforce: a fresh visit never goes through
    // RestockFrom, so this simply starts at 0 again. It IS saved, though (see
    // ShopSnapshot.rerolls) — without that, quitting and resuming would put the
    // price back to its base every time.
    private int rerolls;

    /// <summary>
    /// The bookmark row lets the player reorder while they're standing in the
    /// shop, and that order is part of the run. Writing immediately is safe here
    /// in a way it isn't in the Game scene — there's no board mid-fall to catch
    /// a half-settled snapshot of.
    /// </summary>
    private void OnEnable() => RunState.Changed += OnRunChanged;

    private void OnDisable() => RunState.Changed -= OnRunChanged;

    private void OnRunChanged()
    {
        // OnEnable runs before Start, so there is a window where this object is
        // listening and hasn't been handed a run yet. Nothing raises Changed in
        // it today, but Refresh would dereference `run` if anything ever did.
        if (run == null) return;

        // Refresh too, not just Save: the owned line and any FULL button are
        // reading the same list that just moved.
        Refresh();
        Save();
    }

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
        roller = new ShopRoller(run, rng);

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

    /// <summary>See ShopRoller.WarnAboutFreeRows. Once per stocking, not per Refresh.</summary>
    private void WarnAboutFreeRows() => ShopRoller.WarnAboutFreeRows(offers, this);

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
            rerolls = rerolls,
        };

        var index = run.TileIndex();
        foreach (var offer in offers) snapshot.offers.Add(offer.Capture(index));

        data.shopState = snapshot;
        RunSave.Write(data);
    }

    // ------------------------------------------------------------------------
    // THE STOCK
    //
    // Six slots with fixed roles. What's real here: set prices, one purchase per
    // row, and a shelf that empties as you buy it.
    //
    // 🚧 Still temporary: an upgrade lands on a RANDOM tile from your bag. The
    // roll happens up front so the button can show what you're buying — you never
    // choose it. Choosing needs a bag picker, which is its own piece of work.
    // ------------------------------------------------------------------------

    /// <summary>
    /// Tile-upgrade rows. There were two until consumables landed (2026-09-27);
    /// the second became the consumable row, because two rows selling the same
    /// kind of thing is the cheapest row on the shelf to give up.
    ///
    /// The machinery for more than one is deliberately still here — see the
    /// `taken` list in StockShelves — because it is what makes a second row safe
    /// if one ever comes back.
    /// </summary>
    private const int ModifierSlots = 1;

    /// <summary>Bookmark rows. Always two DIFFERENT bookmarks when two are left.</summary>
    private const int BookmarkSlots = 2;

    /// <summary>The checkout row. One per visit.</summary>
    private const int CheckoutSlots = 1;

    /// <summary>
    /// The new-tile row. One per visit, and unlike every other slot it never
    /// runs out: you may own as many CH tiles as you care to buy, so the same
    /// pair can come back next visit.
    /// </summary>
    private const int TileSlots = 1;

    /// <summary>
    /// The consumable row. One per visit, and like the tile row it never runs
    /// out: an item you spend is not an item you own, so it may offer one you're
    /// already carrying. What stops you buying a third is the carry cap, and
    /// that is answered in Blocked — the row stays on screen and says why.
    /// </summary>
    private const int ConsumableSlots = 1;

    /// <summary>
    /// How many rows the shelf needs. Public because ShopSceneSetup lays out
    /// exactly this many buttons — the count used to be written down separately
    /// in the editor script, and two copies of it would part company the first
    /// time a slot was added.
    /// </summary>
    public const int Slots =
        ModifierSlots + BookmarkSlots + CheckoutSlots + TileSlots + ConsumableSlots;

    // The Offer classes (one per kind of stock) live in Shop/ShopOffers.cs,
    // shared with the 🚧 Bookshop. The rolls live in Shop/ShopRoller.cs.

    /// <summary>
    /// Fills the six slots. THE ROLL ORDER IS PART OF THE SEED — every draw
    /// below comes off one stream in sequence, so reordering these lines changes
    /// what every existing seed stocks. Add to the end, don't reshuffle.
    ///
    /// Slots keep their roles even when a pool is empty: an offer with nothing in
    /// it still takes its place in the list, so the new tile is always row 6 and
    /// a shelf that loses its bookmarks doesn't slide everything up a row.
    /// </summary>
    private void StockShelves()
    {
        offers.Clear();

        // Targets are drawn without replacement ACROSS the upgrade rows. There is
        // only ONE such row today, so this is a list of one and does nothing —
        // kept because it is what makes a second row safe if one comes back, and
        // because the failure it prevents is not obvious: two rows naming the
        // same tile look harmless until that tile is one modifier short of the
        // limit, when buying the first fills it and flips the second row's
        // HasStock false WHILE IT IS STILL UNSOLD. An untouched row vanishing
        // mid-visit is the one thing the shelf promises it never does. It costs
        // no extra draw, so the stream position is unaffected either way.
        int limit = run.Template.maxModifiersPerTile;
        var taken = new List<TileSpec>(ModifierSlots);
        for (int i = 0; i < ModifierSlots; i++)
        {
            // Modifier FIRST: the target roll needs to know what would be
            // stamped, because a 0-point tile is no use to a letter multiplier.
            var modifier = roller.RollModifier();
            var target = roller.RollTarget(modifier, taken);
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
        var bookmarks = roller.RollBookmarks(BookmarkSlots);
        for (int i = 0; i < BookmarkSlots; i++)
            offers.Add(new BookmarkOffer { Bookmark = i < bookmarks.Count ? bookmarks[i] : null });

        for (int i = 0; i < CheckoutSlots; i++)
            offers.Add(new CheckoutOffer { Checkout = roller.RollCheckout() });

        // Appended after everything above it, which is the rule for adding a
        // slot. ⚠️ THE 2026-09-27 CONSUMABLE CHANGE BROKE IT ANYWAY: dropping one
        // of the two upgrade rows removed a draw from the MIDDLE, and every draw
        // after it shifted. Seeds recorded before that date no longer stock the
        // same shelf. That was a deliberate trade (his call — the shop's two
        // upgrade rows did the same job, and nothing records a seed yet), but
        // don't read this comment as saying old seeds are safe. They aren't.
        for (int i = 0; i < TileSlots; i++)
            offers.Add(new TileOffer { Entry = roller.RollTile() });

        // And this one after THAT, for the same reason. The shelf reads by kind
        // top to bottom — upgrade, bookmarks, checkout, tile, item — which is
        // the roll order too, and keeping the two the same is what makes "add to
        // the end" a rule anyone can follow.
        for (int i = 0; i < ConsumableSlots; i++)
            offers.Add(new ConsumableOffer { Consumable = roller.RollConsumable() });
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

        // Before the length check below: even a shelf too broken to restore has
        // to keep its price ladder, or re-rolling it hands back a cheap reroll.
        rerolls = Mathf.Max(0, saved.rerolls);

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
            // ⚠️ An unknown kind is a complaint, never a guess — see
            // ShopRoller.Restore. Restoring it as anything would put the wrong
            // thing on the shelf behind a save version bump that looked fine.
            var offer = roller.Restore(entry);
            if (offer == null)
            {
                Debug.LogError($"Saved shop row has kind '{entry.kind}', which this " +
                               "shop doesn't know how to restore — re-rolling the " +
                               "visit rather than putting the wrong thing on the shelf.");
                StockShelves();
                return;
            }
            offers.Add(offer);
        }
    }

    // ---- Re-rolling the shelf ----------------------------------------------

    /// <summary>What the next reroll costs, or 0 when the mode has them off. See ShopRoller.RerollPrice.</summary>
    private int RerollPrice => ShopRoller.RerollPrice(run, rerolls);
    /// <summary>
    /// Wired to the REROLL button. Buys a whole new shelf.
    ///
    /// Tap-to-buy, which the shelf rows deliberately are NOT: their
    /// select-then-confirm gap exists so a row can be read before it's paid for,
    /// and a reroll has nothing to read — the price is printed on the button, so
    /// a confirmation step would be friction carrying no information.
    ///
    /// StockShelves does the rest by itself. It builds brand-new Offers, so
    /// every row comes back UNSOLD, and it already skips bookmarks and checkouts
    /// the run owns — so something bought a moment ago correctly doesn't return.
    /// The stream needs no special handling either: every roll comes off it in
    /// sequence and Save records the new position, so a resume winds forward past
    /// the rerolls exactly as it already did past the opening shelf.
    /// </summary>
    public void Reroll()
    {
        // Never while a description is open. The button is hidden then, so this
        // is belt and braces — but the offer that description is reading would
        // be thrown away underneath it.
        if (run == null || selected >= 0) return;

        int price = RerollPrice;
        if (price <= 0) return;

        // The real guard. The button only dims when you can't afford it; nothing
        // but TrySpend may decide whether money actually moves.
        if (!run.TrySpend(price)) return;

        rerolls++;
        StockShelves();

        // A reroll is a new shelf, not a re-render, so the unpriced-asset check
        // earns another run: this stock could include an asset the opening shelf
        // never showed.
        WarnAboutFreeRows();

        Refresh();
        Save();
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

        // BEFORE TrySpend, not after. RunState.AddBookmark refuses past the cap
        // too, but by then the money is gone and all it can do is log that the
        // player was charged for nothing.
        if (offer.Blocked(run) != null) return;

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
        if (detailBody != null) detailBody.text = RarityStyle.WithLine(offer.Description, offer.Rarity, offer.Cursed, onDark: true);
        if (detailPrice != null) detailPrice.text = PriceText(offer.ListPrice, price);

        // Three separate reasons a row might not be buyable, and they must not
        // collapse into one: SOLD means you already took it, blocked means you
        // have no room for it, NOT ENOUGH means you can't pay. Being full and
        // being broke are different problems with different answers.
        string blocked = offer.Blocked(run);
        bool affordable = run.CanAfford(price);

        if (buyButton != null) buyButton.interactable = offer.Available && affordable && blocked == null;
        if (buyLabel != null)
            buyLabel.text = !offer.Available ? "SOLD"
                          : blocked ?? (affordable ? "BUY" : "NOT ENOUGH");

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

        // Hidden with Continue, not with the rows: while a description is open,
        // BUY and BACK are the only ways out, and this one spends money on a tap.
        // Refresh decides whether it comes BACK — a mode with rerolls turned off
        // has one that never shows.
        if (rerollButton != null) rerollButton.gameObject.SetActive(visible);

        if (rows == null) return;
        foreach (var row in rows)
            if (row?.button != null) row.button.gameObject.SetActive(visible);
    }

    private static string PriceText(int listPrice, int price) => ShopRoller.PriceText(listPrice, price);

    private void Refresh()
    {
        if (moneyLabel != null)
        {
            // MoneyText, not $"${run.Money}": it reads "$∞" on the unlimited-money
            // test mode. What the round PAID is still a real number either way, and
            // it's the half worth seeing when what you're testing is the economy.
            moneyLabel.text = run.LastPayout > 0
                ? $"{run.MoneyText}   <size=60%>+${run.LastPayout} EARNED</size>"
                : run.MoneyText;
        }

        if (bookmarkLabel != null) bookmarkLabel.text = BuildOwnedLine();

        // A description is open over the top; it owns what's visible until it
        // closes, and it closes through CloseDetail, which calls back here.
        if (selected >= 0) return;

        // Behind that guard on purpose: this can SHOW the reroll button, and a
        // description must stay the only thing on screen while it's open.
        RefreshReroll();

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
                string title = RarityStyle.Name(offer.Title, offer.Rarity, onDark: true);
                row.label.text = offer.Sold
                    ? $"{title}     SOLD"
                    : $"{title}     {PriceText(offer.ListPrice, price)}";

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
    /// The REROLL button: its price, and whether it can be pressed.
    ///
    /// Unlike a shelf row it goes non-interactable when you can't afford it,
    /// because tapping it IS the purchase — there is no description to read
    /// first, so leaving it live would only ever be a tap that does nothing.
    /// It still DIMS rather than disappearing, so the price stays legible as
    /// something to save for.
    /// </summary>
    private void RefreshReroll()
    {
        if (rerollButton == null) return;

        int price = RerollPrice;

        // 0 means the mode switched rerolls off (rerollBasePrice), so there is
        // no button at all rather than a free one. Shown as well as hidden, so
        // this method decides the answer on its own rather than depending on
        // SetShelfVisible having just turned it back on — the same both-ways
        // SetActive the row loop above does.
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

    private string BuildOwnedLine() => ShopRoller.OwnedLine(run);

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
