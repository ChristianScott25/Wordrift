using UnityEngine;

/// <summary>
/// Overflow mode: the board fills from the top and you clear words to hold it
/// back. Every number here is pacing — the mode has no fixed length, it runs
/// until the board wins.
/// </summary>
[CreateAssetMenu(fileName = "OverflowMode", menuName = "Word Crush/Mode/Overflow")]
public class OverflowModeConfig : ModeConfig
{
    [Header("Opening board")]
    [Tooltip("Rows filled at the bottom of every column when the round starts.")]
    [Min(0)] public int startingRows = 4;

    [Header("Drop pacing")]
    [Tooltip("Seconds between drops at the start of the round.")]
    [Min(0.05f)] public float baseDropInterval = 2f;

    [Tooltip("Fastest drops ever get, however long the round runs.")]
    [Min(0.05f)] public float minDropInterval = 0.35f;

    [Tooltip("Seconds of play per speed level.")]
    [Min(1f)] public float secondsPerLevel = 20f;

    [Tooltip("Drop interval is multiplied by this each level. 0.8 = 20% faster per level.")]
    [Range(0.1f, 0.99f)] public float levelSpeedUp = 0.8f;

    [Header("Minimum tiles")]
    [Tooltip("While the board holds fewer tiles than this, drops come at Floor Drop " +
             "Interval instead of waiting out the normal pace. Stops a freshly " +
             "cleared board from sitting half-empty during the slow early levels.")]
    [Min(0)] public int minimumTiles = 12;

    [Tooltip("Seconds between drops while below the minimum tile count.")]
    [Min(0.05f)] public float floorDropInterval = 0.12f;

    public override GameMode CreateMode() => new OverflowMode(this);
}
