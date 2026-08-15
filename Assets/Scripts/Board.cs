using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns the set of cells (from an IBoardShape) and the tiles sitting on them.
/// Handles spawning, demolition, gravity, and refilling. Never assumes the
/// board is rectangular — gravity works per column over whatever cells exist.
/// </summary>
public class Board : MonoBehaviour
{
    public const float CellSize = 0.72f;

    /// <summary>True while tiles are being demolished / falling; input is blocked.</summary>
    public bool Busy { get; private set; }

    public IReadOnlyDictionary<Vector2Int, Tile> Tiles => tiles;

    private readonly Dictionary<Vector2Int, Tile> tiles = new();
    private HashSet<Vector2Int> cells = new();
    private Dictionary<char, Sprite> letterSprites;
    private Vector2 boardCenter;

    public Vector2 BoardCenter => boardCenter;
    public Vector2 BoardSize { get; private set; }

    public void Build(IBoardShape shape)
    {
        LoadSprites();
        cells = new HashSet<Vector2Int>(shape.Cells());

        var min = new Vector2Int(cells.Min(c => c.x), cells.Min(c => c.y));
        var max = new Vector2Int(cells.Max(c => c.x), cells.Max(c => c.y));
        BoardSize = new Vector2((max.x - min.x + 1) * CellSize, (max.y - min.y + 1) * CellSize);
        boardCenter = (CellToWorld(min) + CellToWorld(max)) / 2f;

        Fill(animate: false);
    }

    public void ResetBoard()
    {
        StopAllCoroutines();
        Busy = false;
        foreach (var tile in tiles.Values)
            if (tile != null) Destroy(tile.gameObject);
        tiles.Clear();
        Fill(animate: false);
    }

    public Vector3 CellToWorld(Vector2Int cell) =>
        transform.position + new Vector3(cell.x * CellSize, cell.y * CellSize, 0f);

    /// <summary>The tile whose center is within grab distance of a world point, or null.</summary>
    public Tile TileAt(Vector3 worldPos)
    {
        float grabRadius = CellSize * 0.42f;
        foreach (var tile in tiles.Values)
        {
            if (Vector2.Distance(worldPos, tile.transform.position) <= grabRadius)
                return tile;
        }
        return null;
    }

    public static bool AreAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1 && (dx + dy) > 0;
    }

    public void RemoveTiles(IEnumerable<Tile> toRemove)
    {
        foreach (var tile in toRemove)
        {
            tiles.Remove(tile.Cell);
            tile.Demolish();
        }
        StartCoroutine(ResolveRoutine());
    }

    private IEnumerator ResolveRoutine()
    {
        Busy = true;
        yield return new WaitForSeconds(0.18f); // let the demolish animation play
        ApplyGravityAndRefill();
        yield return new WaitUntil(() => tiles.Values.All(t => t.IsSettled));
        Busy = false;
    }

    private void Fill(bool animate)
    {
        foreach (var cell in cells)
        {
            if (tiles.ContainsKey(cell)) continue;
            Vector3 target = CellToWorld(cell);
            Vector3 start = animate ? target + Vector3.up * BoardSize.y : target;
            SpawnTile(cell, start, target);
        }
    }

    /// <summary>
    /// Per column: existing tiles slide down to the lowest empty cells,
    /// new tiles spawn stacked above the column and fall in.
    /// </summary>
    private void ApplyGravityAndRefill()
    {
        foreach (var columnGroup in cells.GroupBy(c => c.x))
        {
            var columnCells = columnGroup.OrderBy(c => c.y).ToList();
            var columnTiles = columnCells
                .Where(c => tiles.ContainsKey(c))
                .Select(c => tiles[c])
                .ToList();

            // Reassign surviving tiles to the bottom-most cells.
            foreach (var cell in columnCells) tiles.Remove(cell);
            for (int i = 0; i < columnTiles.Count; i++)
            {
                var tile = columnTiles[i];
                var cell = columnCells[i];
                tiles[cell] = tile;
                tile.Cell = cell;
                tile.MoveTo(CellToWorld(cell));
            }

            // Spawn replacements above the top of the column.
            float topY = CellToWorld(columnCells[^1]).y;
            int spawned = 0;
            for (int i = columnTiles.Count; i < columnCells.Count; i++)
            {
                var cell = columnCells[i];
                var start = new Vector3(CellToWorld(cell).x, topY + CellSize * (spawned + 1.5f), 0f);
                var tile = SpawnTile(cell, start, CellToWorld(cell));
                tile.MoveTo(CellToWorld(cell));
                spawned++;
            }
        }
    }

    private Tile SpawnTile(Vector2Int cell, Vector3 startPos, Vector3 targetPos)
    {
        char letter = LetterBag.Draw();
        var go = new GameObject($"Tile {letter} {cell.x},{cell.y}");
        go.transform.SetParent(transform, worldPositionStays: true);
        var tile = go.AddComponent<Tile>();
        tile.Init(letter, letterSprites[letter], cell, startPos, CellSize);
        if (startPos != targetPos) tile.MoveTo(targetPos);
        tiles[cell] = tile;
        return tile;
    }

    private void LoadSprites()
    {
        if (letterSprites != null) return;
        letterSprites = new Dictionary<char, Sprite>();
        for (char c = 'a'; c <= 'z'; c++)
        {
            var loaded = Resources.LoadAll<Sprite>($"Letters/{c}");
            if (loaded.Length == 0)
                Debug.LogError($"No sprite found at Resources/Letters/{c}");
            else
                letterSprites[c] = loaded[0];
        }
    }
}
