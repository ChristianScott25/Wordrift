using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// THE ITEMS BOX — the consumables a run is carrying, top right of the round
/// header, and the panel that reads one out before you spend it.
///
/// It is the ROW; ConsumableSlotView is the card. Same split as
/// BookmarkRowWidget / BookmarkCard, and for the same reason: the row knows what
/// the run holds and where each slot goes, the card knows how to look like one
/// thing and report a finger. When a consumable you DRAG onto the board turns
/// up, it is the card that grows the drag handlers.
///
/// ⚠️ TAP READS, "USE" SPENDS — two steps, deliberately, exactly like the shop's
/// read-then-buy. These sit at the top of the screen where a stray thumb is
/// unlikely, but an item is something the player PAID for, and a mis-tap that
/// burns one has no undo. The panel is also the only place an item's rule is
/// written down.
///
/// ⚠️ THE PANEL IS A SIBLING ON THE CANVAS, NOT A CHILD OF THIS. GameLayout
/// reparents this widget into a slice of the round header about 200px wide; a
/// child panel would be squeezed into it. Its backdrop is a raycast target,
/// which is also what keeps the tap off the board underneath — ChainController's
/// guard is EventSystem.IsPointerOverGameObject on the press frame.
///
/// ⚠️ detailRoot must NOT be this GameObject. Deactivating ourselves would stop
/// us hearing the events that would turn us back on — the rule GameOverPanel
/// already logs an error about.
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

    [Tooltip("Reads ITEMS, or names what's armed to score the next word.")]
    [SerializeField] private TMP_Text captionLabel;

    [Header("Detail panel")]
    [Tooltip("The panel's root, switched on and off. MUST be a sibling on the " +
             "canvas and MUST NOT be this object — see the class comment.")]
    [SerializeField] private GameObject detailRoot;

    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailBody;
    [SerializeField] private Button useButton;
    [SerializeField] private TMP_Text useLabel;
    [SerializeField] private Button cancelButton;

    [Tooltip("The full-screen backdrop, as a button. Tapping outside the card " +
             "closes it — a SECOND way out, because the panel covers the whole " +
             "screen and a single unwired CANCEL would trap the player.")]
    [SerializeField] private Button backdropButton;

    [Tooltip("The session this spends against. A direct reference because this " +
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
    private int selected = -1;

    // What the caption is currently showing, so the every-frame status handler
    // can leave TMP alone unless it actually changed. Status is raised once per
    // frame; assigning the same string still dirties the mesh.
    private string captionShown;

    private void Awake()
    {
        self = (RectTransform)transform;

        if (detailRoot == gameObject)
        {
            Debug.LogError("ConsumablesAreaWidget's 'detailRoot' must be a separate " +
                           "object, not itself — switching ourselves off would stop " +
                           "us hearing events.", this);
            detailRoot = null;
        }

        // Wired here rather than in a prefab: a persistent listener pointing at a
        // scene object does not survive being saved into one.
        if (useButton != null) useButton.onClick.AddListener(Use);
        if (cancelButton != null) cancelButton.onClick.AddListener(CloseDetail);
        if (backdropButton != null) backdropButton.onClick.AddListener(CloseDetail);

        // ⚠️ THE PANEL IS MODAL AND COVERS THE WHOLE SCREEN. With no way to
        // close it the player is stuck on a card they can't dismiss, which is
        // much worse than the box simply not working — so refuse to open it at
        // all rather than open a trap.
        if (detailRoot != null && cancelButton == null && backdropButton == null)
        {
            Debug.LogError("Items box has no CANCEL and no backdrop button — the " +
                           "panel would have no way out. Run Word Crush/Set Up " +
                           "Game Layout.", this);
            detailRoot = null;
        }
    }

    private void OnEnable()
    {
        GameLayout.Changed += PlaceSelf;
        GameEvents.RoundStarted += Refresh;
        GameEvents.StatusChanged += OnStatusChanged;
        GameEvents.RoundEnded += OnRoundEnded;

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
        RunState.Changed -= Refresh;
    }

    // Start, not OnEnable — the layout resolves between the two. See GameLayout.
    private void Start()
    {
        PlaceSelf();
        CloseDetail();
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
    private void Refresh()
    {
        var run = RunState.Current;
        int wanted = SlotCount;

        BuildSlots(wanted);

        for (int i = 0; i < slots.Count; i++)
        {
            var consumable = run != null && i < run.Consumables.Count ? run.Consumables[i] : null;
            slots[i].Bind(this, i, consumable,
                          itemSprite, itemColor, slotSprite, slotColor, itemTextColor);
        }

        // An item spent from underneath an open panel — the only way being to
        // press USE — leaves nothing to read.
        if (selected >= 0 && (run == null || selected >= run.Consumables.Count))
            CloseDetail();
        else if (selected >= 0)
            ShowDetail();

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

            rect.anchorMin = new Vector2(i * pitch, 0f);
            rect.anchorMax = new Vector2((i + 1) * pitch, 1f - captionRoom);
            rect.offsetMin = new Vector2(slotGap * 0.5f, 0f);
            rect.offsetMax = new Vector2(-slotGap * 0.5f, 0f);
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

    // ---- The detail panel ---------------------------------------------------

    /// <summary>A slot was tapped. Opens what it holds for reading.</summary>
    public void Select(int slot)
    {
        var run = RunState.Current;
        if (run == null || slot < 0 || slot >= run.Consumables.Count) return;
        if (detailRoot == null)
        {
            // No panel wired means no way to read or confirm. Better to say so
            // than to silently do nothing on every tap.
            Debug.LogError("Items box has no detail panel — run Word Crush/Set Up Game Layout.", this);
            return;
        }

        selected = slot;
        ShowDetail();
    }

    private void ShowDetail()
    {
        var run = RunState.Current;
        if (detailRoot == null) return;
        if (run == null || selected < 0 || selected >= run.Consumables.Count) return;

        var consumable = run.Consumables[selected];
        if (consumable == null) { CloseDetail(); return; }

        if (detailTitle != null) detailTitle.text = consumable.Title.ToUpperInvariant();
        if (detailBody != null) detailBody.text = consumable.Power;

        // Obeyed, never re-derived — the session is the one place that question
        // is answered, the same deal the PLAY button has with CanSubmit.
        bool usable = session != null && session.CanUseConsumable;
        if (useButton != null) useButton.interactable = usable;
        if (useLabel != null) useLabel.text = usable ? "USE" : "NOT NOW";

        // ⚠️ RAISED EVERY TIME, not just once in the editor. GameLayout creates
        // its band containers as canvas children AT RUNTIME, in GameSession.Awake
        // — so they are added AFTER anything the scene already held, and a panel
        // left where the generator put it would end up drawing underneath every
        // widget on screen. Sibling order is the draw order on a Canvas.
        detailRoot.transform.SetAsLastSibling();
        detailRoot.SetActive(true);
    }

    /// <summary>
    /// The panel is modal and draws over everything, so it must not be the thing
    /// sitting on top of the game-over panel. Not reachable today — a round only
    /// ends as a result of playing a word, which this panel blocks — but it costs
    /// one line and the alternative is a screen nobody can get off.
    /// </summary>
    private void OnRoundEnded(RoundSummary summary)
    {
        CloseDetail();

        // GameSession.Update stops raising StatusChanged once the round is over,
        // so an armed Doubler would leave its name sitting in gold above the
        // items box for the whole game-over screen. Forced back by hand, since
        // nothing is going to tell us again.
        captionShown = null;
        if (captionLabel != null)
        {
            captionLabel.text = caption;
            captionLabel.color = captionColor;
        }
    }

    private void CloseDetail()
    {
        selected = -1;
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    private void Use()
    {
        if (session == null || selected < 0) return;

        // ⚠️ CLOSED BEFORE THE CALL, NOT AFTER. Spending raises RunState.Changed
        // from inside UseConsumable, which runs Refresh — and with `selected`
        // still pointing at slot 0, Refresh would re-open the panel on whatever
        // item slid DOWN into that slot. A one-frame flash of the wrong title.
        int slot = selected;
        CloseDetail();

        if (session.UseConsumable(slot)) return;

        // Refused — the item is still in the run, so nothing raised Changed and
        // nothing refreshed. Re-open, so the button says NOT NOW rather than the
        // panel closing as if something had happened.
        selected = slot;
        ShowDetail();
    }
}
