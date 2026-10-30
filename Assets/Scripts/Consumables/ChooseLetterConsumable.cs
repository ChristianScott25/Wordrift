using UnityEngine;

/// <summary>
/// 🚧 Correction Fluid (2026-10-29): turns the tile it's dropped on into a
/// letter the player picks, for the round. The ONE item that AsksForLetter:
/// GameSession.UseConsumable opens the letter picker first and only calls Use
/// once a letter is chosen — closing the picker spends nothing.
///
/// Board.Rewrite does the work, the same as Ink Blot and Vowel Shower, so the
/// tile plays as a plain catalog letter (no badges) and goes back to itself
/// when it leaves the board. Refused when the tile already is that letter.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_CorrectionFluid", menuName = "Word Crush/Consumable/Choose Letter")]
public class ChooseLetterConsumable : Consumable
{
    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    public override bool AsksForLetter => true;

    public override string PowerText =>
        "Drop it on a tile and pick a letter: the tile becomes that letter for this round. " +
        "It loses its badges. Your bag isn't changed.";

    public override bool Use(ConsumableUse use)
    {
        if (use?.Board == null || use.Tile == null || string.IsNullOrEmpty(use.Letter)) return false;
        if (use.Board.Rewrite(use.Tile, use.Letter)) return true;

        // Picked the letter it already is: say so, rather than the picker just
        // closing with nothing happening.
        use.Tile.FlashInvalid();
        return false;
    }
}
