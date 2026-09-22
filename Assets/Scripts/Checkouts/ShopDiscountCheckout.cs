using UnityEngine;

/// <summary>
/// SENSE AND FRUGALITY. Everything in the shop costs less, for the rest of the run.
///
/// The one checkout that pays for itself rather than doing anything — which makes
/// buying it early a bet on how many more shops you'll see, and buying it late
/// worthless. That shape is the point: it compounds, which only the reroll
/// voucher also does.
///
/// Rounded UP where the discount doesn't divide evenly (see RunState.PriceOf),
/// so it never quite pays the full percentage on a cheap row.
///
/// ⚠️ It buys what is ON THE SHELF and nothing else. Rerolling the shelf pays
/// full price — ShopScreen.RerollPrice deliberately doesn't go through PriceOf —
/// so that a percentage off and the voucher's flat cut can't compound into a
/// free reroll. The wording below has to keep saying so.
/// </summary>
[CreateAssetMenu(fileName = "Checkout_ShopDiscount", menuName = "Word Crush/Checkout/Shop Discount")]
public class ShopDiscountCheckout : Checkout
{
    [Tooltip("Percent off every price in the shop. Stacks additively with any " +
             "other discount, and the total is clamped when it's read so a stack " +
             "can never make the shop free.")]
    [Range(0, 90)] public int discountPercent = 20;

    public override string PowerText =>
        $"Everything on the shelf costs {discountPercent}% less, rounded up. " +
        "Rerolls pay full price.";

    public override void Apply(RunPerks perks) => perks.ShopDiscountPercent += discountPercent;
}
