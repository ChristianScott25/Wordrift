using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bands the gameplay screen is divided into. The ENUM order is only a name;
/// what decides top-to-bottom is the order of GameLayout.bands, which since
/// 2026-10-19 puts the bookmarks ABOVE the word row so the word row sits on the
/// board's page.
/// </summary>
public enum LayoutBand
{
    RoundHeader,
    ResourceStrip,
    Score,
    WordRow,

    /// <summary>
    /// Reserved for the tips of the bookmark cards, which stand up out of the
    /// top edge of the PAGE (the board plus the word row on it).
    ///
    /// ⚠️ NOTHING ATTACHES TO IT — BookmarkRowWidget pins itself against the
    /// page's real top edge instead (GameLayout.PageTopWorld). This band exists
    /// to keep the score bar out of where those card tips are, and to give the
    /// row its height.
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
        // Retuned 2026-10-19 off the mock-up: bookmarks moved ABOVE the word
        // row so the word sits on the board's page, the bookmark tips got
        // taller, the score became a labelled bar, and the header, strip and
        // buttons gave up some height to pay for it.
        // GameLayoutSetup.MoveBandsOn carries a scene still on the 2026-10-07
        // table across to this one.
        new BandSetting { band = LayoutBand.RoundHeader,   weight = 0.100f, minHeight = 190f },
        new BandSetting { band = LayoutBand.ResourceStrip, weight = 0.028f, minHeight = 46f },
        new BandSetting { band = LayoutBand.Score,         weight = 0.072f, minHeight = 110f },
        new BandSetting { band = LayoutBand.Bookmarks,     weight = 0.068f, minHeight = 100f },
        new BandSetting { band = LayoutBand.WordRow,       weight = 0.105f, minHeight = 100f },
        new BandSetting { band = LayoutBand.Board,         weight = 0.420f, minHeight = 240f },
        new BandSetting { band = LayoutBand.Buttons,       weight = 0.056f, minHeight = 95f },
    };

    [Header("Spacing, in canvas units")]
    [Tooltip("Gap between one band and the next.")]
    [SerializeField] private float bandGap = 14f;

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

    /// <summary>
    /// The top edge of the board's 🚧 page in WORLD units — the board plus the
    /// word row sitting on it — or NaN when there is no page to pin to. The
    /// bookmark row stands its cards on this.
    /// </summary>
    public float PageTopWorld { get; private set; } = float.NaN;

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

    /// <summary>
    /// The camera the board is drawn with — for Inspector.ScreenRectOf, which is
    /// how a world-space thing tells a canvas widget where it is on screen.
    ///
    /// ⚠️ NOT a licence to do your own WorldToScreenPoint / scaleFactor maths.
    /// WorldRectOf, CanvasYOf and CanvasXOf below are still the only places the
    /// camera and the canvas are reconciled; this exists so a widget can hand the camera
    /// to the one shared helper rather than keep a second reference to it.
    /// </summary>
    public Camera SceneCamera => sceneCamera;

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
    /// A world-space x as a canvas-space x, measured from the canvas's left edge.
    /// CanvasYOf's twin, for the same reason it lives here.
    /// </summary>
    public float CanvasXOf(float worldX)
    {
        if (sceneCamera == null || canvas == null || canvas.scaleFactor <= 0f) return 0f;

        float screenX = sceneCamera.WorldToScreenPoint(new Vector3(worldX, 0f, 0f)).x;
        return screenX / canvas.scaleFactor;
    }

    /// <summary>
    /// Shrinks a button whose art keeps its shape (preserveAspect) so the art
    /// sits FLUSH against one side of its slot rather than centred in it.
    ///
    /// A centred square in a slightly wider slot leaves a sliver either side,
    /// which is invisible mid-row and obvious at the end of one: it's what put
    /// the info button a few units left of the board's edge and PLAY a few
    /// inside it. Call it after Attach, on the button at each END of a row.
    /// Idempotent — it starts from the slot's own anchors every time.
    /// </summary>
    public static void HugEdge(RectTransform rect, bool toLeft)
    {
        if (rect == null) return;

        var image = rect.GetComponent<Image>();
        if (image == null || image.sprite == null) return;

        Vector2 art = image.sprite.rect.size;
        if (art.x <= 0f || art.y <= 0f) return;

        rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
        rect.offsetMax = new Vector2(0f, rect.offsetMax.y);

        // A sliced plate (PLAY) stretches to its whole slot, so it's already flush.
        if (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled) return;

        float width = rect.rect.width;
        float artWidth = Mathf.Min(width, rect.rect.height * art.x / art.y);
        float spare = Mathf.Max(0f, width - artWidth);

        if (toLeft) rect.offsetMax = new Vector2(-spare, rect.offsetMax.y);
        else rect.offsetMin = new Vector2(spare, rect.offsetMin.y);
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
        LayPage();
        FitBandsToBoard(usable);

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
    /// Narrows (or widens) every band to the board's DRAWN width, so the whole
    /// screen shares one left and one right edge — the book's.
    ///
    /// It used to be the button band alone. The bands all share the usable
    /// width, but the board usually doesn't fill it exactly: when its band is
    /// the tight axis it's narrower, and its border and page always poke a
    /// little past the tiles. Measured off OuterSize, after FrameBoard, because
    /// the camera decides where the board lands. Never past the safe area.
    ///
    /// ⚠️ The WORD ROW takes the page's INSIDE width instead (the grid's
    /// backing), so its tiles can never sit on the cover or hang off the page.
    /// </summary>
    private void FitBandsToBoard(Rect usable)
    {
        if (board == null || sceneCamera == null) return;

        float outer = board.OuterSize.x * 0.5f;
        float inner = board.BackingSize.x * 0.5f;
        if (outer <= 0f) return;

        foreach (var setting in bands)
        {
            if (setting.band == LayoutBand.Board) continue;
            if (!rects.TryGetValue(setting.band, out var rect)) continue;

            float half = setting.band == LayoutBand.WordRow ? inner : outer;
            float left = CanvasXOf(board.BoardCenter.x - half);
            float right = CanvasXOf(board.BoardCenter.x + half);

            left = Mathf.Max(left, usable.xMin - sideMargin);
            right = Mathf.Min(right, usable.xMax + sideMargin);
            if (right - left <= 0f) continue;

            var fitted = new Rect(left, rect.y, right - left, rect.height);
            rects[setting.band] = fitted;
            Place(Container(setting.band), fitted);
        }
    }

    /// <summary>
    /// Lays the 🚧 page behind the board, stretched UP to take in the word row,
    /// so the word you're spelling sits on the same page as the grid.
    ///
    /// The sides and bottom are the board's backing plus the page margin; the
    /// top is the word row band's top edge. Called after FrameBoard, because the
    /// word row's position in WORLD units only exists once the camera is framed.
    /// </summary>
    private void LayPage()
    {
        PageTopWorld = float.NaN;

        var background = board == null ? null : board.Background;
        if (background == null || !background.DrawsPage) return;

        float cell = board.CellSize;
        float margin = background.PageMargin(cell);
        Vector2 backing = board.BackingSize;
        Vector2 centre = board.BoardCenter;

        float left = centre.x - backing.x * 0.5f - margin;
        float right = centre.x + backing.x * 0.5f + margin;
        float bottom = centre.y - backing.y * 0.5f - margin;
        float top = centre.y + backing.y * 0.5f + margin;

        Rect wordRow = WorldRectOf(LayoutBand.WordRow);
        if (wordRow.height > 0f) top = Mathf.Max(top, wordRow.yMax);

        background.SetPage(new Rect(left, bottom, right - left, top - bottom), cell);
        PageTopWorld = background.PaperTop;   // above `top` for the book sprite's drawn border
    }

    /// <summary>
    /// Zooms and pans the camera so the board fills the board band.
    ///
    /// Taking the LARGER of the two world-per-pixel ratios is what makes the
    /// board fit INSIDE the band on any screen: on a 19.5:9 phone the width binds
    /// and the board is full width; on 16:9 the height binds and the board is
    /// narrower with a margin either side. Taking the smaller would overflow the
    /// band on one axis.
    ///
    /// ⚠️ THE FRAME IS LOPSIDED WHEN THERE'S A PAGE (2026-10-26). Below the grid
    /// the book needs its full cover; ABOVE it the page just carries on up into
    /// the word row, so only the page margin is needed there. Framing the cover's
    /// width above as well left most of a cell of empty paper between the word
    /// and the grid once the book became a sprite with a thick cover. And the
    /// frame is pinned to the band's TOP, not centred, so when the width binds
    /// the board sits right under the word row and the spare height goes below
    /// the book (where the ribbon hangs), not between the word and the grid.
    /// </summary>
    private void FrameBoard()
    {
        if (sceneCamera == null || board == null) return;
        if (!rects.TryGetValue(LayoutBand.Board, out var band)) return;
        if (band.width <= 0f || band.height <= 0f) return;

        // The book, not the tiles: framing the tiles alone let the border and
        // the page hang off the sides of the band.
        Vector2 size = board.OuterSize;
        if (size.x <= 0f || size.y <= 0f) return;

        float scale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
        float bandWidthPx = band.width * scale;
        float bandHeightPx = band.height * scale;
        if (bandWidthPx <= 0f || bandHeightPx <= 0f) return;

        // Below the grid: the whole extent (cover, and the sprite's ribbon).
        // Above it: just the page margin when a page carries on up to the word
        // row — the cover's up there, above the word, not above the grid.
        float backing = board.BackingSize.y;
        float below = (size.y - backing) * 0.5f;
        var background = board.Background;
        float above = background != null && background.DrawsPage
            ? background.PageMargin(board.CellSize)
            : below;
        float height = backing + below + above;

        float worldPerPixel = Mathf.Max(height / bandHeightPx, size.x / bandWidthPx);

        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = worldPerPixel * Screen.height * 0.5f;

        // Pin the frame's top to the band's top: the band's top in screen
        // pixels, against the screen's own centre, is how far the camera has to
        // sit below the frame's top edge. When the height binds the frame fills
        // the band and this is the same as centring it.
        float frameTop = board.BoardCenter.y + backing * 0.5f + above;
        float bandTopPx = band.yMax * scale;
        float offsetWorld = (bandTopPx - Screen.height * 0.5f) * worldPerPixel;

        sceneCamera.transform.position = new Vector3(
            board.BoardCenter.x,
            frameTop - offsetWorld,
            sceneCamera.transform.position.z);
    }
}
