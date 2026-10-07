using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Pointer input (touch or mouse) that builds a SELECTION of adjacent tiles.
/// Two ways in, one result: drag across tiles, or tap them one at a time.
///
/// A THIRD gesture also reads: PRESS AND HOLD one tile and its info box opens
/// (see Inspector) over whatever the press already selected. Holding is a
/// read on the board and a plain tap is a read everywhere else, and that split is forced rather than chosen —
/// a tap here already means "add this letter", and it is a supported way to
/// spell a word, so a box on every tap would appear on every letter.
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

    [Header("Reading a tile")]
    [Tooltip("How long a finger must rest on one tile before its info box opens. " +
             "0 switches holding off entirely.")]
    [SerializeField] private float holdSeconds = 0.45f;

    [Tooltip("How far the finger may drift, in SCREEN pixels, and still count as " +
             "holding still. Too tight and nobody can hold steady enough on a " +
             "phone; too loose and the start of a slow drag opens a box.")]
    [SerializeField] private float holdSlopPixels = 28f;

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

    // ---- Holding a tile to read it ------------------------------------------
    //
    // ⚠️ THE PRESS FRAME HAS ALREADY SELECTED BY THE TIME WE KNOW IT IS A HOLD.
    // OnTapped runs the instant the finger lands, because deferring it by the
    // hold threshold would put a visible lag on every tap-to-select. So a hold
    // never changes the selection — it only opens the box over whatever the
    // press already did. That only works because NO press ever deselects the
    // tile under the finger (see OnTapped and pendingUndo), so there is nothing
    // to put back. His call, 2026-10-06: the tile you touch lights up at once
    // and stays lit.
    private Tile pressedTile;
    private Vector2 pressScreenPos;
    private float pressedAt;
    private bool holdFired;

    // A press on the LAST selected tile is a one-letter undo — but only once
    // the finger lifts, because deselecting on the press frame would flicker
    // the tile off under a finger that may be about to hold it. It lands only
    // for a true TAP: no hold fired, the finger stayed within holdSlopPixels,
    // and the word didn't change at all during the press. Checking just "is it
    // still last?" isn't enough — drag onto a neighbour and back and the tile
    // is last again, and the undo would take a letter the player kept.
    // chainEdits is what catches that: it counts every change to the chain,
    // so add-then-remove still reads as a change.
    private Tile pendingUndo;
    private int chainEdits;
    private int chainEditsAtUndo;

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
            CancelPendingUndo();
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

            BeginHold(tile, pointer.position.ReadValue());

            if (tile != null)
            {
                OnTapped(tile);
                lastActedOn = tile;
            }
        }
        else if (pointer.press.isPressed && dragging)
        {
            // Moved off the spot: a drag, not a tap, so it can't be an undo.
            if (Vector2.Distance(pointer.position.ReadValue(), pressScreenPos) > holdSlopPixels)
                CancelPendingUndo();

            if (TryHold(pointer.position.ReadValue()))
            {
                // The gesture is a read now, not a drag. Ending the drag here is
                // what stops the finger carrying on into a word from a press the
                // player has already spent on reading.
                dragging = false;
                RedrawLine(null);
                return;
            }

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
            // drag, so the trailing line stops following the finger — and
            // lands a last-letter undo that the press deferred.
            LandPendingUndo();

            dragging = false;
            lastActedOn = null;
            pressedTile = null;
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
            // The tile under the finger stays selected. Tapping an earlier one
            // drops everything AFTER it, at once. Tapping the last one is a
            // one-letter undo, deferred to release (pendingUndo) so a hold on
            // it never sees it flicker off. Safe for any tile because the
            // selection is a path — cutting it anywhere leaves a shorter
            // valid path.
            if (existing < chain.Count - 1) TruncateTo(existing + 1);
            else
            {
                pendingUndo = tile;
                chainEditsAtUndo = chainEdits;
            }
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

    /// <summary>
    /// Remembers what a press landed on, where, and when.
    /// </summary>
    private void BeginHold(Tile tile, Vector2 screenPos)
    {
        holdFired = false;
        pressedTile = tile;
        pressScreenPos = screenPos;
        pressedAt = Time.unscaledTime;
    }

    /// <summary>
    /// Has this press become a HOLD? True exactly once per press, on the frame
    /// the threshold is crossed with the finger still on the tile it landed on.
    ///
    /// Time.unscaledTime rather than Time.time: the score walk-through and any
    /// future pause both work by stopping the clock, and how long a finger has
    /// been down is a fact about the finger.
    /// </summary>
    private bool TryHold(Vector2 screenPos)
    {
        if (holdFired || holdSeconds <= 0f || pressedTile == null) return false;

        // Moved too far — this is a drag, and it can never become a read.
        if (Vector2.Distance(screenPos, pressScreenPos) > holdSlopPixels)
        {
            pressedTile = null;
            return false;
        }

        if (Time.unscaledTime - pressedAt < holdSeconds) return false;

        holdFired = true;

        // A hold is a read: whatever the press did stands, and a last-letter
        // undo it queued is called off.
        CancelPendingUndo();

        Inspector.Show(pressedTile.Spec, Inspector.ScreenRectOf(pressedTile.WorldBounds, cam));
        return true;
    }

    private bool IsConnectable(Tile from, Tile to)
    {
        if (allowDiagonals) return Board.AreAdjacent(from.Cell, to.Cell);
        var delta = to.Cell - from.Cell;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    /// <summary>
    /// On release: removes the last tile if the press was a clean tap on it.
    /// Every condition but "nothing changed" is enforced by cancelling earlier.
    /// </summary>
    private void LandPendingUndo()
    {
        if (pendingUndo != null && chainEdits == chainEditsAtUndo
            && chain.Count > 0 && chain[chain.Count - 1] == pendingUndo)
            TruncateTo(chain.Count - 1);
        CancelPendingUndo();
    }

    private void CancelPendingUndo() => pendingUndo = null;

    private void AddTile(Tile tile)
    {
        chainEdits++;
        chain.Add(tile);
        tile.SetSelected(true);
        ChainChanged?.Invoke(chain);
    }

    /// <summary>Drops the tile at this index and every one after it.</summary>
    private void TruncateTo(int index)
    {
        chainEdits++;
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
        pressedTile = null;
        CancelPendingUndo();
        ClearSelectionSilently();
        ChainChanged?.Invoke(chain);
    }

    private void ClearSelectionSilently()
    {
        chainEdits++;
        foreach (var tile in chain)
            if (tile != null) tile.SetSelected(false);
        chain.Clear();
        if (line != null) line.positionCount = 0;
    }

    private static bool IsPointerOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// <summary>
    /// The word currently spelled out by a chain.
    ///
    /// Each tile contributes its WHOLE spelling, so a "ch" tile puts two letters
    /// into the word from one cell. It used to fill a char[tiles.Count], one slot
    /// per tile, which is where the second letter of a multi-letter tile went.
    /// </summary>
    public static string WordOf(IReadOnlyList<Tile> tiles)
    {
        var word = new System.Text.StringBuilder(tiles.Count + 4);
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i] != null) word.Append(tiles[i].Letters);
        return word.ToString();
    }

    /// <summary>
    /// How many LETTERS a chain spells — not how many tiles it uses. A "ch" tile
    /// counts for two.
    ///
    /// It lives next to WordOf so there is one definition of how long a chain is:
    /// the scorer asks this and the dictionary asks WordOf().Length, and two
    /// separate walks of the chain would eventually disagree about a word.
    /// </summary>
    public static int LetterCount(IReadOnlyList<Tile> tiles)
    {
        int letters = 0;
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i] != null) letters += tiles[i].Letters.Length;
        return letters;
    }
}
