using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Pointer input (touch or mouse) that builds a SELECTION of adjacent tiles.
/// Two ways in, one result: drag across tiles, or tap them one at a time.
///
/// The selection PERSISTS when the pointer lifts — releasing is no longer a
/// submit. Something outside has to call Submit() or Clear(); the buttons in
/// WordActionsWidget are what do. That's the whole reason this class stopped
/// being "drag a word and let go": the player needs a moment between choosing
/// tiles and committing to them, to discard instead.
///
/// Knows nothing about scoring, words, or game modes — it just reports tiles.
/// </summary>
public class ChainController : MonoBehaviour
{
    [Header("Line")]
    [SerializeField] private LineRenderer line;
    [SerializeField] private Color lineColor = new Color(1f, 0.75f, 0.1f, 0.85f);
    [SerializeField] private float lineWidth = 0.12f;

    [Header("Rules")]
    [Tooltip("Allow diagonal connections between tiles.")]
    [SerializeField] private bool allowDiagonals = true;

    public bool InputEnabled { get; set; } = true;

    /// <summary>Fired whenever the selection grows or shrinks.</summary>
    public event Action<IReadOnlyList<Tile>> ChainChanged;

    /// <summary>Fired by Submit, with the tiles that were selected.</summary>
    public event Action<IReadOnlyList<Tile>> ChainSubmitted;

    /// <summary>The tiles selected right now, in the order they were picked.</summary>
    public IReadOnlyList<Tile> Selection => chain;

    private Board board;
    private Camera cam;
    private readonly List<Tile> chain = new();
    private bool dragging;

    // The tile this gesture last acted on. Without it, the frame after a tap
    // the finger is STILL on that tile, the drag path runs, and it undoes the
    // tap — a tap meant to deselect re-selects instead. Cleared on release, so
    // it only ever suppresses repeats within one press.
    private Tile lastActedOn;

