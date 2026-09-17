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
        if (ctx.Tiles == null || ctx.Tiles.Count != ctx.MinWordLength) return;
        ctx.AddMult(multBonus, displayName);
    }
}
