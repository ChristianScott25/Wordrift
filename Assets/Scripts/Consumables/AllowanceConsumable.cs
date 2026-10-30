using UnityEngine;

/// <summary>
/// 🚧 Changes this round's allowances (2026-10-29): Spare Pen (+1 move), Scrap
/// Paper (+3 discards) and Rush Order (+2 moves, but no discards left) are three
/// assets of this one class.
///
/// All of the work is GameMode.ChangeAllowances, which refuses — so the item
/// isn't spent — when it would hand discards to a round whose rules allow none
/// (The Redactor). The boss beats the item. Round state only: the extra moves
/// and discards die with the round, and they're saved because movesLeft and
/// discardsLeft already are.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_SparePen", menuName = "Word Crush/Consumable/Allowance")]
public class AllowanceConsumable : Consumable
{
    [Tooltip("Moves added to this round. Can be negative.")]
    public int moves = 1;

    [Tooltip("Discards added to this round. Can be negative.")]
    public int discards = 0;

    [Tooltip("Sets this round's discards to 0, after the line above (Rush Order's price).")]
    public bool zeroDiscards = false;

    /// <summary>It doesn't touch the tiles, so the word you've built stays selected.</summary>
    public override bool ChangesBoard => false;

    public override string PowerText
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>(3);
            if (moves != 0) parts.Add($"{Signed(moves)} {(Mathf.Abs(moves) == 1 ? "move" : "moves")}");
            if (discards != 0) parts.Add($"{Signed(discards)} {(Mathf.Abs(discards) == 1 ? "discard" : "discards")}");
            string text = parts.Count > 0 ? $"{string.Join(" and ", parts)} this round." : "";
            if (zeroDiscards) text += (text.Length > 0 ? " But " : "") + "your discards drop to 0 this round.";
            return text;
        }
    }

    private static string Signed(int n) => n > 0 ? $"+{n}" : n.ToString();

    public override bool Use(ConsumableUse use) =>
        use?.Session != null && use.Session.ChangeAllowances(moves, discards, zeroDiscards);
}
