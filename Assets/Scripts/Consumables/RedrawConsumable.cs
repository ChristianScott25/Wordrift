using UnityEngine;

/// <summary>
/// Every tile on the board goes back in the bag, then a whole new board is
/// dealt from it. Return first, then draw (his call, 2026-10-08), so a few of
/// the same tiles may come straight back — and it always works, however low
/// the bag is.
///
/// The old board spins away in rings spreading out from where it was dropped
/// (ConsumableUse.Cell), then the new one is dealt a tile at a time.
///
/// Where Shuffle keeps your letters and moves them, this swaps them out. All of
/// the work is Board.Redraw.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_SecondEdition", menuName = "Word Crush/Consumable/Redraw")]
public class RedrawConsumable : Consumable
{
    public override string PowerText =>
        "Puts every tile on the board back in the bag and deals a fresh board.";

    /// <summary>False mid-fall or on an empty board, and the item is then kept.</summary>
    public override bool Use(ConsumableUse use) =>
        use != null && use.Board != null && use.Board.Redraw(rippleFrom: use.Cell);
}
