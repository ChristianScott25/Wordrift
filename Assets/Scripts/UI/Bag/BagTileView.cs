using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One row of the bag view: a tile, drawn as UI, with how many of it are left.
///
/// ⚠️ IT IS A SECOND WAY TO DRAW A TILE, AND THAT IS A REAL COST. The word row
/// draws REAL Tile prefabs precisely so there is no second copy — but a Tile is
/// a world-space SpriteRenderer with world-space TMP labels, and inside a canvas
/// it would be ignored by every layout group, unclipped by the scroll mask,
/// drawn underneath the overlay canvas entirely, and scaled twice over (its
/// baseScale is in world units, times the canvas factor on top). A scrolling
/// panel wants canvas objects, so canvas objects is what this is.
///
/// What keeps the cost down: this reads the SAME TileSkin fields (baseSprite,
/// letterColor, scoreColor, badgeSprite) and the SAME TileModifier fields
/// (badgeLabel, badgeColor, badgeTextColor) a board tile reads, so a skin swap
/// or a badge recolour still reaches both. Only the LAYOUT is duplicated — and
/// this one is simple fractions of a known square, where Tile.LayOutLabels has
/// to derive everything from sprite bounds because a skin can swap the body for
/// a sprite of any size.
///
/// ⚠️ EVERY SIZE COMES FROM `bodySize`, NEVER FROM A rect.height. These are
/// built and bound inside a GridLayoutGroup, which has not run when Bind is
/// called — reading a rect here would measure a layout that hasn't happened and
/// silently come out as zero on the first open.
///
/// ⚠️ NO DRAG HANDLERS, DELIBERATELY. The drag belongs to the ScrollRect this
/// sits inside; implementing IBeginDragHandler here would swallow it and the
/// panel would stop scrolling. That is also why there is no `dragged` flag like
/// BookmarkCard's — the press target (this) and the drag target (the scroll
/// view) are different objects, so Unity drops eligibleForClick itself when a
/// drag crosses that boundary.
/// </summary>
public class BagTileView : MonoBehaviour, IPointerClickHandler
{
    /// <summary>How far each badge sits from the last, as a fraction of a badge. Matches Tile.badgeSpacing.</summary>
    private const float BadgeStep = 0.7f;

    /// <summary>One badge, as a fraction of the tile body.</summary>
    private const float BadgeSize = 0.36f;

    private TileSpec spec;
    private TileSkin skin;
    private float bodySize;

    private Image body;
    private TMP_Text letterLabel;
    private TMP_Text scoreLabel;
    private TMP_Text countLabel;

    private readonly List<Image> badgeCircles = new();
    private readonly List<TMP_Text> badgeLabels = new();

    /// <summary>
    /// Builds one, children and all. Made in code rather than authored as a
    /// prefab for the same reason the bookmark row makes its cards and the info
    /// box makes its chips: how many there are is the bag's business, not the
    /// scene's.
    /// </summary>
    /// <param name="cell">The grid cell this fills, in canvas units.</param>
    /// <param name="gap">Between the tile and its count.</param>
    public static BagTileView Create(Transform parent, Vector2 cell, float gap)
    {
        var go = new GameObject("Bag Tile", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var view = go.AddComponent<BagTileView>();
        view.Build();
        view.Resize(cell, gap);
        return view;
    }

    /// <summary>Makes the children, once. Every size and position is Resize's job.</summary>
    private void Build()
    {
        body = Make<Image>(transform, "Body");

        // ⚠️ THE ONLY RAYCAST TARGET IN HERE. Anything else would eat the tap
        // meant for this — the same rule ConsumableSlotView's label follows.
        body.raycastTarget = true;

        letterLabel = Text(body.transform, "Letter", TextAlignmentOptions.Center);
        Stretch(letterLabel.rectTransform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));

        // ⚠️ Auto-sized rather than indexing a shrink table the way Tile does.
        // A choice tile's face is five characters ("A/E/I") and would run off
        // the art at full size. Tile rejects auto-sizing because its labels are
        // world-space and their transform is rescaled to cancel the tile's own
        // fit-scale, so there is no sized rect to fit to — here the rect IS
        // sized, so this is the simpler right answer and one less table to keep
        // in step.
        letterLabel.enableAutoSizing = true;
        letterLabel.fontSizeMin = 8f;

        scoreLabel = Text(body.transform, "Score", TextAlignmentOptions.BottomRight);
        Stretch(scoreLabel.rectTransform, new Vector2(0.50f, 0.04f), new Vector2(0.94f, 0.42f));

        // Auto-sized for the same reason the letter is: "x13" fits at this size
        // today, but tileBagSize is one Inspector field and "x104" would not.
        countLabel = Text(transform, "Count", TextAlignmentOptions.Left);
        countLabel.enableAutoSizing = true;
        countLabel.fontSizeMin = 8f;
    }

