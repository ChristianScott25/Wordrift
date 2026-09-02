using UnityEngine;

/// <summary>
/// THE GRANDILOQUENT. Short words aren't words at all this round.
///
/// Note what it deliberately does NOT do: it doesn't raise the mode's
/// minWordLength. That number is also the zero point of the length-multiplier
/// curve, so moving it would quietly re-price every word as well as ban the
/// short ones. A five-letter word should still score exactly what a five-letter
/// word scores — the round is harder, not differently priced.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_MinimumLength",
                 menuName = "Word Crush/Librarian/Minimum Word Length")]
public class MinimumLengthLibrarian : Librarian
{
    [Tooltip("Shortest word the round will accept. The mode's own minimum still " +
             "applies underneath, so setting this lower than it changes nothing.")]
    [Min(2)] public int minimumLength = 5;

    public override string PowerText => $"Words must be {minimumLength} letters or longer.";

    public override string Refuse(WordCheck check) =>
        check.Length < minimumLength
            ? $"Too short — {minimumLength} letters or longer."
            : null;
}
