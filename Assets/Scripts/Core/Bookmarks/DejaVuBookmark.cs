using UnityEngine;

/// <summary>
/// Flat bonus for a word you already spelled this round. Repeats are legal —
/// nothing in the game stops you playing the same word twice — so this turns
/// that into a strategy rather than a quirk.
/// </summary>
[CreateAssetMenu(fileName = "DejaVu", menuName = "Word Crush/Bookmark/Deja Vu")]
public class DejaVuBookmark : Bookmark
{
    [Min(0)] public int bonusPoints = 10;

    public override void OnWordScored(ScoringContext ctx)
    {
        if (!ctx.IsRepeat) return;
        ctx.Points += bonusPoints;
    }
}
