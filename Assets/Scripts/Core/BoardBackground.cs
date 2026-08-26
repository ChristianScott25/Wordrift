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

    private void EnsureRoot()
    {
        if (root != null) return;
        root = new GameObject("Background").transform;
        root.SetParent(transform, false);
    }

    private SpriteRenderer CreateCell()
    {
        var go = new GameObject("Cell", typeof(SpriteRenderer));
        go.transform.SetParent(root, false);
        return go.GetComponent<SpriteRenderer>();
    }
}
