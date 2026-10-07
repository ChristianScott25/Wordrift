using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the tile bag view — the panel the bag button opens.
///
/// ⚠️ IT PARENTS STRAIGHT TO THE CANVAS, NEVER INTO A LAYOUT BAND.
/// GameLayout.Attach stretches whatever it is given to fill its band and resets
/// its scale, which is not what a full-screen modal wants. Same reason the info
/// box and the score pops sit directly under the canvas.
///
/// ⚠️ IF THIS IS EVER RETIRED, ITS NAME GOES IN GameLayoutSetup.Retired. Its
/// backdrop is a full-screen raycast target, and one of those left behind in the
/// scene is exactly what killed word selection when the item panel was removed —
/// which is the reason that list exists.
///
/// Unlike InspectBoxSetup, this one PRESERVES hand-tuned colours and sizes. The
/// info box is the one widget that doesn't, and the stated reason is that there
/// are two of them in two scenes so a value tuned on one drifts from the other.
/// There is exactly one bag view, in one scene, so the ordinary rule applies:
/// structure every run, styling only when something is new.
///
/// Its builders are internal because PauseViewSetup builds the pause screen out
/// of the SAME backdrop, plate and close button — one look, one place, so the
/// two panels can't drift apart.
/// </summary>
public static class BagViewSetup
{
    private const string ViewName = "Bag View";
    private const string CloseSpritePath = "Assets/Sprites/Gameplay UI/X.png";

    /// <summary>Panel inset from the canvas edges, as a fraction.</summary>
    private const float SideInset = 0.04f;
    private const float TopInset = 0.085f;

    /// <summary>Panel padding, the header's height, and the close button, in canvas units.</summary>
    internal const float Pad = 40f;
    internal const float HeaderHeight = 56f;
    internal const float CloseSize = 68f;

    /// <summary>
    /// A starting grid, so the panel looks right in the editor. At runtime
    /// BagViewWidget overwrites the cell and column count from its size slider
    /// and the grid's real width — which is why there is no longer a hand-done
    /// "does the row fit" sum to keep in step here. ⚠️ `Spacing` is still read:
    /// the widget takes it as the NARROWEST gap and the row gap.
    /// </summary>
    private const int Columns = 4;
    private static readonly Vector2 Cell = new Vector2(210f, 150f);
    private static readonly Vector2 Spacing = new Vector2(14f, 16f);

