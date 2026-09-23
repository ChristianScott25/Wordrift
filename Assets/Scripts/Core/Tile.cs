using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// One letter tile. Behaviour and timing are serialized fields tuned on the
/// Tile prefab; the *look* comes in per-tile as a TileLook, so several skins can
/// share one prefab and be mixed on the same board.
///
/// The letter is text on top of a shared tile body — not one sprite per letter —
/// which is what lets a new look be a single asset.
/// </summary>
public class Tile : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The tile body. Its sprite comes from the TileSkin, and it's the " +
             "renderer that gets tinted for selection and modifiers.")]
    [FormerlySerializedAs("letterRenderer")]
    [SerializeField] private SpriteRenderer tileRenderer;

    [SerializeField] private SpriteRenderer badgeRenderer;

    [Tooltip("The selection box: a square drawn BEHIND the tile body, slightly " +
             "larger, so the overhang reads as a border. Behind rather than on " +
             "top because the body is opaque — a box over it would hide the " +
             "letter. Optional: a tile prefab without one still selects, it just " +
             "shows the tint and the scale-up.")]
    [SerializeField] private SpriteRenderer selectionBox;

    [Tooltip("Draws the letter itself. Position and scale are set from the tile " +
             "art at runtime, so don't hand-place it — style it and leave the " +
             "transform alone. Its font can be overridden per mode.")]
    [SerializeField] private TMP_Text letterLabel;

    [Tooltip("Prints what this letter is worth. Its position and scale are set from " +
             "the art every time the tile is initialised, so don't hand-place it — " +
             "style it (font, size, color) and leave the transform alone.")]
    [SerializeField] private TMP_Text scoreLabel;

    [Tooltip("Prints a modifier's short label (2L, 3W) over the badge circle. " +
             "Positioned from the art at runtime, same as the others.")]
    [SerializeField] private TMP_Text badgeLabel;

    [Header("Fit")]
    [Tooltip("Fraction of the cell the tile art fills. 1 = edge to edge.")]
    [Range(0.5f, 1f)][SerializeField] private float fillFraction = 0.92f;

    [Header("Corner labels")]
    [Tooltip("How far in from the bottom-right corner the score sits, as a " +
             "fraction of the tile.")]
    [FormerlySerializedAs("scoreInset")]
    [Range(0f, 0.4f)][SerializeField] private float cornerInset = 0.06f;

    [Tooltip("How far in from the top-left corner the badge sits. Its own knob " +
             "rather than sharing the score's, because the badge is big enough " +
             "to crowd the letter and the score isn't. 0 tucks it into the " +
             "tile's rounded corner.")]
    [Range(0f, 0.4f)][SerializeField] private float badgeInset = 0.01f;

    [Tooltip("Diameter of the badge circle, as a fraction of the tile. Shrinking " +
             "this is the other way to pull it off the letter.")]
    [Range(0.1f, 0.6f)][SerializeField] private float badgeSize = 0.38f;

    [Tooltip("How far apart stacked badges sit, as a fraction of a badge's own " +
             "width. 1 = touching but not overlapping; 0.7 leaves each one's " +
             "label clear of the next. It's a PREFERENCE, not a promise: when " +
             "the tile can't fit that many at this spacing they close up to " +
             "whatever does fit, so badges can never hang off the tile.")]
    [Range(0.1f, 1f)][SerializeField] private float badgeSpacing = 0.7f;

    [Tooltip("Nudge the labels toward the camera so they never sort behind the tile art.")]
    [FormerlySerializedAs("scoreDepthOffset")]
    [SerializeField] private float labelDepthOffset = 0.05f;

    [Header("Movement")]
    [SerializeField] private float fallSpeed = 14f;

    [Header("Selection")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.4f);
    [SerializeField] private float selectedScale = 1.15f;

    [Tooltip("How far the selection box sticks out past the tile, as a fraction " +
             "of the tile. 0.12 = a border a bit over a tenth of a tile wide.")]
    [Range(0f, 0.5f)][SerializeField] private float selectionBoxOvershoot = 0.14f;

    [SerializeField] private Color selectionBoxColor = new Color(1f, 0.75f, 0.1f);

    [Tooltip("Letter font size as a fraction of the size the prefab was authored " +
             "with, indexed by how many characters the tile spells. A two-letter " +
             "tile has to shrink or it runs off the edge of the art; index 0 is a " +
             "one-character tile and must stay 1.")]
    [SerializeField] private float[] letterFontScale = { 1f, 0.62f, 0.45f };

    [Header("Feedback")]
    [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float invalidFlashSeconds = 0.25f;
    [SerializeField] private float demolishSeconds = 0.15f;

    /// <summary>The persistent identity this tile is the body of (see TileSpec).</summary>
    public TileSpec Spec { get; private set; }

    /// <summary>
    /// What this tile plays as — the WHOLE spelling, lowercased. Usually one
    /// character; a multi-letter tile ("ch") hands over both, which is how one
    /// board cell contributes two letters to a word.
    /// </summary>
    public string Letters { get; private set; }

    public Vector2Int Cell { get; set; }
    public bool IsSettled => !moving;

    /// <summary>The spec's baseScore: what this tile is worth before any of its modifiers apply.</summary>
    public int LetterPoints { get; private set; }


    /// <summary>Special properties on this tile (multipliers etc.). Usually empty.</summary>
    public List<TileModifier> Modifiers { get; } = new();

    // Where the first badge sits and how big one badge is, in the tile art's
    // local units — stored by LayOutLabels so ApplyModifierVisuals can lay out
    // however many badges the tile's modifiers need.
    private Vector3 badgeAnchor;
    private float badgeUnit;
    private float spriteUnit = 1f;

    // How far right the LAST badge's centre may sit before its edge leaves the
    // tile. The fan is squeezed to fit inside this rather than allowed to
    // overhang, which is why a tile carrying four badges still looks like a tile.
    private float badgeFanRoom;

    // Distance between badge centres, worked out from how many are actually
    // being drawn — so it's set in ApplyModifierVisuals, not here.
    private float badgeStep;

    // One circle+label pair per modifier shown. Index 0 is the authored pair
    // from the prefab; the rest are runtime clones of it, made only when a
    // tile actually stacks modifiers.
    private readonly List<SpriteRenderer> badgeCircles = new();
    private readonly List<TMP_Text> badgeTexts = new();

    // The letter label's authored font size, captured the first time ApplyLook
    // runs and before anything writes to it — so re-initialising a tile can't
    // compound the multi-letter shrink onto an already-shrunk size.
    private float baseLetterFontSize;

    private Vector3 targetPosition;
    private bool moving;
    private float baseScale = 1f;
    private bool selected;
    private Coroutine flashRoutine;

    public void Init(TileSpec spec, TileLook look, Vector2Int cell, Vector3 startPos, float cellSize)
    {
        Spec = spec;
        Letters = spec.Spelling;
        LetterPoints = spec.baseScore;
        Cell = cell;
        Modifiers.Clear();

        // Before ApplyLook writes the face: a tile being re-initialised must not
        // inherit the letter some earlier selection's wild resolved to.
        shownLetters = null;

        if (tileRenderer == null) tileRenderer = GetComponent<SpriteRenderer>();
        ApplyLook(look);

        // Measure the tile body we actually ended up with, whatever supplied it.
        Sprite sprite = tileRenderer.sprite;

        // Scale so the art fits its cell regardless of the source sprite's size.
        if (sprite != null)
        {
            float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            baseScale = spriteSize > 0f ? cellSize * fillFraction / spriteSize : 1f;
        }
        transform.localScale = Vector3.one * baseScale;
        transform.position = startPos;
        targetPosition = startPos;

        if (badgeRenderer != null) badgeRenderer.enabled = false;
        LayOutLabels(sprite);
        LayOutSelectionBox(sprite);
        ApplyModifierVisuals();
    }

    /// <summary>
    /// Applies the two look axes. Every field is optional: a null skin or font
    /// leaves whatever the prefab was authored with, so the tile still draws
    /// correctly for a mode that hasn't been given either.
    /// </summary>
    private void ApplyLook(TileLook look)
    {
        if (look.Skin != null)
        {
            if (look.Skin.baseSprite != null) tileRenderer.sprite = look.Skin.baseSprite;
            if (letterLabel != null) letterLabel.color = look.Skin.letterColor;
            if (scoreLabel != null) scoreLabel.color = look.Skin.scoreColor;
            if (badgeRenderer != null && look.Skin.badgeSprite != null)
                badgeRenderer.sprite = look.Skin.badgeSprite;
        }

        if (letterLabel != null)
        {
            if (look.LetterFont != null) letterLabel.font = look.LetterFont;
            RefreshLetterLabel();
        }
    }

    /// <summary>
    /// Writes the tile's face. The full spelling — a "CH" tile reads CH — shrunk
    /// to fit, because the label is one line of fixed-size world-space text and
    /// two characters at the one-character size overhang the art.
    ///
    /// The authored size is captured ONCE and before anything writes to the
    /// label, so re-initialising a tile — or a wild flipping between "*" and the
    /// letter it became — can never compound the shrink onto an already-shrunk
    /// size.
    /// </summary>
    private void RefreshLetterLabel()
    {
        if (letterLabel == null) return;

        if (baseLetterFontSize <= 0f) baseLetterFontSize = letterLabel.fontSize;

        // Letters, not Spec.letters: the same accessor ChainController.WordOf
        // spells with, so a tile's face can never disagree with what it plays as.
        string face = string.IsNullOrEmpty(shownLetters) ? Letters : shownLetters;
        letterLabel.text = face.ToUpperInvariant();
        letterLabel.fontSize = baseLetterFontSize * LetterScaleFor(letterLabel.text.Length);
    }

    // What a WILD resolved to for the selection this tile is part of, or empty
    // for "show your own spelling". Display only — see ShowLetters.
    private string shownLetters;

    /// <summary>
    /// Show a letter this tile isn't: what a wild became for the word it's
    /// currently in. DISPLAY ONLY — Spec, Letters and LetterPoints are all
    /// untouched, so nothing that scores, saves or spells can see it, and the
    /// tile is still a wild the moment it leaves the selection.
    ///
    /// Null or empty puts the tile back to its own spelling.
    /// </summary>
    public void ShowLetters(string resolved)
    {
        if (shownLetters == resolved) return;
        shownLetters = resolved;
        RefreshLetterLabel();
    }

    /// <summary>
    /// How much to shrink the letter for a tile that spells this many characters.
    /// Off the end of the table means "as small as the table goes" rather than
    /// full size: a longer tile than anyone planned for should come out cramped,
    /// never hanging off the edge.
    ///
    /// An EMPTY table falls back to the curve the default table was drawn from
    /// (1 / 0.62 / 0.44) rather than to 1. A serialized array that arrives empty
    /// is a plausible accident — a prefab saved before this field existed, or
    /// someone clearing it in the Inspector — and 1 is the one answer that puts
    /// letters outside the tile, which is the failure this exists to prevent.
    /// </summary>
    private float LetterScaleFor(int characters)
    {
        characters = Mathf.Max(1, characters);

        if (letterFontScale == null || letterFontScale.Length == 0)
            return Mathf.Min(1f, 1.55f / (characters + 0.5f));

        int index = Mathf.Clamp(characters - 1, 0, letterFontScale.Length - 1);
        return Mathf.Max(0.01f, letterFontScale[index]);
    }

    public void AddModifier(TileModifier modifier)
    {
        if (modifier == null) return;
        Modifiers.Add(modifier);
        ApplyModifierVisuals();
    }

    /// <summary>
    /// Places and sizes both labels from the tile sprite's own bounds.
    ///
    /// This is art-dependent and so can't be baked into the prefab: the tile is
    /// uniformly scaled to fit its cell, so where the corner sits and how big the
    /// text comes out both move when the sprite's dimensions change — and a skin
    /// can swap that sprite for one of any size.
    /// </summary>
    private void LayOutLabels(Sprite sprite)
    {
        Bounds bounds = sprite != null ? sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
        float spriteSize = Mathf.Max(bounds.size.x, bounds.size.y);
        if (spriteSize <= 0f) spriteSize = 1f;

        PlaceLabel(letterLabel, bounds.center, spriteSize, sortingOffset: 1);

        float inset = cornerInset * spriteSize;
        PlaceLabel(scoreLabel,
            new Vector3(bounds.max.x - inset, bounds.min.y + inset, 0f),
            spriteSize, sortingOffset: 2);

        // Top-left, offset by its own radius so badgeInset measures from the
        // circle's edge to the tile's edge, the same way the score's does.
        // Only the anchor is computed here — ApplyModifierVisuals places the
        // badges, because how many there are depends on the modifiers.
        float radius = badgeSize * spriteSize / 2f;
        float badgeMargin = badgeInset * spriteSize;
        badgeAnchor = new Vector3(
            bounds.min.x + badgeMargin + radius,
            bounds.max.y - badgeMargin - radius,
            0f);
        badgeUnit = badgeSize * spriteSize;

        // Mirror of the anchor: the rightmost centre that keeps a full badge
        // inside the same margin the first one respects.
        badgeFanRoom = Mathf.Max(0f, (bounds.max.x - badgeMargin - radius) - badgeAnchor.x);

        spriteUnit = spriteSize;
    }

    /// <summary>
    /// Sizes a badge circle to badgeSize regardless of what the skin's sprite
    /// measures, so a replacement circle doesn't change the layout.
    /// </summary>
    private void LayOutBadge(SpriteRenderer circle, Vector3 localCenter, float worldSize, int sortingOffset)
    {
        if (circle == null) return;

        var badgeTransform = circle.transform;
        badgeTransform.localPosition = new Vector3(localCenter.x, localCenter.y, -labelDepthOffset);

        var sprite = circle.sprite;
        float spriteSize = sprite != null
            ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)
            : 1f;
        if (spriteSize <= 0f) spriteSize = 1f;

        badgeTransform.localScale = Vector3.one * (worldSize / spriteSize);

        if (tileRenderer != null)
        {
            circle.sortingLayerID = tileRenderer.sortingLayerID;
            circle.sortingOrder = tileRenderer.sortingOrder + sortingOffset;
        }
    }

    private void PlaceLabel(TMP_Text label, Vector3 localCenter, float spriteSize, int sortingOffset)
    {
        if (label == null) return;

        var labelTransform = label.transform;
        labelTransform.localPosition = new Vector3(localCenter.x, localCenter.y, -labelDepthOffset);

        // Cancels this tile's fit scaling, so the font size set on the prefab is
        // the size that actually shows up, whatever the art measures.
        labelTransform.localScale = Vector3.one * spriteSize;

        // A world-space TMP object draws through a MeshRenderer, which won't sort
        // against the tile sprites on its own.
        if (tileRenderer != null && label.TryGetComponent<Renderer>(out var labelRenderer))
        {
            labelRenderer.sortingLayerID = tileRenderer.sortingLayerID;
            labelRenderer.sortingOrder = tileRenderer.sortingOrder + sortingOffset;
        }
    }

    private void RefreshScoreLabel()
    {
        if (scoreLabel == null) return;

        // A wild is worth 0, and a "0" in the corner says nothing at all. The
        // star is what keeps saying "this is a wild" once the big letter has
        // been replaced by whatever the word needed — so it's the permanent
        // mark, and the face is the temporary one.
        //
        // Not a display-side multiply: the corner still means "what this tile is
        // worth", it just says it with a symbol for the one tile whose worth is
        // the point.
        scoreLabel.text = Spec != null && Spec.IsWild
            ? TileSpec.WildSpelling
            : LetterPoints.ToString();
    }

    /// <summary>
    /// Modifiers show up as badges and nothing else — the tile body keeps its
    /// skin color, so a board full of multipliers stays readable. One badge per
    /// modifier, fanned right from the top-left corner.
    ///
    /// The fan is spaced at badgeSpacing where that fits and SQUEEZED where it
    /// doesn't, so the last badge always lands inside the tile however many a
    /// tile ends up carrying. ModeConfig.maxModifiersPerTile is what keeps that
    /// number sane — this is the backstop for when it isn't.
    /// </summary>
    private void ApplyModifierVisuals()
    {
        RefreshScoreLabel();

        if (badgeCircles.Count == 0 && badgeRenderer != null && badgeLabel != null)
        {
            badgeCircles.Add(badgeRenderer);
            badgeTexts.Add(badgeLabel);
        }

        int shown = 0;
        if (badgeCircles.Count > 0)
        {
            // Counted before anything is placed: the spacing depends on how many
            // there are, so the first badge can't be drawn until they're all known.
            int count = 0;
            foreach (var modifier in Modifiers)
                if (modifier != null) count++;

            badgeStep = count > 1
                ? Mathf.Min(badgeSpacing * badgeUnit, badgeFanRoom / (count - 1))
                : 0f;

            foreach (var modifier in Modifiers)
            {
                if (modifier == null) continue;
                ShowBadge(shown, modifier);
                shown++;
            }
        }

        // Hide the pairs beyond what this tile carries — the authored pair
        // included, which is how a plain tile shows no badge at all.
        for (int i = shown; i < badgeCircles.Count; i++)
        {
            if (badgeCircles[i] != null) badgeCircles[i].enabled = false;
            if (badgeTexts[i] != null) badgeTexts[i].enabled = false;
        }

        SetColor(RestingColor);
    }

    private void ShowBadge(int index, TileModifier modifier)
    {
        // Clone the authored pair on demand; clones live and die with the tile.
        while (badgeCircles.Count <= index)
        {
            badgeCircles.Add(Instantiate(badgeRenderer, badgeRenderer.transform.parent));
            badgeTexts.Add(Instantiate(badgeLabel, badgeLabel.transform.parent));
        }

        var circle = badgeCircles[index];
        var text = badgeTexts[index];
        var center = badgeAnchor + new Vector3(index * badgeStep, 0f, 0f);

        // Each badge sorts two above the previous so its circle clears the
        // previous badge's label.
        LayOutBadge(circle, center, badgeUnit, sortingOffset: 3 + index * 2);
        circle.sprite = badgeRenderer.sprite;
        circle.color = modifier.badgeColor;

        // The circle needs a sprite from the skin; the label doesn't. If the
        // sprite is missing the label still draws, bare — wrong-looking, but
        // legible, which beats a silently invisible multiplier.
        circle.enabled = badgeRenderer.sprite != null;

        PlaceLabel(text, center, spriteUnit, sortingOffset: 4 + index * 2);
        text.text = modifier.badgeLabel;
        text.color = modifier.badgeTextColor;
        text.enabled = true;
    }

    private void SetColor(Color color)
    {
        if (tileRenderer != null) tileRenderer.color = color;
    }

    /// <summary>
    /// The tile body's colour when nothing is happening to it. Modifiers no
    /// longer tint the body, but this is where a skin-supplied body colour would
    /// slot in if one is ever added.
    /// </summary>
    private Color RestingColor => normalColor;

    public void MoveTo(Vector3 target)
    {
        targetPosition = target;
        moving = true;
    }

    private void Update()
    {
        if (!moving) return;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, fallSpeed * Time.deltaTime);
        if ((transform.position - targetPosition).sqrMagnitude < 0.0001f)
        {
            transform.position = targetPosition;
            moving = false;
        }
    }

    public void SetSelected(bool value)
    {
        selected = value;
        SetColor(value ? selectedColor : RestingColor);
        transform.localScale = Vector3.one * (value ? baseScale * selectedScale : baseScale);
        if (selectionBox != null) selectionBox.enabled = value;
    }

    /// <summary>
    /// Sizes the box from the body sprite's bounds, for the same reason the
    /// labels are: a skin can swap the body for a sprite of any size, and the
    /// whole tile is then uniformly scaled to its cell. Sits one sorting step
    /// BELOW the body, which still leaves it above the board backing at -10.
    /// </summary>
    private void LayOutSelectionBox(Sprite sprite)
    {
        if (selectionBox == null) return;

        selectionBox.enabled = false;
        selectionBox.color = selectionBoxColor;

        Bounds bounds = sprite != null ? sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
        float bodySize = Mathf.Max(bounds.size.x, bounds.size.y);
        if (bodySize <= 0f) bodySize = 1f;

        var boxSprite = selectionBox.sprite;
        float boxSize = boxSprite != null
            ? Mathf.Max(boxSprite.bounds.size.x, boxSprite.bounds.size.y)
            : 1f;
        if (boxSize <= 0f) boxSize = 1f;

        var boxTransform = selectionBox.transform;
        boxTransform.localPosition = new Vector3(bounds.center.x, bounds.center.y, labelDepthOffset);
        boxTransform.localScale =
            Vector3.one * (bodySize * (1f + selectionBoxOvershoot) / boxSize);

        if (tileRenderer != null)
        {
            selectionBox.sortingLayerID = tileRenderer.sortingLayerID;
            selectionBox.sortingOrder = tileRenderer.sortingOrder - 1;
        }
    }

    /// <summary>Brief flash used when a chain isn't a valid word.</summary>
    public void FlashInvalid()
    {
        if (!gameObject.activeInHierarchy) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        SetColor(invalidColor);
        yield return new WaitForSeconds(invalidFlashSeconds);
        if (!selected) SetColor(RestingColor);
        flashRoutine = null;
    }

    /// <summary>Shrink away, then destroy the GameObject.</summary>
    public void Demolish()
    {
        StopAllCoroutines();
        StartCoroutine(DemolishRoutine());
    }

    private IEnumerator DemolishRoutine()
    {
        float start = transform.localScale.x;
        for (float t = 0f; t < demolishSeconds; t += Time.deltaTime)
        {
            transform.localScale = Vector3.one * Mathf.Lerp(start, 0f, t / demolishSeconds);
            yield return null;
        }
        Destroy(gameObject);
    }
}
