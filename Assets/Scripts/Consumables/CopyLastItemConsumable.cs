using UnityEngine;

/// <summary>
/// 🚧 Reprint (2026-10-29): gives you another copy of the last item you used
/// this run. Never a copy of itself (RememberedAsLastUsed is false), or it would
/// be an endless supply.
///
/// The copy arrives through ConsumableUse.GiveAfterSpend: the session adds it
/// once Reprint's own slot is free, so it fits even with every slot full.
/// Refused when nothing has been used yet this run.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Reprint", menuName = "Word Crush/Consumable/Copy Last Item")]
public class CopyLastItemConsumable : Consumable
{
    public override bool ChangesBoard => false;

    public override bool RememberedAsLastUsed => false;

    public override string PowerText =>
        "Gives you a copy of the last item you used this run.";

    public override bool Use(ConsumableUse use)
    {
        var last = use?.Run?.LastUsedConsumable;
        if (last == null || last is CopyLastItemConsumable) return false;
        use.GiveAfterSpend = last;
        return true;
    }
}
