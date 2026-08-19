using UnityEngine;

/// <summary>Limited-moves mode: get the best score in a fixed number of words.</summary>
[CreateAssetMenu(fileName = "MovesMode", menuName = "Word Crush/Mode/Moves")]
public class MovesModeConfig : ModeConfig
{
    [Header("Moves")]
    [Min(1)] public int moves = 20;

    [Tooltip("If true, submitting an invalid word also costs a move.")]
    public bool rejectedWordsCostMoves = false;

    [Tooltip("Counter turns red at or below this many moves.")]
    public int urgentMoves = 3;

    public override GameMode CreateMode() => new MovesMode(this);
}
