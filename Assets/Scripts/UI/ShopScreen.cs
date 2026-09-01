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
/// The STOCK below is temporary (see the banner further down); the money and
/// purchase plumbing around it is not.
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

    [Tooltip("Lists the bookmarks the run owns. Blank when it owns none.")]
    [SerializeField] private TMP_Text bookmarkLabel;

    [Tooltip("The buy rows, in order. A row with nothing wired is skipped, so the " +
             "screen still works while the scene is being built up.")]
    [SerializeField] private OfferRow[] rows;

    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "Main Menu";

    private readonly List<Offer> offers = new();
    private RunState run;

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

        rng = run.StreamFor(RunState.ShopStream);

        if (headline != null) headline.text = $"ROUND {run.Round} CLEARED";
        if (detail != null) detail.text = $"NEXT TARGET   {run.Template.TargetForRound(run.Round + 1)}";

        // A visit that was interrupted comes back as it was — the same shelf at
        // the same prices, minus whatever was bought. Restocking would re-roll
        // it, and a bookmark already bought would be back on offer.
        var saved = RunState.TakePendingShop();
        if (saved != null && saved.captured) RestockFrom(saved); else StockShelves();

        Refresh();
        Save();
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
    // TEMPORARY STOCK — this is a placeholder shop, not the design.
    //
    // The real shop's stock has to change constantly, look different, and sell
    // more than modifiers (bookmarks, tiles, tile-bag upgrades). This version puts
    // the mode's four multipliers on the shelf at a fixed price ladder so the
    // whole economy can be played end to end: clear a round, earn, spend, and
    // watch the purchase persist into later rounds.
    //
    // Also temporary: an upgrade lands on a RANDOM tile from your bag. The
    // roll happens up front so the button can show what you're buying — you
    // never choose it. Choosing needs a bag picker, which belongs to the real
    // shop.
    // ------------------------------------------------------------------------
    /// <summary>
    /// One thing on the shelf. Subclassed rather than switched on, so the row
    /// rendering below never learns what kinds of stock exist — which is the
    /// only part of this screen the real shop will keep.
    /// </summary>
    private abstract class Offer
    {
        public abstract string Label { get; }
        public abstract int Price { get; }

        /// <summary>False when there's nothing left to sell — the row hides itself.</summary>
        public virtual bool InStock => true;

        /// <summary>Applies the purchase to the run. Money has already been taken.</summary>
        public abstract void Deliver(RunState run);

        /// <summary>
        /// This row, written down. Stock is SAVED rather than re-derived from the
        /// seed: the shop's roll order changes whenever its code does, and a
        /// saved shelf must not depend on that.
        /// </summary>
        public abstract ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex);
    }

    /// <summary>A tile upgrade, landing on a random bag tile rolled up front.</summary>
    private class ModifierOffer : Offer
    {
        public TileModifier Modifier;
        public TileSpec Target;    // the bag tile this purchase would gild
        public int TimesBought;    // this visit only — that's what escalates the price
        public float Growth = 1.5f;
        public System.Func<TileSpec> RollTarget;

        public override bool InStock => Target != null;
        public override int Price => Mathf.RoundToInt(Modifier.price * Mathf.Pow(Growth, TimesBought));

        public override string Label =>
            $"{Modifier.badgeLabel} → {Target.letters.ToUpperInvariant()}     ${Price}";

        public override void Deliver(RunState run)
        {
            Target.AddModifier(Modifier);
            TimesBought++;
            Target = RollTarget();   // the next one lands on a different tile
        }

        public override ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex) => new ShopOfferData
        {
            kind = ShopOfferData.Modifier,
            assetName = Modifier == null ? "" : Modifier.name,
            targetTile = Target != null && tileIndex.TryGetValue(Target, out int i) ? i : -1,
            timesBought = TimesBought,
        };
    }

    /// <summary>
    /// A bookmark, one per visit, never one the run already owns. When there are
    /// none left unowned it simply goes out of stock and its row disappears.
    /// </summary>
    private class BookmarkOffer : Offer
    {
        public Bookmark Bookmark;
        public System.Func<Bookmark> RollBookmark;

        public override bool InStock => Bookmark != null;
        public override int Price => Bookmark.price;
        public override string Label => $"{Bookmark.displayName.ToUpperInvariant()}     ${Price}";

        public override void Deliver(RunState run)
        {
            run.AddBookmark(Bookmark);
            Bookmark = RollBookmark();   // may be null: that's "sold out", not an error
        }

        public override ShopOfferData Capture(Dictionary<TileSpec, int> tileIndex) => new ShopOfferData
        {
            kind = ShopOfferData.Bookmark,

            // Null here is the whole point: it means the run already owns every
            // bookmark, and the row must stay gone after a resume.
            assetName = Bookmark == null ? "" : Bookmark.name,
        };
    }

    private void StockShelves()
    {
        offers.Clear();

        // Never stock more than there are rows to draw it in.
        int shelfSpace = rows == null ? 0 : rows.Length;

        // Roll the bookmark FIRST, so a row is only held back for it when there
        // actually is one — otherwise owning them all would silently cost the
        // shop a tile-upgrade row as well.
        var bookmark = RollBookmark();
        int modifierSpace = shelfSpace - (bookmark != null ? 1 : 0);

        var pool = run.Template.tileModifiers;
        if (pool != null)
        {
            foreach (var modifier in pool)
            {
                if (modifier == null || offers.Count >= modifierSpace) break;
                offers.Add(new ModifierOffer
                {
                    Modifier = modifier,
                    Target = RollTarget(),
                    Growth = Mathf.Max(1f, run.Template.repeatPriceGrowth),
                    RollTarget = RollTarget,
                });
            }
        }

        // The bookmark takes the last row, and holds the same offer for the
        // whole visit.
        if (bookmark != null)
            offers.Add(new BookmarkOffer { Bookmark = bookmark, RollBookmark = RollBookmark });
    }

    /// <summary>
    /// Rebuilds the shelf exactly as it was saved, and winds the shop's stream
    /// forward past the rolls it had already made — a restored stream restarts
    /// from the seed, so without the skip the next re-roll would repeat one the
    /// player has already seen.
    ///
    /// An offer naming an asset that's since vanished is dropped rather than
    /// guessed at; a shelf one row short is a much smaller problem than a shop
    /// that throws.
    /// </summary>
    private void RestockFrom(ShopSnapshot saved)
    {
        offers.Clear();
        rng.Skip(saved.rngDraws);

        int shelfSpace = rows == null ? 0 : rows.Length;
        foreach (var entry in saved.offers)
        {
            if (offers.Count >= shelfSpace) break;

            if (entry.kind == ShopOfferData.Bookmark)
            {
                offers.Add(new BookmarkOffer
                {
                    // Deliberately allowed to be null — that's a sold-out row,
                    // which InStock reports and Refresh hides.
                    Bookmark = FindByName(run.Template.bookmarks, entry.assetName),
                    RollBookmark = RollBookmark,
                });
                continue;
            }

            var modifier = FindByName(run.Template.tileModifiers, entry.assetName);
            if (modifier == null) continue;

            offers.Add(new ModifierOffer
            {
                Modifier = modifier,
                Target = run.TileAt(entry.targetTile) ?? RollTarget(),
                TimesBought = entry.timesBought,
                Growth = Mathf.Max(1f, run.Template.repeatPriceGrowth),
                RollTarget = RollTarget,
            });
        }
    }

    private static T FindByName<T>(List<T> pool, string assetName) where T : UnityEngine.Object
    {
        if (pool == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (var asset in pool)
            if (asset != null && asset.name == assetName) return asset;
        return null;
    }

    /// <summary>A random tile out of the run's bag — what the next purchase would land on.</summary>
    private TileSpec RollTarget() =>
        run.TileBag.Count == 0 ? null : run.TileBag[rng.Range(0, run.TileBag.Count)];

    /// <summary>
    /// A random bookmark the run doesn't own yet, or null when it owns every one
    /// the mode offers. Null is a normal answer — the shop just has none today.
    /// </summary>
    private Bookmark RollBookmark()
    {
        var pool = run.Template.bookmarks;
        if (pool == null) return null;

        var available = new List<Bookmark>();
        foreach (var bookmark in pool)
            if (bookmark != null && !run.Owns(bookmark)) available.Add(bookmark);

        return available.Count == 0 ? null : available[rng.Range(0, available.Count)];
    }

    /// <summary>Wired to a buy button, one per row index.</summary>
    public void Buy(int index)
    {
        if (run == null || index < 0 || index >= offers.Count) return;

        var offer = offers[index];
        if (!offer.InStock) return;

        // The button is disabled when you can't afford it; this is the real
        // guard, since nothing else may take money.
        if (!run.TrySpend(offer.Price)) return;

        offer.Deliver(run);
        Refresh();
        Save();
    }

    private void Refresh()
    {
        if (moneyLabel != null)
        {
            moneyLabel.text = run.LastPayout > 0
                ? $"${run.Money}   <size=60%>+${run.LastPayout} EARNED</size>"
                : $"${run.Money}";
        }

        if (bookmarkLabel != null)
        {
            var names = new List<string>();
            foreach (var owned in run.Bookmarks) names.Add(owned.Name.ToUpperInvariant());
            bookmarkLabel.text = names.Count == 0 ? "" : $"BOOKMARKS   {string.Join("  ·  ", names)}";
        }

        if (rows == null) return;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null) continue;

            bool stocked = i < offers.Count && offers[i].InStock;
            if (row.button != null) row.button.gameObject.SetActive(stocked);
            if (!stocked) continue;

            var offer = offers[i];
            bool affordable = run.CanAfford(offer.Price);

            if (row.label != null)
            {
                row.label.text = offer.Label;
                row.label.alpha = affordable ? 1f : 0.4f;
            }
            if (row.button != null) row.button.interactable = affordable;
        }
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
