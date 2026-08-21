using UnityEngine;

/// <summary>Countdown mode: score as much as you can before time runs out.</summary>
[CreateAssetMenu(fileName = "TimedMode", menuName = "Word Crush/Mode/Timed")]
public class TimedModeConfig : ModeConfig
{
    [Header("Timer")]
    [Min(1f)] public float roundSeconds = 60f;

    [Tooltip("Seconds added for every valid word. 0 = no bonus time.")]
    public float secondsPerWord = 0f;

    [Tooltip("Extra seconds per letter beyond extraLettersStartAt.")]
    public float secondsPerExtraLetter = 0f;

    [Tooltip("Word length at which letters start earning bonus time. 4 means a " +
             "5-letter word has 1 extra letter. Deliberately separate from " +
             "minWordLength: short words stay playable, they just pay no time.")]
    [Min(1)] public int extraLettersStartAt = 4;

    [Tooltip("Timer turns red at or below this many seconds.")]
    public float urgentSeconds = 10f;

    public override GameMode CreateMode() => new TimedMode(this);
}
