using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything a shop ROLLS: which bookmark, which tile, which upgrade target —
/// plus turning a saved row back into an offer, and what a reroll costs.
/// Moved out of ShopScreen on 2026-10-28 so the 🚧 Bookshop rolls the same way
/// off the same stream, rather than keeping a second copy that would drift.
///
/// One per visit, built around the shop's stream. ⚠️ Every roll draws from that
/// stream in sequence, so the ORDER a shop calls these in is part of the seed —
/// that rule belongs to the screen calling them, not to this class.
///
/// The `exclude` lists are new with the Bookshop, whose sections hold several
/// rows of one kind. They only ever shrink the list a single draw indexes into,
/// so they never change how many draws happen — and null (what ShopScreen
/// passes) is exactly the old behaviour.
/// </summary>
public class ShopRoller
{
    private readonly RunState run;
    private readonly Rng rng;

    public ShopRoller(RunState run, Rng rng)
    {
        this.run = run;
        this.rng = rng;
    }

    // ---- Restoring a saved row ---------------------------------------------

    /// <summary>
    /// A saved row, rebuilt. Null when the row's KIND is one this code doesn't
    /// know, which the caller must treat as "this shelf can't be restored" —
    /// silently restoring it as something else is how the wrong stock ends up on
    /// the shelf with no warning.
    ///
    /// An offer naming an asset that's since vanished comes back EMPTY rather
    /// than null: dropping it would slide every row below it up a slot.
    /// </summary>
    public ShopOffer Restore(ShopOfferData entry)
    {
        switch (entry.kind)
        {
            case ShopOfferData.Bookmark:
                // Resolved whether or not the run owns it — a SOLD row has to
                // keep saying which bookmark it sold. Null is allowed and means
                // the slot was empty, which the screen hides.
                return new BookmarkOffer
                {
                    Bookmark = FindByName(run.Template.bookmarks, entry.assetName),
                    Sold = entry.sold,
                };

            case ShopOfferData.Checkout:
                return new CheckoutOffer
                {
                    Checkout = FindByName(run.Template.checkouts, entry.assetName),
                    Sold = entry.sold,
                };

            case ShopOfferData.Tile:
                return new TileOffer
                {
                    Entry = FindEntry(entry.assetName),
                    Sold = entry.sold,
                };

            case ShopOfferData.Consumable:
                return new ConsumableOffer
                {
                    Consumable = FindByName(run.Template.consumables, entry.assetName),
                    Sold = entry.sold,
                };

            // ⚠️ The modifier row is an explicit case, and an unknown kind is
            // null. It used to BE the default, which meant a kind nobody had
            // written a case for restored silently as a tile upgrade.
            case ShopOfferData.Modifier:
                // Read out rather than built inline: the re-roll below has to be
                // handed the same modifier, or a restored 2L row could land on a
                // 0-point tile that a fresh roll would never have offered.
                var modifier = FindByName(run.Template.tileModifiers, entry.assetName);
                var target = run.TileAt(entry.targetTile);

                // A saved target that's since filled up (it can't within one
                // visit, but a save is not one visit) is re-rolled rather than
                // restored — otherwise the row would offer a purchase Deliver
                // would refuse. A SOLD row keeps its target either way, since the
                // tile it names is the one it landed on.
                if (!entry.sold && target != null &&
                    !target.CanAddModifier(run.Template.maxModifiersPerTile))
                    target = null;

                return new ModifierOffer
                {
                    Modifier = modifier,
                    Target = target ?? (entry.sold ? null : RollTarget(modifier)),
                    ModifierLimit = run.Template.maxModifiersPerTile,
                    Sold = entry.sold,
                };

            default:
                return null;
        }
    }

    /// <summary>
    /// The catalog row a saved tile row named, or null if the catalog no longer
    /// has it. Null is allowed and means the slot comes back empty, the same as
    /// every other kind of vanished stock.
    /// </summary>
    private LetterSet.Entry FindEntry(string key)
    {
        var letters = run.Template.letterSet;
        return letters == null ? null : letters.EntryFor(key);
    }

