using System.Collections;
using UnityEngine;

/// <summary>
/// One letter tile on the board. Handles its own movement toward a target
/// position (falling), selection highlight, invalid-word flash, and the
/// demolish animation.
/// </summary>
public class Tile : MonoBehaviour
{
    private const float FallSpeed = 14f;   // units per second
    private const float SelectedScale = 1.15f;

    public char Letter { get; private set; }
    public Vector2Int Cell { get; set; }
    public bool IsSettled => !moving;

    private SpriteRenderer sr;
    private Vector3 targetPosition;
    private bool moving;
    private float baseScale = 1f;
    private bool selected;
    private Coroutine flashRoutine;

    public void Init(char letter, Sprite sprite, Vector2Int cell, Vector3 startPos, float cellSize)
    {
        Letter = letter;
        Cell = cell;
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;

        // Scale the sprite to fit the cell with a small gap.
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        baseScale = cellSize * 0.92f / spriteSize;
        transform.localScale = Vector3.one * baseScale;
        transform.position = startPos;
        targetPosition = startPos;
    }

    public void MoveTo(Vector3 target)
    {
        targetPosition = target;
        moving = true;
    }

    private void Update()
    {
        if (!moving) return;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, FallSpeed * Time.deltaTime);
        if ((transform.position - targetPosition).sqrMagnitude < 0.0001f)
        {
            transform.position = targetPosition;
            moving = false;
        }
    }

    public void SetSelected(bool value)
    {
        selected = value;
        sr.color = value ? new Color(1f, 0.9f, 0.4f) : Color.white;
        transform.localScale = Vector3.one * (value ? baseScale * SelectedScale : baseScale);
    }

    /// <summary>Brief red flash used when a chain isn't a valid word.</summary>
    public void FlashInvalid()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        sr.color = new Color(1f, 0.35f, 0.35f);
        yield return new WaitForSeconds(0.25f);
        if (!selected) sr.color = Color.white;
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
        float duration = 0.15f;
        float start = transform.localScale.x;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.one * Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }
        Destroy(gameObject);
    }
}
