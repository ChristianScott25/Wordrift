using UnityEngine;

/// <summary>
/// 🚧 CURSED (2026-10-29): Spoiler. Finds the best word starting on the tile
/// it's dropped on — the Cheat Sheet's search — and PLAYS IT FOR YOU, at a
/// multiplier. You get no say in the word.
///
/// It arms itself as an IScoreRule (like the Doubler) and then lets
/// GameSession.PlayBestWordFrom select the word and press PLAY once the last
/// tile is in. Press the board mid-way and the hint stops and nothing is played;
/// the multiplier then waits for your next word like any armed item.
/// Refused when no word starts there.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Spoiler", menuName = "Word Crush/Consumable/Auto Play Best Word")]
public class AutoPlayBestWordConsumable : Consumable, IScoreRule
{
    [Min(1f)] public float multiplier = 2f;

    public override ConsumableTarget Targets => ConsumableTarget.BoardTile;

    public override string PowerText =>
        "Drop it on a tile: it picks the best word starting there and plays it for you, " +
        $"at x{ScoringContext.Trim(multiplier)} Mult. Touch the board while it's picking and " +
        "it won't play — the bonus then goes on your next word.";

    public override bool Use(ConsumableUse use) =>
        use?.Session != null && use.Tile != null && use.Session.PlayBestWordFrom(use.Tile, this);

    public void Score(ScoringContext ctx) => ctx?.MultiplyMult(multiplier, Title);
}
