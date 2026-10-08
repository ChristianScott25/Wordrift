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

    [Tooltip("A clear that ripples out from one tile (a cleared row or column, a " +
             "redraw): seconds between one ring of tiles spinning away and the next.")]
    [Min(0f)][SerializeField] private float rippleStep = 0.07f;

    [Tooltip("A full deal (the opening board, a redraw): seconds between one tile " +
             "dropping in and the next.")]
    [Min(0f)][SerializeField] private float dealStep = 0.05f;

    [Tooltip("An everyday refill (after a word, a discard, a cleared line): seconds " +
             "between one column's new tiles dropping in and the next column's.")]
    [Min(0f)][SerializeField] private float sweepStep = 0.06f;

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

    /// <summary>
    /// The board as it's DRAWN: BoardSize plus the backing's border, which
    /// sticks out half of (cellScale - 1) cells past the tiles on every side.
    /// What anything lining itself up with the board's visible edge wants —
    /// BoardSize is the tiles alone, and is a little narrower than what you see.
    /// </summary>
    public Vector2 BackingSize
    {
        get
        {
            if (background == null) background = GetComponent<BoardBackground>();
            float extra = background != null ? (background.CellScale - 1f) * cellSize : 0f;
            return BoardSize + new Vector2(extra, extra);
        }
    }

    /// <summary>Tiles on the board right now, counting ones still falling in.</summary>
    public int TileCount => tiles.Count;

    /// <summary>
    /// LETTERS the board could still spell, counting ones still falling in — a
    /// multi-letter tile counts for all of them. Different from TileCount the
    /// moment a "ch" tile is on the board, and it's this number that decides
    /// whether a word is still possible at all.
    /// </summary>
    public int LetterCount
    {
        get
        {
            int letters = 0;
            foreach (var tile in tiles.Values)
                if (tile != null) letters += tile.Letters.Length;
            return letters;
        }
    }

    /// <summary>How many tiles the board holds when completely full.</summary>
    public int CellCount => cells.Count;

    /// <summary>Every column that exists, left to right.</summary>
    public IEnumerable<int> Columns => columnCells.Keys.OrderBy(x => x);

    /// <summary>
    /// Cells the shape has that this round doesn't. Subtracted from the shape
    /// every time the board is laid out, so a closed cell simply isn't part of
    /// the board: nothing spawns there, nothing can be selected there, and the
    /// backing isn't drawn under it.
    ///
    /// The falling behaviour comes free from ColumnGravity, which compacts each
    /// column's cells in order — a column that lost its middle cell has one
    /// fewer slot, so surviving tiles fall straight PAST the gap and stack up
    /// from the bottom again.
    ///
    /// Set it in GameMode.Attach, before Build: that's the only window where it
    /// still affects the opening fill.
    /// </summary>
    public IEnumerable<Vector2Int> ClosedCells { get; set; }

    /// <summary>Swap this to change how tiles fall (see IGravityRule).</summary>
    public IGravityRule Gravity { get; set; } = new ColumnGravity();

    /// <summary>Swap this to change what happens to cleared cells (see IRefillPolicy).</summary>
    public IRefillPolicy Refill { get; set; } = new FillEveryCell();

    /// <summary>
    /// Where new tiles come from (see ITileSource). Install a finite one in
    /// GameMode.Attach to give a mode a bag it can empty; left alone, Build
    /// fits an endless draw over the mode's LetterSet.
    /// </summary>
    public ITileSource TileSource { get; set; }

    /// <summary>
    /// When each new tile of an everyday refill starts to fall (see
    /// ITileFillAnimation). Null = SweepColumns at this board's sweepStep.
    /// </summary>
    public ITileFillAnimation RefillAnimation { get; set; }

    /// <summary>
    /// When each tile of a full deal starts to fall — the opening board and a
    /// Redraw. Null = DealRowByRow at this board's dealStep.
    /// </summary>
    public ITileFillAnimation DealAnimation { get; set; }

    private ITileFillAnimation Refilling => RefillAnimation ?? new SweepColumns(sweepStep);
    private ITileFillAnimation Dealing => DealAnimation ?? new DealRowByRow(dealStep);

    private readonly Dictionary<Vector2Int, Tile> tiles = new();

    /// <summary>Every cell of each column, ordered bottom to top.</summary>
    private readonly Dictionary<int, List<Vector2Int>> columnCells = new();

    private HashSet<Vector2Int> cells = new();

    /// <summary>
    /// The shape this board was built from, kept so ResetBoard can lay the cells
    /// out again rather than reusing the set Build happened to produce.
    /// </summary>
    private IBoardShape shape;

    /// <summary>Outstanding resolve passes. A count, since clears can overlap.</summary>
    private int resolving;
    private BoardBackground background;
    private LetterSet letterSet;
    private IReadOnlyList<TileSkin> skins;
    private TMP_FontAsset letterFont;

    public void Build(IBoardShape shape, LetterSet letters,
                      IReadOnlyList<TileSkin> availableSkins = null,
                      TMP_FontAsset font = null)
    {
        letterSet = letters;
        if (letters == null)
            Debug.LogError("Board built with no LetterSet — no tiles can spawn.", this);

        // A mode may have installed a finite source in Attach; only fall back
        // when it didn't. Reset here rather than in Build's caller so the
        // opening fill always draws from a full source.
        TileSource ??= new EndlessTiles(letters);
        TileSource.Reset();

        skins = availableSkins;
        letterFont = font;
        this.shape = shape;

        if (!LayOutCells()) return;

        ClearTiles();
        FillEmptyCells();
    }

    /// <summary>
    /// Works out which cells exist — the shape, minus whatever this round has
    /// closed — and everything that follows from that: the columns, the framing
    /// size, and the backing.
    ///
    /// Run again on ResetBoard rather than only on Build, so a round played with
    /// holes in the board can't leave them behind for the replay.
    ///
    /// Returns false when there's nothing to play on, which is the one case the
    /// caller has to stop for.
    /// </summary>
    private bool LayOutCells()
    {
        if (shape == null)
        {
            Debug.LogError("Board has no shape to lay out.", this);
            return false;
        }

        cells = new HashSet<Vector2Int>(shape.Cells());
        if (ClosedCells != null) cells.ExceptWith(ClosedCells);

        if (cells.Count == 0)
        {
            Debug.LogError("Board shape produced no cells.", this);
            return false;
        }

        var min = new Vector2Int(cells.Min(c => c.x), cells.Min(c => c.y));
        var max = new Vector2Int(cells.Max(c => c.x), cells.Max(c => c.y));
        BoardSize = new Vector2((max.x - min.x + 1) * cellSize, (max.y - min.y + 1) * cellSize);
        BoardCenter = (CellToWorld(min) + CellToWorld(max)) / 2f;

        columnCells.Clear();
        foreach (var column in cells.GroupBy(c => c.x))
            columnCells[column.Key] = column.OrderBy(c => c.y).ToList();

        BuildBackground();
        return true;
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
        TileSource?.Reset();  // a finite bag is whole again for the replay
        DisposeReleased();    // a played word's tiles, if a walk was cut short

        // The shape again, not the set of cells the last round left behind: a
        // librarian may have closed some, and the replay is a different round.
        if (!LayOutCells()) return;

        ClearTiles();
        FillEmptyCells();
    }

    private void ClearTiles()
    {
        foreach (var tile in tiles.Values)
            if (tile != null) Destroy(tile.gameObject);
        tiles.Clear();
    }

    /// <summary>
    /// Rebuilds the board from an exact layout — every cell that isn't listed is
    /// left empty, and nothing is drawn from the tile source. This is how a saved
    /// round comes back: the tiles it names were dealt in an earlier session, so
    /// re-dealing them is precisely what must not happen.
    ///
    /// Cells the shape doesn't have are skipped rather than placed, so a board
    /// that changed shape under a save degrades instead of throwing.
    /// </summary>
    public void Restore(IReadOnlyDictionary<Vector2Int, TileSpec> layout)
    {
        StopAllCoroutines();
        Busy = false;
        resolving = 0;
        ClearTiles();
        if (layout == null) return;

        foreach (var placement in layout)
        {
            if (!cells.Contains(placement.Key)) continue;
            Vector3 target = CellToWorld(placement.Key);
            PlaceTile(placement.Key, placement.Value, target, target);
        }
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

    // ---- Shuffling ----

    /// <summary>
    /// The tiles swap into each other's places. Nothing is added, nothing is
    /// removed, and the board is left holding exactly the letters it held. Some
    /// tiles keep their cell — that is what a random rearrangement is — but not
    /// all of them at once; see Permutation.
    ///
    /// Returns false when nothing happened — the board is mid-fall, or has fewer
    /// than two tiles on it. The caller (a consumable) relies on that to know not
    /// to spend itself. An arrangement identical to the one it started from is
    /// NOT one of the refusals; see Permutation, which rules it out instead.
    ///
    /// ⚠️ IT PERMUTES ONLY CELLS THAT ALREADY HOLD A TILE, which is what keeps
    /// gravity out of it: the occupancy pattern afterwards is identical, so the
    /// next ColumnGravity.Plan is a fixed point (every move From == To) and
    /// nothing gets compacted. Shuffling tiles into EMPTY cells would look right
    /// and then be silently undone the next time a word cleared.
    ///
    /// ⚠️ THE ORDER IT WALKS CELLS IN MUST NOT COME FROM A HASHSET OR A
    /// DICTIONARY. It goes through columnCells, which is ordered by column and
    /// then by y. Enumerating `cells` or `tiles` instead would deal a different
    /// shuffle in the editor than on device from the very same seed, because
    /// neither one's enumeration order is stable across runtimes — the same trap
    /// The Dilapidated's candidate list exists to avoid.
    ///
    /// ⚠️ `resolving` is deliberately NOT touched. It means "the columns hold
    /// gaps gravity would never leave", which a permutation never produces.
    /// </summary>
    public bool Shuffle(Rng rng)
    {
        if (rng == null)
        {
            Debug.LogError("Board.Shuffle needs a seeded Rng — refusing rather than " +
                           "reaching for UnityEngine.Random.", this);
            return false;
        }

        if (Busy || Resolving) return false;

        var occupied = OccupiedInOrder();
        if (occupied.Count < 2) return false;

        var shuffled = Permutation(occupied, rng);

        // Rebuilt from the permutation rather than swapped in place, for the
        // same reason ApplyGravityAndRefill does it: overlapping writes into a
        // map you are still reading clobber each other.
        var previous = new Dictionary<Vector2Int, Tile>(tiles);
        tiles.Clear();

        var moved = new List<Tile>(occupied.Count);
        for (int i = 0; i < occupied.Count; i++)
        {
            if (!previous.TryGetValue(shuffled[i], out var tile) || tile == null)
            {
                // `tiles` has already been cleared, so skipping here leaves the
                // destination cell empty AND orphans whatever was in the source
                // one — still drawn, no longer clearable, no longer saved. It
                // also punches a hole in the occupancy pattern, which is the one
                // thing this method promises it never does. Can't happen (Board
                // never stores a null tile), so if it ever does, say so.
                Debug.LogError($"Shuffle found no tile at {shuffled[i]}, which it " +
                               "had just read out of the board.", this);
                continue;
            }

            var cell = occupied[i];

            // The key and tile.Cell are ONE fact and are always written
            // together — RemoveTiles does tiles.Remove(tile.Cell), and the save
            // keys the board off this dictionary.
            tiles[cell] = tile;
            tile.Cell = cell;
            tile.name = $"Tile {tile.Face.ToUpperInvariant()} ({cell.x},{cell.y})";

            if (cell == shuffled[i]) continue;
            tile.MoveTo(CellToWorld(cell));
            moved.Add(tile);
        }

        // Permutation guarantees at least one tile moves, so this is a bug, not
        // an unlucky draw.
        if (moved.Count == 0)
        {
            Debug.LogError("Shuffle moved nothing — the permutation came back as " +
                           "the arrangement it started from.", this);
            return false;
        }

        StartCoroutine(HoldInputUntilSettled(moved));
        return true;
    }

    /// <summary>
    /// A rearrangement of these cells in which AT LEAST ONE tile moves.
    ///
    /// ⚠️ THE IDENTITY IS EXCLUDED ON PURPOSE, and it is not paranoia: a shuffle
    /// is a paid item, so an arrangement the player cannot see is worth nothing.
    /// It is one draw in 25 factorial on a full board and A COIN TOSS ON A
    /// TWO-TILE ONE.
    ///
    /// It matters more than it looks, because a refusal cannot be saved. Nothing
    /// is spent when a shuffle does nothing, so no save is queued — and a resumed
    /// round winds this stream back to the position BEFORE the refusal, draws the
    /// same identity again, and refuses again. Deterministically, forever. So the
    /// identity is ruled out here rather than reported upwards.
    ///
    /// Forced rather than re-rolled: swapping the first two entries always works,
    /// takes no extra draws, and can't loop. It biases the (rare) identity case
    /// toward one particular answer, which is a fair price for terminating.
    /// </summary>
    private static List<Vector2Int> Permutation(List<Vector2Int> cells, Rng rng)
    {
        // Fisher-Yates over a copy, so cell i of `cells` gets the tile that was
        // at cell i of the result. One Range draw per cell past the first, which
        // is what the round's consumable stream records.
        var shuffled = new List<Vector2Int>(cells);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        for (int i = 0; i < shuffled.Count; i++)
            if (shuffled[i] != cells[i]) return shuffled;

        (shuffled[0], shuffled[1]) = (shuffled[1], shuffled[0]);
        return shuffled;
    }

    /// <summary>
    /// Holds input until these tiles have landed — a shuffle's slide, or the
    /// opening deal. Input is gated unconditionally, unlike ResolveRoutine's
    /// GateInputWhileResolving: that flag exists for a mode that drips tiles in
    /// forever and so never settles, and both of these always settle. A tile
    /// that has already landed would otherwise be grabbable while the rest of
    /// the board was still moving.
    /// </summary>
    private IEnumerator HoldInputUntilSettled(List<Tile> moved)
    {
        Busy = true;
        yield return new WaitUntil(() => moved.TrueForAll(t => t == null || t.IsSettled));
        Busy = false;
    }

    /// <summary>
    /// The cells holding a tile, in a stable order — by column, then bottom to
    /// top. See the warning on Shuffle: this exists so that no shuffle ever
    /// takes its order from a HashSet or a Dictionary.
    /// </summary>
    private List<Vector2Int> OccupiedInOrder()
    {
        var occupied = new List<Vector2Int>(tiles.Count);
        foreach (int column in Columns)
        {
            if (!columnCells.TryGetValue(column, out var list)) continue;
            foreach (var cell in list)
                if (tiles.ContainsKey(cell)) occupied.Add(cell);
        }
        return occupied;
    }

    // ---- What items do to the board ----

    /// <summary>
    /// Every tile in the same row (horizontal) or column as this cell — what
    /// LineClearConsumable clears. Walked through columnCells like
    /// Shuffle is, so the order never comes from a HashSet or a Dictionary.
    /// </summary>
    public List<Tile> TilesInLine(Vector2Int cell, bool horizontal)
    {
        var line = new List<Tile>();
        foreach (int column in Columns)
        {
            if (!horizontal && column != cell.x) continue;
            if (!columnCells.TryGetValue(column, out var list)) continue;
            foreach (var c in list)
            {
                if (horizontal && c.y != cell.y) continue;
                if (tiles.TryGetValue(c, out var tile) && tile != null) line.Add(tile);
            }
        }
        return line;
    }

    /// <summary>
    /// Turns the tile on a cell into a wild until it leaves the board
    /// (WildTileConsumable, and a resumed round putting one back). The wild is a
    /// spec off the catalog's "*" row and lives on the Tile alone — it is in no
    /// bag — so nothing past this round can see it. See Tile.BecomeWild.
    ///
    /// ⚠️ A FRESH SPEC PER TILE, NEVER ONE SHARED WILD. The word row finds the
    /// tile a score beat belongs to by the spec OBJECT (ScoreStep.Actor), so two
    /// blotted tiles sharing one spec would both pulse on the first one's beat.
    ///
    /// False when there's nothing to change: no tile, a tile mid-fall, or one
    /// that's already a wild. A choice tile is allowed — one letter of three
    /// becoming any of 26 is still an upgrade.
    /// </summary>
    public bool MakeWild(Tile tile)
    {
        if (tile == null || !tile.IsSettled || tile.Spec == null || tile.Spec.IsWild) return false;
        if (!tiles.TryGetValue(tile.Cell, out var onBoard) || onBoard != tile) return false;
        if (letterSet == null) return false;

        var wild = letterSet.CreateSpec(TileSpec.WildSpelling);
        if (!wild.IsWild) return false;

        tile.BecomeWild(wild);
        return true;
    }

    /// <summary>
    /// Puts every tile on the board back in the bag and deals a new board —
    /// RedrawConsumable. Return first, then draw (his call, 2026-10-08), so a
    /// few of the same tiles can come straight back; it also means it always
    /// works, however empty the bag is.
    ///
    /// ⚠️ It returns each tile's ORIGIN, never its Spec: a blotted tile goes
    /// back as the tile it was dealt as, and the throwaway wild is simply lost.
    ///
    /// Walks OccupiedInOrder so the tiles go back in a stable order — the bag
    /// indexes into its list, so a different order would deal a different board
    /// from the same seed. The new board falls in through the ordinary resolve,
    /// so Busy and Resolving cover it exactly as they cover a cleared word.
    ///
    /// Looks: the old board spins away in rings spreading out from `rippleFrom`
    /// (where the item was dropped), and the new one is DEALT, a tile at a time.
    /// </summary>
    public bool Redraw(Vector2Int rippleFrom)
    {
        if (Busy || Resolving || TileSource == null) return false;

        var occupied = OccupiedInOrder();
        if (occupied.Count == 0) return false;

        var vanishing = new List<Tile>(occupied.Count);
        foreach (var cell in occupied)
        {
            if (!tiles.TryGetValue(cell, out var tile) || tile == null) continue;
            tiles.Remove(cell);
            TileSource.Return(tile.Origin);
            tile.Demolish(RippleDelay(cell, rippleFrom));
            vanishing.Add(tile);
        }

        resolving++;
        StartCoroutine(ResolveRoutine(vanishing, Dealing));
        return true;
    }

    /// <summary>
    /// The cell closest to a world point — where an item that targets no tile
    /// was dropped, so its effect can still start under the finger. Walks
    /// columnCells, so a tie always goes the same way.
    /// </summary>
    public Vector2Int CellNearest(Vector3 world)
    {
        Vector2Int best = default;
        float bestDistance = float.MaxValue;
        foreach (int column in Columns)
        {
            if (!columnCells.TryGetValue(column, out var list)) continue;
            foreach (var cell in list)
            {
                float d = ((Vector2)(CellToWorld(cell) - world)).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = cell;
            }
        }
        return best;
    }

    // ---- Clearing and settling ----

    /// <summary>
    /// Spins the tiles away and lets the board fall and refill — a discard, or a
    /// cleared row or column.
    ///
    /// With `rippleFrom`, the clear spreads out from that cell: it goes first,
    /// then every tile one ring away, then two, `rippleStep` apart. A ring is
    /// max(|dx|, |dy|), so a line spreads equally both ways and a whole board
    /// spreads in squares. Without it (a discard, which has no one starting
    /// tile), they all go at once.
    ///
    /// Out of the tile map at once either way, so nothing can select, save or
    /// hit-test a tile that's still spinning.
    /// </summary>
    public void RemoveTiles(IEnumerable<Tile> toRemove, Vector2Int? rippleFrom = null)
    {
        var vanishing = new List<Tile>();
        foreach (var tile in toRemove)
        {
            if (tile == null) continue;
            tiles.Remove(tile.Cell);
            tile.Demolish(rippleFrom.HasValue ? RippleDelay(tile.Cell, rippleFrom.Value) : 0f);
            vanishing.Add(tile);
        }
        resolving++;
        StartCoroutine(ResolveRoutine(vanishing, Refilling));
    }

    private float RippleDelay(Vector2Int cell, Vector2Int from) =>
        Mathf.Max(Mathf.Abs(cell.x - from.x), Mathf.Abs(cell.y - from.y)) * rippleStep;

    /// <summary>
    /// Takes tiles off the board WITHOUT destroying them, and starts the
    /// collapse and refill straight away — a played word, whose tiles fly up
    /// into the word row while the board refills underneath the score count.
    ///
    /// ⚠️ The tiles are the CALLER's to animate and the BOARD's to destroy: they
    /// go on `released` and stay alive until DisposeReleased, or until
    /// ResetBoard sweeps them, so a round torn down mid-flight can't strand a
    /// tile in the scene. They are out of the tile map from this moment, so
    /// nothing that plays, falls, saves or hit-tests can see them.
    ///
    /// DISCARD still goes through RemoveTiles — nothing flies there.
    /// </summary>
    public void ReleaseTiles(IEnumerable<Tile> toRelease)
    {
        foreach (var tile in toRelease)
        {
            if (tile == null) continue;
            tiles.Remove(tile.Cell);
            released.Add(tile);
        }
        resolving++;
        StartCoroutine(ResolveRoutine(null, Refilling));
    }

    /// <summary>Destroys every tile ReleaseTiles handed out.</summary>
    public void DisposeReleased()
    {
        foreach (var tile in released)
            if (tile != null) Destroy(tile.gameObject);
        released.Clear();
    }

    private readonly List<Tile> released = new();

    /// <summary>
    /// Waits for the cleared tiles to finish leaving, then collapses the stack
    /// and refills it.
    ///
    /// ⚠️ It waits until every VANISHING tile is actually destroyed — not a fixed
    /// time — so nothing ever falls through a tile that's still spinning away,
    /// however long a ripple runs. The tile owns how long it takes (Demolish);
    /// the board just watches. A played word has nothing vanishing (its tiles
    /// fly off), so it keeps the old fixed settleDelay.
    /// </summary>
    private IEnumerator ResolveRoutine(List<Tile> vanishing, ITileFillAnimation fill)
    {
        if (GateInputWhileResolving) Busy = true;

        if (vanishing != null && vanishing.Count > 0)
            yield return new WaitUntil(() => vanishing.TrueForAll(t => t == null));
        else
            yield return new WaitForSeconds(settleDelay);

        // Wait only on the tiles this clear actually set in motion. Waiting on
        // every tile would never finish in a mode that drips new ones in.
        var moved = ApplyGravityAndRefill(fill);
        resolving--;
        yield return new WaitUntil(() => moved.TrueForAll(t => t == null || t.IsSettled));

        if (GateInputWhileResolving) Busy = false;
    }

    /// <summary>
    /// The opening fill (Build, ResetBoard): every empty cell DEALT in, a tile at
    /// a time, with input held until the last one lands.
    ///
    /// Tiles are spawned — and so drawn from the bag — in the same order as
    /// ever; the deal only decides when each one starts to fall.
    /// </summary>
    private void FillEmptyCells()
    {
        var empties = cells.Where(c => !tiles.ContainsKey(c)).ToList();
        var dealt = new List<(Tile tile, Vector3 target)>();
        foreach (var cell in Refill.CellsToFill(empties))
        {
            if (tiles.ContainsKey(cell)) continue;
            Vector3 target = CellToWorld(cell);
            var start = new Vector3(target.x, ColumnTopY(cell.x) + cellSize * 1.5f, target.z);
            var tile = SpawnTile(cell, start, start);
            if (tile != null) dealt.Add((tile, target));
        }

        var moved = Launch(dealt, Dealing);
        if (moved.Count > 0) StartCoroutine(HoldInputUntilSettled(moved));
    }

    /// <summary>
    /// Sends freshly spawned tiles to their cells on the animation's schedule.
    /// They were spawned standing at their start points, so this is the only
    /// place their fall begins.
    /// </summary>
    private static List<Tile> Launch(List<(Tile tile, Vector3 target)> spawned,
                                     ITileFillAnimation fill)
    {
        var moved = new List<Tile>(spawned.Count);
        if (spawned.Count == 0) return moved;

        var cellsFilled = new List<Vector2Int>(spawned.Count);
        foreach (var s in spawned) cellsFilled.Add(s.tile.Cell);

        var delays = new float[spawned.Count];
        fill?.Delays(cellsFilled, delays);

        for (int i = 0; i < spawned.Count; i++)
        {
            spawned[i].tile.MoveTo(spawned[i].target, delays[i]);
            moved.Add(spawned[i].tile);
        }
        return moved;
    }

    /// <summary>Returns every tile this pass set moving, for the settle wait.</summary>
    private List<Tile> ApplyGravityAndRefill(ITileFillAnimation fill)
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
        // Spawned (and drawn) in the same order as ever; `fill` only decides
        // when each one starts to fall.
        var spawnedTiles = new List<(Tile tile, Vector3 target)>();
        foreach (var column in Refill.CellsToFill(plan.Empties).GroupBy(c => c.x))
        {
            var ordered = column.OrderBy(c => c.y).ToList();
            float topY = ColumnTopY(column.Key);
            for (int i = 0; i < ordered.Count; i++)
            {
                Vector3 target = CellToWorld(ordered[i]);
                var start = new Vector3(target.x, topY + cellSize * (i + 1.5f), target.z);
                var spawned = SpawnTile(ordered[i], start, start);
                if (spawned != null) spawnedTiles.Add((spawned, target));
            }
        }

        moved.AddRange(Launch(spawnedTiles, fill));
        return moved;
    }

    /// <summary>
    /// Instantiates one tile, or returns null when the letter source has nothing
    /// left. Null is a normal outcome for a finite source, not an error — the
    /// cell just stays empty.
    /// </summary>
    private Tile SpawnTile(Vector2Int cell, Vector3 startPos, Vector3 targetPos)
    {
        if (TileSource == null || !TileSource.TryDraw(out TileSpec spec)) return null;
        return PlaceTile(cell, spec, startPos, targetPos);
    }

    /// <summary>
    /// Puts one specific spec on one specific cell, drawing nothing. The half of
    /// spawning that doesn't involve the tile source — which is what a restored
    /// board needs, since the tiles it puts back were drawn in a previous session.
    /// </summary>
    private Tile PlaceTile(Vector2Int cell, TileSpec spec, Vector3 startPos, Vector3 targetPos)
    {
        if (spec == null) return null;

        var tile = Instantiate(tilePrefab, startPos, Quaternion.identity, transform);
        // Face, not Spelling: a wild and a choice tile both SPELL "*", so the
        // hierarchy would be a column of identical names while debugging.
        tile.name = $"Tile {spec.Face.ToUpperInvariant()} ({cell.x},{cell.y})";
        tile.Init(spec, NextLook(), cell, startPos, cellSize);

        // Modifiers come from the spec and ONLY the spec: a tile has a
        // multiplier because the run put it there (an upgrade), never because
        // the board rolled dice at spawn. Boards don't decide upgrades.
        ApplySpecModifiers(tile, spec);
        if (startPos != targetPos) tile.MoveTo(targetPos);
        tiles[cell] = tile;
        return tile;
    }

    /// <summary>
    /// A tile that is NOT on the board — a display copy, for a readout that wants
    /// to show real tiles rather than imitate them (the word row).
    ///
    /// Deliberately never entered into `tiles`, so nothing that plays, falls,
    /// clears or saves can see it: the board's idea of what is on it is exactly
    /// the cells it dealt. The caller owns the object's lifetime.
    /// </summary>
    public Tile CreateDisplayTile(Transform parent)
    {
        if (tilePrefab == null) return null;

        var tile = Instantiate(tilePrefab, Vector3.zero, Quaternion.identity, parent);
        tile.name = "Display Tile";
        return tile;
    }

    /// <summary>
    /// Gives a display tile a spec, a place and a size, in the round's own look.
    ///
    /// Separate from creating it because the word row RESIZES its tiles as the
    /// word grows, and re-Init is what moves the body, the labels and the badge
    /// fan together — nudging a scale would move only one of the three.
    ///
    /// Safe to call repeatedly: Init clears the modifier list before this puts
    /// the spec's own back on, so re-dressing can't stack duplicates.
    /// </summary>
    public void DressDisplayTile(Tile tile, TileSpec spec, Vector3 at, float size)
    {
        if (tile == null || spec == null) return;

        tile.name = $"Display {spec.Face.ToUpperInvariant()}";
        tile.Init(spec, NextLook(), Vector2Int.zero, at, size);
        ApplySpecModifiers(tile, spec);
    }

    /// <summary>
    /// The look for the next tile. Both parts are fixed for the round — a null
    /// skin leaves the prefab's own art.
    /// </summary>
    private TileLook NextLook() => new TileLook
    {
        Skin = PickSkin(),
        LetterFont = letterFont,
    };

    /// <summary>
    /// The first skin the mode lists. Deliberately NOT a random draw: skins are
    /// heading toward being something the player chooses, not something the
    /// board rolls, so there's no randomness here to seed or to reason about.
    /// The list stays a list because a chooser will need one.
    /// </summary>
    private TileSkin PickSkin() =>
        skins == null ? null : skins.FirstOrDefault(s => s != null);

    /// <summary>Attaches every modifier the spec carries — a tile can have several.</summary>
    private static void ApplySpecModifiers(Tile tile, TileSpec spec)
    {
        if (spec.modifiers == null) return;
        foreach (var modifier in spec.modifiers)
            if (modifier != null) tile.AddModifier(modifier);
    }
}
