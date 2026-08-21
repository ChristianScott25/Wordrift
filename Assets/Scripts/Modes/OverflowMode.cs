using System.Linq;
using UnityEngine;

/// <summary>
/// Tetris-ish mode. The board opens partly filled and new tiles drip in from
/// the top, faster and faster. Clearing a word does NOT refill the hole — the
/// stack just compacts — so the only thing holding the board back is the
/// player. The round ends when a tile has nowhere left to land.
///
/// Unlike the other modes this one owns the board's population, which is why
/// it's the first mode to use the Board handle from Attach.
/// </summary>
public class OverflowMode : GameMode
{
    private readonly OverflowModeConfig config;

    private float elapsed;
    private float dropTimer;
    private float floorTimer;
    private bool overflowed;

    public OverflowMode(OverflowModeConfig config) => this.config = config;

    public override void Attach(Board board)
    {
        base.Attach(board);

        // Cleared cells stay empty — that's the entire mode.
        board.Refill = new NeverRefill();

        // Tiles are always in the air here, so the settle gate would never
        // reopen and input would be dead after the first word.
        board.GateInputWhileResolving = false;
    }

    public override void Begin()
    {
        elapsed = 0f;
        dropTimer = 0f;
        floorTimer = 0f;
        overflowed = false;

        // Board.Build left it empty (NeverRefill), so lay down the opening rows.
        board.FillLowestRows(config.startingRows);
    }

    public override void Tick(float deltaTime)
    {
        if (overflowed) return;
        elapsed += deltaTime;

        // Don't drop into a board that's mid-collapse: its columns still have
        // the holes the cleared word left, and a tile aimed at one would fall
        // through the tiles above it. The timers keep running while we wait, so
        // the drop fires the instant the collapse finishes.
        bool settling = board.Resolving;

        // The floor runs alongside the normal pace, not instead of it: an
        // early-game interval of two seconds would otherwise leave a freshly
        // cleared board with nothing to play with.
        if (board.TileCount < config.minimumTiles)
        {
            floorTimer += deltaTime;
            if (floorTimer >= config.floorDropInterval && !settling)
            {
                floorTimer = 0f;
                Drop();
            }
        }
        else floorTimer = 0f;

        dropTimer += deltaTime;
        if (dropTimer < DropInterval || settling) return;
        dropTimer -= DropInterval;   // keep the remainder so fast levels don't drift
        Drop();
    }

    /// <summary>
    /// Drops into the emptiest column, so the stack stays flat and the letters
    /// stay clustered enough to actually spell with. Ties break randomly —
    /// always taking the leftmost would make the pattern learnable.
    /// </summary>
    private void Drop()
    {
        var open = board.Columns.Where(x => !board.ColumnFull(x)).ToList();
        if (open.Count == 0)
        {
            overflowed = true;
            return;
        }

        int lowest = open.Min(board.ColumnHeight);
        var shortest = open.Where(x => board.ColumnHeight(x) == lowest).ToList();
        int column = shortest[Random.Range(0, shortest.Count)];

        // Every column we consider reported room, so a refusal means the board
        // and this mode disagree. Skip the drop and say so — ending the round on
        // it would read as a phantom game over.
        if (!board.TryDropInto(column))
            Debug.LogWarning($"Overflow: column {column} reported room but refused a drop.");
    }

    /// <summary>Seconds between drops right now. Shrinks geometrically, then flattens.</summary>
    private float DropInterval => Mathf.Max(config.minDropInterval, UncappedInterval);

    private float UncappedInterval => config.baseDropInterval *
        Mathf.Pow(config.levelSpeedUp, elapsed / config.secondsPerLevel);

    private int Level => Mathf.FloorToInt(elapsed / config.secondsPerLevel) + 1;

    public override bool IsRoundOver => overflowed;

    public override ModeStatus Status => new ModeStatus
    {
        Label = "LEVEL",
        Value = Level.ToString(),
        Fraction = Mathf.Repeat(elapsed / config.secondsPerLevel, 1f),
        Urgent = UncappedInterval <= config.minDropInterval,
    };
}
