using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// THE INFO BOX — a small card that says what the thing you just touched is,
/// tucked under it, and stays until you do something else.
///
/// It listens to Inspector and draws whatever it is handed. It knows nothing
/// about tiles, bookmarks or items: everything describes itself (IInspectable),
/// which is why adding a fifth kind of thing to read costs this file nothing.
///
/// ⚠️ EVERY GRAPHIC IN IT MUST HAVE raycastTarget OFF, AND THAT IS NOT A DETAIL.
/// The box floats over the board, and ChainController refuses to start a word
/// whenever EventSystem.IsPointerOverGameObject() is true on the press frame.
/// One raycast target here and word selection stops working entirely — not
/// degraded, dead. Nothing in the box is interactive, so there is no reason for
/// one; RefuseRaycasts enforces it at runtime rather than trusting the scene.
///
/// ⚠️ IT IS RAISED TO THE FRONT EVERY TIME IT OPENS. GameLayout creates its band
/// containers as canvas children AT RUNTIME, in GameSession.Awake, so they are
/// added after anything the scene already held — and sibling order is draw order
/// on a Canvas. A box left where the generator put it draws underneath the HUD.
///
/// ⚠️ root MUST NOT be this GameObject. Switching ourselves off would stop us
/// hearing the event that would turn us back on — the rule GameOverPanel and the
/// items box both already log an error about.
/// </summary>
public class InspectBoxWidget : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("The card, switched on and off. Must be a CHILD, never this object.")]
    [SerializeField] private GameObject root;

    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;

    [Tooltip("Where the quality chips go. They are built in code, because how " +
             "many there are is the thing being read's business.")]
    [SerializeField] private RectTransform tagRow;

    [Tooltip("One chip's background. Left empty, chips draw as plain text. " +
             "It is the same sprite as the card's plate, 9-sliced.")]
    [SerializeField] private Sprite chipSprite;

    // ⚠️ EVERY FIELD BELOW IS REWRITTEN BY InspectBoxSetup ON EVERY RUN of
    // Word Crush/Set Up Game Layout, because there is a box in each of two scenes
    // and a value tuned on one of them is a value the other doesn't have. Drag
    // them here to find something you like — OnValidate redraws the box as you
    // go — then put the number in that script, which is where it survives.
    [Header("Look — set by InspectBoxSetup, not preserved")]
    [SerializeField] private Color chipColor = new Color(0.47f, 0.60f, 0.82f, 1f);
    [SerializeField] private Color chipTextColor = Color.white;
    [SerializeField] private float chipFontSize = 26f;

    [Tooltip("How big the drawn border and corners come out. HIGHER IS CHUNKIER: " +
             "1 draws the art's 8-pixel corner as 8 canvas units, 2.5 draws it as " +
             "20. Drives the card and the chips together.")]
    [SerializeField] private float borderScale = 2.5f;

    [Header("Placement")]
    [Tooltip("Gap in canvas units between the thing being read and the box.")]
    [SerializeField] private float gap = 14f;

    [Tooltip("How close to the screen edge the box may get.")]
    [SerializeField] private float screenMargin = 16f;

    private readonly List<GameObject> chips = new();
    private RectTransform self;
    private RectTransform canvasRect;
    private Canvas canvas;

    // The frame the box opened on. The press that opens it would otherwise be
    // seen by our own Update as "the player did something else" and close it
    // again — on the very same frame, so the box would never appear at all.
    private int openedFrame = -1;
    private bool showing;

    private void Awake()
    {
        self = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = (RectTransform)canvas.transform;

        if (root == gameObject)
        {
            Debug.LogError("InspectBoxWidget's 'root' must be a child object, not " +
                           "itself — switching ourselves off would stop us hearing " +
                           "the event that turns us back on.", this);
            root = null;
        }

        // Settled once. Place() then only moves it — reading self.rect.width
        // after changing anchors would read whatever the new anchoring implied
        // rather than the width the box was built with.
        self.anchorMin = Vector2.zero;
        self.anchorMax = Vector2.zero;
        self.pivot = new Vector2(0.5f, 1f);          // hangs DOWN from its top edge

        RefuseRaycasts();
        ApplySliceSettings();
        Hide();
    }

#if UNITY_EDITOR
    // So dragging borderScale in the Inspector redraws the box immediately
    // instead of only once the game is running. The whole point of the field is
    // that it gets tuned by eye.
    private void OnValidate() => ApplySliceSettings();
