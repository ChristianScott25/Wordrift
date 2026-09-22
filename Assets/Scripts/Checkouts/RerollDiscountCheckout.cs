using UnityEngine;

/// <summary>
/// A TALE OF TWO SHELVES. Rerolling the shop starts cheaper, every visit.
///
/// The cut lands on the BASE price rather than on one reroll, so the growth
/// factor multiplies a smaller number and the whole ladder comes down with it —
/// $5·$8·$12·$17 becomes $3·$5·$7·$11. That makes it the second compounding
/// purchase in the roster after Sense and Frugality, and the only one that pays
/// nothing at all to a player who never rerolls.
///
/// Floored at $1 where it's read (ShopScreen.RerollPrice), for the same reason
/// RunState.PriceOf clamps its discount at 90%: a shop that gives things away
/// has stopped being a decision.
/// </summary>
[CreateAssetMenu(fileName = "Checkout_RerollDiscount", menuName = "Word Crush/Checkout/Reroll Discount")]
public class RerollDiscountCheckout : Checkout
{
    [Tooltip("Dollars off the first reroll of every shop visit. A run that " +
             "rerolls often saves more than this number, because the ladder " +
             "compounds off the reduced base rather than the full one.")]
    [Min(0)] public int discountOff = 2;

    public override string PowerText =>
        $"Rerolling the shelf starts ${discountOff} cheaper, every shop.";

    public override void Apply(RunPerks perks) => perks.RerollDiscount += discountOff;
}
