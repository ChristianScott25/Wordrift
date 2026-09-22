using UnityEngine;

/// <summary>
/// Adds to the MULT for a word of exactly the shortest length the mode allows.
///
/// The "go wide" half of a build fork, and the one that pulls against the rest of
/// the game: everything else pays you for length, so this is the only thing that
/// makes the cheap three-letter word a decision rather than a fallback. It also
/// compounds with an economy that already pays for unused moves.
///
/// Additive, and the mirror of Marginalia — owning both is close to owning
/// neither, which is the point of putting them in the same pool.
/// </summary>
[CreateAssetMenu(fileName = "Shorthand", menuName = "Word Crush/Bookmark/Shorthand")]
public class ShorthandBookmark : Bookmark
{
    [Min(0)] public int multBonus = 4;

    public override void OnWordScored(ScoringContext ctx)
    {
        // LETTERS, not ctx.Tiles.Count. A "ch" tile makes those different
        // numbers, and counting tiles would pay this out on CH+A+T — a
        // four-letter word — which is the exact opposite of what it rewards.
        if (string.IsNullOrEmpty(ctx.Word) || ctx.Word.Length != ctx.MinWordLength) return;
        ctx.AddMult(multBonus, displayName);
    }
}