    /// <summary>
    /// 🚧 The size slider: a strip along the panel's bottom, a thin line, and a
    /// round knob. Borrowed sprites — the flat white square for the line and the
    /// badge circle for the knob — until it gets art of its own.
    /// </summary>
    private const string SliderLinePath = "Assets/Sprites/White Square.png";
    private const string SliderKnobPath = "Assets/Sprites/Score Multiplier - White.png";
    private const float SliderHeight = 80f;
    private const float SliderLineHeight = 12f;
    private const float SliderKnobSize = 64f;
    private const float SliderGap = 16f;
    private static readonly Color KnobColor = new Color(1f, 0.97f, 0.90f, 1f);

    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.55f);

    /// <summary>
    /// 🚧 A grey wash over the plate, because the plate is the info box's cream
    /// art and the tiles sitting on it are cream too — side by side they read as
    /// one surface with letters floating on it.
    ///
    /// It TINTS rather than replaces: the colour multiplies the sprite, so the
    /// art's black outline stays black (anything times 0 is 0) and only the
    /// cream fill moves. Both this and the borrowed plate go when the panel gets
    /// art of its own.
    /// </summary>
    private static readonly Color PanelColor = new Color(0.78f, 0.78f, 0.80f, 1f);
    internal static readonly Color InkColor = new Color(0.10f, 0.09f, 0.12f, 1f);
    private static readonly Color QuietColor = new Color(0.27f, 0.25f, 0.29f, 1f);

    internal static BagViewWidget Build(Canvas canvas, GameSession session)
    {
        if (canvas == null) return null;

        var widget = Object.FindFirstObjectByType<BagViewWidget>(FindObjectsInactive.Include);
        GameObject outer;

        if (widget != null && !PrefabUtility.IsPartOfPrefabInstance(widget.gameObject))
        {
            outer = widget.gameObject;
        }
        else
        {
            outer = new GameObject(ViewName, typeof(RectTransform), typeof(BagViewWidget));
            outer.transform.SetParent(canvas.transform, false);
            widget = outer.GetComponent<BagViewWidget>();
        }

        Fill((RectTransform)outer.transform);

        // The listener lives on the outer object, which stays active; only the
        // inner root is shown and hidden. Same split GameOverPanel uses.
        var root = Slot(outer.transform, "Root");
        Fill((RectTransform)root.transform);

        BuildBackdrop(root.transform);
        var panel = BuildPanel(root.transform);

        // Title on the left half, count on the right half, and the right half
        // stops short of the close button rather than running under it.
        var title = Label(panel.transform, "Title", 40, TextAlignmentOptions.Left, InkColor, "TILE BAG");
        HeaderBand(title.rectTransform, 0f, 0.5f, Pad, 0f);

        var count = Label(panel.transform, "Count", 34, TextAlignmentOptions.Right, QuietColor, "0 / 0");
        HeaderBand(count.rectTransform, 0.5f, 1f, 0f, Pad * 2f + CloseSize);

        var close = BuildClose(panel.transform);
        var grid = BuildScroll(panel.transform);
        var slider = BuildSizeSlider(panel.transform);

        WordCrushSetup.SetRef(widget, "root", root);
        WordCrushSetup.SetRef(widget, "session", session);
        WordCrushSetup.SetRef(widget, "grid", grid);
        WordCrushSetup.SetRef(widget, "countLabel", count);
        WordCrushSetup.SetRef(widget, "closeButton", close);
        WordCrushSetup.SetRef(widget, "sizeSlider", slider);

        var skin = AssetDatabase.LoadAssetAtPath<TileSkin>("Assets/GameData/Skins/TileSkin_White.asset");
        if (skin != null) WordCrushSetup.SetRef(widget, "skin", skin);

        outer.transform.SetAsLastSibling();
        root.SetActive(false);
        EditorUtility.SetDirty(widget);
        return widget;
    }

    /// <summary>
    /// ⚠️ THE INPUT BLOCKER, and the reason this panel needs no other pausing.
    /// A full-screen raycast target makes EventSystem.IsPointerOverGameObject()
    /// true everywhere, and ChainController returns on the press frame when it
    /// is — so tap, drag and press-and-hold all go dead while this is up, with
    /// no new state anywhere. Everywhere ELSE in this project that would be a
    /// bug; here it is the feature. It is safe only because the root is off
    /// whenever the view is closed.
    /// </summary>
    internal static void BuildBackdrop(Transform parent)
    {
        var go = Slot(parent, "Backdrop");
        Fill((RectTransform)go.transform);

        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = BackdropColor;
        image.raycastTarget = true;
    }

    internal static GameObject BuildPanel(Transform parent)
    {
        var go = Slot(parent, "Panel");

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(SideInset, TopInset);
        rect.anchorMax = new Vector2(1f - SideInset, 1f - TopInset);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();

        // 🚧 The info box's plate, borrowed, and greyed down so the cream tiles
        // read against it. Both go when this gets art of its own.
        InspectBoxSetup.StampSpriteImport(InspectBoxSetup.BoxSpritePath, InspectBoxSetup.SpriteBorder);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(InspectBoxSetup.BoxSpritePath);

        // Sliced, and deliberately not through WordCrushSetup.SetSprite — that
        // sets preserveAspect, and stretching is the entire point of a 9-slice.
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.preserveAspect = false;
        image.color = PanelColor;

        // Bigger than the info box's corners, because the panel is far bigger
        // and the same 20 units would read as a thin rounding on it.
        image.pixelsPerUnitMultiplier = 1f / 3f;

        // ⚠️ A raycast target on purpose: without it, a tap on the panel's own
        // background would fall through to the backdrop. Harmless today (the
        // backdrop does nothing) and the thing that stops a future "tap outside
        // to close" from firing on taps INSIDE the panel.
        image.raycastTarget = true;
        return go;
    }

    internal static Button BuildClose(Transform parent)
    {
        var go = Slot(parent, "Close");

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(CloseSize, CloseSize);
        rect.anchoredPosition = new Vector2(-Pad, -Pad);

        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        InspectBoxSetup.StampSpriteImport(CloseSpritePath, 0);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloseSpritePath);
        image.raycastTarget = true;

        // White, so the art's own black lines come through unchanged — a tint
        // multiplies, and the X is drawn as an outline on nothing.
        image.color = Color.white;
        image.preserveAspect = true;

        var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    /// <summary>
    /// The scrolling grid. ⚠️ The first ScrollRect in this project — there was
    /// no scrolling anywhere before this, so none of it is a pattern borrowed
    /// from somewhere nearby.
    /// </summary>
    private static GridLayoutGroup BuildScroll(Transform parent)
    {
        var scrollGo = Slot(parent, "Scroll");

        var scrollRect = (RectTransform)scrollGo.transform;
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        // The bottom leaves room for the size slider underneath.
        scrollRect.offsetMin = new Vector2(Pad, Pad + SliderHeight + SliderGap);
        scrollRect.offsetMax = new Vector2(-Pad, -(Pad + HeaderHeight + 16f));

        var viewGo = Slot(scrollGo.transform, "Viewport");
        Fill((RectTransform)viewGo.transform);

        // ⚠️ Invisible BUT a raycast target. Without it a drag that starts in
        // the gap between two tiles has nothing to hit and the panel won't
        // scroll. Safe because the backdrop already blocks everything below.
        var viewImage = viewGo.GetComponent<Image>() ?? viewGo.AddComponent<Image>();
        viewImage.color = new Color(1f, 1f, 1f, 0f);
        viewImage.raycastTarget = true;

        // RectMask2D rather than Mask: no stencil, no extra draw call, and it
        // needs nothing of the Image above beyond its rect.
        if (viewGo.GetComponent<RectMask2D>() == null) viewGo.AddComponent<RectMask2D>();

        // Pinned to the viewport's top edge and full width; its HEIGHT belongs
        // to the ContentSizeFitter below, which is what makes it taller than the
        // viewport and so gives the scroll something to do.
        var contentGo = Slot(viewGo.transform, "Content");
        var contentRect = (RectTransform)contentGo.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);
        contentRect.anchoredPosition = Vector2.zero;

        var grid = contentGo.GetComponent<GridLayoutGroup>() ?? contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = Cell;
        grid.spacing = Spacing;
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;

        // What makes the content taller than the viewport, which is what there
        // is to scroll.
        var fitter = contentGo.GetComponent<ContentSizeFitter>() ?? contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>() ?? scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 30f;
        scroll.viewport = (RectTransform)viewGo.transform;
        scroll.content = contentRect;
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbar = null;

        return grid;
    }

    /// <summary>
    /// The tile-size slider, along the bottom of the panel — where the thumb
    /// already is. Unity's own Slider with no fill bar: a line and a knob.
    ///
    /// Range, value and listener are the WIDGET's business (it owns the
    /// min/max and the remembered setting), so only the look and structure are
    /// built here. It is outside the scroll view, so the two never compete for
    /// a drag; and it sits inside the panel, over the backdrop, so it changes
    /// nothing about what the board can be touched by.
    /// </summary>
    private static Slider BuildSizeSlider(Transform parent)
    {
        bool isNew = parent.Find("Size Slider") == null;
        var go = Slot(parent, "Size Slider");

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(Pad, Pad);
        rect.offsetMax = new Vector2(-Pad, Pad + SliderHeight);

        // ⚠️ An invisible hit area the height of the whole strip. The line is
        // 12 units tall — far too thin for a finger — so a tap ANYWHERE in the
        // strip lands here and the Slider jumps the knob to it. Safe: it is
        // inside the panel, which already blocks everything under it.
        var hit = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        // The line.
        var lineGo = Slot(go.transform, "Background");
        var lineRect = (RectTransform)lineGo.transform;
        lineRect.anchorMin = new Vector2(0f, 0.5f);
        lineRect.anchorMax = new Vector2(1f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.sizeDelta = new Vector2(-SliderKnobSize, SliderLineHeight);
        lineRect.anchoredPosition = Vector2.zero;

        var line = lineGo.GetComponent<Image>() ?? lineGo.AddComponent<Image>();
        if (line.sprite == null) line.sprite = LoadSprite(SliderLinePath);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
        line.raycastTarget = false;   // the strip behind it takes the taps
        if (isNew) line.color = InkColor;

        // ⚠️ The knob's travel is inset by half a knob each side, so at either
        // end it stops flush with the line's end rather than hanging off it.
        var areaGo = Slot(go.transform, "Handle Slide Area");
        var areaRect = (RectTransform)areaGo.transform;
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.offsetMin = new Vector2(SliderKnobSize * 0.5f, 0f);
        areaRect.offsetMax = new Vector2(-SliderKnobSize * 0.5f, 0f);

        var knobGo = Slot(areaGo.transform, "Handle");
        var knobRect = (RectTransform)knobGo.transform;
        knobRect.sizeDelta = new Vector2(SliderKnobSize, 0f);

        var knob = knobGo.GetComponent<Image>() ?? knobGo.AddComponent<Image>();
        if (knob.sprite == null) knob.sprite = LoadSprite(SliderKnobPath);
        knob.preserveAspect = true;
        knob.raycastTarget = true;
        if (isNew) knob.color = KnobColor;

        var slider = go.GetComponent<Slider>() ?? go.AddComponent<Slider>();
        slider.fillRect = null;
        slider.handleRect = knobRect;
        slider.targetGraphic = knob;
        slider.direction = Slider.Direction.LeftToRight;
        // Smooth, not stepped: the tile grows under the finger. The widget
        // sets this again on Awake; it's here so the editor shows the truth.
        slider.wholeNumbers = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        // Just so the knob sits somewhere sensible in the editor — the widget
        // puts it where the player left it.
        if (isNew) slider.SetValueWithoutNotify(0.5f);

        return slider;
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"Bag view: no sprite at {path}, so the size slider draws a plain box there.");
        return sprite;
    }

    // ---- small builders -----------------------------------------------------

    /// <summary>
    /// A band across the top of the panel, between two horizontal anchor
    /// fractions and inset from each by a number of units.
    ///
    /// All four are spelled out because a single "left/right" pair that meant a
    /// fraction on one call and units on the next is exactly how the title
    /// ended up stretched across the whole header, underneath the count and the
    /// close button, with nothing on screen to say why.
    /// </summary>
    internal static void HeaderBand(RectTransform rect, float fromX, float toX,
                                   float insetLeft, float insetRight)
    {
        rect.anchorMin = new Vector2(fromX, 1f);
        rect.anchorMax = new Vector2(toX, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(insetLeft, -(Pad + HeaderHeight));
        rect.offsetMax = new Vector2(-insetRight, -Pad);
    }

    internal static void Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    internal static GameObject Slot(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>
    /// Finds or makes a label. Size, colour and text are written only when it is
    /// NEW — styling is worth tuning on a real screen and a re-run that reset it
    /// would be infuriating. Position is structure and is ours every run.
    /// </summary>
    internal static TMP_Text Label(Transform parent, string name, float size,
                                  TextAlignmentOptions align, Color color, string placeholder)
    {
        var existing = parent.Find(name);
        var label = existing != null ? existing.GetComponent<TMP_Text>() : null;

        if (label == null)
        {
            var made = WordCrushSetup.MakeText(parent, name, size, align);
            made.color = color;
            made.text = placeholder;
            label = made;
        }

        label.raycastTarget = false;
        return label;
    }
}
