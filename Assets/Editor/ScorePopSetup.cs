using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the layer the score walk-through's floating numbers are drawn on.
///
/// There is nothing to author inside it — the labels are pooled and made at
/// runtime, exactly as the bookmark row makes its cards and the info box makes
/// its chips — so this is one empty object, in the right place, with the widget
/// on it.
///
/// ⚠️ IT PARENTS STRAIGHT TO THE CANVAS AND GOES LAST, like the info box. The
/// layout bands are built at runtime in GameSession.Awake, so a scene-authored
/// object ends up BEHIND them; and anything put inside a band would be stretched
/// to fill it by GameLayout.Attach, which is not what a floating label wants.
///
/// Called from GameLayoutSetup, beside InspectBoxSetup.Build, because that is
/// where canvas-level widgets are made. Running Set Up Score Tally alone will NOT
/// create it.
/// </summary>
public static class ScorePopSetup
{
    private const string LayerName = "Score Pops";

    internal static ScorePopWidget Build(Canvas canvas)
    {
        if (canvas == null) return null;

        var widget = Object.FindFirstObjectByType<ScorePopWidget>(FindObjectsInactive.Include);
        GameObject root;

        if (widget != null && !PrefabUtility.IsPartOfPrefabInstance(widget.gameObject))
        {
            root = widget.gameObject;
        }
        else
        {
            root = new GameObject(LayerName, typeof(RectTransform), typeof(ScorePopWidget));
            root.transform.SetParent(canvas.transform, false);
            widget = root.GetComponent<ScorePopWidget>();
        }

        // Fills the canvas and frames nothing: the labels place themselves in
        // screen pixels, so this is only somewhere for them to hang.
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        root.transform.SetAsLastSibling();
        EditorUtility.SetDirty(widget);
        return widget;
    }
}
