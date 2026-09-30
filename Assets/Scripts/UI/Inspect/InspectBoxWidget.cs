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

    [Tooltip("One chip's background. Left empty, chips draw as plain text.")]
    [SerializeField] private Sprite chipSprite;

    [Header("Look")]
    [SerializeField] private Color chipColor = new Color(0.30f, 0.33f, 0.42f, 1f);
    [SerializeField] private Color chipTextColor = Color.white;
    [SerializeField] private float chipFontSize = 22f;

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
        Hide();
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
    /// Tucks the box under the thing being read, and keeps it on screen.
    ///
    /// Flips above rather than being squashed when there is no room below, which
    /// is what the bottom row of the board needs — the alternative is a box half
    /// off the bottom of the phone.
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

        float y = itemBottomY - gap;
        if (y - height < screenMargin) y = itemTopY + gap + height;

        // ⚠️ Both ranges are built so the low bound can never exceed the high
        // one. Mathf.Clamp with min > max silently returns the MIN, so a box
        // wider or taller than the screen would be pinned to a corner with no
        // hint that the range was nonsense.
        float halfWidth = width * 0.5f;
        float minX = halfWidth + screenMargin;
        float maxX = Mathf.Max(minX, canvasWidth - halfWidth - screenMargin);
        float x = Mathf.Clamp(itemCentreX, minX, maxX);

        float maxY = canvasHeight - screenMargin;
        float minY = Mathf.Min(height + screenMargin, maxY);
        y = Mathf.Clamp(y, minY, maxY);

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

        var group = go.GetComponent<HorizontalLayoutGroup>();
        group.padding = new RectOffset(12, 12, 4, 4);
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
