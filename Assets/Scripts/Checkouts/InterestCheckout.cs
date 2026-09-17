using UnityEngine;

/// <summary>
/// GREAT EXPECTATIONS. Money you hold on to earns money.
///
/// The only thing in the game that makes NOT spending a decision — which is
/// exactly why the Encyclopedia has been calling interest the obvious next
/// economic hook. Buying it is a bet that you can afford to sit on a balance for
/// several rounds, in a shop that is otherwise always worth emptying.
///
/// The RATE is here; the CEILING is a config number (maxInterest), because a cap
/// belongs with maxRoundPayout and must bound every source of interest at once
/// rather than one asset at a time.
/// </summary>
[CreateAssetMenu(fileName = "Checkout_Interest", menuName = "Word Crush/Checkout/Interest")]
public class InterestCheckout : Checkout
{
    [Tooltip("Dollars paid at the end of a cleared round for every $10 the run " +
             "is holding when it clears. Capped by the mode's Max Interest.")]
    [Min(0)] public int perTenHeld = 1;

    public override string PowerText =>
        $"Earn ${perTenHeld} per $10 you're holding when a round is cleared.";

    public override void Apply(RunPerks perks) => perks.InterestPer10 += perTenHeld;
}
