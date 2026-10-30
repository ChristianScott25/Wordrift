using System.Collections.Generic;
using UnityEngine;

// The things a shop sells, one class per KIND of stock. Moved out of ShopScreen
// on 2026-10-28 so the 🚧 Bookshop (BookshopScreen) sells exactly the same
// things the same way — two copies of "what a bookmark row does when you buy
// it" would drift apart the first time one was fixed. Nothing here knows which
// shop it's on.

/// <summary>
/// One thing on the shelf. Subclassed rather than switched on, so the screens
/// that draw rows never learn what kinds of stock exist.
/// </summary>
public abstract class ShopOffer
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

    /// <summary>How rare the thing on this row is — null for nothing to show.</summary>
    public virtual Rarity? Rarity => null;

    /// <summary>Does the thing on this row have a downside? See Rarity.cs.</summary>
    public virtual bool Cursed => false;

    /// <summary>
    /// False when this slot has nothing to sell at all — every bookmark
    /// owned, every tile full. The row hides rather than greying out: there
    /// is nothing to grey.
    /// </summary>
    public abstract bool HasStock { get; }

    /// <summary>Can still be bought right now.</summary>
    public bool Available => !Sold && HasStock;

    /// <summary>
    /// Why this can't be taken right now, in the player's own words — or null
    /// when it can. The buy button shows it instead of BUY.
    ///
    /// Deliberately NOT the same question as HasStock. Out of stock means
    /// there is nothing to sell and the row hides, because there is nothing
    /// to read. Blocked means there IS something, and what it is and what it
    /// does are exactly what the player needs in order to decide whether to
    /// go and make room — so the row stays, and says why.
    ///
    /// It takes the run rather than caching an answer because the answer
    /// changes mid-visit: buying the bookmark in row 2 is what fills the run
    /// up and blocks row 3.
    /// </summary>
    public virtual string Blocked(RunState run) => null;

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
public class ModifierOffer : ShopOffer
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
    public override Rarity? Rarity => Modifier == null ? null : Modifier.rarity;

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
public class BookmarkOffer : ShopOffer
{
    public Bookmark Bookmark;

    public override bool HasStock => Bookmark != null;
    public override int ListPrice => Bookmark == null ? 0 : Bookmark.price;
    public override Rarity? Rarity => Bookmark == null ? null : Bookmark.rarity;
    public override bool Cursed => Bookmark != null && Bookmark.cursed;
    public override string Title =>
        Bookmark == null ? "" : Bookmark.displayName.ToUpperInvariant();
    public override string Description => Bookmark == null ? "" : Bookmark.description;

    /// <summary>
    /// ⚠️ The cap is answered HERE and not in HasStock, on purpose. HasStock
    /// decides whether the row exists, and buying the bookmark in row 2 is
    /// what fills the run — so gating stock on the cap would make row 3
    /// VANISH while still unsold, which is the one thing the shelf promises
    /// it never does. (It's the same failure RollTarget's `taken` list was
    /// written to avoid on the upgrade rows.) The row stays and says FULL.
    /// </summary>
    public override string Blocked(RunState run) =>
        run != null && run.BookmarksFull ? "BOOKMARKS FULL" : null;

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
public class CheckoutOffer : ShopOffer
{
    public Checkout Checkout;

    public override bool HasStock => Checkout != null;
    public override int ListPrice => Checkout == null ? 0 : Checkout.price;
    public override Rarity? Rarity => Checkout == null ? null : Checkout.rarity;
    public override bool Cursed => Checkout != null && Checkout.cursed;
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
/// A one-shot item to carry into a round.
///
/// Like the tile row and unlike every other kind, it NEVER RUNS OUT: an item
/// you spend is not an item you own, so the same one may be offered while
/// you're already carrying it.
///
/// ⚠️ THE CARRY CAP IS A `Blocked`, NOT A `HasStock`. Out of stock means
/// there is nothing to sell and the row hides; full means there IS something
/// and you can't take it, which is exactly when knowing what it does matters
/// most. Gating HasStock on the cap would make the row vanish WHILE STILL
/// UNSOLD the moment you bought your second item — the one thing the shelf
/// promises it never does. Same rule BookmarkOffer follows.
/// </summary>
public class ConsumableOffer : ShopOffer
{
    public Consumable Consumable;

    public override bool HasStock => Consumable != null;
    public override int ListPrice => Consumable == null ? 0 : Consumable.price;
    public override Rarity? Rarity => Consumable == null ? null : Consumable.rarity;
    public override bool Cursed => Consumable != null && Consumable.cursed;
    public override string Title => Consumable == null ? "" : Consumable.Title.ToUpperInvariant();
    public override string Description =>
        Consumable == null
            ? ""
            : $"{Consumable.Power}\n\nCarried into a round and gone once you use it.";

    public override string Blocked(RunState run) =>
        run != null && run.ConsumablesFull ? "ITEMS FULL" : null;

