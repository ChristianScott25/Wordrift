using UnityEngine;

/// <summary>Countdown mode: score as much as you can before time runs out.</summary>
[CreateAssetMenu(fileName = "TimedMode", menuName = "Word Crush/Mode/Timed")]
public class TimedModeConfig : ModeConfig
{
    [Header("Timer")]
    [Min(1f)] public float roundSeconds = 60f;

    [Tooltip("Seconds added for every valid word. 0 = no bonus time.")]
    public float secondsPerWord = 0f;

    [Tooltip("Extra seconds per letter beyond the minimum word length.")]
    public float secondsPerExtraLetter = 0f;

    [Tooltip("Timer turns red at or below this many seconds.")]
    public float urgentSeconds = 10f;

    public override GameMode CreateMode() => new TimedMode(this);
}
