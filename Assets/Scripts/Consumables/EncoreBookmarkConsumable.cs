using UnityEngine;

/// <summary>
/// 🚧 Ghostwriter (2026-10-29): on the next word, your LEFTMOST bookmark fires a
/// second time.
///
/// Armed like the Doubler — it's an IScoreRule the round holds until a word is
/// accepted — and its Score just calls that bookmark's Apply again, after every
/// bookmark and the librarian have had their turn. Safe under speculative
/// scoring for the same reason the Doubler is: a bookmark only writes into the
/// context it's handed. It reads the bookmark at SCORE time, so reordering the
/// row after arming changes which one gets the encore.
///
/// Refused when you own no bookmarks.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Ghostwriter", menuName = "Word Crush/Consumable/Encore Bookmark")]
public class EncoreBookmarkConsumable : Consumable, IScoreRule
{
    public override bool ChangesBoard => false;

    public override string PowerText =>
        "On the next word you play, your leftmost bookmark fires twice.";

    public override bool Use(ConsumableUse use)
    {
        if (use?.Run == null || use.Session == null || use.Run.Bookmarks.Count == 0) return false;
        return use.Session.ArmForNextWord(this);
    }

    public void Score(ScoringContext ctx)
    {
        var run = RunState.Current;
        if (ctx == null || run == null || run.Bookmarks.Count == 0) return;
        run.Bookmarks[0]?.Apply(ctx);
    }
}
