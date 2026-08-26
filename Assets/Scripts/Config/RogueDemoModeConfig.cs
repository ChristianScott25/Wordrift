using UnityEngine;

/// <summary>
/// First pass at the roguelike round: no clock, a finite bag of letters, and a
/// score you have to reach inside a fixed number of words.
///
/// Named "Rogue Demo" on purpose — it's somewhere to start and build off, not
/// the shape the real mode will end up in.
/// </summary>
[CreateAssetMenu(fileName = "RogueDemoMode", menuName = "Word Crush/Mode/Rogue Demo")]
public class RogueDemoModeConfig : ModeConfig
{
    [Header("Round")]
    [Tooltip("Words the player gets to reach the target.")]
    [Min(1)] public int moves = 20;

    [Tooltip("Score needed to clear the round.")]
    [Min(1)] public int targetScore = 120;

    [Tooltip("End the round the moment the target is reached, rather than always " +
             "playing out every move.")]
    public bool endOnTargetReached = true;

    [Tooltip("If true, submitting an invalid word also costs a move.")]
    public bool rejectedWordsCostMoves = false;

    [Tooltip("Move counter turns red at or below this many moves.")]
    public int urgentMoves = 3;

    [Header("Tile bag")]
    [Tooltip("Full copies of the Letter Set's distribution to pour into the bag. " +
             "1 = a single Scrabble bag — 98 tiles, nine A's, one Q. The board's " +
             "opening fill is paid for out of this, and once it's empty tiles stop " +
             "falling and the board only ever gets smaller.")]
    [Min(1)] public int bagCopies = 1;

    public override GameMode CreateMode() => new RogueDemoMode(this);
}
