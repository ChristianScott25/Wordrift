using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines which grid cells exist on the board. The board itself never assumes
/// a rectangle — any set of coordinates works (holes, L-shapes, hexagon-ish
/// outlines built from square cells, etc.). Implement this to add new shapes.
/// </summary>
public interface IBoardShape
{
    IEnumerable<Vector2Int> Cells();
}

/// <summary>Simple filled rectangle, (0,0) at bottom-left.</summary>
public class RectangleShape : IBoardShape
{
    private readonly int width;
    private readonly int height;

    public RectangleShape(int width, int height)
    {
        this.width = width;
        this.height = height;
    }

    public IEnumerable<Vector2Int> Cells()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                yield return new Vector2Int(x, y);
    }
}

/// <summary>Any explicit list of cells, for fully custom board shapes.</summary>
public class CustomShape : IBoardShape
{
    private readonly List<Vector2Int> cells;

    public CustomShape(IEnumerable<Vector2Int> cells)
    {
        this.cells = new List<Vector2Int>(cells);
    }

    public IEnumerable<Vector2Int> Cells() => cells;
}
