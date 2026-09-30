using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds THE INFO BOX — the little card that says what the thing you just
/// touched is.
///
/// It lives in its own file rather than inside GameLayoutSetup because BOTH
/// scenes need one. The bookmark row is in the Game scene and the Shop, and a
/// tap on a card raises Inspector either way; a box in only one of them would
/// make the same gesture work in one scene and silently do nothing in the other.
///
/// ⚠️ A SIBLING ON THE CANVAS, NOT A CHILD OF ANY WIDGET. It has to reach every
/// corner of the screen — it tucks under a bookmark near the bottom and under a
/// tile in the middle — and GameLayout.Attach would otherwise trap it inside
/// whichever band its owner lives in.
///
/// ⚠️ NOTHING IN IT IS A RAYCAST TARGET, and that is not decoration. In the Game
/// scene it floats over the board, and ChainController refuses to start a word
/// whenever EventSystem.IsPointerOverGameObject() is true on the press frame —
/// so one raycast target here stops word selection working at all, which looks
/// nothing like a problem with a decorative panel. InspectBoxWidget re-asserts
/// it at runtime; this is the other half.
///
/// ⚠️ ITS SIZE COMES FROM ITS CONTENTS. The box has to know how tall it came out
/// before it can tuck under anything, so the layout groups and the
/// ContentSizeFitter are STRUCTURE and are rewritten on every run. The width,
/// the colours and the font sizes are styling and are only ever set on a box
/// that didn't exist yet — the same split GameLayoutSetup.Text draws, and for
/// the same reason.
/// </summary>
public static class InspectBoxSetup
{
    private const string BoxName = "Info Box";

    /// <summary>
    /// Every info box is this wide, in canvas units, whatever it is showing.
    ///
    /// ⚠️ WRITTEN ON EVERY RUN, unlike the colours and font sizes beside it —
    /// this one is STRUCTURE, not styling. The whole point of one shared box is
    /// that reading a tile and reading a bookmark look like the same thing, and
    /// there is a box in each of TWO scenes: leaving the width to be hand-tuned
    /// per scene is leaving them free to drift apart, which is exactly what the
    /// box exists to avoid. Change it here and both follow.
    ///
    /// 720 of the canvas's 1080 — two thirds of the screen, and about five and a
    /// half tiles, so a description gets a readable line length while a box
    /// tucked under a tile near the edge still has somewhere to sit after the
    /// clamp. Height is never set: it is measured from the contents.
    /// </summary>
    private const float BoxWidth = 720f;

    /// <summary>
    /// Makes or refreshes the box on a canvas. Returns the widget, or null if
    /// there was no canvas to put it on.
    /// </summary>
    public static InspectBoxWidget Build(Canvas canvas)
    {
        if (canvas == null) return null;

        var widget = Object.FindFirstObjectByType<InspectBoxWidget>(FindObjectsInactive.Include);
        GameObject root;

        if (widget != null && !PrefabUtility.IsPartOfPrefabInstance(widget.gameObject))
        {
            root = widget.gameObject;
        }
        else
        {
            root = new GameObject(BoxName, typeof(RectTransform), typeof(InspectBoxWidget));
            root.transform.SetParent(canvas.transform, false);
            widget = root.GetComponent<InspectBoxWidget>();
        }

        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;

        // Hangs DOWN from its top edge, which is what "tucked under the thing
        // you touched" means once the box can be any height.
        rect.pivot = new Vector2(0.5f, 1f);

        // Width is a decision; height is measured from the contents.
        //
        // ⚠️ This used to be guarded by `sizeDelta.x <= 1f` — "only set it on a
        // box that is new". That guard NEVER FIRED: a fresh RectTransform
        // defaults to 100x100, not 0x0, so every box came out 100 units wide and
        // wrapped its description to about four characters. Kept as a comment
        // because the same guard shape is correct for colours and font sizes
        // below, and the difference is only that they start out unset.
        rect.sizeDelta = new Vector2(BoxWidth, rect.sizeDelta.y);

        Column(root, padding: 0, spacing: 0f);

        var card = Slot(root.transform, "Card");
        var plate = card.GetComponent<Image>() ?? card.AddComponent<Image>();
        plate.sprite = Square();
        plate.raycastTarget = false;
        if (plate.color == Color.white) plate.color = new Color(0.14f, 0.15f, 0.19f, 0.98f);
        Column(card, padding: 18, spacing: 8f);

        var titleText = Label(Slot(card.transform, "Title"), 34, TextAlignmentOptions.Left,
                              Color.white);

        var bodyText = Label(Slot(card.transform, "Body"), 26, TextAlignmentOptions.TopLeft,
                             new Color(0.86f, 0.88f, 0.92f));
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        var tags = Slot(card.transform, "Tags");
        var row = tags.GetComponent<HorizontalLayoutGroup>() ?? tags.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        Fit(tags);

        WordCrushSetup.SetRef(widget, "root", card);
        WordCrushSetup.SetRef(widget, "titleLabel", titleText);
        WordCrushSetup.SetRef(widget, "bodyLabel", bodyText);
        WordCrushSetup.SetRef(widget, "tagRow", (RectTransform)tags.transform);
        WordCrushSetup.SetRef(widget, "chipSprite", Square());

        // Drawn over everything, whatever order the widgets were made in. The
        // widget raises it again on every open, because GameLayout adds its band
        // containers to this same canvas AT RUNTIME and so ends up on top.
        root.transform.SetAsLastSibling();

        // Closed. A box left switched on in the saved scene is the first thing
        // you would see on opening it.
        card.SetActive(false);
        return widget;
    }

    /// <summary>
    /// Stacks a slot's children top to bottom and lets it grow to fit them.
    ///
    /// Horizontal fit is deliberately Unconstrained: the box's WIDTH is a
    /// decision, not a consequence of how long the longest word in a description
    /// happens to be.
    /// </summary>
    private static void Column(GameObject slot, int padding, float spacing)
    {
        var group = slot.GetComponent<VerticalLayoutGroup>() ?? slot.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.spacing = spacing;
        group.childAlignment = TextAnchor.UpperLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        Fit(slot);
    }

    private static void Fit(GameObject slot)
    {
        var fitter = slot.GetComponent<ContentSizeFitter>() ?? slot.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// A child of a layout group. Its anchors are NOT set here on purpose — the
    /// group overwrites them every time it lays out, so anything written here
    /// would be a lie about who decides where this sits.
    /// </summary>
    private static GameObject Slot(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>
    /// ⚠️ ALIGNMENT IS APPLIED EVERY RUN; SIZE AND COLOUR ONLY WHEN THE LABEL IS
    /// NEW. Alignment is structure — it decides where in the card the text lands.
    /// Size and colour are worth hand-tuning on a real screen, and a re-run that
    /// reset them would be infuriating. Same split GameLayoutSetup.Text draws.
    /// </summary>
    private static TMP_Text Label(GameObject slot, float size, TextAlignmentOptions align, Color color)
    {
        var text = slot.GetComponent<TMP_Text>();
        if (text == null)
        {
            // A new TMP_Text takes whatever font is TMP's DEFAULT when it is
            // created, which Word Crush/Create Font Asset has already set to the
            // game's one typeface. That is the whole reason that default is set.
            text = slot.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.fontStyle = FontStyles.Bold;
            text.text = "";
        }

        text.alignment = align;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite Square() =>
        AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/White Square.png");
}
