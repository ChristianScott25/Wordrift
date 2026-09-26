using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The word row — the strip above the board showing the word you're spelling,
/// as tiles, and the reason it won't score when it won't.
///
/// 🎯 IT EXISTS BECAUSE THE BOARD IS UNREADABLE MID-DRAG. A word snakes through
/// a 5x5 grid in whatever order your finger went, and your thumb is over some of
/// it. Laying the same tiles out left to right, outside the thumb's reach, is
/// the whole feature.
///
/// ⚠️ THESE ARE REAL Tile PREFABS, NOT A UI IMITATION. Tile.Init takes a world
/// position and a cell size and touches nothing on the Board, so a display tile
/// is an Instantiate and an Init — and it then carries the same skin, the same
/// score corner and the same modifier badges the board's tile is carrying, for
/// free and with no second copy of that drawing code to keep in step.
///
/// ⚠️ It is therefore WORLD-SPACE, not canvas-space, and gets its rectangle from
/// GameLayout.WorldRectOf rather than from its own RectTransform.
///
/// ⚠️ SelectionChanged FIRES EVERY FRAME OF A DRAG. Tiles are pooled and only
/// rebuilt when the selection actually changed — an Instantiate per frame would
/// be unplayable.
/// </summary>
public class CurrentWordWidget : MonoBehaviour
{
    [Header("Tiles")]
    [Tooltip("The board this mirrors — for the tile prefab and the tile look, so " +
             "the row can't drift from the board's own art.")]
    [SerializeField] private Board board;

    [Tooltip("Fraction of the band's height one tile may take.")]
    [Range(0.3f, 1f)][SerializeField] private float tileHeightFraction = 0.86f;

    [Tooltip("Gap between tiles, as a fraction of a tile.")]
    [Range(0f, 0.5f)][SerializeField] private float tileGap = 0.12f;

    [Header("Message")]
    [Tooltip("Shown in place of the tiles when the selection won't score.")]
    [SerializeField] private TMP_Text messageLabel;

    [SerializeField] private string wontScoreText = "WON'T SCORE";

    [SerializeField] private Color messageColor = new Color(1f, 0.45f, 0.45f);

    /// <summary>
    /// How small the reason under the message is drawn, as a percentage.
    /// </summary>
    [Range(30, 100)][SerializeField] private int reasonPercent = 55;

    private readonly List<Tile> tiles = new();
    private readonly List<TileSpec> shownSpecs = new();

    private RectTransform self;
    private Transform holder;
    private bool dirty = true;

    private void Awake()
    {
        self = (RectTransform)transform;

        // The display tiles hang off a child of the board rather than off this
        // widget: this is a canvas object, and a canvas's scale has nothing to do
        // with world units. Parenting sprites under it would scale them by the
        // canvas factor on top of their own fit-scale.
        var go = new GameObject("Word Row Tiles");
        holder = go.transform;
        if (board != null) holder.SetParent(board.transform.parent, false);
    }

    private void OnEnable()
    {
        GameEvents.SelectionChanged += OnSelectionChanged;
        GameEvents.RoundStarted += OnRoundStarted;
        GameLayout.Changed += OnLayoutChanged;
    }

    private void OnDisable()
    {
        GameEvents.SelectionChanged -= OnSelectionChanged;
        GameEvents.RoundStarted -= OnRoundStarted;
        GameLayout.Changed -= OnLayoutChanged;
    }

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start() => PlaceSelf();

    private void PlaceSelf()
    {
        GameLayout.Attach(self, LayoutBand.WordRow);
        dirty = true;
    }

    private void OnLayoutChanged()
    {
        PlaceSelf();
        LayOutTiles();
    }

    private void OnRoundStarted() => Clear();

    private void OnSelectionChanged(SelectionState selection)
    {
        ShowMessage(selection);

        var chain = selection.Tiles;
        if (chain == null || chain.Count == 0)
        {
            Clear();
            return;
        }

        if (NeedsRebuild(chain)) Rebuild(chain);
        else Mirror(chain);
    }