    /// <summary>
    /// Sizes everything to a grid cell. Called once on creation and again
    /// whenever the bag view's size slider moves, so it must be safe to repeat:
    /// it only ever SETS sizes, never scales what is already there.
    /// </summary>
    /// <param name="cell">The grid cell this fills, in canvas units.</param>
    /// <param name="gap">Between the tile and its count.</param>
    public void Resize(Vector2 cell, float gap)
    {
        // Square, and as big as the cell allows once the count has its share.
        bodySize = Mathf.Max(1f, Mathf.Min(cell.y, cell.x * 0.58f));

        Place(body.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
              new Vector2(bodySize, bodySize), Vector2.zero);

        letterLabel.fontSizeMax = bodySize * 0.60f;
        scoreLabel.fontSize = bodySize * 0.22f;

        Place(countLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
              new Vector2(Mathf.Max(10f, cell.x - bodySize - gap), bodySize),
              new Vector2(bodySize + gap, 0f));
        countLabel.fontSize = bodySize * 0.30f;
        countLabel.fontSizeMax = bodySize * 0.30f;

        // Badge size comes from bodySize too, and they were placed for the old one.
        if (spec != null) DrawBadges(skin);
    }

    /// <summary>
    /// Shows one group: what the tile is, and how many of it are left. `copies`
    /// is the whole group, so a lone tile still reads "x1" — a ragged column
    /// where some rows carry a number and others don't reads worse than one
    /// that always does.
    /// </summary>
    public void Bind(TileSpec spec, int copies, TileSkin skin, Color countColor)
    {
        this.spec = spec;
        this.skin = skin;

        if (spec == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        body.sprite = skin == null ? null : skin.baseSprite;
        body.color = Color.white;

        // Face, never Spelling: a choice tile SPELLS "*" and would hide the very
        // thing a bag view exists to show. Same trap BuildLetterPool avoids.
        letterLabel.text = spec.Face.ToUpperInvariant();
        letterLabel.color = skin == null ? Color.black : skin.letterColor;

        // A wild is worth 0 and has no points of its own, so a corner reading
        // "0" would call it worthless rather than undecided. The board tile
        // makes the same call.
        scoreLabel.text = spec.IsWild ? "" : spec.baseScore.ToString();
        scoreLabel.color = skin == null ? Color.black : skin.scoreColor;

        countLabel.text = $"x{copies}";
        countLabel.color = countColor;

        DrawBadges(skin);
    }

    /// <summary>
    /// One badge per modifier, fanned right from the top-left corner, the way a
    /// board tile draws them. Surplus badges are hidden, never destroyed — the
    /// next group usually has a similar number.
    /// </summary>
    private void DrawBadges(TileSkin skin)
    {
        var modifiers = spec.modifiers;

        float size = bodySize * BadgeSize;
        float step = size * BadgeStep;
        float inset = bodySize * 0.04f;

        int shown = 0;
        for (int i = 0; modifiers != null && i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier == null) continue;

            while (badgeCircles.Count <= shown) MakeBadge();

            var circle = badgeCircles[shown];
            var label = badgeLabels[shown];

            circle.gameObject.SetActive(true);
            circle.sprite = skin == null ? null : skin.badgeSprite;
            circle.color = modifier.badgeColor;

            Place(circle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(size, size), new Vector2(inset + shown * step, -inset));

            label.text = modifier.badgeLabel;
            label.color = modifier.badgeTextColor;
            label.fontSize = size * 0.44f;

            shown++;
        }

        for (int i = shown; i < badgeCircles.Count; i++)
            badgeCircles[i].gameObject.SetActive(false);
    }

    private void MakeBadge()
    {
        var circle = Make<Image>(body.transform, $"Badge {badgeCircles.Count}");
        circle.raycastTarget = false;

        var label = Text(circle.transform, "Label", TextAlignmentOptions.Center);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);

        badgeCircles.Add(circle);
        badgeLabels.Add(label);
    }

    /// <summary>
    /// Says what this tile is. The only thing a bag tile does — it is a picture
    /// of something you own, not something you can play.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // A scroll is not a tap. Belt and braces: Unity should already have
        // dropped eligibleForClick when the drag went to the scroll view.
        if (eventData != null && eventData.dragging) return;
        if (spec == null) return;

        // The BODY, not the whole cell: the cell includes the "x3" to the right,
        // so anchoring to it would sit the box off-centre from the tile it is
        // describing.
        Inspector.Show(spec, Inspector.ScreenRectOf(body.rectTransform));
    }

    // ---- small builders -----------------------------------------------------

    private static T Make<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    private static TMP_Text Text(Transform parent, string name, TextAlignmentOptions align)
    {
        // A new TMP_Text takes TMP's DEFAULT font, which Word Crush/Create Font
        // Asset has already set to the game's one typeface.
        var text = Make<TextMeshProUGUI>(parent, name);
        text.alignment = align;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;   // must not eat the tap meant for the body
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot,
                              Vector2 size, Vector2 at)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = at;
    }
}
