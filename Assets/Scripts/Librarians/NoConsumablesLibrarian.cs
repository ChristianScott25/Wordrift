using UnityEngine;

/// <summary>
/// No items this round. Whatever the run is carrying stays in the box.
///
/// Pure allowance: it sets RoundRules.ConsumablesAllowed and nothing else, and
/// GameSession.CanUseConsumable folds that in — so the items area refuses every
/// pick-up without knowing why. Tapping an item still opens its info box;
/// reading what you can't use is how you plan for the round after.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Shusher",
                 menuName = "Word Crush/Librarian/No Consumables")]
public class NoConsumablesLibrarian : Librarian
{
    public override string PowerText => "Items can't be used this round.";

    public override void Apply(RoundRules rules) => rules.ConsumablesAllowed = false;
}