#endif

    /// <summary>
    /// Dresses the card's plate and every chip as 9-SLICED art at one shared
    /// corner size.
    ///
    /// ⚠️ ONE CORNER SIZE REACHES BOTH THE CARD AND THE CHIPS, and that is the
    /// point of this method being public. The card's plate is built by
    /// InspectBoxSetup and the chips are built at runtime by this class, so the
    /// number has two call sites — and two call sites with their own copies is
    /// how the chips end up with a visibly different corner radius from the box
    /// they sit in. The setup script writes the value onto this object and then
    /// asks for this, rather than dressing the plate itself.
    ///
    /// Unity's own knob is pixelsPerUnitMultiplier, which DIVIDES the border, so
    /// bigger means smaller. It is inverted here because this field exists to be
    /// dragged in the Inspector, and a slider that shrinks things as it goes up is
    /// a slider nobody tunes correctly the first time.
    /// </summary>
    public void ApplySliceSettings()
    {
        if (root != null) DressPlate(root.GetComponent<Image>());

        for (int i = 0; i < chips.Count; i++)
            if (chips[i] != null) DressPlate(chips[i].GetComponent<Image>());
    }

    private void DressPlate(Image image)
    {
        if (image == null || image.sprite == null) return;

        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, borderScale);
    }

    private void OnEnable()
    {
        Inspector.Opened += OnOpened;
        Inspector.Closed += Hide;
        GameEvents.RoundStarted += Hide;
        GameEvents.RoundEnded += OnRoundEnded;
        GameEvents.WordSubmitted += OnWordSubmitted;
    }

    private void OnDisable()
    {
        Inspector.Opened -= OnOpened;
        Inspector.Closed -= Hide;
        GameEvents.RoundStarted -= Hide;
        GameEvents.RoundEnded -= OnRoundEnded;
        GameEvents.WordSubmitted -= OnWordSubmitted;
    }

    /// <summary>
    /// Nothing in this box is interactive, and a raycast target in it would stop
    /// the board working. Enforced here rather than left to whoever edits the
    /// scene next, because the symptom — tiles simply stop selecting — points
    /// nowhere near a decorative panel nobody touched.
    /// </summary>
    private void RefuseRaycasts()
    {
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            if (graphics[i] != null) graphics[i].raycastTarget = false;
    }

    /// <summary>
    /// Closes on the next press that isn't the one that opened it — "stays until
    /// you do something else", where doing something else always starts with
    /// putting a finger down.
    ///
    /// Polled rather than driven by a backdrop, deliberately: a full-screen
    /// backdrop is the obvious way to catch the tap, and it is exactly the
    /// raycast target that would kill word selection.
    /// </summary>
    private void Update()
    {
        if (!showing) return;
        if (Time.frameCount == openedFrame) return;

        var pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame) Hide();
    }

    private void OnRoundEnded(RoundSummary summary) => Hide();

    private void OnWordSubmitted(WordResult result) => Hide();

    private void OnOpened(InspectInfo info, Rect screenRect)
    {
        if (root == null || info == null) return;

        if (titleLabel != null) titleLabel.text = info.Title;
        if (bodyLabel != null) bodyLabel.text = info.Body;
        BuildChips(info.Tags);

        root.SetActive(true);
        showing = true;
        openedFrame = Time.frameCount;

        // Raised every time, not once in the editor — see the class comment.
        transform.SetAsLastSibling();

        // The layout has to settle before the box can be placed, because where
        // it goes depends on how tall it came out — a four-line description and
        // a one-line one tuck under the same tile at different heights.
        LayoutRebuilder.ForceRebuildLayoutImmediate(self);
        Place(screenRect);
    }

    private void Hide()
    {
        showing = false;
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// Puts the box beside the thing being read, and keeps it on screen.
    ///
    /// ⚠️ IT GOES ABOVE BY DEFAULT, NOT BELOW, AND THAT IS THE WHOLE POINT. You
    /// hold a phone from the bottom, so a box drawn under the thing you just
    /// touched is a box drawn behind your own hand. It used to prefer below and
    /// flip up only when it physically wouldn't fit, which meant every tile on
    /// the board put its description under your finger.
    ///
    /// Preferring above also means there is no threshold to tune. The board
    /// occupies 43%-92% of the screen and a box is roughly a ninth of the screen
    /// tall, so everything from the bookmarks down has room above it and reads
    /// upward; only the round header at the very top runs out of ceiling and
    /// falls back to drawing below, on its own, with nothing to configure. A
    /// percentage threshold would instead cut somewhere through the board, and
    /// one row of tiles behaving differently from the four under it reads as a
    /// bug rather than as a rule. His call, 2026-09-30.
    /// </summary>
    private void Place(Rect screenRect)
    {
        if (canvasRect == null || canvas == null) return;

        float scale = canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;

        // A Screen Space - Overlay canvas measures in screen pixels divided by
        // its scale factor, which is the same conversion GameLayout.CanvasYOf
        // makes — the only two places the camera and the canvas are reconciled.
        float itemCentreX = screenRect.center.x / scale;
        float itemBottomY = screenRect.yMin / scale;
        float itemTopY = screenRect.yMax / scale;

        float width = self.rect.width;
        float height = self.rect.height;
        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;

        // ⚠️ Every range here is built so the low bound can never exceed the
        // high one. Mathf.Clamp with min > max silently returns the MIN, so a box
        // wider or taller than the screen would be pinned to a corner with no
        // hint that the range was nonsense.
        float halfWidth = width * 0.5f;
        float minX = halfWidth + screenMargin;
        float maxX = Mathf.Max(minX, canvasWidth - halfWidth - screenMargin);
        float x = Mathf.Clamp(itemCentreX, minX, maxX);

        // Both of these are the box's TOP edge, because the pivot is (0.5, 1).
        float above = itemTopY + gap + height;
        float below = itemBottomY - gap;

        float highest = canvasHeight - screenMargin;
        float lowest = Mathf.Min(height + screenMargin, highest);

        float y;
        if (above <= highest) y = above;              // the default
        else if (below >= lowest) y = below;

        // Neither side fits, which takes a box about as tall as the screen. Take
        // the one that misses by less and let the clamp do the rest — the old
        // code flipped blindly here, so a tall box could be sent to the side with
        // LESS room and end up jammed against the margin.
        else y = (above - highest) <= (lowest - below) ? above : below;

        y = Mathf.Clamp(y, lowest, highest);

        self.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>
    /// The quality chips. Pooled rather than rebuilt: this runs off a touch, and
    /// a tile with three badges would otherwise make and destroy three
    /// GameObjects every time it was read.
    /// </summary>
    private void BuildChips(List<string> tags)
    {
        if (tagRow == null) return;

        int wanted = tags == null ? 0 : tags.Count;

        while (chips.Count < wanted) chips.Add(MakeChip($"Tag {chips.Count}"));

        for (int i = 0; i < chips.Count; i++)
        {
            bool used = i < wanted;
            chips[i].SetActive(used);
            if (!used) continue;

            var label = chips[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = tags[i];
        }

        tagRow.gameObject.SetActive(wanted > 0);
    }

    private GameObject MakeChip(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image),
                                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(tagRow, false);

        var image = go.GetComponent<Image>();
        if (chipSprite != null) image.sprite = chipSprite;
        image.color = chipColor;
        image.raycastTarget = false;
        DressPlate(image);

        var group = go.GetComponent<HorizontalLayoutGroup>();

        // ⚠️ THE PADDING IS WHAT KEEPS A CHIP BIGGER THAN ITS OWN CORNERS. A
        // sliced image smaller than its borders added together does not overflow
        // — Unity quietly shrinks the border to fit — so a cramped chip comes out
        // with a tighter corner radius than the card it sits in, which reads as
        // sloppy art rather than as a layout number being too small. At the
        // default borderScale the corners want about 40 units in each direction,
        // and "1 PT" at font size 26 does not reach that on its own.
        group.padding = new RectOffset(22, 22, 14, 14);
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var text = new GameObject("Label", typeof(RectTransform));
        text.transform.SetParent(go.transform, false);

        // A new TMP_Text takes whatever font is TMP's DEFAULT when it is created,
        // which Word Crush/Create Font Asset has already set to the game's one
        // typeface. That is the whole reason that default is set.
        var tmp = text.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.fontSize = chipFontSize;
        tmp.color = chipTextColor;
        tmp.raycastTarget = false;

        return go;
    }
}
