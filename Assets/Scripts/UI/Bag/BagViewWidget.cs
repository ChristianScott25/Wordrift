using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tile bag, opened over the game: every tile still undrawn this round,
/// grouped by what it is, with how many are left.
///
/// 🎯 UNDRAWN, NOT OWNED. A round deals 25 cells before the first word, so this
/// opens showing noticeably less than the run's whole bag — which is the point.
/// It answers "what can still come out", not "what did I buy". The full bag with
/// the spent tiles greyed is the obvious next version and is NOT this one.
///
/// ⚠️ THE BACKDROP IS A FULL-SCREEN RAYCAST TARGET, AND HERE THAT IS CORRECT.
/// Everywhere else in this project that would be a bug: ChainController refuses
/// to start a word whenever EventSystem.IsPointerOverGameObject() is true on the
/// press frame, so a stray raycast target over the board kills word selection
/// stone dead with a symptom pointing nowhere near the thing that caused it (see
/// the retired Item Panel, and why the info box has no backdrop at all). While
/// this panel is UP, that is exactly the effect wanted: every press frame
/// returns early, `dragging` is never set so the drag path is unreachable, and
/// BeginHold never runs either. The board goes inert with no new state and the
/// selection survives untouched, because nothing calls CancelChain.
///
/// So the whole safety story is: THE ROOT MUST BE OFF WHEN THIS IS CLOSED.
/// Nothing else protects the board.
///
/// ⚠️ NOT A Time.timeScale PAUSE, AND IT MUST NOT BECOME ONE. There is no clock
/// to stop — GameMode.Tick is a no-op in every mode and nothing in the project
/// reads timeScale — but GameSession.ScoreThenClear waits on WaitForSeconds,
/// which IS scaled, so a zero timescale would strand a score walk mid-count
/// forever.
/// </summary>
public class BagViewWidget : MonoBehaviour
{
    [Tooltip("The visuals to show/hide. Must NOT be this object — deactivating " +
             "ourselves would stop us hearing the events that close us.")]
    [SerializeField] private GameObject root;

    [Tooltip("Where the remaining tiles come from. The mode owns the bag; this " +
             "only ever reads it.")]
    [SerializeField] private GameSession session;

    [Tooltip("The look a bag tile is drawn in. Same asset the board uses, so a " +
             "skin swap reaches both — exactly what BookmarkRowWidget.cardSkin does.")]
    [SerializeField] private TileSkin skin;

    [Tooltip("The scrolling grid the tiles go into.")]
    [SerializeField] private GridLayoutGroup grid;

    [Tooltip("\"79 / 104\" — what's left out of the whole bag.")]
    [SerializeField] private TMP_Text countLabel;

    [SerializeField] private Button closeButton;

    [Tooltip("Sets how big the tiles are, smoothly. Right = bigger.")]
    [SerializeField] private Slider sizeSlider;

    [Header("Tile size")]
    [Tooltip("The SMALLEST tile is the one that fits this many to a row. Any " +
             "more and a tile is too small to read.")]
    [SerializeField, Min(1)] private int maxColumns = 6;

    [Tooltip("The BIGGEST tile is the one that fits this many to a row.")]
    [SerializeField, Min(1)] private int minColumns = 2;

    [Tooltip("The size before the player has ever moved the slider: the tile " +
             "that fits this many to a row.")]
    [SerializeField, Min(1)] private int startColumns = 4;

    [Tooltip("A cell's height as a fraction of its width. Fixed, so a cell keeps " +
             "its shape at every size.")]
    [SerializeField, Min(0.1f)] private float cellAspect = 150f / 210f;

    [Header("Look")]
    [Tooltip("Gap between a tile and its \"x3\", in canvas units.")]
    [SerializeField] private float countGap = 12f;

    [SerializeField] private Color countColor = new Color(0.12f, 0.13f, 0.17f, 1f);

    /// <summary>
    /// One kind of tile and how many of it are left.
    ///
    /// Face and Badges are worked out ONCE, when the group is made, rather than
    /// per comparison: TileSpec.Face allocates (it lowercases `letters`) and the
    /// grouping walk asks about it for every tile against every group found so
    /// far. Caching turns n-squared allocations into n.
    /// </summary>
    private struct Group
    {
        public TileSpec Spec;
        public string Face;
        public string Badges;
        public int Count;
    }

