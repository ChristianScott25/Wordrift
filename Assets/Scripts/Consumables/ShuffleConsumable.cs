using UnityEngine;

/// <summary>
/// The board is shuffled: the tiles swap into each other's places. Nothing is
/// added, nothing is removed — the same letters, somewhere else.
///
/// Some tiles land where they started; that is what a random rearrangement is.
/// What can't happen is ALL of them doing so — Board.Permutation rules the
/// do-nothing arrangement out, because this is a paid item.
///
/// The escape hatch for a board you can't read: you keep your tiles and your
/// bag, and pay only what the item cost. Discarding throws letters away; this
/// doesn't, which is why the two can sit side by side.
///
/// All of the work is Board.Shuffle. It lives there because the board's tile map
/// is private and has to stay that way — and because "shuffle one column" or
/// "shuffle these five" belongs next to it rather than in here.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Shuffle", menuName = "Word Crush/Consumable/Shuffle")]
public class ShuffleConsumable : Consumable
{
    public override string PowerText =>
        "Shuffles the board. The tiles swap into new places — nothing is added, " +
        "nothing is removed.";

    /// <summary>
    /// False whenever the board refuses — mid-fall, or with fewer than two tiles
    /// on it. The item is then NOT spent, which is the point of the bool.
    /// </summary>
    public override bool Use(ConsumableUse use) =>
        use != null && use.Board != null && use.Board.Shuffle(use.Rng);
}
