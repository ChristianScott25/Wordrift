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

    [Tooltip("Draws the letter itself. Position and scale are set from the tile " +
             "art at runtime, so don't hand-place it — style it and leave the " +
             "transform alone. Its font can be overridden per mode.")]
    [SerializeField] private TMP_Text letterLabel;

    [Tooltip("Prints what this letter is worth. Its position and scale are set from " +
             "the art every time the tile is initialised, so don't hand-place it — " +
             "style it (font, size, color) and leave the transform alone.")]
    [SerializeField] private TMP_Text scoreLabel;

    [Header("Fit")]
    [Tooltip("Fraction of the cell the tile art fills. 1 = edge to edge.")]
    [Range(0.5f, 1f)][SerializeField] private float fillFraction = 0.92f;

    [Header("Score label")]
    [Tooltip("How far in from the art's bottom-right corner, as a fraction of the tile.")]
    [Range(0f, 0.4f)][SerializeField] private float scoreInset = 0.06f;

    [Tooltip("Nudge the labels toward the camera so they never sort behind the tile art.")]
    [FormerlySerializedAs("scoreDepthOffset")]
    [SerializeField] private float labelDepthOffset = 0.05f;

    [Header("Movement")]
    [SerializeField] private float fallSpeed = 14f;

    [Header("Selection")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.9f, 0.4f);
    [SerializeField] private float selectedScale = 1.15f;

    [Header("Feedback")]
    [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float invalidFlashSeconds = 0.25f;
    [SerializeField] private float demolishSeconds = 0.15f;

    public char Letter { get; private set; }
    public Vector2Int Cell { get; set; }
    public bool IsSettled => !moving;

    /// <summary>What this letter is worth before any of its modifiers apply.</summary>
    public int LetterPoints { get; private set; }

    /// <summary>The value shown on the tile: LetterPoints run through its modifiers.</summary>
    public int DisplayPoints => TileModifier.ApplyLetterModifiers(LetterPoints, Modifiers);

    /// <summary>Special properties on this tile (multipliers etc.). Usually empty.</summary>
    public List<TileModifier> Modifiers { get; } = new();

    private Vector3 targetPosition;
    private bool moving;
    private float baseScale = 1f;
    private bool selected;
    private Coroutine flashRoutine;

    public void Init(char letter, int points, TileLook look, Vector2Int cell, Vector3 startPos, float cellSize)
    {
        Letter = letter;
        LetterPoints = points;
        Cell = cell;
        Modifiers.Clear();

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
        }

        if (letterLabel != null)
        {
            if (look.LetterFont != null) letterLabel.font = look.LetterFont;
            letterLabel.text = char.ToUpperInvariant(Letter).ToString();
        }
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

        float inset = scoreInset * spriteSize;
        PlaceLabel(scoreLabel,
            new Vector3(bounds.max.x - inset, bounds.min.y + inset, 0f),
            spriteSize, sortingOffset: 2);
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
        if (scoreLabel != null) scoreLabel.text = DisplayPoints.ToString();
    }

    private void ApplyModifierVisuals()
    {
        RefreshScoreLabel();

        if (Modifiers.Count == 0)
        {
            if (badgeRenderer != null) badgeRenderer.enabled = false;
            SetColor(normalColor);
            return;
        }

        var top = Modifiers[Modifiers.Count - 1];
        SetColor(top.tint);
        if (badgeRenderer != null)
        {
            badgeRenderer.sprite = top.badge;
            badgeRenderer.enabled = top.badge != null;
        }
    }

    private void SetColor(Color color)
    {
        if (tileRenderer != null) tileRenderer.color = color;
    }

    /// <summary>The tile's resting color, accounting for any modifier tint.</summary>
    private Color RestingColor =>
        Modifiers.Count > 0 ? Modifiers[Modifiers.Count - 1].tint : normalColor;

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
