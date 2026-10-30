using UnityEngine;

/// <summary>
/// 🚧 CURSED (2026-10-29): Plagiarism. Pays money, and takes one of your
/// bookmarks away at random.
///
/// The bookmark is drawn from the round's item stream (ConsumableUse.Rng), whose
/// position is saved, so quitting and continuing can't re-roll which one goes.
/// Refused — before any draw — when you own no bookmarks, or it would be free
/// money.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Plagiarism", menuName = "Word Crush/Consumable/Trade Bookmark")]
public class TradeBookmarkConsumable : Consumable
{
    [Min(0)] public int money = 15;

    public override bool ChangesBoard => false;

    public override string PowerText =>
        $"Get ${money} right now, but lose one of your bookmarks at random.";

    public override bool Use(ConsumableUse use)
    {
        var run = use?.Run;
        if (run == null || use.Rng == null || run.Bookmarks.Count == 0) return false;

        var lost = run.Bookmarks[use.Rng.Range(0, run.Bookmarks.Count)];
        if (!run.LoseBookmark(lost)) return false;
        run.CreditFromItem(money);
        return true;
    }
}
