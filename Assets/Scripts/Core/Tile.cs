using System.Collections;
using System.Collections.Generic;
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

    [Header("Fit")]
    [Tooltip("Fraction of the cell the tile art fills. 1 = edge to edge.")]
    [Range(0.5f, 1f)][SerializeField] private float fillFraction = 0.92f;

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

    /// <summary>Special properties on this tile (multipliers etc.). Usually empty.</summary>
    public List<TileModifier> Modifiers { get; } = new();

    private Vector3 targetPosition;
    private bool moving;
    private float baseScale = 1f;
    private bool selected;
    private Coroutine flashRoutine;

    public void Init(char letter, Sprite sprite, Vector2Int cell, Vector3 startPos, float cellSize)
    {
        Letter = letter;
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
        ApplyModifierVisuals();
    }

    public void AddModifier(TileModifier modifier)
    {
        if (modifier == null) return;
        Modifiers.Add(modifier);
        ApplyModifierVisuals();
    }

    private void ApplyModifierVisuals()
    {
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