    private readonly List<Group> groups = new();
    private readonly List<BagTileView> views = new();

    /// <summary>
    /// ⚠️ THE ONE PlayerPrefs KEY IN THE PROJECT. The tile size is a player
    /// setting, not part of the run — it outlives every run and has nothing to
    /// do with a seed — so it stays out of RunSave and its fingerprint. His
    /// call, 2026-10-07: remembered for good, across rounds and relaunches.
    ///
    /// ⚠️ It stores a FRACTION of the way from smallest to biggest, not a size
    /// in units: the panel is a different width on every phone, and a fraction
    /// means the same thing on all of them. Renamed from "BagView.Columns" when
    /// the slider stopped snapping, so an old whole-number value is ignored
    /// rather than misread.
    /// </summary>
    private const string SizeKey = "BagView.Size";

    /// <summary>0 = smallest tile, 1 = biggest. Negative = never set; startColumns decides.</summary>
    private float size = -1f;

    /// <summary>Set when the slider moves, cleared once it's on disk.</summary>
    private bool sizeUnsaved;

    /// <summary>The gap between rows, and the narrowest gap between tiles — the grid's authored spacing.</summary>
    private Vector2 baseSpacing;

    private void Awake()
    {
        if (root == gameObject)
        {
            Debug.LogError("BagViewWidget's 'root' must be a child object, not itself.", this);
            root = null;
        }

        // Added here rather than in the prefab: a persistent listener pointing
        // at a scene object doesn't survive being saved into one.
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        SetUpSlider();

        if (root != null) root.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.RoundStarted += Close;
        GameEvents.RoundEnded += OnRoundEnded;
    }

    private void OnDisable()
    {
        GameEvents.RoundStarted -= Close;
        GameEvents.RoundEnded -= OnRoundEnded;
    }

    private void OnRoundEnded(RoundSummary summary) => Close();

    /// <summary>
    /// Shows what's left. Grouped fresh every time rather than kept up to date:
    /// nothing can draw a tile while this is open (the board is inert), so
    /// reopening is the only refresh there is to need.
    /// </summary>
    public void Open()
    {
        if (root == null) return;

        if (session == null)
            Debug.LogError("The bag view has no GameSession, so it can't see the bag. " +
                           "Run Word Crush > Set Up Game Layout.", this);

        root.SetActive(true);

        // After SetActive: the cell is worked out from the grid's width, and a
        // rect under an inactive parent isn't one to trust.
        ApplySize();
        Rebuild();

        // ⚠️ The layout bands are built at runtime in GameSession.Awake and
        // appended after anything the scene already held, and sibling order is
        // draw order — without this the panel draws UNDER every HUD widget.
        // GameOverPanel has exactly this bug and only gets away with it because
        // nothing is interactive once a round is over.
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Puts it away, and the info box with it — a card read off a bag tile has
    /// nothing to describe once the bag is gone. Same tidy-up
    /// ConsumablesAreaWidget does when a round ends.
    /// </summary>
    public void Close()
    {
        if (root != null) root.SetActive(false);
        Inspector.Hide();

        // Written to disk here rather than on every slider move — a smooth
        // drag changes the value every frame. Close runs on X, and on every
        // round start and end, so this is never far off.
        SaveSize();
    }

    /// <summary>
    /// iOS doesn't reliably give an app it kills a quit-time flush, and this is
    /// the last callback it reliably does run — the same reason GameSession
    /// saves the run here.
    /// </summary>
    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveSize();
    }