    public void Init(Board board, Camera cam)
    {
        this.board = board;
        this.cam = cam;

        if (line == null) line = GetComponent<LineRenderer>();
        if (line != null)
        {
            line.startColor = line.endColor = lineColor;
            line.startWidth = line.endWidth = lineWidth;
            line.positionCount = 0;
        }
    }

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || board == null) return;

        if (!InputEnabled || board.Busy)
        {
            // Input off mid-gesture: stop tracking the drag, but KEEP the
            // selection. The board being busy is a pause, not a cancel, and
            // wiping the tiles here would undo a choice the player made.
            dragging = false;
            lastActedOn = null;
            RedrawLine(null);
            return;
        }

        Vector3 worldPos = cam.ScreenToWorldPoint(pointer.position.ReadValue());
        worldPos.z = 0f;

        if (pointer.press.wasPressedThisFrame)
        {
            // A press that starts on the HUD belongs to the HUD — without this,
            // tapping ENTER over the board area would also poke a tile.
            if (IsPointerOverUI()) return;

            dragging = true;
            lastActedOn = null;
            var tile = board.TileAt(worldPos);
            if (tile != null)
            {
                OnTapped(tile);
                lastActedOn = tile;
            }
        }
        else if (pointer.press.isPressed && dragging)
        {
            var tile = board.TileAt(worldPos);

            // Only when the pointer reaches a DIFFERENT tile. Holding still on
            // the one the press landed on must not act on it a second time.
            if (tile != null && tile != lastActedOn)
            {
                OnDraggedOver(tile);
                lastActedOn = tile;
            }
        }
        else if (!pointer.press.isPressed)
        {
            // Release does NOT submit and does NOT clear. It only ends the
            // drag, so the trailing line stops following the finger.
            dragging = false;
            lastActedOn = null;
        }

        RedrawLine(dragging ? worldPos : (Vector3?)null);
    }

    /// <summary>
    /// A fresh press on a tile. This is the tap-to-select path, and it's more
    /// permissive than dragging on purpose: a tap is a deliberate act, so it's
    /// allowed to restart the selection somewhere else entirely.
    /// </summary>
    private void OnTapped(Tile tile)
    {
        int existing = chain.IndexOf(tile);
        if (existing >= 0)
        {
            // Truncate back to it: tapping the last tile is a one-tile undo,
            // tapping an earlier one drops everything after it. Safe for any
            // tile because the selection is a path — cutting it anywhere
            // leaves a shorter valid path.
            TruncateTo(existing);
            return;
        }

        // Not touching what's already selected: the player has moved on, so
        // start again from here rather than ignoring the tap.
        if (chain.Count > 0 && !IsConnectable(chain[chain.Count - 1], tile))
            ClearSelectionSilently();

        AddTile(tile);
    }

    /// <summary>
    /// The pointer moved onto a tile with the button already down. Stricter
    /// than a tap: a fast drag skips over tiles, so a non-adjacent tile here is
    /// far more likely to be a gap in the sampling than an intent to start over.
    /// Ignoring it is what keeps a quick swipe from wiping the selection.
    /// </summary>
    private void OnDraggedOver(Tile tile)
    {
        if (chain.Count == 0)
        {
            AddTile(tile);
            return;
        }

        // Dragging back to the second-to-last tile removes the last one.
        if (chain.Count >= 2 && tile == chain[chain.Count - 2])
        {
            TruncateTo(chain.Count - 1);
            return;
        }

        if (chain.Contains(tile)) return;                 // each tile only once
        if (!IsConnectable(chain[chain.Count - 1], tile)) return;

        AddTile(tile);
    }

    private bool IsConnectable(Tile from, Tile to)
    {
        if (allowDiagonals) return Board.AreAdjacent(from.Cell, to.Cell);
        var delta = to.Cell - from.Cell;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    private void AddTile(Tile tile)
    {
        chain.Add(tile);
        tile.SetSelected(true);
        ChainChanged?.Invoke(chain);
    }

    /// <summary>Drops the tile at this index and every one after it.</summary>
    private void TruncateTo(int index)
    {
        for (int i = chain.Count - 1; i >= index; i--)
        {
            if (chain[i] != null) chain[i].SetSelected(false);
            chain.RemoveAt(i);
        }
        ChainChanged?.Invoke(chain);
    }

    private void RedrawLine(Vector3? pointerWorld)
    {
        if (line == null) return;
        if (chain.Count == 0)
        {
            line.positionCount = 0;
            return;
        }

        // The trailing segment to the finger exists only while dragging; a
        // tapped selection is just the tiles joined up.
        bool trail = pointerWorld.HasValue;
        line.positionCount = chain.Count + (trail ? 1 : 0);
        for (int i = 0; i < chain.Count; i++)
            line.SetPosition(i, chain[i].transform.position);
        if (trail) line.SetPosition(chain.Count, pointerWorld.Value);
    }

    /// <summary>
    /// Hands the selection off to whoever is listening and empties it. The only
    /// way a word gets played — called by the ENTER button, via GameSession.
    /// </summary>
    public void Submit()
    {
        if (chain.Count == 0) return;
        var submitted = new List<Tile>(chain);
        ClearSelectionSilently();
        ChainSubmitted?.Invoke(submitted);
        ChainChanged?.Invoke(chain);
    }

    /// <summary>
    /// Takes the selected tiles out WITHOUT submitting them, leaving the
    /// selection empty. What discarding needs: the caller gets the tiles and
    /// decides what happens to them.
    /// </summary>
    public List<Tile> TakeSelection()
    {
        var taken = new List<Tile>(chain);
        ClearSelectionSilently();
        ChainChanged?.Invoke(chain);
        return taken;
    }

    /// <summary>Drops the selection. Used when a round ends or restarts.</summary>
    public void CancelChain()
    {
        dragging = false;
        ClearSelectionSilently();
        ChainChanged?.Invoke(chain);
    }

    private void ClearSelectionSilently()
    {
        foreach (var tile in chain)
            if (tile != null) tile.SetSelected(false);
        chain.Clear();
        if (line != null) line.positionCount = 0;
    }

    private static bool IsPointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// <summary>The word currently spelled out by a chain.</summary>
    public static string WordOf(IReadOnlyList<Tile> tiles)
    {
        var chars = new char[tiles.Count];
        for (int i = 0; i < tiles.Count; i++) chars[i] = tiles[i].Letter;
        return new string(chars);
    }
}
