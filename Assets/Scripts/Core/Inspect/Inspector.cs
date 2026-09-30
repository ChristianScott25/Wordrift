using System;
using UnityEngine;

/// <summary>
/// THE INFO BOX BUS — "the player wants to know what this is".
///
/// A static channel rather than a reference on each widget, for the same reason
/// GameEvents and RunState.Changed are: the things that raise it live in three
/// unrelated places (world-space tiles under ChainController, canvas widgets in
/// the round header, and a different scene entirely), and exactly one thing
/// listens. Wiring that up per caller would be six Inspector fields to get
/// wrong.
///
/// ⚠️ SUBSCRIBE IN OnEnable, UNSUBSCRIBE IN OnDisable, or a listener outlives its
/// scene. Same rule GameLayout.Changed and RunState.Changed carry.
///
/// ⚠️ ONE InspectInfo, REFILLED. The instance handed to Opened is reused on the
/// next Show, so a listener must draw from it immediately and must never hold on
/// to it. That is deliberate: this runs off a touch, and a box that allocated
/// per tap would allocate per tap forever.
/// </summary>
public static class Inspector
{
    /// <summary>
    /// Read this, and it sits under THIS — the screen-space rectangle of the
    /// thing that was touched, so the box can tuck under it the way Balatro's
    /// does rather than appear under the finger that is covering it.
    /// </summary>
    public static event Action<InspectInfo, Rect> Opened;

    /// <summary>Put it away.</summary>
    public static event Action Closed;

    private static readonly InspectInfo info = new();

    /// <summary>
    /// Opens the box on something that can describe itself. A null thing closes
    /// the box instead of opening an empty one.
    /// </summary>
    public static void Show(IInspectable thing, Rect screenRect)
    {
        if (thing == null) { Hide(); return; }

        info.Clear();
        thing.Describe(info);
        Opened?.Invoke(info, screenRect);
    }

    /// <summary>
    /// Opens the box on words the caller already has.
    ///
    /// The round header is the case this exists for: ModeStatus already carries
    /// the librarian's name and its power text, including whatever THIS round
    /// rolled (the Censor's banned letter), and the librarian asset alone cannot
    /// say that — Librarian.PowerFor needs the round's note. Asking the asset
    /// would print the generic rule next to a banner showing the specific one.
    /// </summary>
    public static void ShowText(string title, string body, Rect screenRect)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            Hide();
            return;
        }

        info.Clear();
        info.Title = title;
        info.Body = body;
        Opened?.Invoke(info, screenRect);
    }

    public static void Hide() => Closed?.Invoke();

    /// <summary>
    /// A canvas widget's rectangle in SCREEN pixels.
    ///
    /// ⚠️ Only correct for a Screen Space - Overlay canvas, which is what both
    /// this project's canvases are. In overlay mode a canvas's world coordinates
    /// ARE screen pixels, so the world corners need no conversion at all. On a
    /// Screen Space - Camera canvas they would, and this would quietly return
    /// numbers in the wrong units. (The canvas cannot become Screen Space -
    /// Camera anyway: BookmarkCard's drag reads pressEventCamera, which is null
    /// only in overlay mode.)
    /// </summary>
    public static Rect ScreenRectOf(RectTransform rect)
    {
        if (rect == null) return new Rect();

        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        // 0 is bottom-left, 2 is top-right.
        return new Rect(corners[0].x, corners[0].y,
                        corners[2].x - corners[0].x,
                        corners[2].y - corners[0].y);
    }

    /// <summary>
    /// A world-space sprite's rectangle in SCREEN pixels — what a board tile
    /// needs, since the board is sprites and not UI.
    /// </summary>
    public static Rect ScreenRectOf(Bounds worldBounds, Camera cam)
    {
        if (cam == null) return new Rect();

        Vector3 min = cam.WorldToScreenPoint(worldBounds.min);
        Vector3 max = cam.WorldToScreenPoint(worldBounds.max);

        // WorldToScreenPoint doesn't promise min stays below max once a camera
        // can be rotated, and the alternative is a box with a negative height
        // that lands off screen.
        float x0 = Mathf.Min(min.x, max.x), x1 = Mathf.Max(min.x, max.x);
        float y0 = Mathf.Min(min.y, max.y), y1 = Mathf.Max(min.y, max.y);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }
}
