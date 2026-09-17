using UnityEngine;

/// <summary>
/// LATE FEES AND OTHER STORIES. Every cleared round pays a little more.
///
/// A percentage rather than a flat sum, so it keeps up with a run that's earning
/// well instead of fading into the background by round 8 — but it's also the
/// first thing the $200 payout cap eats, since the cap is applied last.
/// </summary>
[CreateAssetMenu(fileName = "Checkout_PayoutBonus", menuName = "Word Crush/Checkout/Payout Bonus")]
public class PayoutBonusCheckout : Checkout
{
    [Tooltip("Percent added to what a cleared round pays. Applied after the " +
             "librarian's multiplier and before the round payout cap.")]
    [Min(0)] public int bonusPercent = 10;

    public override string PowerText =>
        $"Cleared rounds pay {bonusPercent}% more.";

    public override void Apply(RunPerks perks) => perks.PayoutBonusPercent += bonusPercent;
}
