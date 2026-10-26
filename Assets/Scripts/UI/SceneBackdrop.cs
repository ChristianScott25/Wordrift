using UnityEngine;

/// <summary>
/// Draws one picture behind everything the camera sees, scaled to COVER the
/// view — the screen is always filled, and whatever doesn't fit is cropped off
/// evenly on both sides (a 2:3 picture on a 19.5:9 phone loses some of its left
/// and right edges, never its top or bottom).
///
/// A world-space sprite on the camera rather than a UI image, because all three
/// scenes draw their HUD on Screen Space - Overlay canvases, which always draw
/// OVER world sprites: a full-screen image on one would hide the board. Under
/// the camera, it follows GameLayout's zoom and pan with nothing to wire, and
/// it's re-fitted whenever the view changes shape.
///
/// 🚧 The picture it ships with ("Cozy Twilight Pixel Library.png") is TEMPORARY
/// AI-GENERATED art and must be replaced before any release.
///
/// ⚠️ NOT A RAYCAST TARGET AND NOT UI — it can't block a touch, so it's safe
/// behind the board, where a full-screen UI image would kill word selection.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SceneBackdrop : MonoBehaviour
{
    [Tooltip("The picture. Empty = the camera's plain background colour, as before.")]
    [SerializeField] private Sprite sprite;

    [Tooltip("Multiplied over the picture — darken it if the HUD gets hard to read.")]
    [SerializeField] private Color tint = Color.white;

    [Tooltip("Draw order. Far below everything: the board's page sits at -12.")]
    [SerializeField] private int sortingOrder = -1000;

    private Camera view;
    private SpriteRenderer image;
    private float lastSize = -1f;
    private float lastAspect = -1f;
    private Sprite lastSprite;

    private void Awake() => view = GetComponent<Camera>();

    private void LateUpdate()
    {
        if (sprite == null)
        {
            if (image != null) image.gameObject.SetActive(false);
            return;
        }

        if (image == null) image = Create();
        image.gameObject.SetActive(true);
        image.sprite = sprite;
        image.color = tint;
        image.sortingOrder = sortingOrder;

        // Cheap enough to poll, and GameLayout re-zooms on any screen change.
        if (lastSprite == sprite &&
            Mathf.Approximately(lastSize, view.orthographicSize) &&
            Mathf.Approximately(lastAspect, view.aspect))
            return;

        lastSprite = sprite;
        lastSize = view.orthographicSize;
        lastAspect = view.aspect;
        Fit();
    }

    private void Fit()
    {
        float viewHeight = view.orthographicSize * 2f;
        float viewWidth = viewHeight * view.aspect;
        Vector2 art = sprite.bounds.size;
        if (art.x <= 0f || art.y <= 0f) return;

        // The LARGER of the two scales: cover, not fit.
        float scale = Mathf.Max(viewWidth / art.x, viewHeight / art.y);
        image.transform.localScale = new Vector3(scale, scale, 1f);

        // Centred on the view, as far back as the camera can still see.
        float depth = view.nearClipPlane + (view.farClipPlane - view.nearClipPlane) * 0.9f;
        Vector2 pivotOffset = (Vector2)sprite.bounds.center * scale;
        image.transform.localPosition = new Vector3(-pivotOffset.x, -pivotOffset.y, depth);
        image.transform.localRotation = Quaternion.identity;
    }

    private SpriteRenderer Create()
    {
        var go = new GameObject("Backdrop", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        return go.GetComponent<SpriteRenderer>();
    }
}
