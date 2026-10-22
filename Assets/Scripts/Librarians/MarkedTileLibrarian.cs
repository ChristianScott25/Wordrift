using UnityEngine;

/// <summary>
/// Marks a handful of board tiles, and every word has to START on one — or,
/// with atEnd, END on one. One class for both, the same way the two line bombs
/// are one LineClearConsumable with a flag.
///
/// A marked tile in the MIDDLE of a word is fine; only the first (or last) tile
/// is checked. The marking itself is round state and lives on the mode's
/// TileMarker: it tries for marked tiles with different letters, and whenever
/// marked tiles leave the board — played, discarded, bombed — it marks new
/// ones to bring the count back up. Marked tiles CAN be discarded (his call,
/// 2026-10-22): it costs discards and the replacement is random, and a mark you
/// could never get rid of is how a round freezes with moves left.
///
/// The count shrinks as the run goes on: `count`, one fewer every
/// `stepEveryRounds`, never below `minCount`.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Prefacer",
                 menuName = "Word Crush/Librarian/Marked Tiles")]
public class MarkedTileLibrarian : Librarian
{
    [Tooltip("Off: words must START on a marked tile. On: they must END on one.")]
    public bool atEnd;

    [Tooltip("How many tiles are marked early in the run.")]
    [Min(1)] public int count = 5;

    [Tooltip("The fewest it ever drops to.")]
    [Min(1)] public int minCount = 3;

    [Tooltip("One fewer marked tile every this many rounds. 0 = never changes.")]
    [Min(0)] public int stepEveryRounds = 6;

    public override string PowerText =>
        $"Words must {(atEnd ? "end" : "start")} on a marked tile.";

    public int CountFor(int round)
    {
        int steps = stepEveryRounds > 0 ? Mathf.Max(0, round - 1) / stepEveryRounds : 0;
        return Mathf.Max(Mathf.Min(minCount, count), count - steps);
    }

    public override void Apply(RoundRules rules)
    {
        rules.MarkKind = TileMark.Required;
        rules.MarkCount = CountFor(rules.Round);
    }

    public override string Refuse(WordCheck check)
    {
        var tiles = check.Tiles;
        if (tiles == null || tiles.Count == 0) return null;

        var tile = atEnd ? tiles[tiles.Count - 1] : tiles[0];
        if (tile != null && tile.Mark == TileMark.Required) return null;

        return atEnd ? "End on a marked tile." : "Start on a marked tile.";
    }
}
