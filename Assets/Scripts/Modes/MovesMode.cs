using UnityEngine;

/// <summary>
/// Limited-moves mode: no clock, you just get N words to score as high as
/// you can. Same board, same input, same scoring — only the resource changes.
/// </summary>
public class MovesMode : GameMode
{
    private readonly MovesModeConfig config;
    private int movesLeft;

    public MovesMode(MovesModeConfig config) => this.config = config;

    public override void Begin() => movesLeft = config.moves;

    public override void OnWordAccepted(WordResult result) => movesLeft--;

    public override void OnWordRejected(WordResult result)
    {
        if (config.rejectedWordsCostMoves) movesLeft--;
    }

    public override bool IsRoundOver => movesLeft <= 0;

    public override ModeStatus Status => new ModeStatus
    {
        Label = "MOVES",
        Value = Mathf.Max(0, movesLeft).ToString(),
        Fraction = config.moves > 0 ? (float)movesLeft / config.moves : 0f,
        Urgent = movesLeft <= config.urgentMoves,
    };
}
