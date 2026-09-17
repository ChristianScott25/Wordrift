using UnityEngine;

/// <summary>
/// ONE MORE CHAPTER. An extra word every round, for the rest of the run.
///
/// The strongest shape of perk in a move-limited game, and priced for it: a move
/// is both another chance at the target and — if you don't need it — another
/// dollar at the end of the round.
/// </summary>
[CreateAssetMenu(fileName = "Checkout_ExtraMoves", menuName = "Word Crush/Checkout/Extra Moves")]
public class ExtraMovesCheckout : Checkout
{
    [Tooltip("Words added to every round's move allowance.")]
    [Min(0)] public int extraMoves = 1;

    public override string PowerText =>
        extraMoves == 1 ? "One more move every round."
                        : $"{extraMoves} more moves every round.";

    public override void Apply(RunPerks perks) => perks.ExtraMoves += extraMoves;
}