    private static T FindByName<T>(List<T> pool, string assetName) where T : Object
    {
        if (pool == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (var asset in pool)
            if (asset != null && asset.name == assetName) return asset;
        return null;
    }

    // ---- Rolls ---------------------------------------------------------------

    /// <summary>
    /// One of the mode's modifiers, at random. Rolled rather than taken in list
    /// order, so which upgrades a visit offers is part of the seed rather than
    /// part of how the Inspector list happens to be sorted.
    /// </summary>
    public TileModifier RollModifier()
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
    /// <param name="modifier">
    /// What this row would stamp on. Needed because a tile worth 0 — a choice
    /// tile over 1-point letters, say — is a pointless target for a LETTER
    /// multiplier: 2L on nothing is nothing, and the row would charge for it.
    /// Word multipliers are fine there, so the test asks the modifier rather than
    /// its type (TileModifier.WorksOnZeroPointTile).
    /// </param>
    /// <param name="taken">
    /// Tiles another row on this shelf is already offering, or null for none.
    /// Excluded so two rows can't sell upgrades for the same tile — see
    /// ShopScreen.StockShelves. Excluding shrinks the list a single draw indexes
    /// into; it never changes how many draws happen.
    /// </param>
    public TileSpec RollTarget(TileModifier modifier, List<TileSpec> taken = null)
    {
        int limit = run.Template.maxModifiersPerTile;

        // Worked out once rather than per tile: it's a property of the modifier,
        // and the bag is a hundred tiles long.
        //
        // It can't empty the list on its own — every one of the starting 104
        // tiles scores, and RunState.AddTile only ever appends — so this narrows
        // what the single draw below indexes into without changing whether that
        // draw happens. Same property the `taken` exclusion relies on, and the
        // reason neither of them moves an existing seed.
        bool zeroPointOk = modifier == null || modifier.WorksOnZeroPointTile;

        var available = new List<TileSpec>();
        foreach (var tile in run.TileBag)
            // A WILD is skipped outright: it has no letter of its own to gild,
            // and a 3W that fits into any word at all would be the strongest
            // thing in the game by a distance. A CHOICE tile is fenced in enough
            // that it stays a legitimate target — a 3W on Q/Z is 21 points, well
            // under the 3W'd QU the shop already sells. Same "nothing to sell"
            // answer as a bag with no room — the row just doesn't draw.
            if (tile != null && !tile.Retired && !tile.IsWild && tile.CanAddModifier(limit) &&
                (zeroPointOk || tile.baseScore != 0) &&
                (taken == null || !taken.Contains(tile)))
                available.Add(tile);

        return available.Count == 0 ? null : available[rng.Range(0, available.Count)];
    }

    /// <summary>
    /// Up to `count` DIFFERENT bookmarks the run doesn't own — fewer, or none, when
    /// it owns nearly all of them. A short list is a normal answer, not an error.
    /// </summary>
    /// <param name="exclude">
    /// Bookmarks already on the shelf, so a slot the Bookshop adds mid-visit
    /// can't repeat one beside it. Null for none.
    /// </param>
    public List<Bookmark> RollBookmarks(int count, ICollection<Bookmark> exclude = null)
    {
        var picked = new List<Bookmark>();

        var available = new List<Bookmark>();
        var pool = run.Template.bookmarks;
        if (pool != null)
            foreach (var bookmark in pool)
                if (bookmark != null && !run.Owns(bookmark) &&
                    (exclude == null || !exclude.Contains(bookmark)))
                    available.Add(bookmark);

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
    /// <param name="exclude">Checkouts already on the shelf. Null for none.</param>
    public Checkout RollCheckout(ICollection<Checkout> exclude = null)
    {
        var pool = run.Template.checkouts;
        if (pool == null) return null;

        var available = new List<Checkout>();
        foreach (var checkout in pool)
            if (checkout != null && !run.Owns(checkout) &&
                (exclude == null || !exclude.Contains(checkout)))
                available.Add(checkout);

        return available.Count == 0 ? null : available[rng.Range(0, available.Count)];
    }

    /// <summary>
    /// A random multi-letter tile from the catalog, or null when the catalog has
    /// none. Deliberately unfiltered by what the run already owns: duplicates are
    /// the point, so a pair you bought last visit can be offered again.
    ///
    /// A row left at price 0 is NOT filtered out either — it reaches the shelf
    /// and trips WarnAboutFreeRows, which is a loud complaint about an unpriced
    /// catalog row rather than a tile that silently never appears.
    /// </summary>
    /// <param name="exclude">
    /// Tiles already on the shelf, PREFERRED against rather than forbidden: a
    /// section of three is nicer as three different tiles, but if the catalog
    /// runs out the duplicate is still a real thing to buy. Null for none.
    /// </param>
    public LetterSet.Entry RollTile(ICollection<LetterSet.Entry> exclude = null)
    {
        var letters = run.Template.letterSet;
        if (letters == null) return null;

        var available = new List<LetterSet.Entry>();
        foreach (var entry in letters.Entries)
            if (entry != null && entry.IsForSale) available.Add(entry);

        return PickPreferring(available, exclude);
    }

    /// <summary>
    /// A random consumable from the mode's pool, or null when the pool is empty.
    ///
    /// Deliberately unfiltered by what the run is carrying — the same call the
    /// tile row makes, and for the same reason: duplicates are legal, and with a
    /// pool this small, filtering would leave the row empty most visits.
    ///
    /// Exactly ONE draw, whatever the pool, so the stream stays predictable.
    /// </summary>
    /// <param name="exclude">Preferred against, like RollTile's. Null for none.</param>
    public Consumable RollConsumable(ICollection<Consumable> exclude = null)
    {
        var pool = run.Template.consumables;
        if (pool == null) return null;

        var available = new List<Consumable>();
        foreach (var consumable in pool)
            if (consumable != null) available.Add(consumable);

        return PickPreferring(available, exclude);
    }

    /// <summary>
    /// One draw from `available`, skipping anything in `exclude` unless that
    /// would leave nothing — then duplicates are allowed. Either way it is ONE
    /// draw (or none, on an empty pool), so the stream position never depends on
    /// what was excluded.
    /// </summary>
    private T PickPreferring<T>(List<T> available, ICollection<T> exclude) where T : class
    {
        if (available.Count == 0) return null;

        if (exclude != null && exclude.Count > 0)
        {
            var fresh = new List<T>(available.Count);
            foreach (var item in available)
                if (!exclude.Contains(item)) fresh.Add(item);
            if (fresh.Count > 0) available = fresh;
        }

        return available[rng.Range(0, available.Count)];
    }

    // ---- Prices and text --------------------------------------------------

    /// <summary>
    /// What the next reroll costs, or 0 when the mode has rerolls switched off.
    ///
    /// Compounds off the EXACT price and rounds up only at the very end, so the
    /// ceiling can't accumulate into a ladder steeper than the config asks for:
    /// $5 · $8 · $12 · $17 · $26 at 1.5x, rather than $5 · $8 · $12 · $18 · $27.
    ///
    /// NOT run through RunState.PriceOf, deliberately. PriceOf is still the only
    /// place a PERCENTAGE discount is applied — a reroll simply isn't discounted
    /// by one, so that Sense and Frugality and the voucher below can't compound
    /// into a free reroll. RunPerks.RerollDiscount is the different thing: a flat
    /// cut to the BASE, which shifts the whole ladder rather than one rung, and
    /// THIS IS THE ONE PLACE IT LANDS.
    /// </summary>
    /// <param name="rerolls">Rerolls already bought THIS VISIT.</param>
    public static int RerollPrice(RunState run, int rerolls)
    {
        if (run == null || run.Template == null) return 0;

        // The config's own number decides whether rerolls exist at all, and it
        // is read BEFORE the voucher: otherwise a run carrying the voucher into a
        // mode with rerolls switched off would resurrect them at $1.
        int basePrice = run.Template.rerollBasePrice;
        if (basePrice <= 0) return 0;

        // Floored at 1 for the reason RunState.PriceOf clamps its discount at
        // 90%: a shop that gives things away has stopped being a decision.
        basePrice = Mathf.Max(1, basePrice - run.Perks.RerollDiscount);

        double exact = basePrice *
            System.Math.Pow(Mathf.Max(1f, run.Template.rerollPriceGrowth), rerolls);

        // Rounded to six places BEFORE the ceiling. Math.Pow can land a hair
        // over a whole number, and a ceiling turns 9.000000000000002 into 10 —
        // an off-by-one on a price the player is charged, with nothing on screen
        // to suggest anything went wrong. Belt and braces at today's numbers
        // (1.5^n is exact in binary), but the growth factor is an Inspector field
        // and won't always be 1.5.
        double ceiling = System.Math.Ceiling(System.Math.Round(exact, 6));
        if (double.IsNaN(ceiling)) return basePrice;

        // Saturating, like every other number the player is charged: 1.5^n
        // passes int.MaxValue somewhere around the 52nd reroll, and a wrapped
        // price comes back NEGATIVE — which TrySpend refuses with no explanation
        // at all. Same argument as RunState.PriceOf and ScoreLimits. Infinity
        // satisfies the comparison, so it's covered.
        return ceiling >= int.MaxValue ? int.MaxValue : (int)ceiling;
    }

    /// <summary>
    /// "$22" normally, "<s>$22</s> $18" when a checkout is taking something off —
    /// the discount is only believable if the player can see what it used to cost.
    /// </summary>
    public static string PriceText(int listPrice, int price) =>
        price == listPrice ? $"${price}" : $"<alpha=#60><s>${listPrice}</s><alpha=#FF> ${price}";

    /// <summary>
    /// A row that costs nothing is almost always an asset nobody priced: `price`
    /// defaults to 0 on every sellable asset, and the seeders only fill a 0 for
    /// the assets they know about — so a checkout or bookmark authored by hand
    /// and dropped into a mode's pool is FREE, and TrySpend(0) succeeds without
    /// complaint. Checked once per stocking rather than per redraw.
    /// </summary>
    public static void WarnAboutFreeRows(IEnumerable<ShopOffer> offers, Object context)
    {
        foreach (var offer in offers)
            if (offer != null && offer.HasStock && offer.ListPrice <= 0)
                Debug.LogWarning($"Shop row '{offer.Title}' costs nothing — its asset's " +
                                 "price is still 0. Set one, or run the matching " +
                                 "Word Crush > Create ... Assets to seed the default.", context);
    }

    /// <summary>
    /// What the run is carrying, on one line — the things with no picture of
    /// their own. Bookmarks used to share it; they have cards now.
    /// </summary>
    public static string OwnedLine(RunState run)
    {
        // Bookmarks are deliberately NOT here any more: BookmarkRowWidget draws
        // them as cards a few pixels above this line, in the order they score.
        // A second copy as text could only ever be redundant or wrong.
        var names = new List<string>();
        foreach (var checkout in run.Checkouts) names.Add(checkout.Title.ToUpperInvariant());

        // The bag size rides along because it is the ONLY thing buying a tile
        // changes that the player can see. Everything else on this shelf hands
        // over something with a name; a tile goes into a bag of a hundred and
        // then waits for a round to deal it, so without this the purchase looks
        // like it did nothing.
        string bag = $"BAG {run.ActiveTileCount}";

        // Items, for the same reason the bag count is here: you cannot see them
        // from the shop, and "ITEMS 2/2" is what explains the consumable row's
        // buy button reading ITEMS FULL. Shown only when carrying something —
        // an empty count would be one more number to read on every visit.
        string items = run.Consumables.Count == 0
            ? ""
            : run.Template.maxConsumables > 0
                ? $"      ITEMS {run.Consumables.Count}/{run.Template.maxConsumables}"
                : $"      ITEMS {run.Consumables.Count}";

        return names.Count == 0
            ? bag + items
            : $"OWNED   {string.Join("  ·  ", names)}      {bag}{items}";
    }
}
