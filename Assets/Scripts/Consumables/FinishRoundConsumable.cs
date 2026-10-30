using UnityEngine;

/// <summary>
/// 🚧 Epilogue (2026-10-29): when you're close enough to the round's target,
/// it tops your score up to it and the round clears the ordinary way.
///
/// All of the work is GameMode.TryFinishEarly → GameSession.RaiseScoreTo. The
/// raised score is what's saved, so there's no new round state. Refused (and
/// not spent) below the threshold or once the target is already reached.
/// </summary>
[CreateAssetMenu(fileName = "Consumable_Epilogue", menuName = "Word Crush/Consumable/Finish Round")]
public class FinishRoundConsumable : Consumable
{
    [Tooltip("How far to the target you must already be, 0..1.")]
    [Range(0f, 1f)] public float threshold = 0.75f;

    public override bool ChangesBoard => false;

    public override string PowerText =>
        $"If your score is at least {Mathf.RoundToInt(threshold * 100f)}% of the target, " +
        "it's topped up to the target and the round is cleared.";

    public override bool Use(ConsumableUse use) =>
        use?.Session != null && use.Session.TryFinishEarly(threshold);
}
