using UnityEngine;

/// <summary>
/// Drop it on a tile and it selects the highest-scoring word that STARTS on that
/// tile, one tile at a time, ready to PLAY. Tap anywhere else on the board and
/// the word is deselected. The Cheat Sheet (his call, 2026-10-09).
///
/// All the work is GameSession.HintBestWordFrom: WordFinder lists every word
/// from the tile, the session keeps the best the round allows, and
/// ChainController.PlayHint selects it. Refused — and so not spent — when no
/// word starts on that tile; the tile flashes red to say so.
///
/// It changes nothing: no tile moves, nothing is drawn from the bag or from
/// Rng. What it costs is that the selection you had is cleared when you drop it,
/// which every item does.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_CheatSheet", menuName = "Word Crush/Consumable/Best Word")]
public class BestWordConsumable : Consumable
{
    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    public override string PowerText =>
        "Drop it on a tile to select the highest-scoring word that starts there. " +
        "Tap anywhere else on the board to let it go.";

    public override bool Use(ConsumableUse use) =>
        use != null && use.Session != null && use.Session.HintBestWordFrom(use.Tile);
}
