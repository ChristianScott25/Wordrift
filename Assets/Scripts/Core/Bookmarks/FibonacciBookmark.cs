using UnityEngine;

/// <summary>
/// A big x MULT when the round's last few words follow a set run of lengths,
/// ending on this one — 3 letters, then 5, then 8.
///
/// Lengths are LETTERS in the spelled word (a CH tile is two). The words have
/// to be back to back: anything played in between breaks the run, and the
/// run can't carry over from one round to the next.
/// </summary>
[CreateAssetMenu(fileName = "GoldenRatio", menuName = "Word Crush/Bookmark/Fibonacci")]
public class FibonacciBookmark : Bookmark
{
    [Tooltip("The lengths, oldest first. The LAST one is the word being scored.")]
    public int[] lengths = { 3, 5, 8 };

    [Min(1f)] public float multiplier = 5f;

    public override void OnWordScored(ScoringContext ctx)
    {
        if (lengths == null || lengths.Length == 0 || string.IsNullOrEmpty(ctx.Word)) return;

        int last = lengths.Length - 1;
        if (ctx.Word.Length != lengths[last]) return;

        for (int back = 1; back <= last; back++)
        {
            string earlier = ctx.WordBefore(back);
            if (earlier == null || earlier.Length != lengths[last - back]) return;
        }

        ctx.MultiplyMult(multiplier, displayName);
    }
}
