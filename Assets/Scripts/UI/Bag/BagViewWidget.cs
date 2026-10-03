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

        Rebuild();
        root.SetActive(true);

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
