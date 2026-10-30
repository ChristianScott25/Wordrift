using UnityEngine;

/// <summary>
/// 🚧 Pays money out mid-round (2026-10-29): Late Fee (a flat $8) and Overdue
/// Notice ($1 for every word played this round) are two assets of this class.
///
/// Money comes in through RunState.CreditFromItem, which leaves LastPayout
/// alone — the shop's "+$X EARNED" stays what the ROUND paid. Refused (and so
/// not spent) when it would pay nothing, e.g. Overdue Notice before your first
/// word.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_LateFee", menuName = "Word Crush/Consumable/Money")]
public class MoneyConsumable : Consumable
{
    [Min(0)] public int flatMoney = 8;

    [Tooltip("Paid per word played so far this round (repeats count).")]
    [Min(0)] public int moneyPerWord = 0;

    public override bool ChangesBoard => false;

    public override string PowerText
    {
        get
        {
            if (moneyPerWord > 0 && flatMoney > 0)
                return $"Get ${flatMoney}, plus ${moneyPerWord} for every word you've played this round.";
            if (moneyPerWord > 0)
                return $"Get ${moneyPerWord} for every word you've played this round.";
            return $"Get ${flatMoney} right now.";
        }
    }

    public override bool Use(ConsumableUse use)
    {
        if (use?.Run == null) return false;
        int words = use.Session != null ? use.Session.WordsThisRound.Count : 0;
        long amount = flatMoney + (long)moneyPerWord * words;
        if (amount <= 0) return false;

        use.Run.CreditFromItem((int)System.Math.Min(amount, int.MaxValue));
        return true;
    }
}
