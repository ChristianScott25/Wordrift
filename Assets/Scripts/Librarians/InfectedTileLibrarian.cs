using UnityEngine;

/// <summary>
/// Infects a handful of board tiles. An infected tile takes away what it would
/// have added: its letter points come OFF the score, its letters come OFF the
/// word's length, and its badges do nothing (ScoreCalculator.WalkTiles and
/// ChainController.ScoringLength — the scoring half lives in Core, keyed off
/// Tile.Mark, so the librarian itself only asks for the marks).
///
/// ⚠️ THE SHORTER LENGTH COUNTS EVERYWHERE (his call, 2026-10-22): the length
/// points and multiplier, the length bookmarks (ScoringContext.Length), AND the
/// minimum word length — which is what Refuse below enforces, so a three-letter
/// word through an infected tile can't be played at all.
///
/// Infected tiles CAN be discarded — that's the honest way out, and it costs
/// discards — and the mode's TileMarker infects a new one to keep the count up.
/// The count GROWS as the run goes on: `count`, one more every
/// `stepEveryRounds`, never past `maxCount` (a flooded board is unplayable).
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Mildewed",
                 menuName = "Word Crush/Librarian/Infected Tiles")]
public class InfectedTileLibrarian : Librarian
{
    [Tooltip("How many tiles are infected early in the run.")]
    [Min(1)] public int count = 3;

    [Tooltip("The most it ever grows to. 6 is about a quarter of a 5x5 board.")]
    [Min(1)] public int maxCount = 6;

    [Tooltip("One more infected tile every this many rounds. 0 = never changes.")]
    [Min(0)] public int stepEveryRounds = 6;

    public override string PowerText =>
        "Infected tiles take away their points and letters, and their badges don't work.";

    public int CountFor(int round)
    {
        int steps = stepEveryRounds > 0 ? Mathf.Max(0, round - 1) / stepEveryRounds : 0;
        return Mathf.Min(Mathf.Max(maxCount, count), count + steps);
    }

    public override void Apply(RoundRules rules)
    {
        rules.MarkKind = TileMark.Infected;
        rules.MarkCount = CountFor(rules.Round);
    }

    public override string Refuse(WordCheck check)
    {
        if (check.Tiles == null || check.MinWordLength <= 0) return null;
        return ChainController.ScoringLength(check.Tiles) < check.MinWordLength
            ? "Infected letters make this too short."
            : null;
    }
}
