using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns the set of cells (from an IBoardShape) and the tiles sitting on them.
/// Handles spawning, demolition, gravity, and refilling. Never assumes the
/// board is rectangular — it only ever works with the cells it was given.
/// </summary>
public class Board : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tile tilePrefab;

    [Header("Layout")]
    [Tooltip("World-space size of one cell.")]
    [SerializeField] private float cellSize = 0.72f;

    [Tooltip("How close to a tile's center the finger must be to grab it, as a fraction of cell size.")]
    [Range(0.2f, 0.7f)][SerializeField] private float grabRadiusFraction = 0.42f;

    [Header("Timing")]
    [Tooltip("Pause after tiles are demolished, before the rest fall in.")]
    [SerializeField] private float settleDelay = 0.18f;

    public float CellSize => cellSize;

    /// <summary>True while tiles are being demolished / falling; input is blocked.</summary>
    public bool Busy { get; private set; }

    public IReadOnlyDictionary<Vector2Int, Tile> Tiles => tiles;
    public Vector2 BoardCenter { get; private set; }
    public Vector2 BoardSize { get; private set; }

    /// <summary>Swap this to change how tiles fall (see IGravityRule).</summary>
    public IGravityRule Gravity { get; set; } = new ColumnGravity();

    private readonly Dictionary<Vector2Int, Tile> tiles = new();
    private readonly Dictionary<int, float> columnTopY = new();
    private HashSet<Vector2Int> cells = new();
    private LetterSet letterSet;
    private IReadOnlyList<TileModifier> modifiers;

    public void Build(IBoardShape shape, LetterSet letters, IReadOnlyList<TileModifier> availableModifiers = null)
    {
        letterSet = letters;
        modifiers = availableModifiers;
        cells = new HashSet<Vector2Int>(shape.Cells());

        if (cells.Count == 0)
        {
            Debug.LogError("Board shape produced no cells.", this);
            return;
        }

        var min = new Vector2Int(cells.Min(c => c.x), cells.Min(c => c.y));
        var max = new Vector2Int(cells.Max(c => c.x), cells.Max(c => c.y));
        BoardSize = new Vector2((max.x - min.x + 1) * cellSize, (max.y - min.y + 1) * cellSize);
        BoardCenter = (CellToWorld(min) + CellToWorld(max)) / 2f;

        columnTopY.Clear();
        foreach (var column in cells.GroupBy(c => c.x))
            columnTopY[column.Key] = CellToWorld(new Vector2Int(column.Key, column.Max(c => c.y))).y;

        ClearTiles();
        FillEmptyCells(animate: false);
    }

    public void ResetBoard()
    {
        StopAllCoroutines();
        Busy = false;
        ClearTiles();
        FillEmptyCells(animate: false);
    }

    private void ClearTiles()
    {
        foreach (var tile in tiles.Values)
            if (tile != null) Destroy(tile.gameObject);
        tiles.Clear();
    }

    public Vector3 CellToWorld(Vector2Int cell) =>
        transform.position + new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);

    /// <summary>The tile whose center is within grab distance of a world point, or null.</summary>
    public Tile TileAt(Vector3 worldPos)
    {
        float grabRadius = cellSize * grabRadiusFraction;
        foreach (var tile in tiles.Values)
        {
            if (tile == null) continue;
            if (Vector2.Distance(worldPos, tile.transform.position) <= grabRadius)
                return tile;
        }
        return null;
    }

    /// <summary>Adjacency includes diagonals.</summary>
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
            if (tile == null) continue;
            tiles.Remove(tile.Cell);
            tile.Demolish();
        }
        StartCoroutine(ResolveRoutine());
    }

    private IEnumerator ResolveRoutine()
    {
        Busy = true;
        yield return new WaitForSeconds(settleDelay);
        ApplyGravityAndRefill();
        yield return new WaitUntil(() => tiles.Values.All(t => t == null || t.IsSettled));
        Busy = false;
    }

    private void FillEmptyCells(bool animate)
    {
        foreach (var cell in cells)
        {
            if (tiles.ContainsKey(cell)) continue;
            Vector3 target = CellToWorld(cell);
            Vector3 start = animate ? target + Vector3.up * BoardSize.y : target;
            SpawnTile(cell, start, target);
        }
    }

    private void ApplyGravityAndRefill()
    {
        var occupied = new HashSet<Vector2Int>(tiles.Keys);
        var plan = Gravity.Plan(cells, occupied);

        // Rebuild the map from the plan so overlapping moves can't clobber each other.
        var previous = new Dictionary<Vector2Int, Tile>(tiles);
        tiles.Clear();
        foreach (var move in plan.Moves)
        {
            if (!previous.TryGetValue(move.From, out var tile) || tile == null) continue;
            tiles[move.To] = tile;
            if (move.From == move.To) continue;
            tile.Cell = move.To;
            tile.MoveTo(CellToWorld(move.To));
        }

        // New tiles enter stacked above their column so they visibly fall in.
        foreach (var column in plan.Spawns.GroupBy(c => c.x))
        {
            var ordered = column.OrderBy(c => c.y).ToList();
            float topY = columnTopY.TryGetValue(column.Key, out float y) ? y : 0f;
            for (int i = 0; i < ordered.Count; i++)
            {
                Vector3 target = CellToWorld(ordered[i]);
                var start = new Vector3(target.x, topY + cellSize * (i + 1.5f), target.z);
                SpawnTile(ordered[i], start, target);
            }
        }
    }

    private Tile SpawnTile(Vector2Int cell, Vector3 startPos, Vector3 targetPos)
    {
        char letter = letterSet.Draw();
        var tile = Instantiate(tilePrefab, startPos, Quaternion.identity, transform);
        tile.name = $"Tile {char.ToUpperInvariant(letter)} ({cell.x},{cell.y})";
        tile.Init(letter, letterSet.PointsFor(letter), letterSet.SpriteFor(letter),
                  cell, startPos, cellSize);
        RollModifiers(tile);
        if (startPos != targetPos) tile.MoveTo(targetPos);
        tiles[cell] = tile;
        return tile;
    }

    private void RollModifiers(Tile tile)
    {
        if (modifiers == null) return;
        foreach (var modifier in modifiers)
        {
            if (modifier == null || modifier.spawnChance <= 0f) continue;
            if (Random.value < modifier.spawnChance)
            {
                tile.AddModifier(modifier);
                return; // at most one modifier per tile for now
            }
        }
    }
}