    public override void Deliver(RunState run)
    {
        // Reachable only if the cap check above and this one disagree, which
        // they can't — but the money is already gone by the time this runs,
        // so it shouts rather than shrugging. See BookmarkOffer.Deliver.
        // Consumable?.name, not Consumable.name: AddConsumable(null) returns
        // false too, and an error path that throws reports nothing at all.
        if (!run.AddConsumable(Consumable))
            Debug.LogError($"Bought consumable '{Consumable?.name}' the run can't hold — " +
                           "the player has been charged and given nothing.");
    }

    public override ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex) => new ShopOfferData
    {
        kind = ShopOfferData.Consumable,
        assetName = Consumable == null ? "" : Consumable.name,
        sold = Sold,
    };
}

/// <summary>
/// A brand-new tile for the bag — a multi-letter one, which is the only kind
/// anything sells. Unlike every other row it can't run out and doesn't care
/// what the run already owns: two CH tiles are twice as likely to turn up as
/// one, which is the whole reason to buy a second.
///
/// It holds the catalog ROW rather than a TileSpec so that nothing is built
/// until the purchase goes through — and so the row's price and score come
/// from the asset every time they're read, rather than from a copy taken when
/// the shop opened.
/// </summary>
public class TileOffer : ShopOffer
{
    public LetterSet.Entry Entry;

    public override bool HasStock => Entry != null;
    public override int ListPrice => Entry == null ? 0 : Entry.price;
    public override Rarity? Rarity => LetterSet.RarityOf(Entry);

    public override string Title =>
        Entry == null ? ""
        : Entry.IsWild ? $"NEW TILE   WILD {TileSpec.WildSpelling}"
        : $"NEW TILE   {Entry.letter.ToUpperInvariant()}";

    /// <summary>
    /// A choice row's options read out as words — "A, E or I". Built from the
    /// row rather than written down, so a group retuned in the catalog can't
    /// be described here by the letters it used to have.
    /// </summary>
    private string OptionList
    {
        get
        {
            if (Entry == null || string.IsNullOrEmpty(Entry.letter)) return "";

            var options = Entry.letter.ToUpperInvariant().Split(TileSpec.ChoiceSeparator);
            if (options.Length == 1) return options[0];

            return string.Join(", ", options, 0, options.Length - 1) +
                   " or " + options[options.Length - 1];
        }
    }

    /// <summary>
    /// Written from the row's own numbers, the same discipline as
    /// Checkout.PowerText — a hand-authored second copy is how the shop ends
    /// up describing a tile that was retuned last week.
    /// </summary>
    public override string Description
    {
        get
        {
            if (Entry == null) return "";

            if (Entry.IsWild)
                return "A wild tile, worth no points at all, added to your bag " +
                       "for the rest of the run.\n\n" +
                       "It becomes whichever letter makes the best word it can " +
                       "— counting the round's rules, so it will dodge a banned " +
                       "letter if any other letter fits. It can't be upgraded.";

            // Deliberately says what it ISN'T. The pairs are the other thing
            // this row sells, and "does it lengthen my word?" is the one
            // question that tells the two apart.
            if (Entry.IsChoice)
                return $"A {Entry.letter.ToUpperInvariant()} tile, worth " +
                       $"{Entry.points} points, added to your bag for the " +
                       "rest of the run.\n\n" +
                       $"It becomes {OptionList} — whichever of them makes " +
                       "the best word it can, counting the round's rules. It " +
                       "plays as ONE letter, so it won't lengthen a word the " +
                       "way a letter pair does.";

            string spelling = Entry.letter.ToUpperInvariant();
            string howMany = Entry.letter.Length == 2 ? "both letters" : "every letter";
            return $"A new {spelling} tile, worth {Entry.points} points, added to " +
                   "your bag for the rest of the run.\n\n" +
                   $"It spells {howMany} from a single square, and the word counts " +
                   "them all — so it reaches a longer word off fewer tiles.";
        }
    }

    public override void Deliver(RunState run)
    {
        // The spec is built HERE, not when the shelf was stocked: one made
        // up front and then not bought is a tile nobody owns, and — worse —
        // buying twice would hand out the SAME spec object, which is two
        // board tiles sharing one identity, so gilding one would gild both.
        //
        // Null-guarded rather than assumed. ConfirmBuy checks Available,
        // which checks HasStock, so it can't be null — but Deliver runs
        // AFTER the money is gone, and an exception here would leave the
        // player charged with the shelf half-updated.
        if (Entry == null || !run.AddTile(LetterSet.CreateSpec(Entry)))
            Debug.LogError($"Bought a '{(Entry == null ? "?" : Entry.letter)}' tile that " +
                           "couldn't be added to the bag — the player has been charged " +
                           "and given nothing.");
    }

    public override ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex) => new ShopOfferData
    {
        kind = ShopOfferData.Tile,

        // The catalog KEY, not an asset file name — a catalog row isn't an
        // asset of its own. It resolves the same way everything else does:
        // against the mode's own letter set, so there's nothing to keep in
        // sync and a renamed row invalidates the save exactly like a renamed
        // asset would.
        assetName = Entry == null ? "" : Entry.letter,
        sold = Sold,
    };
}
