using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An explicit list of cells, for hand-authored non-rectangular boards.
/// </summary>
[CreateAssetMenu(fileName = "CustomBoard", menuName = "Word Crush/Board Shape/Custom")]
public class CustomBoardShape : BoardShapeAsset
{
    [Tooltip("Every cell that exists on this board.")]
    public List<Vector2Int> cells = new();

    public override IEnumerable<Vector2Int> Cells() => cells;
}
