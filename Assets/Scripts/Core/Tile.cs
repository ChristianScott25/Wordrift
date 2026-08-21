using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// One letter tile. Everything visual is a serialized field so it can be
/// tuned on the Tile prefab without touching code — colors, speeds, scale.
/// Swapping the art is a matter of changing the sprite in the LetterSet.
/// </summary>
public class Tile : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer letterRenderer;
    [SerializeField] private SpriteRenderer badgeRenderer;

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

    [Tooltip("Nudge toward the camera so the number never sorts behind the letter art.")]
    [SerializeField] private float scoreDepthOffset = 0.05f;

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

    public void Init(char letter, int points, Sprite sprite, Vector2Int cell, Vector3 startPos, float cellSize)
    {
        Letter = letter;
        LetterPoints = points;
        Cell = cell;
        Modifiers.Clear();

        if (letterRenderer == null) letterRenderer = GetComponent<SpriteRenderer>();
        letterRenderer.sprite = sprite;

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
        LayOutScoreLabel(sprite);
        ApplyModifierVisuals();
    }

    public void AddModifier(TileModifier modifier)
    {
        if (modifier == null) return;
        Modifiers.Add(modifier);
        ApplyModifierVisuals();
    }

    /// <summary>
    /// Places and sizes the score label from the sprite's own bounds.
    ///
    /// Both are art-dependent and so can't be baked into the prefab: the tile is
    /// uniformly scaled to fit its cell, so where the corner sits and how big the
    /// text comes out both move when the sprite's dimensions change — and all the
    /// current letter art is placeholder.
    /// </summary>
    private void LayOutScoreLabel(Sprite sprite)
    {
        if (scoreLabel == null) return;

        var labelTransform = scoreLabel.transform;
        Bounds bounds = sprite != null ? sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
        float spriteSize = Mathf.Max(bounds.size.x, bounds.size.y);
        if (spriteSize <= 0f) spriteSize = 1f;

        float inset = scoreInset * spriteSize;
        labelTransform.localPosition = new Vector3(
            bounds.max.x - inset,
            bounds.min.y + inset,
            -scoreDepthOffset);

        // Cancels this tile's fit scaling, so the font size set on the prefab is
        // the size that actually shows up, whatever the art measures.
        labelTransform.localScale = Vector3.one * spriteSize;

        // A world-space TMP object draws through a MeshRenderer, which won't sort
        // against the tile sprites on its own.
        if (letterRenderer != null && scoreLabel.TryGetComponent<Renderer>(out var labelRenderer))
        {
            labelRenderer.sortingLayerID = letterRenderer.sortingLayerID;
            labelRenderer.sortingOrder = letterRenderer.sortingOrder + 1;
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
        if (letterRenderer != null) letterRenderer.color = color;
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
