using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🚧 Builds the pause screen — the panel the pause button opens. Called by
/// Word Crush > Set Up Game Layout.
///
/// It is the tile bag view's shell with a MAIN MENU button where the grid goes:
/// the backdrop, the plate and the close button all come from BagViewSetup's
/// own builders, so changing the look of one changes both.
///
/// ⚠️ PARENTED STRAIGHT TO THE CANVAS, NEVER INTO A LAYOUT BAND — same reason as
/// the bag view: GameLayout.Attach would stretch it into a band.
///
/// ⚠️ IF THIS IS EVER RETIRED, ITS NAME GOES IN GameLayoutSetup.Retired. Its
/// backdrop is a full-screen raycast target, and one left behind in the scene
/// kills word selection.
///
/// Structure every run, styling only when something is new — except the MAIN
/// MENU button, which is dressed every run by the same code as PLAY/DISCARD.
/// </summary>
public static class PauseViewSetup
{
    private const string ViewName = "Pause View";

    private static readonly Vector2 MenuButtonSize = new Vector2(560f, 140f);
    private static readonly Color MenuButtonColor = new Color(0.95f, 0.75f, 0.30f, 1f);

    internal static PauseViewWidget Build(Canvas canvas, GameSession session)
    {
        if (canvas == null) return null;

        var widget = Object.FindFirstObjectByType<PauseViewWidget>(FindObjectsInactive.Include);
        GameObject outer;

        if (widget != null && !PrefabUtility.IsPartOfPrefabInstance(widget.gameObject))
        {
            outer = widget.gameObject;
        }
        else
        {
            outer = new GameObject(ViewName, typeof(RectTransform), typeof(PauseViewWidget));
            outer.transform.SetParent(canvas.transform, false);
            widget = outer.GetComponent<PauseViewWidget>();
        }

        BagViewSetup.Fill((RectTransform)outer.transform);

        // The listener lives on the outer object, which stays active; only the
        // inner root is shown and hidden.
        var root = BagViewSetup.Slot(outer.transform, "Root");
        BagViewSetup.Fill((RectTransform)root.transform);

        BagViewSetup.BuildBackdrop(root.transform);
        var panel = BagViewSetup.BuildPanel(root.transform);

        var title = BagViewSetup.Label(panel.transform, "Title", 40, TextAlignmentOptions.Left,
                                       BagViewSetup.InkColor, "PAUSED");
        BagViewSetup.HeaderBand(title.rectTransform, 0f, 1f, BagViewSetup.Pad,
                                BagViewSetup.Pad * 2f + BagViewSetup.CloseSize);

        var close = BagViewSetup.BuildClose(panel.transform);
        var menu = BuildMenuButton(panel.transform);

        WordCrushSetup.SetRef(widget, "root", root);
        WordCrushSetup.SetRef(widget, "session", session);
        WordCrushSetup.SetRef(widget, "closeButton", close);
        WordCrushSetup.SetRef(widget, "menuButton", menu);

        outer.transform.SetAsLastSibling();
        root.SetActive(false);
        EditorUtility.SetDirty(widget);
        return widget;
    }

    /// <summary>
    /// MAIN MENU, centred in the panel, dressed by the SAME code as PLAY and
    /// DISCARD (WordActionsSetup.Dress) — plate, ColorBlock, greyed look, label
    /// colour and sizing — so retuning those buttons retunes this one. The
    /// greyed "board still settling" state then comes from `interactable`.
    /// </summary>
    private static Button BuildMenuButton(Transform parent)
    {
        bool isNew = parent.Find("MainMenuButton") == null;
        var go = BagViewSetup.Slot(parent, "MainMenuButton");

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        if (isNew) rect.sizeDelta = MenuButtonSize;

        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.raycastTarget = true;
        var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();

        // The words first, so Dress finds a label to style rather than making an
        // empty one.
        if (go.GetComponentInChildren<TMP_Text>(true) == null)
        {
            var label = WordCrushSetup.MakeText(go.transform, "Label", 40, TextAlignmentOptions.Center);
            WordCrushSetup.Stretch(label.gameObject);
            label.text = "MAIN MENU";
        }

        WordActionsSetup.Dress(button, MenuButtonColor);
        return button;
    }
}