    private void SaveSize()
    {
        if (!sizeUnsaved) return;
        sizeUnsaved = false;
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------------
    // TILE SIZE
    // ------------------------------------------------------------------------

    /// <summary>
    /// The slider runs 0..1, smallest tile to biggest, and does NOT snap — the
    /// tile grows under the finger. His call, 2026-10-07, after a whole-step
    /// version felt notchy.
    /// </summary>
    private void SetUpSlider()
    {
        if (grid != null) baseSpacing = grid.spacing;

        if (PlayerPrefs.HasKey(SizeKey))
            size = Mathf.Clamp01(PlayerPrefs.GetFloat(SizeKey));

        if (sizeSlider == null) return;

        sizeSlider.wholeNumbers = false;
        sizeSlider.minValue = 0f;
        sizeSlider.maxValue = 1f;

        // The knob for a never-set size is placed by ApplySize, which is the
        // first thing that knows the panel's width.
        if (size >= 0f) sizeSlider.SetValueWithoutNotify(size);
        sizeSlider.onValueChanged.AddListener(OnSizeChanged);
    }

    private void OnSizeChanged(float value)
    {
        size = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SizeKey, size);   // to disk on Close, not per frame
        sizeUnsaved = true;
        ApplySize();
    }

    /// <summary>
    /// Sizes the tiles from the slider, against the grid's REAL width.
    ///
    /// The size is smooth but a row still holds a whole number of tiles, so
    /// there is nearly always some width left over. ⚠️ IT GOES INTO THE GAPS
    /// BETWEEN TILES, spread evenly, so the first and last tile of every row
    /// stay flush with the panel's edges at every size. The price: the moment a
    /// growing tile means one fewer fits, a tile drops to the next row and the
    /// gaps open wide (about 128 units at the 4-to-3 point on a 1080-wide
    /// canvas), then close up smoothly as the tiles keep growing into them.
    /// Centring the row instead would move both edges in and out — the
    /// alignment this panel was fixed for.
    /// </summary>
    private void ApplySize()
    {
        if (grid == null) return;

        int most = Mathf.Max(1, maxColumns);
        int fewest = Mathf.Clamp(minColumns, 1, most);

        var content = (RectTransform)grid.transform;
        float room = content.rect.width - grid.padding.left - grid.padding.right;
        float gap = baseSpacing.x;

        // The tile that fits n to a row with the narrowest gaps.
        float Fits(int n) => (room - gap * (n - 1)) / n;

        float smallest = Fits(most);
        float biggest = Fits(fewest);

        // A panel too narrow for even the smallest tile — nothing sensible to draw.
        if (smallest <= 1f) return;

        if (size < 0f)
        {
            size = Mathf.Approximately(biggest, smallest)
                ? 0f
                : Mathf.InverseLerp(smallest, biggest, Fits(Mathf.Clamp(startColumns, fewest, most)));
            if (sizeSlider != null) sizeSlider.SetValueWithoutNotify(size);
        }

        float cellWidth = Mathf.Lerp(smallest, biggest, size);

        // As many as fit with the narrowest gaps. The 0.001 stops a float
        // landing a hair under a whole number at either end of the slider and
        // dropping a column the maths says fits exactly.
        int columns = Mathf.Clamp(Mathf.FloorToInt((room + gap) / (cellWidth + gap) + 0.001f),
                                  fewest, most);

        float spread = columns > 1 ? (room - cellWidth * columns) / (columns - 1) : 0f;

        var cell = new Vector2(cellWidth, cellWidth * cellAspect);

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = cell;
        grid.spacing = new Vector2(Mathf.Max(gap, spread), baseSpacing.y);

        for (int i = 0; i < views.Count; i++)
            if (views[i] != null) views[i].Resize(cell, countGap);
    }

    // ------------------------------------------------------------------------
    // GROUPING
    // ------------------------------------------------------------------------

    private void Rebuild()
    {
        groups.Clear();

        // ⚠️ Read once, into our own list, and let go. This is the bag's OWN
        // live list — drawing a tile swap-removes from it — so holding the
        // reference would mean reading a collection the board mutates.
        var remaining = session == null ? null : session.RemainingTiles;

        if (remaining != null)
            for (int i = 0; i < remaining.Count; i++)
                Add(remaining[i]);

        groups.Sort(Compare);

        DrawCount(remaining == null ? 0 : remaining.Count);
        DrawGroups();
    }

