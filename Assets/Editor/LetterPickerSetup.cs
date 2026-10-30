using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🚧 Builds the letter picker (2026-10-29) — what Correction Fluid opens to ask
/// which letter. Called by Word Crush > Set Up Game Layout.
///
/// The pause view's shell (BagViewSetup's backdrop, plate and close button) with
/// a grid where the MAIN MENU button would be. The grid holds ONE letter button,
/// dressed like PLAY/DISCARD and switched off; LetterPickerWidget copies it 26
/// times at runtime.
///
/// ⚠️ PARENTED STRAIGHT TO THE CANVAS, NEVER INTO A LAYOUT BAND, and ⚠️ IF THIS
/// IS EVER RETIRED, ITS NAME GOES IN GameLayoutSetup.Retired — its backdrop is a
/// full-screen raycast target, and one left behind in the scene kills word
/// selection. Same rules as the pause view.
/// </summary>
public static class LetterPickerSetup
{
    private const string ViewName = "Letter Picker";

    private const int Columns = 6;
    private static readonly Vector2 Cell = new Vector2(120f, 120f);
    private static readonly Vector2 Spacing = new Vector2(18f, 18f);
    private static readonly Color LetterColor = new Color(0.95f, 0.88f, 0.70f, 1f);

    internal static LetterPickerWidget Build(Canvas canvas, GameSession session)
    {
        if (canvas == null) return null;

        var widget = Object.FindFirstObjectByType<LetterPickerWidget>(FindObjectsInactive.Include);
        GameObject outer;

        if (widget != null && !PrefabUtility.IsPartOfPrefabInstance(widget.gameObject))
        {
            outer = widget.gameObject;
        }
        else
        {
            outer = new GameObject(ViewName, typeof(RectTransform), typeof(LetterPickerWidget));
            outer.transform.SetParent(canvas.transform, false);
            widget = outer.GetComponent<LetterPickerWidget>();
        }

        BagViewSetup.Fill((RectTransform)outer.transform);

        // The listener lives on the outer object, which stays active; only the
        // inner root is shown and hidden.
        var root = BagViewSetup.Slot(outer.transform, "Root");
        BagViewSetup.Fill((RectTransform)root.transform);

        BagViewSetup.BuildBackdrop(root.transform);
        var panel = BagViewSetup.BuildPanel(root.transform);

        var title = BagViewSetup.Label(panel.transform, "Title", 40, TextAlignmentOptions.Left,
                                       BagViewSetup.InkColor, "PICK A LETTER");
        BagViewSetup.HeaderBand(title.rectTransform, 0f, 1f, BagViewSetup.Pad,
                                BagViewSetup.Pad * 2f + BagViewSetup.CloseSize);

        var close = BagViewSetup.BuildClose(panel.transform);
        var grid = BuildGrid(panel.transform);
        var template = BuildTemplate(grid);

        WordCrushSetup.SetRef(widget, "root", root);
        WordCrushSetup.SetRef(widget, "session", session);
        WordCrushSetup.SetRef(widget, "closeButton", close);
        WordCrushSetup.SetRef(widget, "grid", grid);
        WordCrushSetup.SetRef(widget, "letterTemplate", template);
        WordCrushSetup.SetRef(session, "letterPicker", widget);

        outer.transform.SetAsLastSibling();
        root.SetActive(false);
        EditorUtility.SetDirty(widget);
        return widget;
    }

    /// <summary>The grid the letters go in: centred in the panel, a fixed cell, six across.</summary>
    private static RectTransform BuildGrid(Transform panel)
    {
        var go = BagViewSetup.Slot(panel, "Letters");
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -BagViewSetup.HeaderHeight * 0.5f);

        // Wide enough for exactly six cells, tall enough for five rows.
        rect.sizeDelta = new Vector2(Columns * Cell.x + (Columns - 1) * Spacing.x,
                                     5 * Cell.y + 4 * Spacing.y);

        var layout = go.GetComponent<GridLayoutGroup>() ?? go.AddComponent<GridLayoutGroup>();
        layout.cellSize = Cell;
        layout.spacing = Spacing;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Columns;
        return rect;
    }

    /// <summary>
    /// The one letter button every letter is copied from — dressed by the SAME
    /// code as PLAY and DISCARD, so retuning those retunes these.
    /// </summary>
    private static Button BuildTemplate(RectTransform grid)
    {
        var go = BagViewSetup.Slot(grid, "LetterTemplate");
        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.raycastTarget = true;
        var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();

        if (go.GetComponentInChildren<TMP_Text>(true) == null)
        {
            var label = WordCrushSetup.MakeText(go.transform, "Label", 56, TextAlignmentOptions.Center);
            WordCrushSetup.Stretch(label.gameObject);
            label.text = "A";
        }

        WordActionsSetup.Dress(button, LetterColor);
        go.SetActive(false);
        return button;
    }
}
