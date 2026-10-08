using System.Collections;
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

    [Tooltip("How much of the band the TILES get. The rest is the message strip " +
             "underneath them.")]
    [Range(0.4f, 1f)][SerializeField] private float tileAreaFraction = 0.72f;

    [Tooltip("How much of a tile's BODY shows while the word is only spelled, " +
             "not played. The letter, score and badges always show in full. The " +
             "played tiles fly up and land on these, which is what turns them solid.")]
    [Range(0.05f, 1f)][SerializeField] private float ghostAlpha = 0.4f;

    [Header("Message")]
    [Tooltip("Shown UNDER the tiles when the selection won't score.")]
    [SerializeField] private TMP_Text messageLabel;

    [SerializeField] private string wontScoreText = "WON'T SCORE";

    [SerializeField] private Color messageColor = new Color(1f, 0.45f, 0.45f);

    /// <summary>
    /// How small the reason under the message is drawn, as a percentage.
    /// </summary>
    [Range(30, 100)][SerializeField] private int reasonPercent = 55;

    private readonly List<Tile> tiles = new();
    private readonly List<TileSpec> shownSpecs = new();

    // Which row tiles a played tile has landed on — those are drawn solid.
    // Only ever true during a walk; everything else is faded.
    private readonly List<bool> landed = new();

    // The board's own tiles, mid-air. Borrowed: the BOARD destroys them at the
    // end of the count (Board.DisposeReleased); this only flies and hides them.
    private readonly List<Tile> fliers = new();

    // The solid tiles shrinking away after the count. See OnWalkEnded.
    private Coroutine popRoutine;

    private RectTransform self;
    private Transform holder;
    private bool dirty = true;

    /// <summary>
    /// The word has been played and these tiles are being counted, so hold them.
    ///
    /// ⚠️ WITHOUT THIS THE WORD ROW EMPTIES THE INSTANT PLAY IS PRESSED.
    /// ChainController.Submit raises the word — which starts the walk-through —
    /// and then immediately raises an EMPTY selection, which used to land here as
    /// Clear(). So the one row showing the tiles being scored vanished at exactly
    /// the moment they started scoring. Same guard ScoreTallyWidget keeps on its
    /// numbers, for the same reason.
    /// </summary>
    private bool walking;

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
        GameEvents.WordSubmitted += OnWordSubmitted;
        GameEvents.TilesLaunched += OnTilesLaunched;
        GameEvents.ScoreBeat += OnScoreBeat;
        GameEvents.ScoreWalkEnded += OnWalkEnded;
        GameEvents.RoundEnded += OnRoundEnded;
        GameLayout.Changed += OnLayoutChanged;
    }

    private void OnDisable()
    {
        GameEvents.SelectionChanged -= OnSelectionChanged;
        GameEvents.RoundStarted -= OnRoundStarted;
        GameEvents.WordSubmitted -= OnWordSubmitted;
        GameEvents.TilesLaunched -= OnTilesLaunched;
        GameEvents.ScoreBeat -= OnScoreBeat;
        GameEvents.ScoreWalkEnded -= OnWalkEnded;
        GameEvents.RoundEnded -= OnRoundEnded;
        GameLayout.Changed -= OnLayoutChanged;

        // Or a re-enabled row would think a walk were still running and ignore
        // every selection from then on.
        walking = false;
        GroundFliers();
        popRoutine = null;   // a disabled object's coroutines are already dead

        // A pop or a bump cut off here would leave tiles at whatever scale it
        // had reached; clearing makes the next word re-dress them from scratch.
        dirty = true;
        Clear();
    }

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start() => PlaceSelf();

    private void PlaceSelf()
    {
        GameLayout.Attach(self, LayoutBand.WordRow);
        PlaceMessage();
        dirty = true;
    }

    /// <summary>
    /// Pins the message to the bottom slice of the band.
    ///
    /// Done here rather than authored in the editor script so the split is ONE
    /// number: the tiles read `tileAreaFraction` to know how much room they
    /// have, and if the label's rect were set somewhere else the two would
    /// drift and the message would creep back under the tiles.
    /// </summary>
    private void PlaceMessage()
    {
        if (messageLabel == null) return;

        var rect = messageLabel.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 1f - tileAreaFraction);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Shrinks to fit: the strip got shorter when the tiles got the room
        // (2026-10-07), and WON'T SCORE plus a librarian's reason is two lines.
        messageLabel.enableAutoSizing = true;
        messageLabel.fontSizeMin = 18f;
        messageLabel.fontSizeMax = 44f;
    }

    private void OnLayoutChanged()
    {
        // ⚠️ Before LayOutTiles, which re-dresses every tile through Tile.Init —
        // and Init writes localScale absolutely. A pulse still holding its
        // captured scale would put the OLD size back when it finished. See Jolt.
        CancelPulses();

        PlaceSelf();
        LayOutTiles();
    }

    private void OnRoundStarted() => StopWalk();

    private void OnRoundEnded(RoundSummary summary) => StopWalk();

    /// <summary>
    /// The word has been played: hold these tiles up while they're counted,
    /// rather than letting the empty selection that follows wipe them.
    /// </summary>
    private void OnWordSubmitted(WordResult result)
    {
        if (!result.Accepted) return;
        walking = true;
        landed.Clear();
    }

    /// <summary>
    /// Shakes the tile that just scored and floats its number off the top of it.
    ///
    /// The step carries the TileSpec rather than an index, which is what lets
    /// this work at all: these are display COPIES of the board's tiles, and the
    /// spec is the one object both of them hold. See ScoreStep.Actor.
    /// </summary>
    private void OnScoreBeat(ScoreStep step)
    {
        if (step.Kind != ScoreActor.Tile) return;

        int index = shownSpecs.IndexOf(step.Actor as TileSpec);
        if (index < 0 || index >= tiles.Count) return;

        var tile = tiles[index];
        if (tile == null || !tile.gameObject.activeSelf) return;

        // ⚠️ Sampled BEFORE the pulse. Tile.WorldBounds is the renderer's world
        // AABB, and an AABB GROWS as the sprite inside it rotates — a label
        // tracking it would drift and jitter in time with the shake.
        var cam = GameLayout.Current == null ? null : GameLayout.Current.SceneCamera;
        Rect at = cam == null ? new Rect() : Inspector.ScreenRectOf(tile.WorldBounds, cam);

        Jolt.Pulse(tile.transform);

        if (cam != null) ScorePop.Show(step.Amount, at, step.Side);
    }

    /// <summary>
    /// Flies each of the played word's board tiles up onto its faded twin in
    /// the row, one after another. Each landing turns its twin solid and hides
    /// the board tile.
    ///
    /// ⚠️ Every flight must START in here — GameSession checks IsSettled the
    /// moment this returns. See GameEvents.TilesLaunched.
    /// </summary>
    private void OnTilesLaunched(IReadOnlyList<Tile> chain)
    {
        if (!walking || chain == null) return;

        // The row should already be showing exactly this word — it's the
        // selection the player just pressed PLAY on. Rebuilt if not, so index
        // i is always chain[i]'s twin.
        if (NeedsRebuild(chain)) Rebuild(chain);

        float stagger = ScoreTallyTiming.FlightStagger();
        float seconds = ScoreTallyTiming.Flight();

        for (int i = 0; i < chain.Count; i++)
        {
            var flier = chain[i];
            if (flier == null) continue;

            var twin = i < tiles.Count ? tiles[i] : null;
            if (twin == null || !twin.gameObject.activeSelf)
            {
                flier.gameObject.SetActive(false);
                continue;
            }

            // The row tiles are smaller than the board's, so the flier shrinks
            // on the way. Worked out in world scale because the two hang off
            // different parents.
            var parent = flier.transform.parent;
            float parentScale = parent == null ? 1f : parent.lossyScale.x;
            float targetScale = twin.transform.lossyScale.x / Mathf.Max(0.0001f, parentScale);

            fliers.Add(flier);
            int index = i;
            flier.FlyTo(twin.transform.position, targetScale, i * stagger, seconds,
                        f => Land(f, index));
        }
    }

    private void Land(Tile flier, int index)
    {
        if (flier != null) flier.gameObject.SetActive(false);
        if (!walking || index >= tiles.Count || tiles[index] == null) return;

        while (landed.Count <= index) landed.Add(false);
        landed[index] = true;
        tiles[index].SetBodyAlpha(1f);
        StartCoroutine(Bump(tiles[index].transform));
    }

    /// <summary>
    /// A small swell on landing — deliberately NOT Jolt, which is the score
    /// beat's shake; a landing that looked like a beat would read as a point
    /// being scored before the count has started.
    /// </summary>
    private static IEnumerator Bump(Transform target)
    {
        const float seconds = 0.12f;
        float rest = target.localScale.x;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            if (target == null) yield break;
            float hump = Mathf.Sin(Mathf.PI * (t / seconds));
            target.localScale = Vector3.one * rest * (1f + 0.08f * hump);
            yield return null;
        }
        if (target != null) target.localScale = Vector3.one * rest;
    }

    /// <summary>
    /// The count is over: the solid tiles do a quick pop — a slight swell, then
    /// shrink to nothing — and the row is free for the next word.
    /// </summary>
    private void OnWalkEnded()
    {
        walking = false;
        dirty = true;
        GroundFliers();
        StopAllCoroutines();   // landing bumps — the pop writes the same scales
        CancelPulses();
        popRoutine = StartCoroutine(PopAway());
    }

    private IEnumerator PopAway()
    {
        float seconds = ScoreTallyTiming.Pop();
        var rest = new List<float>(tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
            rest.Add(tiles[i] == null ? 0f : tiles[i].transform.localScale.x);

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            // Up to 1.15 over the first third, then down to nothing.
            float factor = k < 0.3f
                ? Mathf.Lerp(1f, 1.15f, k / 0.3f)
                : Mathf.Lerp(1.15f, 0f, (k - 0.3f) / 0.7f);

            for (int i = 0; i < tiles.Count; i++)
                if (tiles[i] != null && tiles[i].gameObject.activeSelf)
                    tiles[i].transform.localScale = Vector3.one * rest[i] * factor;
            yield return null;
        }

        popRoutine = null;
        Clear();
    }

    /// <summary>
    /// Hides any board tile still in the air. Hiding counts as landing (see
    /// Tile.OnDisable), so GameSession can never be left waiting on one.
    /// </summary>
    private void GroundFliers()
    {
        for (int i = 0; i < fliers.Count; i++)
            if (fliers[i] != null && fliers[i].gameObject.activeSelf)
                fliers[i].gameObject.SetActive(false);
        fliers.Clear();
    }

    /// <summary>
    /// Lets go of the played word. `dirty` because NeedsRebuild compares against
    /// shownSpecs, and the next selection has to be measured against nothing
    /// rather than against the word that was just taken off the board.
    /// </summary>
    private void StopWalk()
    {
        walking = false;
        dirty = true;
        GroundFliers();
        StopAllCoroutines();
        popRoutine = null;
        Clear();
    }

    private void OnSelectionChanged(SelectionState selection)
    {
        // A walk in progress owns these tiles until it's finished — see `walking`.
        if (walking) return;

        // ⚠️ The session raises an EMPTY selection right after the walk ends,
        // which would Clear() the row before the pop is seen. An empty one is
        // ignored while popping; a real new word cuts the pop short.
        if (popRoutine != null)
        {
            if (selection.Tiles == null || selection.Tiles.Count == 0) return;
            StopCoroutine(popRoutine);
            popRoutine = null;
            dirty = true;
        }

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
    /// The message sits UNDER the tiles, never over them.
    ///
    /// ⚠️ It cannot simply fill the band. The tiles are world-space sprites and
    /// this is a canvas label, so the canvas draws over them unconditionally —
    /// a message across the middle of the band printed itself straight through
    /// the word it was talking about. The band is split instead: tiles in the
    /// top `tileAreaFraction`, message in what's left.
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

        // The top slice only — the bottom is the message's, and a tile drawn
        // into it would have the message printed across it.
        float tileHeight = band.height * tileAreaFraction;
        band = new Rect(band.x, band.yMax - tileHeight, band.width, tileHeight);

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

            // Faded until a played tile lands on it. Re-applied on every dress,
            // so a re-layout mid-walk keeps the landed ones solid.
            bool solid = walking && i < landed.Count && landed[i];
            tiles[i].SetBodyAlpha(solid ? 1f : ghostAlpha);
        }
    }

    private void Clear()
    {
        shownSpecs.Clear();
        landed.Clear();
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i] != null && tiles[i].gameObject.activeSelf)
                tiles[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Puts every tile back to its resting size before something else writes to
    /// it. Deactivating one restores it on its own (Jolt.OnDisable), so this is
    /// only needed where a tile stays up and gets re-laid-out under the pulse.
    /// </summary>
    private void CancelPulses()
    {
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i] != null) Jolt.Cancel(tiles[i].transform);
    }
}