    private void Add(TileSpec spec)
    {
        if (spec == null) return;

        string face = spec.Face;

        for (int i = 0; i < groups.Count; i++)
        {
            if (!string.Equals(groups[i].Face, face, StringComparison.Ordinal)) continue;
            if (groups[i].Spec.baseScore != spec.baseScore) continue;
            if (!SameModifiers(groups[i].Spec.modifiers, spec.modifiers)) continue;

            var hit = groups[i];
            hit.Count++;
            groups[i] = hit;
            return;
        }

        groups.Add(new Group
        {
            Spec = spec,
            Face = face,
            Badges = BadgeKey(spec),
            Count = 1,
        });
    }

    /// <summary>
    /// Do two tiles carry the same modifiers? A MULTISET comparison — 2L twice
    /// is x4 and is a thing you can buy, so "has a 2L" isn't the question.
    ///
    /// ⚠️ ORDER IS DELIBERATELY IGNORED, AND THAT IS ONLY FREE TODAY. The shop
    /// appends in purchase order and saves it, so one tile can hold 2L then 3W
    /// and another 3W then 2L. Every modifier in the game is a multiplier and
    /// multiplication commutes, so those two really are the same tile. The day a
    /// "+5 points" modifier lands — which TileModifier already anticipates —
    /// 2L-then-+5 stops equalling +5-then-2L and these have to split.
    ///
    /// ⚠️ Compares the ASSET, never badgeLabel: the label is display text and
    /// two different modifiers are free to share one.
    /// </summary>
    private static bool SameModifiers(List<TileModifier> a, List<TileModifier> b)
    {
        if (Count(a) != Count(b)) return false;

        // Cheap at any size a tile can reach — maxModifiersPerTile is 3.
        for (int i = 0; a != null && i < a.Count; i++)
        {
            if (a[i] == null) continue;
            if (Occurrences(a, a[i]) != Occurrences(b, a[i])) return false;
        }

        return true;
    }

    private static int Count(List<TileModifier> list)
    {
        if (list == null) return 0;

        int found = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) found++;

        return found;
    }

    private static int Occurrences(List<TileModifier> list, TileModifier of)
    {
        if (list == null) return 0;

        int found = 0;
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], of)) found++;

        return found;
    }

    /// <summary>The badges as one string, only ever used to order two groups of the same letter.</summary>
    private static string BadgeKey(TileSpec spec)
    {
        if (spec.modifiers == null || spec.modifiers.Count == 0) return "";

        var key = "";
        for (int i = 0; i < spec.modifiers.Count; i++)
            if (spec.modifiers[i] != null) key += spec.modifiers[i].badgeLabel + " ";

        return key;
    }

    /// <summary>
    /// Alphabetical, then plain tiles before stamped ones.
    ///
    /// ⚠️ ORDINAL, never culture-aware: it is what puts "*" (wild) first and
    /// sorts "a/e/i" straight after "a" rather than wherever a locale decides a
    /// slash belongs. Multi-letter tiles need no special case either — "ch"
    /// falls between "c" and "d" on its own.
    /// </summary>
    private static int Compare(Group x, Group y)
    {
        int byFace = string.CompareOrdinal(x.Face, y.Face);
        if (byFace != 0) return byFace;

        int byCount = Count(x.Spec.modifiers).CompareTo(Count(y.Spec.modifiers));
        if (byCount != 0) return byCount;

        return string.CompareOrdinal(x.Badges, y.Badges);
    }

    // ------------------------------------------------------------------------
    // DRAWING
    // ------------------------------------------------------------------------

    private void DrawCount(int remaining)
    {
        if (countLabel == null) return;

        var run = RunState.Current;
        int total = run == null ? 0 : run.TileBag.Count;

        countLabel.text = total > 0 ? $"{remaining} / {total}" : remaining.ToString();
    }

    /// <summary>
    /// Grows the pool to what's needed and hides the rest. Views are reused and
    /// never destroyed — a bag doesn't change shape much between openings, and a
    /// hidden child is ignored by the grid.
    /// </summary>
    private void DrawGroups()
    {
        if (grid == null) return;

        while (views.Count < groups.Count)
            views.Add(BagTileView.Create(grid.transform, grid.cellSize, countGap));

        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] == null) continue;

            if (i < groups.Count) views[i].Bind(groups[i].Spec, groups[i].Count, skin, countColor);
            else views[i].Bind(null, 0, skin, countColor);
        }
    }
}
