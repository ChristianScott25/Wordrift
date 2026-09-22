using UnityEngine;

/// <summary>
/// Adds to the MULT for every letter past the shortest word the mode allows.
///
/// The "go tall" half of a build fork: it steepens the length curve the mode
/// already has, so a 6-letter word stops being a nice-to-have and starts being
/// the plan. Its opposite number is Shorthand.
///
/// Additive, which is what makes it worth buying BEFORE a multiplicative
/// bookmark and much less worth buying after one.
/// </summary>
[CreateAssetMenu(fileName = "Marginalia", menuName = "Word Crush/Bookmark/Marginalia")]
public class MarginaliaBookmark : Bookmark
{
    [Min(0)] public int multPerExtraLetter = 1;

    public override void OnWordScored(ScoringContext ctx)
    {
        if (string.IsNullOrEmpty(ctx.Word)) return;

        // LETTERS, not ctx.Tiles.Count — a "ch" tile is one tile and two letters,
        // and this bookmark says "per letter". Off the context's minimum too,
        // never a hardcoded 3 — see ScoringContext.
        int extra = ctx.Word.Length - ctx.MinWordLength;
        if (extra <= 0) return;

        ctx.AddMult(extra * multPerExtraLetter, displayName);
    }
}
