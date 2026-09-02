using UnityEngine;

/// <summary>
/// THE REDACTOR. Nothing leaves the board unplayed.
///
/// It takes nothing away from the score and adds nothing to the target — it
/// removes the escape hatch, so a board that won't give you a word is now yours
/// to solve rather than yours to reshuffle.
///
/// Written as a LIMIT rather than a value: it can only ever lower the round's
/// allowance, never raise it. A librarian that handed out extra discards would
/// be a gift, and this hook isn't where gifts belong.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_DiscardLimit",
                 menuName = "Word Crush/Librarian/Discard Limit")]
public class DiscardLimitLibrarian : Librarian
{
    [Tooltip("The most tiles the player may discard this round. 0 turns discarding off.")]
    [Min(0)] public int discards = 0;

    public override string PowerText =>
        discards <= 0 ? "No discards this round."
                      : $"Only {discards} tiles may be discarded this round.";

    public override void Apply(RoundRules rules) =>
        rules.Discards = Mathf.Min(rules.Discards, discards);
}
