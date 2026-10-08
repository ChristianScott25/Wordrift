using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// THE ITEMS BOX — the consumables a run is carrying, top right of the round
/// header.
///
/// It is the ROW; ConsumableSlotView is the card. Same split as
/// BookmarkRowWidget / BookmarkCard, and for the same reason: the row knows what
/// the run holds and where each slot goes, the card knows how to look like one
/// thing and report a finger.
///
/// ⚠️ TAP READS, DRAG ONTO THE BOARD SPENDS (2026-09-29). This replaced a
/// tap-then-USE panel, and the swap was forced by the info box: tapping now
/// means "tell me what this is" everywhere on the canvas, so it cannot also mean
/// "spend the thing I paid for". Dragging is the better half of the trade
/// anyway — it is the gesture a targeted item needs, so there is one way to play
/// an item rather than two, and it is deliberate enough to need no confirmation.
///
/// ⚠️ A DRAGGED CARD IS REPARENTED ONTO THE CANVAS ROOT, and it has to be.
/// GameLayout puts this widget in a slice of the round header about 200px wide,
/// and a card that stayed inside it would be dragged around inside a box in the
/// corner instead of down to the board. Reparenting also means its anchors have
/// to be frozen first — a stretched rect re-parented to the canvas stretches to
/// the CANVAS, which is a card the size of the phone.
/// </summary>
public class ConsumablesAreaWidget : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("Fallback only. The real count is the mode config's Max Consumables; " +
             "this is what gets used in a scene opened with no run in progress.")]
    [Range(1, 6)][SerializeField] private int slotCount = 2;

    [Tooltip("An empty slot's sprite — the flat square, so a gap reads as a place " +
             "something goes rather than as a hole in the layout.")]
    [SerializeField] private Sprite slotSprite;

    [Tooltip("A carried item's sprite. 🚧 The tile body, for now: an item should " +
             "eventually have art of its own.")]
    [SerializeField] private Sprite itemSprite;

    [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.08f);

    [SerializeField] private Color itemColor = Color.white;

    [SerializeField] private Color itemTextColor = new Color(0.12f, 0.13f, 0.17f);

    [SerializeField] private float slotGap = 8f;

    [Tooltip("How far the slots sit inside the area's edges — clears the plate " +
             "the area is drawn on. 0 with no plate.")]
    [Min(0f)][SerializeField] private float plateInset;

    [Tooltip("Reads ITEMS, or names what's armed to score the next word.")]
    [SerializeField] private TMP_Text captionLabel;

    [Tooltip("The session this spends against, and the one thing that knows " +
             "where the board is on screen. A direct reference because this " +
             "widget lives in one scene — the same shape WordActionsWidget uses.")]
    [SerializeField] private GameSession session;

    [Header("Look")]
    [SerializeField] private string caption = "ITEMS";

    [Tooltip("Caption colour while something is armed to score the next word.")]
    [SerializeField] private Color armedColor = new Color(1f, 0.85f, 0.3f);

    [SerializeField] private Color captionColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("Place in band")]
    [Range(0f, 1f)][SerializeField] private float bandXMin = 0.81f;

    [Range(0f, 1f)][SerializeField] private float bandXMax = 1f;

    private readonly List<ConsumableSlotView> slots = new();
    private RectTransform self;

    // The card currently under the finger, and where it came from, so it can be
    // put back whatever happens — a refused drop, a round ending mid-drag, or
    // the scene going away.
    private ConsumableSlotView dragging;
    private Transform dragHome;
    private RectTransform canvasRect;

    // What the caption is currently showing, so the every-frame status handler
    // can leave TMP alone unless it actually changed. Status is raised once per
    // frame; assigning the same string still dirties the mesh.
    private string captionShown;

    private void Awake()
    {
        self = (RectTransform)transform;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = (RectTransform)canvas.transform;
    }

    private void OnEnable()
    {
        GameLayout.Changed += PlaceSelf;
        GameEvents.RoundStarted += Refresh;
        GameEvents.StatusChanged += OnStatusChanged;
        GameEvents.RoundEnded += OnRoundEnded;
        GameEvents.ScoreBeat += OnScoreBeat;

        // The run telling the UI it moved — the opposite direction from
        // GameEvents. It is what fires when an item is bought or spent.
        RunState.Changed += Refresh;
    }

    private void OnDisable()
    {
        GameLayout.Changed -= PlaceSelf;
        GameEvents.RoundStarted -= Refresh;
        GameEvents.StatusChanged -= OnStatusChanged;
        GameEvents.RoundEnded -= OnRoundEnded;
        GameEvents.ScoreBeat -= OnScoreBeat;
        RunState.Changed -= Refresh;

        // A card left parented to the canvas root would survive this widget
        // being switched off and sit on screen with nothing driving it.
        //
        // ⚠️ ReturnDraggedCard, NOT CancelDrag — this runs on scene teardown too,
        // and CancelDrag ends in a Refresh, which can BUILD slots. Creating
        // GameObjects while the scene is going away is how you get objects that
        // outlive it.
        ReturnDraggedCard();
    }

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start()
    {
        PlaceSelf();
        Refresh();
    }

    private void PlaceSelf()
    {
        GameLayout.Attach(self, LayoutBand.RoundHeader, bandXMin, bandXMax);
        LayOut();
    }

    /// <summary>How many slots this run has. The config is the authority.</summary>
    private int SlotCount
    {
        get
        {
            var run = RunState.Current;
            if (run == null || run.Template == null) return slotCount;

            // 0 means unlimited on the config, which this box cannot draw — it is
            // one small row. Fall back to what the run is actually carrying.
            int max = run.Template.maxConsumables;
            return max > 0 ? max : Mathf.Max(slotCount, run.Consumables.Count);
        }
    }

    /// <summary>Rebuilds the slots from what the run is carrying.</summary>
    /// <summary>
    /// Shakes the slot holding whatever just scored — an armed Doubler taking its
    /// turn — and floats its number off the top of it.
    ///
    /// ⚠️ Found by the ASSET, which two slots can legitimately share: duplicates
    /// are allowed, so arming two Doublers shakes the first slot twice rather
    /// than one each. Harmless, and the alternative is the mode keeping track of
    /// which slot an armed item came from, which nothing else needs.
    /// </summary>
    private void OnScoreBeat(ScoreStep step)
    {
        if (step.Kind != ScoreActor.Consumable) return;
        if (step.Actor is not Consumable consumable) return;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            // activeSelf in the SAME test as the item, not after it: a slot past
            // the carried count is hidden but still holds what it last showed.
            if (slot == null || slot.Rect == null || !slot.gameObject.activeSelf) continue;
            if (!ReferenceEquals(slot.Consumable, consumable)) continue;

            // Sampled before the pulse — see BookmarkRowWidget.
            var at = Inspector.ScreenRectOf(slot.Rect);

            Jolt.Pulse(slot.Rect);
            ScorePop.Show(step.Amount, at, step.Side);
            return;
        }
    }

    /// <summary>
    /// Stops every slot mid-pulse and puts it back. Before a Refresh, which
    /// rebinds slots to different items — a slot left shaking through that would
    /// be the wrong slot shaking.
    /// </summary>
    private void CancelPulses()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null) Jolt.Cancel(slots[i].Rect);
    }

    private void Refresh()
    {
        CancelPulses();

        // Spending an item raises RunState.Changed from inside UseConsumable,
        // which lands here — and re-binding the card still under the finger
        // would renumber it mid-gesture. The drag puts the card back and
        // refreshes itself when it ends, so there is nothing lost by waiting.
        if (dragging != null) return;

        var run = RunState.Current;
        int wanted = SlotCount;

        BuildSlots(wanted);

        for (int i = 0; i < slots.Count; i++)
        {
            var consumable = run != null && i < run.Consumables.Count ? run.Consumables[i] : null;
            slots[i].Bind(this, i, consumable,
                          itemSprite, itemColor, slotSprite, slotColor, itemTextColor);
        }

        LayOut();
    }

    private void BuildSlots(int wanted)
    {
        while (slots.Count < wanted)
        {
            slots.Add(ConsumableSlotView.Create(transform, $"Item {slots.Count}"));
        }

        // Only ever grown, never shrunk: maxConsumables can't change mid-run
        // (it's stamped into the fingerprint), so a shrink would only ever be a
        // scene opened without a run. Hide the surplus rather than destroying
        // objects the next Refresh might want back.
        for (int i = 0; i < slots.Count; i++)
            slots[i].gameObject.SetActive(i < wanted);
    }

    /// <summary>Slots divide the width evenly, under the caption.</summary>
    private void LayOut()
    {
        int shown = Mathf.Min(slots.Count, SlotCount);
        if (shown == 0) return;

        float captionRoom = captionLabel == null ? 0f : 0.28f;
        float pitch = 1f / shown;

        for (int i = 0; i < shown; i++)
        {
            var rect = slots[i].Rect;
            if (rect == null) continue;

            // The pivot is reset because a DRAGGED card has its own — see
            // BeginItemDrag. Anchors and offsets alone would leave a card that
            // had been dragged and put back sitting half a slot off.
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Even shares of the width INSIDE the inset: slot i's left edge is
            // inset + i × (width − 2 × inset) / shown, written as an anchor
            // plus an offset so it still follows the area when it resizes.
            rect.anchorMin = new Vector2(i * pitch, 0f);
            rect.anchorMax = new Vector2((i + 1) * pitch, 1f - captionRoom);
            rect.offsetMin = new Vector2(plateInset * (1f - 2f * i * pitch) + slotGap * 0.5f,
                                         plateInset);
            rect.offsetMax = new Vector2(plateInset * (1f - 2f * (i + 1) * pitch) - slotGap * 0.5f,
                                         0f);
        }
    }

    /// <summary>
    /// The caption. ⚠️ Runs EVERY FRAME — StatusChanged is raised unconditionally
    /// from GameSession.Update — so it allocates nothing and touches TMP only
    /// when the text actually changed. The armed string itself is built by the
    /// mode, when the arming changes.
    /// </summary>
    private void OnStatusChanged(ModeStatus status)
    {
        if (captionLabel == null) return;

        string wanted = status.HasArmed ? status.ArmedText : caption;
        if (wanted == captionShown) return;

        captionShown = wanted;
        captionLabel.text = wanted;
        captionLabel.color = status.HasArmed ? armedColor : captionColor;
    }

    /// <summary>
    /// The round is over.
    ///
    /// GameSession.Update stops raising StatusChanged once it is, so an armed
    /// Doubler would leave its name sitting in gold above the items box for the
    /// whole game-over screen. Forced back by hand, since nothing is going to
    /// tell us again.
    /// </summary>
    private void OnRoundEnded(RoundSummary summary)
    {
        // A round can end while a card is in the air — the game-over panel would
        // otherwise appear underneath it.
        CancelDrag();
        Inspector.Hide();

        captionShown = null;
        if (captionLabel != null)
        {
            captionLabel.text = caption;
            captionLabel.color = captionColor;
        }
    }

    // ---- Reading and playing an item ----------------------------------------

    /// <summary>A slot was tapped. Says what it is; never spends it.</summary>
    public void Read(int slot)
    {
        var run = RunState.Current;
        if (run == null || slot < 0 || slot >= run.Consumables.Count) return;
        if (slot >= slots.Count) return;

        Inspector.Show(run.Consumables[slot], Inspector.ScreenRectOf(slots[slot].Rect));
    }

    /// <summary>
    /// The player picked a card up.
    ///
    /// Refused outright when the session says an item can't be played right now
    /// — a board mid-fall or mid-tally. Obeyed, never re-derived: CanUseConsumable
    /// is the one place that question is answered, the same deal PLAY has with
    /// CanSubmit. Refusing at pick-up rather than at drop is the honest place for
    /// it, because a card that won't lift says so before the player has aimed.
    /// </summary>
    internal void BeginItemDrag(ConsumableSlotView slot, PointerEventData eventData)
    {
        if (slot == null || slot.Consumable == null) return;
        if (session == null || !session.CanUseConsumable) return;
        if (canvasRect == null) return;

        // Reading and playing are different gestures; doing one ends the other.
        Inspector.Hide();

        var rect = slot.Rect;
        if (rect == null) return;

        dragging = slot;
        dragHome = rect.parent;

        // ⚠️ FREEZE THE SIZE BEFORE REPARENTING. The slot's anchors are a
        // fraction of the items box; carried onto the canvas root unchanged they
        // would mean the same fraction of the whole SCREEN, and the card would
        // balloon the instant it was picked up.
        Vector2 size = rect.rect.size;

        rect.SetParent(canvasRect, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.SetAsLastSibling();

        // Placed here and not left to the first OnDrag. Unity does send OnDrag
        // in the same frame as OnBeginDrag today, so this is currently invisible
        // — but the card's anchoredPosition was meaningless the moment its
        // anchors changed, so without this the card's position for that frame
        // depends on an ordering detail of the input module rather than on
        // anything here. One line to not care.
        DragItem(slot, eventData);
    }

    /// <summary>
    /// The card follows the finger.
    ///
    /// Screen pixels are assigned straight to `position` because this canvas is
    /// Screen Space - Overlay, where a canvas's world coordinates ARE screen
    /// pixels — the same fact Inspector.ScreenRectOf leans on. On a Screen Space
    /// - Camera canvas this would need converting, and would silently be wrong.
    /// </summary>
    internal void DragItem(ConsumableSlotView slot, PointerEventData eventData)
    {
        if (dragging != slot || slot.Rect == null || eventData == null) return;
        slot.Rect.position = new Vector3(eventData.position.x, eventData.position.y, 0f);
    }

    /// <summary>
    /// Dropped. On the board it plays; anywhere else it goes back and costs
    /// nothing.
    ///
    /// ⚠️ THE CARD IS PUT BACK BEFORE THE SESSION IS ASKED. Spending raises
    /// RunState.Changed from inside UseConsumable, which runs Refresh — and
    /// Refresh re-binds every slot. Doing that while one of them is still
    /// parented to the canvas root would leave the spent card floating over the
    /// screen with the NEXT item's name on it.
    /// </summary>
    internal void EndItemDrag(ConsumableSlotView slot, PointerEventData eventData)
    {
        if (dragging != slot) return;

        int index = slot.Slot;
        CancelDrag();

        if (session == null || eventData == null) return;

        // Refusals cost nothing: UseConsumable checks where it landed before it
        // clears anything or spends anything.
        session.UseConsumable(index, eventData.position);
    }

    /// <summary>
    /// Puts a dragged card back in the box and redraws. Safe to call when
    /// nothing is being dragged.
    /// </summary>
    private void CancelDrag()
    {
        if (!ReturnDraggedCard()) return;
        Refresh();
    }

    /// <summary>
    /// Reparents a dragged card back into the box, wherever it got to, and
    /// nothing else. Returns whether there was one.
    ///
    /// Split out from CancelDrag so OnDisable can use it: that runs on scene
    /// teardown, where a Refresh is free to build slots that nothing will ever
    /// own. Position is left wrong on purpose — the next Refresh fixes it, and
    /// on teardown there is no next frame to see it in.
    /// </summary>
    private bool ReturnDraggedCard()
    {
        if (dragging == null) return false;

        var rect = dragging.Rect;
        dragging = null;

        if (rect != null && dragHome != null) rect.SetParent(dragHome, false);
        dragHome = null;
        return true;
    }
}
