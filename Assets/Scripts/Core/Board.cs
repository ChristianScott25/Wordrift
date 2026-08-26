using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Owns the set of cells (from an IBoardShape) and the tiles sitting on them.
/// Handles spawning, demolition, gravity, and refilling. Never assumes the
/// board is rectangular — it only ever works with the cells it was given.
///
/// It doesn't assume every cell is occupied either. What happens to a hole
/// after a clear is Refill's decision (see IRefillPolicy), so a mode can leave
/// the board partly empty and feed tiles in on its own schedule.
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

    /// <summary>
    /// Whether clearing a word blocks input until the stack has settled.
    ///
    /// A mode where tiles are always in the air must turn this off — otherwise
    /// there is always something falling, Busy never clears, and input dies for
    /// the rest of the round. No current mode needs to; Overflow did.
    /// </summary>
    public bool GateInputWhileResolving { get; set; } = true;

    /// <summary>
    /// True from the moment a word's tiles are removed until the stack has been
    /// re-compacted. In that window the columns still contain the holes the word
    /// left behind, so anything asking "where would a tile land here?" gets a
    /// misleading answer — a cell in the middle of the stack.
    /// </summary>
    public bool Resolving => resolving > 0;

    public IReadOnlyDictionary<Vector2Int, Tile> Tiles => tiles;
    public Vector2 BoardCenter { get; private set; }
    public Vector2 BoardSize { get; private set; }

    /// <summary>Tiles on the board right now, counting ones still falling in.</summary>
    public int TileCount => tiles.Count;

    /// <summary>How many tiles the board holds when completely full.</summary>
    public int CellCount => cells.Count;

    /// <summary>Every column that exists, left to right.</summary>
    public IEnumerable<int> Columns => columnCells.Keys.OrderBy(x => x);

    /// <summary>Swap this to change how tiles fall (see IGravityRule).</summary>
    public IGravityRule Gravity { get; set; } = new ColumnGravity();

    /// <summary>Swap this to change what happens to cleared cells (see IRefillPolicy).</summary>
    public IRefillPolicy Refill { get; set; } = new FillEveryCell();

    /// <summary>
    /// Where new tiles' letters come from (see ILetterSource). Install a finite
    /// one in GameMode.Attach to give a mode a bag it can empty; left alone,
    /// Build fits an endless draw over the mode's LetterSet.
    /// </summary>
    public ILetterSource Letters { get; set; }

    private readonly Dictionary<Vector2Int, Tile> tiles = new();

    /// <summary>Every cell of each column, ordered bottom to top.</summary>
    private readonly Dictionary<int, List<Vector2Int>> columnCells = new();

    private HashSet<Vector2Int> cells = new();

    /// <summary>Outstanding resolve passes. A count, since clears can overlap.</summary>
    private int resolving;
    private BoardBackground background;
    private LetterSet letterSet;
    private IReadOnlyList<TileModifier> modifiers;
    private IReadOnlyList<TileSkin> skins;
    private TMP_FontAsset letterFont;

    public void Build(IBoardShape shape, LetterSet letters,
                      IReadOnlyList<TileModifier> availableModifiers = null,
                      IReadOnlyList<TileSkin> availableSkins = null,
                      TMP_FontAsset font = null)
    {
        letterSet = letters;
        if (letters == null)
            Debug.LogError("Board built with no LetterSet — no tiles can spawn.", this);

        // A mode may have installed a finite source in Attach; only fall back
        // when it didn't. Reset here rather than in Build's caller so the
        // opening fill always draws from a full source.
        Letters ??= new EndlessLetters(letters);
        Letters.Reset();

        modifiers = availableModifiers;
        skins = availableSkins;
        letterFont = font;
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

        columnCells.Clear();
        foreach (var column in cells.GroupBy(c => c.x))
            columnCells[column.Key] = column.OrderBy(c => c.y).ToList();

        BuildBackground();
        ClearTiles();
        FillEmptyCells();
    }

    /// <summary>
    /// Lays the board's backing under every cell. Optional: a Board with no
    /// BoardBackground component just plays on the scene's background colour.
    /// </summary>
    private void BuildBackground()
    {
        if (background == null) background = GetComponent<BoardBackground>();
        if (background == null) return;

        // Hand over world positions rather than cells, so the cell-to-world
        // maths stays in one place.
        var centers = new List<Vector3>(cells.Count);
        foreach (var cell in cells) centers.Add(CellToWorld(cell));
        background.Rebuild(centers, cellSize);
    }

    public void ResetBoard()
    {
        StopAllCoroutines();
        Busy = false;
        resolving = 0;   // the routines that would have decremented it are gone
        Letters?.Reset();  // a finite bag is whole again for the replay
        ClearTiles();
        FillEmptyCells();
    }

    private void ClearTiles()
    {
        foreach (var tile in tiles.Values)
            if (tile != null) Destroy(tile.gameObject);
        tiles.Clear();
    }

    public Vector3 CellToWorld(Vector2Int cell) =>
        transform.position + new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);

    /// <summary>
    /// The tile whose center is within grab distance of a world point, or null.
    /// Tiles still in the air are skipped — one would otherwise slide out from
    /// under the finger mid-chain and break adjacency.
    /// </summary>
    public Tile TileAt(Vector3 worldPos)
    {
        float grabRadius = cellSize * grabRadiusFraction;
        foreach (var tile in tiles.Values)
        {
            if (tile == null || !tile.IsSettled) continue;
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

    // ---- Column queries, for modes that manage the board's population ----

    /// <summary>Tiles in this column, counting ones still falling into it.</summary>
    public int ColumnHeight(int column) =>
        columnCells.TryGetValue(column, out var list) ? list.Count(tiles.ContainsKey) : 0;

    /// <summary>How many tiles this column holds when full.</summary>
    public int ColumnCapacity(int column) =>
        columnCells.TryGetValue(column, out var list) ? list.Count : 0;

    public bool ColumnFull(int column) => ColumnHeight(column) >= ColumnCapacity(column);

    /// <summary>
    /// Drops one new tile into a column from above the board, landing on top of
    /// whatever is already there. Returns false if the column has no room, which
    /// is how a mode feeding the board itself detects it has run out of space.
    ///
    /// Unused since Overflow mode was cut, as are the column queries above.
    /// </summary>
    public bool TryDropInto(int column)
    {
        if (!columnCells.TryGetValue(column, out var list)) return false;

        // Sit on top of what's already there, rather than in the lowest gap.
        // Gravity normally leaves no gaps, but during a resolve it does, and a
        // tile aimed into one falls straight past everything above it.
        int highest = -1;
        for (int i = 0; i < list.Count; i++)
            if (tiles.ContainsKey(list[i])) highest = i;

        int landingIndex = highest + 1;
        if (landingIndex >= list.Count) return false;
        Vector2Int landing = list[landingIndex];

        // Stack the entry point above anything still falling in this column,
        // or fast drops would spawn on top of each other in mid-air.
        int inFlight = list.Count(c =>
            tiles.TryGetValue(c, out var t) && t != null && !t.IsSettled);

        Vector3 target = CellToWorld(landing);
        var start = new Vector3(
            target.x, ColumnTopY(column) + cellSize * (1.5f + inFlight), target.z);

        // Also false when the letter source has run dry, so a caller can't tell
        // "no room" from "no tiles" — fine while nothing calls this.
        return SpawnTile(landing, start, target) != null;
    }

    /// <summary>
    /// Fills the lowest cells of every column, ignoring the refill policy.
    /// For a mode that opens on a partly-filled board.
    /// </summary>
    public void FillLowestRows(int rows)
    {
        foreach (var list in columnCells.Values)
        {
            for (int i = 0; i < rows && i < list.Count; i++)
            {
                if (tiles.ContainsKey(list[i])) continue;
                Vector3 target = CellToWorld(list[i]);
                SpawnTile(list[i], target, target);
            }
        }
    }

    private float ColumnTopY(int column) =>
        columnCells.TryGetValue(column, out var list) && list.Count > 0
            ? CellToWorld(list[list.Count - 1]).y
            : transform.position.y;

    // ---- Clearing and settling ----

    public void RemoveTiles(IEnumerable<Tile> toRemove)
    {
        foreach (var tile in toRemove)
        {
            if (tile == null) continue;
            tiles.Remove(tile.Cell);
            tile.Demolish();
        }
        resolving++;
        StartCoroutine(ResolveRoutine());
    }

    private IEnumerator ResolveRoutine()
    {
        if (GateInputWhileResolving) Busy = true;
        yield return new WaitForSeconds(settleDelay);

        // Wait only on the tiles this clear actually set in motion. Waiting on
        // every tile would never finish in a mode that drips new ones in.
        var moved = ApplyGravityAndRefill();
        resolving--;
        yield return new WaitUntil(() => moved.TrueForAll(t => t == null || t.IsSettled));

        if (GateInputWhileResolving) Busy = false;
    }

    private void FillEmptyCells()
    {
        var empties = cells.Where(c => !tiles.ContainsKey(c)).ToList();
        foreach (var cell in Refill.CellsToFill(empties))
        {
            if (tiles.ContainsKey(cell)) continue;
            Vector3 target = CellToWorld(cell);
            SpawnTile(cell, target, target);
        }
    }

    /// <summary>Returns every tile this pass set moving, for the settle wait.</summary>
    private List<Tile> ApplyGravityAndRefill()
    {
        var moved = new List<Tile>();
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
            moved.Add(tile);
        }

        // New tiles enter stacked above their column so they visibly fall in.
        foreach (var column in Refill.CellsToFill(plan.Empties).GroupBy(c => c.x))
        {
            var ordered = column.OrderBy(c => c.y).ToList();
            float topY = ColumnTopY(column.Key);
            for (int i = 0; i < ordered.Count; i++)
            {
                Vector3 target = CellToWorld(ordered[i]);
                var start = new Vector3(target.x, topY + cellSize * (i + 1.5f), target.z);
                var spawned = SpawnTile(ordered[i], start, target);
                if (spawned != null) moved.Add(spawned);
            }
        }

        return moved;
    }

    /// <summary>
    /// Instantiates one tile, or returns null when the letter source has nothing
    /// left. Null is a normal outcome for a finite source, not an error — the
    /// cell just stays empty.
    /// </summary>
    private Tile SpawnTile(Vector2Int cell, Vector3 startPos, Vector3 targetPos)
    {
        if (Letters == null || !Letters.TryDraw(out char letter)) return null;

        var tile = Instantiate(tilePrefab, startPos, Quaternion.identity, transform);
        tile.name = $"Tile {char.ToUpperInvariant(letter)} ({cell.x},{cell.y})";
        tile.Init(letter, letterSet.PointsFor(letter), NextLook(), cell, startPos, cellSize);
        RollModifiers(tile);
        if (startPos != targetPos) tile.MoveTo(targetPos);
        tiles[cell] = tile;
        return tile;
    }

    /// <summary>
    /// The look for the next tile. The font is fixed for the round; the skin is
    /// a weighted draw, mirroring how LetterSet draws letters, so a mode can mix
    /// tile types on one board. A null skin leaves the prefab's own art.
    /// </summary>
    private TileLook NextLook() => new TileLook
    {
        Skin = PickSkin(),
        LetterFont = letterFont,
    };

    private TileSkin PickSkin()
    {
        if (skins == null || skins.Count == 0) return null;

        int total = 0;
        foreach (var skin in skins)
            if (skin != null) total += Mathf.Max(0, skin.weight);

        // Every weight zeroed out is a config mistake, not a reason to draw nothing.
        if (total <= 0) return skins.FirstOrDefault(s => s != null);

        int roll = Random.Range(0, total);
        foreach (var skin in skins)
        {
            if (skin == null) continue;
            roll -= Mathf.Max(0, skin.weight);
            if (roll < 0) return skin;
        }
        return skins.FirstOrDefault(s => s != null);
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
