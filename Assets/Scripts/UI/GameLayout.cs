using System;
using UnityEngine;

/// <summary>The bands the gameplay screen is divided into, top to bottom.</summary>
public enum LayoutBand
{
    RoundHeader,
    ResourceStrip,
    Score,
    WordRow,

    /// <summary>
    /// Reserved for the tips of the bookmark cards, which stand up out of the
    /// board's top edge.
    ///
    /// ⚠️ NOTHING ATTACHES TO IT — BookmarkRowWidget pins itself against the
    /// board's real top edge instead, because the board can sit slightly inside
    /// its band. This band exists purely to stop the word row from being where
    /// those card tips are. Without it the cards punch straight into the row
    /// above, and since the HUD canvas draws over every world sprite, the cards
    /// would cover the word's tiles.
    /// </summary>
    Bookmarks,

    Board,
    Buttons,
}

/// <summary>
/// Where everything on the gameplay screen goes.
///
/// The screen is a vertical stack of BANDS. This resolves that stack against
/// whatever screen it finds itself on, gives each band a RectTransform to live
/// in, and frames the camera so the board fills the board band.
///
/// ⚠️ THE BOARD IS SIZED TO FIT ITS BAND, NOT THE OTHER WAY ROUND. The camera
/// used to be framed by GameSession.FrameBoard, which fitted the board plus a
/// fixed padding and let the board's share of the screen fall out of the aspect
/// ratio. A banded layout needs the opposite: the chrome takes its bands and the
/// board fits what's left. `Board` itself is untouched by this — its cellSize
/// stays put and the CAMERA zooms.
///
/// ⚠️ ONE AUTHORITY, TWO COORDINATE SYSTEMS. The HUD is canvas-space UI and the
/// board and the word row's tiles are world-space sprites, so both `BandOf`
/// (canvas) and `WorldRectOf` (world) answer out of the same resolved rects.
/// Two places computing band positions is two answers to one question — which is
/// exactly the drift `BookmarkRowWidget` used to risk by converting the board's
/// edge to canvas space by hand.
///
/// ⚠️ ORDERING. Resolve() needs the board BUILT (it reads BoardSize), so
/// GameSession.Awake calls it right after Board.Build. Widgets must therefore
/// read their band in Start or from the Changed event — NEVER in OnEnable, which
/// runs before any Start and would read a band that doesn't exist yet. This is
/// the same ordering GameEvents already depends on.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class GameLayout : MonoBehaviour
{
    /// <summary>
    /// One band's share of the screen.
    ///
    /// `weight` is RELATIVE, not absolute — the weights are normalised against
    /// their own sum, so nudging one band doesn't mean re-tuning the other five
    /// to keep a total of 1. `minHeight` is the floor in canvas units, for the
    /// bands that stop being usable below a certain size (a button band that
    /// scales away is a button band you can't hit).
    /// </summary>
    [Serializable]
    public class BandSetting
    {
        public LayoutBand band;

        [Tooltip("Share of the leftover height, relative to the other bands.")]
        public float weight = 1f;

        [Tooltip("Floor in canvas units. 0 means the band may shrink freely.")]
        public float minHeight;
    }

    [Header("Scene")]
    [Tooltip("The board this frames the camera onto. Required.")]
    [SerializeField] private Board board;

    [Tooltip("Left empty, falls back to Camera.main.")]
    [SerializeField] private Camera sceneCamera;

    [Header("Bands, top to bottom")]
    [SerializeField]
    private BandSetting[] bands =
    {
        new BandSetting { band = LayoutBand.RoundHeader,   weight = 0.155f, minHeight = 200f },
        new BandSetting { band = LayoutBand.ResourceStrip, weight = 0.033f, minHeight = 52f },
        new BandSetting { band = LayoutBand.Score,         weight = 0.085f, minHeight = 110f },
        new BandSetting { band = LayoutBand.WordRow,       weight = 0.075f, minHeight = 96f },
        new BandSetting { band = LayoutBand.Bookmarks,     weight = 0.042f, minHeight = 70f },
        new BandSetting { band = LayoutBand.Board,         weight = 0.440f, minHeight = 240f },
        new BandSetting { band = LayoutBand.Buttons,       weight = 0.070f, minHeight = 110f },
    };

    [Header("Spacing, in canvas units")]
    [Tooltip("Gap between one band and the next.")]
    [SerializeField] private float bandGap = 18f;

    [Tooltip("Margin inside the safe area, left and right.")]
    [SerializeField] private float sideMargin = 32f;

    [Tooltip("Extra margin inside the safe area, top and bottom.")]
    [SerializeField] private float edgeMargin = 12f;

    /// <summary>
    /// The layout in the current scene. Widgets find it here rather than through
    /// a serialized reference each, because there is exactly one and half of them
    /// are placed by an editor script that would otherwise have six more fields
    /// to wire and to get wrong.
    /// </summary>
    public static GameLayout Current { get; private set; }

    /// <summary>
    /// The bands moved — a resize, a rotation, or the first resolve. Widgets that
    /// place themselves off a band listen for this.
    ///
    /// Static for the same reason RunState.Changed is: the listener is a widget
    /// in a scene, and it must not need wiring to a host object it can't see.
    /// Subscribe in OnEnable, unsubscribe in OnDisable, or a listener outlives
    /// its scene.
    /// </summary>
    public static event Action Changed;

    /// <summary>Has the stack been resolved at least once?</summary>
    public bool IsResolved { get; private set; }

    private readonly System.Collections.Generic.Dictionary<LayoutBand, RectTransform> containers = new();
    private readonly System.Collections.Generic.Dictionary<LayoutBand, Rect> rects = new();

    private Canvas canvas;
    private RectTransform canvasRect;
    private Vector2Int lastScreen;
    private Rect lastSafeArea;

    private void Awake()
    {
        Current = this;
        canvas = GetComponent<Canvas>();
        canvasRect = (RectTransform)transform;
        FindRefs();
    }

    /// <summary>
    /// Fills in anything the editor script didn't wire.
    ///
    /// Both are scene objects, so a prefab could never carry them, and a layout
    /// that silently did nothing because one field was empty is a whole screen
    /// laid out wrong — the same reason WordActionsWidget looks its session up.
    /// </summary>
    private void FindRefs()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (board == null) board = FindFirstObjectByType<Board>();
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    private void Update()
    {
        // A resize, a rotation, or the notch moving. Cheap enough to poll: two
        // int compares and a Rect compare, against the alternative of every
        // widget re-deriving its own position every frame.
        if (!IsResolved) return;
        if (lastScreen.x == Screen.width && lastScreen.y == Screen.height &&
            lastSafeArea == Screen.safeArea)
            return;

        Resolve();
    }

    /// <summary>
    /// Parents a widget into its band and stretches it to fill.
    ///
    /// The one way a widget takes up its position, so that "where does this go?"
    /// has a single answer and a landscape re-flow later is a matter of moving
    /// containers rather than editing six widgets. Returns false when the layout
    /// hasn't resolved yet, which is the caller's cue to wait for Changed rather
    /// than place itself somewhere wrong.
    /// </summary>
    /// <param name="xMin">Left edge as a fraction of the band, for the bands
    /// that several widgets share — the header holds the librarian box, the tile
    /// bag and the consumables area side by side.</param>
    /// <param name="xMax">Right edge, same units.</param>
    public static bool Attach(RectTransform rect, LayoutBand band,
                              float xMin = 0f, float xMax = 1f)
    {
        if (rect == null || Current == null || !Current.IsResolved) return false;

        var container = Current.BandOf(band);
        if (container == null) return false;

        rect.SetParent(container, false);
        rect.anchorMin = new Vector2(Mathf.Clamp01(xMin), 0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(xMax), 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        return true;
    }

    /// <summary>
    /// The RectTransform a band's widgets live inside. Null before the first
    /// Resolve, or for a band with no setting.
    /// </summary>
    public RectTransform BandOf(LayoutBand band) =>
        containers.TryGetValue(band, out var rect) ? rect : null;

    /// <summary>The band's rect in canvas units, measured from the canvas's bottom-left.</summary>
    public Rect CanvasRectOf(LayoutBand band) =>
        rects.TryGetValue(band, out var rect) ? rect : new Rect();

    /// <summary>
    /// The band's rect in WORLD units — what the board and the word row's tiles
    /// need, since those are sprites and not UI.
    ///
    /// Only meaningful once the camera has been framed, which Resolve does, and
    /// only for the camera this layout owns.
    /// </summary>
    public Rect WorldRectOf(LayoutBand band)
    {
        var rect = CanvasRectOf(band);
        if (sceneCamera == null || rect.width <= 0f) return new Rect();

        float scale = canvas.scaleFactor;
        Vector3 min = sceneCamera.ScreenToWorldPoint(
            new Vector3(rect.xMin * scale, rect.yMin * scale, 0f));
        Vector3 max = sceneCamera.ScreenToWorldPoint(
            new Vector3(rect.xMax * scale, rect.yMax * scale, 0f));

        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    /// <summary>
    /// A world-space height as a canvas-space height, measured from the canvas's
    /// bottom edge.
    ///
    /// The inverse of WorldRectOf, and here for the same reason: the bookmark row
    /// pins itself to the board's real top edge, which moves with the screen's
    /// shape, and it used to do this conversion itself. Two places converting
    /// between the camera and the canvas is two places to get it wrong.
    /// </summary>
    public float CanvasYOf(float worldY)
    {
        if (sceneCamera == null || canvas == null || canvas.scaleFactor <= 0f) return 0f;

        float screenY = sceneCamera.WorldToScreenPoint(new Vector3(0f, worldY, 0f)).y;
        return screenY / canvas.scaleFactor;
    }

    /// <summary>
    /// Resolves the band stack against the current screen and frames the camera.
    ///
    /// Called by GameSession.Awake once the board exists, and again by Update
    /// whenever the screen changes shape.
    /// </summary>
    public void Resolve()
    {
        // Defensive: GameSession.Awake calls this, and the order between two
        // Awakes is undefined — ours may not have run. Everything Awake sets is
        // re-derived here for the same reason.
        Current = this;

        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvasRect == null) canvasRect = (RectTransform)transform;
        FindRefs();

        lastScreen = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;

        Rect usable = UsableRect();
        LayOutBands(usable);
        FrameBoard();

        IsResolved = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// The canvas-space rectangle the bands may use: the safe area, inset by the
    /// authored margins.
    ///
    /// ⚠️ The safe area is the whole reason this exists. Without it the round
    /// header slides under an iPhone's notch and the button band under the home
    /// indicator — on every phone this game is being built for.
    /// </summary>
    private Rect UsableRect()
    {
        float scale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
        Rect safe = Screen.safeArea;

        float x = safe.x / scale + sideMargin;
        float y = safe.y / scale + edgeMargin;
        float width = safe.width / scale - sideMargin * 2f;
        float height = safe.height / scale - edgeMargin * 2f;

        return new Rect(x, y, Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    /// <summary>
    /// Shares the usable height out among the bands and positions a container
    /// for each, top to bottom.
    ///
    /// Two passes, because a minimum breaks a proportional split: the first
    /// shares by weight, the second pins any band that came out under its floor
    /// and re-shares what's left among the rest. One repeat is enough for the
    /// band table this ships with — a screen short enough to need more has no
    /// room to play on anyway.
    /// </summary>
    private void LayOutBands(Rect usable)
    {
        if (bands == null || bands.Length == 0) return;

        int count = bands.Length;
        float available = usable.height - bandGap * (count - 1);
        if (available <= 0f) return;

        var heights = new float[count];
        var pinned = new bool[count];

        for (int pass = 0; pass < 2; pass++)
        {
            float freeHeight = available;
            float freeWeight = 0f;

            for (int i = 0; i < count; i++)
            {
                if (pinned[i]) freeHeight -= heights[i];
                else freeWeight += Mathf.Max(0f, bands[i].weight);
            }

            if (freeWeight <= 0f) break;

            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                if (pinned[i]) continue;

                heights[i] = freeHeight * Mathf.Max(0f, bands[i].weight) / freeWeight;
                if (pass == 0 && heights[i] < bands[i].minHeight)
                {
                    heights[i] = bands[i].minHeight;
                    pinned[i] = true;
                    changed = true;
                }
            }

            if (!changed) break;
        }

        // Top down: the array is authored in the order they appear on screen.
        float top = usable.yMax;
        for (int i = 0; i < count; i++)
        {
            float height = Mathf.Max(0f, heights[i]);
            var rect = new Rect(usable.x, top - height, usable.width, height);

            rects[bands[i].band] = rect;
            Place(Container(bands[i].band), rect);

            top -= height + bandGap;
        }
    }

    /// <summary>
    /// The container for a band, made on first use.
    ///
    /// Made in code rather than authored so that adding a band is one array entry
    /// and not an editor round-trip — and so a scene that predates a band still
    /// gets one.
    /// </summary>
    private RectTransform Container(LayoutBand band)
    {
        if (containers.TryGetValue(band, out var existing) && existing != null)
            return existing;

        string name = $"Band - {band}";
        var child = transform.Find(name) as RectTransform;
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            child = (RectTransform)go.transform;
            child.SetParent(transform, false);

            // Bands are structure, not chrome: they must never eat a touch meant
            // for the board underneath, and nothing draws them.
            go.layer = gameObject.layer;
        }

        containers[band] = child;
        return child;
    }

    /// <summary>
    /// Pins a container to a canvas-space rect.
    ///
    /// Anchored to the canvas's bottom-left with a bottom-left pivot, so the rect
    /// is used literally and nothing depends on the canvas's own anchoring. Band
    /// contents then anchor INSIDE this, which is what makes a landscape re-flow
    /// later a matter of moving containers rather than hunting widgets.
    /// </summary>
    private static void Place(RectTransform rect, Rect at)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(at.x, at.y);
        rect.sizeDelta = new Vector2(at.width, at.height);
    }

    /// <summary>
    /// Zooms and pans the camera so the board fills the board band.
    ///
    /// Taking the LARGER of the two world-per-pixel ratios is what makes the
    /// board fit INSIDE the band on any screen: on a 19.5:9 phone the width binds
    /// and the board is full width; on 16:9 the height binds and the board is
    /// narrower with a margin either side. Taking the smaller would overflow the
    /// band on one axis.
    /// </summary>
    private void FrameBoard()
    {
        if (sceneCamera == null || board == null) return;
        if (!rects.TryGetValue(LayoutBand.Board, out var band)) return;
        if (band.width <= 0f || band.height <= 0f) return;

        Vector2 size = board.BoardSize;
        if (size.x <= 0f || size.y <= 0f) return;

        float scale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
        float bandWidthPx = band.width * scale;
        float bandHeightPx = band.height * scale;
        if (bandWidthPx <= 0f || bandHeightPx <= 0f) return;

        float worldPerPixel = Mathf.Max(size.y / bandHeightPx, size.x / bandWidthPx);

        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = worldPerPixel * Screen.height * 0.5f;

        // The band's centre in screen pixels, against the screen's own centre:
        // that offset is how far the camera has to sit from the board's centre
        // for the board to land in the band rather than in the middle.
        float bandCentrePx = band.center.y * scale;
        float offsetWorld = (bandCentrePx - Screen.height * 0.5f) * worldPerPixel;

        sceneCamera.transform.position = new Vector3(
            board.BoardCenter.x,
            board.BoardCenter.y - offsetWorld,
            sceneCamera.transform.position.z);
    }
}
