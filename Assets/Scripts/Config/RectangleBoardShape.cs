using System.Collections.Generic;
using UnityEngine;

/// <summary>Filled rectangle with (0,0) at the bottom-left.</summary>
[CreateAssetMenu(fileName = "RectangleBoard", menuName = "Word Crush/Board Shape/Rectangle")]
public class RectangleBoardShape : BoardShapeAsset
{
    [Min(1)] public int width = 5;
    [Min(1)] public int height = 10;

    public override IEnumerable<Vector2Int> Cells()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                yield return new Vector2Int(x, y);
    }
}
