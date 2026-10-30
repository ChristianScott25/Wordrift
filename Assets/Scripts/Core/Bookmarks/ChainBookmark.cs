using UnityEngine;

/// <summary>
/// Flat POINTS when the word starts with the letter the previous word ended on
/// — TEN, then NAP. Bookend's cousin, across two words instead of inside one.
///
/// Never fires on a round's first word: the word list starts fresh each round.
/// </summary>
[CreateAssetMenu(fileName = "Sequel", menuName = "Word Crush/Bookmark/Chain")]
public class ChainBookmark : Bookmark
{
    [Min(0)] public int bonusPoints = 20;

    public override void OnWordScored(ScoringContext ctx)
    {
        string word = ctx.Word;
        string previous = ctx.PreviousWord;
        if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(previous)) return;
        if (word[0] != previous[previous.Length - 1]) return;

        ctx.AddPoints(bonusPoints, displayName);
    }
}
