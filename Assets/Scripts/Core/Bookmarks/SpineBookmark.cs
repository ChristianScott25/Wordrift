using UnityEngine;

/// <summary>
/// Multiplies the MULT when no letter appears twice in the word.
///
/// The same rule The Abridged enforces for a round, sold back to you as a power —
/// so the boss that once cost you a round teaches you what this is worth.
///
/// Multiplicative because the condition is a real constraint on a board that
/// keeps dealing you doubles, and because it wants to land after an additive
/// +Mult rather than before it.
/// </summary>
[CreateAssetMenu(fileName = "Spine", menuName = "Word Crush/Bookmark/Spine")]
public class SpineBookmark : Bookmark
{
    [Min(1f)] public float multiplier = 2f;

    public override void OnWordScored(ScoringContext ctx)
    {
        string word = ctx.Word;
        if (string.IsNullOrEmpty(word)) return;

        // A 32-bit set over a-z: one shift and one test per letter, no allocation
        // on a method that runs for every word of every round.
        int seen = 0;
        foreach (char c in word)
        {
            // Anything outside a-z can't be tracked in the mask, so it's skipped
            // rather than folded into the wrong bit — C# masks the shift count,
            // so an out-of-range letter would silently alias onto a real one.
            // Nothing submits one today; the word arrives lowercased.
            if (c < 'a' || c > 'z') continue;

            int bit = 1 << (c - 'a');
            if ((seen & bit) != 0) return;
            seen |= bit;
        }

        ctx.MultiplyMult(multiplier, displayName);
    }
}
