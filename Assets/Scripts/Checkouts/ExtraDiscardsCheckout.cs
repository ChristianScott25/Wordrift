using UnityEngine;

/// <summary>
/// SECOND THOUGHTS. A bigger discard allowance, every round from now on.
///
/// It buys its way out of bad boards rather than into big words, which is the
/// half of the game money couldn't touch before.
///
/// ⚠️ The Redactor still beats it. That librarian sets the round's allowance with
/// Mathf.Min (see DiscardLimitLibrarian), so a no-discards round is still a
/// no-discards round no matter what you own — the boss beats the shop, which is
/// what a boss is for.
/// </summary>
[CreateAssetMenu(fileName = "Checkout_ExtraDiscards", menuName = "Word Crush/Checkout/Extra Discards")]
public class ExtraDiscardsCheckout : Checkout
{
    [Tooltip("Tiles added to every round's discard allowance, in TILES not uses — " +
             "the same unit RogueDemoModeConfig.discardsPerRound counts in.")]
    [Min(0)] public int extraDiscards = 2;

    public override string PowerText =>
        $"Discard {extraDiscards} more tiles every round.";

    public override void Apply(RunPerks perks) => perks.ExtraDiscards += extraDiscards;
}
