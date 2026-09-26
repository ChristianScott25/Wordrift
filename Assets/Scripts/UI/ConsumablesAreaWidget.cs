using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🚧 THE CONSUMABLES AREA, WHICH HAS NOTHING IN IT.
///
/// Consumables are a planned idea: one-shot items you buy in the shop and spend
/// during a round. None of that exists — there is no Consumable asset, nothing
/// in the shop sells one, and the run holds none.
///
/// This reserves the space so the layout can be judged at its real size, and
/// draws empty slots so it reads as "a place things go" rather than as a gap.
/// When consumables are built, this widget gets a list to render and the slot
/// count stops being a serialized guess.
/// </summary>
public class ConsumablesAreaWidget : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("🚧 A guess. How many consumables a run may carry is undecided.")]
    [Range(1, 6)][SerializeField] private int slotCount = 2;

    [SerializeField] private Sprite slotSprite;

    [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.08f);

    [SerializeField] private float slotGap = 8f;

    [Tooltip("Optional caption above the slots.")]
    [SerializeField] private TMP_Text captionLabel;

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0.81f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 1f;

    private readonly List<RectTransform> slots = new();
    private RectTransform self;

    private void Awake() => self = (RectTransform)transform;

    private void OnEnable() => GameLayout.Changed += PlaceSelf;

    private void OnDisable() => GameLayout.Changed -= PlaceSelf;

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start()
    {
        PlaceSelf();
        BuildSlots();
    }

    private void PlaceSelf()
    {
        GameLayout.Attach(self, LayoutBand.RoundHeader, bandXMin, bandXMax);
        LayOut();
    }

    private void BuildSlots()
    {
        while (slots.Count < slotCount)
        {
            var go = new GameObject($"Slot {slots.Count}", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);

            var image = go.GetComponent<Image>();
            image.sprite = slotSprite;
            image.color = slotColor;

            // Nothing here is interactive yet, and an empty slot that swallowed
            // a tap would be worse than one that doesn't.
            image.raycastTarget = false;

            slots.Add(rect);
        }

        LayOut();
    }

    /// <summary>Slots divide the width evenly, under the caption if there is one.</summary>
    private void LayOut()
    {
        if (slots.Count == 0) return;

        float captionRoom = captionLabel == null ? 0f : 0.28f;
        float pitch = 1f / slots.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            var rect = slots[i];
            rect.anchorMin = new Vector2(i * pitch, 0f);
            rect.anchorMax = new Vector2((i + 1) * pitch, 1f - captionRoom);
            rect.offsetMin = new Vector2(slotGap * 0.5f, 0f);
            rect.offsetMax = new Vector2(-slotGap * 0.5f, 0f);
        }
    }
}