    /// <summary>
    /// The message replaces the tiles rather than sitting beside them: a row
    /// that showed both would be two things to read at the moment the player
    /// most needs one.
    ///
    /// Empty RefusedReason is the normal case and correct — "that isn't a word"
    /// needs no caption, and captioning it would bury the one message that IS
    /// informative when a librarian is the reason.
    /// </summary>
    private void ShowMessage(SelectionState selection)
    {
        if (messageLabel == null) return;

        bool wontScore = !selection.IsEmpty && !selection.CanSubmit;
        if (!wontScore)
        {
            messageLabel.text = "";
            return;
        }

        messageLabel.color = messageColor;
        messageLabel.text = string.IsNullOrEmpty(selection.RefusedReason)
            ? wontScoreText
            : $"{wontScoreText}\n<size={reasonPercent}%>{selection.RefusedReason}</size>";
    }

    /// <summary>
    /// Has the selection actually changed, or is this another frame of the same
    /// drag? Compared by SPEC IDENTITY — the specs are the run's own objects, so
    /// reference equality is exactly right and costs nothing.
    /// </summary>
    private bool NeedsRebuild(IReadOnlyList<Tile> chain)
    {
        if (dirty || chain.Count != shownSpecs.Count) return true;

        for (int i = 0; i < chain.Count; i++)
            if (chain[i] == null || !ReferenceEquals(chain[i].Spec, shownSpecs[i]))
                return true;

        return false;
    }

    private void Rebuild(IReadOnlyList<Tile> chain)
    {
        shownSpecs.Clear();
        for (int i = 0; i < chain.Count; i++)
            if (chain[i] != null) shownSpecs.Add(chain[i].Spec);

        LayOutTiles();
        Mirror(chain);
        dirty = false;
    }

    /// <summary>
    /// Copies across what each board tile is currently SHOWING, so a wild or a
    /// choice tile reads the same letter in both places. Never re-derived here —
    /// see Tile.Shown.
    /// </summary>
    private void Mirror(IReadOnlyList<Tile> chain)
    {
        for (int i = 0; i < tiles.Count && i < chain.Count; i++)
            if (tiles[i] != null && chain[i] != null)
                tiles[i].ShowLetters(chain[i].Shown);
    }

    /// <summary>
    /// Sizes and places the display tiles inside the band's WORLD rect.
    ///
    /// Shrink to fit: a long word gets smaller tiles rather than a row that runs
    /// off the side. Height is capped too, so a three-letter word doesn't draw
    /// tiles twice the size of the board's.
    /// </summary>
    private void LayOutTiles()
    {
        int count = shownSpecs.Count;
        if (count == 0)
        {
            Clear();
            return;
        }

        if (board == null || GameLayout.Current == null) return;

        Rect band = GameLayout.Current.WorldRectOf(LayoutBand.WordRow);
        if (band.width <= 0f || band.height <= 0f) return;

        // One tile plus its gap, across the whole row, capped by the band height
        // and never larger than a board tile — the row is a readout, not a
        // second board.
        float pitchLimit = band.width / (count + tileGap * (count - 1));
        float cellSize = Mathf.Min(pitchLimit, band.height * tileHeightFraction, board.CellSize);
        if (cellSize <= 0f) return;

        float pitch = cellSize * (1f + tileGap);
        float totalWidth = pitch * count - cellSize * tileGap;
        float left = band.center.x - totalWidth * 0.5f + cellSize * 0.5f;

        EnsureTiles(count, cellSize);

        for (int i = 0; i < count; i++)
        {
            if (tiles[i] == null) continue;
            var at = new Vector3(left + i * pitch, band.center.y, 0f);
            tiles[i].transform.position = at;
        }
    }

    /// <summary>
    /// Grows the pool to the size needed and re-inits every tile at the current
    /// cell size. Surplus tiles are hidden, never destroyed — the next word is
    /// usually a similar length.
    /// </summary>
    private void EnsureTiles(int count, float cellSize)
    {
        while (tiles.Count < count)
        {
            var tile = board.CreateDisplayTile(holder);
            tiles.Add(tile);
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == null) continue;

            bool used = i < count;
            if (tiles[i].gameObject.activeSelf != used) tiles[i].gameObject.SetActive(used);
            if (!used) continue;

            // Re-dressed rather than scale-nudged: Init is what sizes the body,
            // re-lays the labels out against it and re-fans the badges, and all
            // three have to move together when the cell size changes.
            board.DressDisplayTile(tiles[i], shownSpecs[i], tiles[i].transform.position, cellSize);
        }
    }

    private void Clear()
    {
        shownSpecs.Clear();
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i] != null && tiles[i].gameObject.activeSelf)
                tiles[i].gameObject.SetActive(false);
    }
}
