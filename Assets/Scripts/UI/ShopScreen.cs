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

    [Tooltip("The buy rows, in order. A row with nothing wired is skipped, so the " +
             "screen still works while the scene is being built up.")]
    [SerializeField] private OfferRow[] rows;

    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string menuSceneName = "Main Menu";

    private readonly List<Offer> offers = new();
    private RunState run;

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

        if (headline != null) headline.text = $"ROUND {run.Round} CLEARED";
        if (detail != null) detail.text = $"NEXT TARGET   {run.Template.TargetForRound(run.Round + 1)}";

        StockShelves();
        Refresh();
    }

    // ------------------------------------------------------------------------
    // TEMPORARY STOCK — this is a placeholder shop, not the design.
    //
    // The real shop's stock has to change constantly, look different, and sell
    // more than modifiers (bookmarks, tiles, sack upgrades). This version puts
    // the mode's four multipliers on the shelf at a fixed price ladder so the
    // whole economy can be played end to end: clear a round, earn, spend, and
    // watch the purchase persist into later rounds.
    //
    // Also temporary: an upgrade lands on a RANDOM tile from your sack. The
    // roll happens up front so the button can show what you're buying — you
    // never choose it. Choosing needs a sack picker, which belongs to the real
    // shop.
    // ------------------------------------------------------------------------
    private class Offer
    {
        public TileModifier Modifier;
        public TileSpec Target;    // the sack tile this purchase would gild
        public int TimesBought;    // this visit only — that's what escalates the price
        public float Growth = 1.5f;

        public int Price => Mathf.RoundToInt(Modifier.price * Mathf.Pow(Growth, TimesBought));
    }

    private void StockShelves()
    {
        offers.Clear();
        var pool = run.Template.tileModifiers;
        if (pool == null) return;

        // Never stock more than there are rows to draw it in.
        int shelfSpace = rows == null ? 0 : rows.Length;

        foreach (var modifier in pool)
        {
            if (modifier == null || offers.Count >= shelfSpace) continue;
            offers.Add(new Offer
            {
                Modifier = modifier,
                Target = RollTarget(),
                Growth = Mathf.Max(1f, run.Template.repeatPriceGrowth),
            });
        }
    }

    /// <summary>A random tile out of the run's sack — what the next purchase would land on.</summary>
    private TileSpec RollTarget() =>
        run.Sack.Count == 0 ? null : run.Sack[UnityEngine.Random.Range(0, run.Sack.Count)];

    /// <summary>Wired to a buy button, one per row index.</summary>
    public void Buy(int index)
    {
        if (run == null || index < 0 || index >= offers.Count) return;

        var offer = offers[index];
        if (offer.Target == null) return;

        // The button is disabled when you can't afford it; this is the real
        // guard, since nothing else may take money.
        if (!run.TrySpend(offer.Price)) return;

        offer.Target.AddModifier(offer.Modifier);
        offer.TimesBought++;
        offer.Target = RollTarget();   // the next one is a different tile

        Refresh();
    }

    private void Refresh()
    {
        if (moneyLabel != null)
        {
            moneyLabel.text = run.LastPayout > 0
                ? $"${run.Money}   <size=60%>+${run.LastPayout} EARNED</size>"
                : $"${run.Money}";
        }

        if (rows == null) return;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null) continue;

            bool stocked = i < offers.Count && offers[i].Target != null;
            if (row.button != null) row.button.gameObject.SetActive(stocked);
            if (!stocked) continue;

            var offer = offers[i];
            bool affordable = run.CanAfford(offer.Price);

            if (row.label != null)
            {
                row.label.text =
                    $"{offer.Modifier.badgeLabel} → {offer.Target.letters.ToUpperInvariant()}     ${offer.Price}";
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
