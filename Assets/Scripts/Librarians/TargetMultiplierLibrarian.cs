using UnityEngine;

/// <summary>
/// Asks for far more of the same thing: the round's score target, multiplied.
///
/// The one librarian that changes nothing about HOW you play — every word you
/// could have played you still can. It only moves the bar, which makes it the
/// cleanest test of whether a run's scoring is actually keeping up, and the
/// reason it wants a factor rather than a number: three times whatever this
/// round was going to ask for stays meaningful on round 20.
/// </summary>
[CreateAssetMenu(fileName = "Librarian_Insatiable",
                 menuName = "Word Crush/Librarian/Target Multiplier")]
public class TargetMultiplierLibrarian : Librarian
{
    [Tooltip("What the round's score target is multiplied by. 3 = three times " +
             "the number this round would otherwise have asked for.")]
    [Min(0.1f)] public float targetMultiplier = 3f;

    public override string PowerText =>
        $"Score target ×{ScoringContext.Trim(targetMultiplier)}.";

    public override void Apply(RoundRules rules) =>
        rules.TargetMultiplier *= Mathf.Max(0f, targetMultiplier);
}
