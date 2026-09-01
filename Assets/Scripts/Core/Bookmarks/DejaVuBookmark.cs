using UnityEngine;

/// <summary>
/// Flat POINTS for a word you already spelled this round. Repeats are legal —
/// nothing in the game stops you playing the same word twice — so this turns
/// that into a strategy rather than a quirk.
///
/// The points-side one of the three. It lands AFTER the word's 2W/3W has already
/// been folded into Points, so it's a steady trickle rather than something a
/// tile multiplier can amplify.
/// </summary>
[CreateAssetMenu(fileName = "DejaVu", menuName = "Word Crush/Bookmark/Deja Vu")]
public class DejaVuBookmark : Bookmark
{
    [Min(0)] public int bonusPoints = 10;

    public override void OnWordScored(ScoringContext ctx)
    {
        if (!ctx.IsRepeat) return;
        ctx.AddPoints(bonusPoints, displayName);
    }
}
