using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the board itself: one square behind every cell a tile could occupy,
/// whether or not a tile is there right now.
///
/// Per cell rather than one stretched quad, so the backing follows whatever
/// IBoardShape produced — holes, L-shapes, a different shape per mode — with no
/// perimeter maths anywhere. Each square is drawn a little larger than a cell,
/// so neighbours overlap and their union comes out as the board slab plus a
/// uniform border. The amount above 1 is exactly the border width.
///
/// That overlap is why the colour has to be opaque: at any alpha below 1 every
/// overlap band would render twice and show up as a grid of seams.
///
/// 🚧 THE PAGE (2026-10-19). Behind the cells it can also draw a PAGE — one flat
/// rectangle with a darker COVER edge round it — which GameLayout stretches up
/// past the board to take in the word row, so the word you're spelling sits on
/// the same "page" as the grid it came from. Two flat rectangles are a stand-in
/// for a book sprite that doesn't exist yet; swapping one in later is this
/// class's business and nobody else's. The cells still draw on top, as the
/// grid's inset, which is what keeps a closed cell (The Dilapidated) visible as
/// a hole — it shows the page through it.
/// </summary>
public class BoardBackground : MonoBehaviour
{
    [Tooltip("Plain square, sharp corners, no outline. Rounded corners scallop the " +
             "board's edge where neighbours overlap, and an outline would " +
             "criss-cross the interior.")]
    [SerializeField] private Sprite cellSprite;

    [Tooltip("The board's colour. Must be fully opaque — overlapping squares would " +
             "otherwise show as a grid of darker seams.")]
    [SerializeField] private Color color = new Color(0.09f, 0.15f, 0.29f, 1f);

    [Tooltip("Square size as a multiple of one cell. Everything above 1 becomes the " +
             "border: 1.12 gives a border of 0.06 cells on every side.")]
    [Range(1f, 1.6f)][SerializeField] private float cellScale = 1.12f;

    [Tooltip("Draw order. Has to be below the tiles, whose body starts at 1.")]
    [SerializeField] private int sortingOrder = -10;

    [Tooltip("Nudge away from the camera, so nothing z-sorts in front of a tile.")]
    [SerializeField] private float depthOffset = 0.1f;

    [Header("🚧 Page (placeholder for a book sprite)")]
    [Tooltip("Draw the page and cover behind the board at all. Off = the cells alone, " +
             "as before.")]
    [SerializeField] private bool drawPage = true;

    [Tooltip("The page. Opaque, like the cells.")]
    [SerializeField] private Color pageColor = new Color(0.93f, 0.86f, 0.70f, 1f);

    [Tooltip("The book's cover, showing as an edge round the page.")]
    [SerializeField] private Color coverColor = new Color(0.40f, 0.23f, 0.14f, 1f);

    [Tooltip("Page showing round the grid, in cells.")]
    [Range(0f, 1f)][SerializeField] private float pageMargin = 0.14f;

    [Tooltip("Cover showing round the page, in cells.")]
    [Range(0f, 0.5f)][SerializeField] private float coverWidth = 0.08f;

    /// <summary>Square size as a multiple of one cell — everything above 1 is the border.</summary>
    public float CellScale => cellScale;

    /// <summary>
    /// How far the page AND its cover stick out past the cells' backing on each
    /// side, in world units. Zero when the page is off, so a caller can add it
    /// without asking.
    /// </summary>
    public float PageExtent(float cellSize) =>
        DrawsPage ? (pageMargin + coverWidth) * cellSize : 0f;

    /// <summary>How far the page alone (no cover) sticks out past the backing.</summary>
    public float PageMargin(float cellSize) => DrawsPage ? pageMargin * cellSize : 0f;

    /// <summary>
    /// Will a page actually be drawn? Needs the sprite as well as the switch —
    /// without it SetPage draws nothing, and a layout that still made room for
    /// an invisible page would pin the bookmarks to thin air.
    /// </summary>
    public bool DrawsPage => drawPage && cellSprite != null;

    private SpriteRenderer page;
    private SpriteRenderer cover;

    private readonly List<SpriteRenderer> cells = new();
    private Transform root;
    private bool warnedAboutAlpha;

    /// <summary>
    /// Lays a square under each of these world positions. Safe to call again —
    /// renderers are reused, so restarting a round doesn't churn 50 objects.
    /// </summary>
    public void Rebuild(IReadOnlyList<Vector3> cellCenters, float cellSize)
    {
        if (cellSprite == null)
        {
            Debug.LogWarning("BoardBackground has no cell sprite — run " +
                             "'Word Crush/Set Up Board Background'.", this);
            return;
        }

        if (color.a < 1f && !warnedAboutAlpha)
        {
            warnedAboutAlpha = true;
            Debug.LogWarning("BoardBackground colour isn't opaque, so the squares' " +
                             "overlaps will show as seams across the board.", this);
        }

        EnsureRoot();

        float spriteSize = Mathf.Max(cellSprite.bounds.size.x, cellSprite.bounds.size.y);
        if (spriteSize <= 0f) spriteSize = 1f;
        float scale = cellSize * cellScale / spriteSize;

        while (cells.Count < cellCenters.Count) cells.Add(CreateCell());

        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell == null) continue;

            bool used = i < cellCenters.Count;
            cell.gameObject.SetActive(used);
            if (!used) continue;

            var center = cellCenters[i];
            cell.transform.position = new Vector3(center.x, center.y, center.z + depthOffset);
            cell.transform.localScale = Vector3.one * scale;
            cell.sprite = cellSprite;
            cell.color = color;
            cell.sortingOrder = sortingOrder;
        }
    }

    /// <summary>
    /// Lays the page over `area` (world units, the page itself — the cover is
    /// drawn round it) behind everything the board draws. Safe to call on every
    /// layout pass. Does nothing when the page is switched off.
    /// </summary>
    public void SetPage(Rect area, float cellSize)
    {
        if (!drawPage || cellSprite == null || area.width <= 0f || area.height <= 0f)
        {
            if (page != null) page.gameObject.SetActive(false);
            if (cover != null) cover.gameObject.SetActive(false);
            return;
        }

        EnsureRoot();
        if (page == null) page = CreateCell("Page");
        if (cover == null) cover = CreateCell("Cover");

        float edge = coverWidth * cellSize;
        Place(page, area, pageColor, sortingOrder - 1);
        Place(cover, new Rect(area.x - edge, area.y - edge,
                              area.width + edge * 2f, area.height + edge * 2f),
              coverColor, sortingOrder - 2);
    }

    /// <summary>Stretches one flat square over a world rect.</summary>
    private void Place(SpriteRenderer renderer, Rect area, Color tint, int order)
    {
        renderer.gameObject.SetActive(true);
        renderer.sprite = cellSprite;
        renderer.color = tint;
        renderer.sortingOrder = order;

        Vector2 size = cellSprite.bounds.size;
        if (size.x <= 0f || size.y <= 0f) size = Vector2.one;

        // The board's own z, nudged back a little further each layer.
        float z = transform.position.z + depthOffset * (sortingOrder - order + 1);
        renderer.transform.position = new Vector3(area.center.x, area.center.y, z);
        renderer.transform.localScale = new Vector3(area.width / size.x, area.height / size.y, 1f);
    }

    private void EnsureRoot()
    {
        if (root != null) return;
        root = new GameObject("Background").transform;
        root.SetParent(transform, false);
    }

    private SpriteRenderer CreateCell(string name = "Cell")
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(root, false);
        return go.GetComponent<SpriteRenderer>();
    }
}
