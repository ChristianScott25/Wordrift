using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// The run's bookmarks, as a row of cards you can drag to reorder.
///
/// This is not decoration. RunState.Bookmarks is walked by index in
/// ScoreCalculator.Evaluate — "slot order is the call order" — so left-to-right
/// on this row IS the order they touch a word's score, and dragging one is a
/// real move: Vowel Fanatic before Bookend is (×1 +4) ×2 = ×10, and the other
/// way round it's ×6.
///
/// It reads RunState.Current directly rather than taking a GameEvents payload,
/// the same way SeedWidget does and for the same reason — what it needs is the
/// live list, and no event carries one. Refresh() therefore runs BEFORE the
/// subscriptions in OnEnable, so the row is right even when it enables after the
/// round's opening events have already gone out.
///
/// The same prefab is placed in the Game scene and the Shop. Only the Game scene
/// wires `board`; everywhere else the row keeps the position it was authored at.
/// </summary>
public class BookmarkRowWidget : MonoBehaviour
{
    [Tooltip("Holds the cards. Hidden when the run owns no bookmarks, which is " +
             "why it must be a CHILD and never this object — see OnEnable.")]
    [SerializeField] private RectTransform row;

    [Tooltip("The card to clone, one per bookmark. Kept switched off; it is a " +
             "recipe, not a card.")]
    [SerializeField] private BookmarkCard cardTemplate;

    [Tooltip("Where a card's look comes from. The same skin the board's tiles " +
             "use, so a bookmark reads as something out of this game rather " +
             "than a panel from another one.")]
    [SerializeField] private TileSkin cardSkin;

    [Tooltip("Gap between cards, in canvas pixels. The card WIDTH is derived " +
             "from this and the cap — the row always divides into maxBookmarks " +
             "slots, so cards don't change size as you buy them.")]
    [SerializeField] private float cardGap = 12f;

    [Tooltip("Body tint. The tile sprite is near-white so it can be tinted; " +
             "this is what stops the cards reading as blank tiles.")]
    [SerializeField] private Color cardColor = new Color(0.94f, 0.90f, 0.78f, 1f);

    [Tooltip("The name's colour.")]
    [SerializeField] private Color cardTextColor = new Color(0.16f, 0.17f, 0.23f, 1f);

    [Header("Game scene only")]
    [Tooltip("Optional. When set, the row pins its BOTTOM edge to the board's " +
             "TOP edge, so the cards look like bookmarks sticking out of a book. " +
             "The board is camera-framed, so where that edge falls changes with " +
             "the screen's shape. Leave empty in the Shop, which has no board.")]
    [SerializeField] private Board board;

    [Tooltip("Nudge off the board's top edge. Negative tucks the cards further " +
             "into the board, which is what sells the illusion when the board " +
             "has a border drawn outside its cells.")]
    [SerializeField] private float boardGap = -6f;

    [Tooltip("The highest the row may be pinned. The ceiling that stops the " +
             "cards climbing into the word row above them \u2014 see PinAboveBoard.")]
    [SerializeField] private float maxY = 2400f;

    // Live cards, in SLOT ORDER — index 0 is the leftmost, which is the first to
    // score. During a drag this list is reordered as the finger moves so the row
    // shows the answer before it's committed; RunState is only told on release.
    private readonly List<BookmarkCard> cards = new();

    private RectTransform self;

    // Where the drag started and where it has got to. -1 means no drag.
    private int dragFrom = -1;
    private int dragTo = -1;

    // WHICH card is under the finger. Tracked as well as the indices because the
    // EventSystem happily runs two pointers at once on a touchscreen: a second
    // finger landing on a second card would otherwise drive this one's indices,
    // and DragCard's RemoveAt(dragTo) would pull out a card that isn't the one
    // being moved.
    private BookmarkCard dragCard;

    private bool Dragging => dragFrom >= 0 && dragCard != null;

    private void Awake()
    {
        self = (RectTransform)transform;

        // A recipe, not a card. Switched off here rather than trusted to be off
        // in the prefab, because a template left visible looks exactly like a
        // sixth bookmark nobody owns.
        if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // This object stays active whatever happens: it's the listener, and a
        // listener that switches itself off never hears the event that would
        // switch it back on. Only the child row is hidden.
        if (row != null && row.gameObject == gameObject)
        {
            Debug.LogError("BookmarkRowWidget's 'row' is the widget itself — hiding it would " +
                           "switch off its own listener. Point it at a child object.", this);
            row = null;
        }

        // Without a template there is nothing to clone, so the row would simply
        // never appear — which looks exactly like a run that owns no bookmarks,
        // and is the sort of thing nobody thinks to check. Loud instead.
        if (cardTemplate == null || row == null)
            Debug.LogError("BookmarkRowWidget has no card template or no row — it will draw " +
                           "nothing. Run Word Crush > Set Up Bookmark Row.", this);

        // Before subscribing, not after: OnEnable can run after the round's
        // opening events have already fired, and an empty row on the first frame
        // of a resumed run looks exactly like a run that lost its bookmarks.
        Refresh();

        GameEvents.RoundStarted += Refresh;
        RunState.Changed += OnRunChanged;

        // The board's top edge moves when the bands are re-resolved, so the row
        // has to follow it. Refresh (above) runs before the layout exists on the
        // first frame; this is what puts the row in the right place once it does.
        GameLayout.Changed += PinAboveBoard;
    }

    private void OnDisable()
    {
        GameLayout.Changed -= PinAboveBoard;

        GameEvents.RoundStarted -= Refresh;
        RunState.Changed -= OnRunChanged;

        // A drag doesn't survive the widget going away. Left set, dragFrom would
        // still be pointing at a slot when this came back, and the next stray
        // release would commit a move the player never made.
        dragFrom = -1;
        dragTo = -1;
        dragCard = null;
    }

    /// <summary>
    /// We raise this ourselves on release, so we hear our own reorder and rebuild
    /// from the run rather than trusting the layout we were mid-way through.
    /// </summary>
    private void OnRunChanged()
    {
        // Can't happen today — nothing else moves a bookmark while a finger is
        // down — but rebuilding under a drag would leave the card being held
        // pointing at a slot that had moved out from under it.
        if (Dragging) return;
        Refresh();
    }

    // ------------------------------------------------------------------------
    // DRAWING
    // ------------------------------------------------------------------------

    private void Refresh()
    {
        var run = RunState.Current;
        var owned = run?.Bookmarks;
        int count = owned == null ? 0 : owned.Count;

        // Hidden rather than left as an empty strip: a run owns nothing until it
        // has been to the shop, and a band of nothing above the buttons is worse
        // than no band at all.
        if (row != null && row.gameObject.activeSelf != (count > 0))
            row.gameObject.SetActive(count > 0);

        if (count == 0)
        {
            ReleaseFrom(0);
            return;
        }

        EnsureCards(count);

        // EnsureCards gives up when the template or the row is missing, and then
        // there is nothing to index. OnEnable has already said so loudly; this is
        // what stops it also throwing once per round.
        count = Mathf.Min(count, cards.Count);
        if (count == 0) return;

        Sprite sprite = cardSkin == null ? null : cardSkin.baseSprite;
        for (int i = 0; i < count; i++)
            cards[i].Bind(this, owned[i].Name, sprite, cardColor, cardTextColor);

        LayOut(-1);
        PinAboveBoard();
    }

    /// <summary>
    /// Grows the row to `count` cards, cloning the template for any it's short.
    /// Cards are REUSED rather than rebuilt — a reorder hands card 0 a different
    /// bookmark — so nothing is destroyed until the run somehow holds fewer.
    /// </summary>
    private void EnsureCards(int count)
    {
        if (cardTemplate == null || row == null) return;

        while (cards.Count < count)
        {
            var made = Instantiate(cardTemplate, row);
            made.name = $"Card{cards.Count}";
            cards.Add(made);
        }

        // Switched on EXPLICITLY, not just when freshly cloned. A card that has
        // been through ReleaseFrom keeps activeSelf false, and re-activating the
        // row doesn't undo that — so a run that ever showed a count of 0 and
        // then went back up would lay its cards out perfectly and draw nothing.
        for (int i = 0; i < count; i++)
            if (cards[i] != null && !cards[i].gameObject.activeSelf)
                cards[i].gameObject.SetActive(true);

        ReleaseFrom(count);
    }

    /// <summary>Switches off every card from `first` on. Kept, not destroyed.</summary>
    private void ReleaseFrom(int first)
    {
        for (int i = first; i < cards.Count; i++)
            if (cards[i] != null) cards[i].gameObject.SetActive(false);
    }

    /// <summary>How many cards the row divides into. The cap, so a card is the
    /// same size whether you own one or five.</summary>
    private int SlotCount(int count)
    {
        int cap = RunState.Current?.Template == null ? 0 : RunState.Current.Template.maxBookmarks;
        return Mathf.Max(1, cap > 0 ? cap : count);
    }

    private float RowWidth => row == null ? 1000f : row.rect.width;

    private float SlotWidth(int count)
    {
        int slots = SlotCount(count);
        return Mathf.Max(1f, (RowWidth - cardGap * (slots - 1)) / slots);
    }

    private float Pitch(int count) => SlotWidth(count) + cardGap;

    /// <summary>X of the leftmost card's centre. The cards are centred on the
    /// row for however many there are, not left-packed into fixed slots.</summary>
    private float FirstX(int count)
    {
        float slot = SlotWidth(count);
        float total = count * slot + cardGap * (count - 1);
        return -total * 0.5f + slot * 0.5f;
    }

    /// <summary>
    /// Puts every card where its slot says, skipping `held` — the one under the
    /// finger, which is following the pointer instead.
    /// </summary>
    private void LayOut(int held)
    {
        int count = ActiveCount();
        if (count == 0) return;

        float slot = SlotWidth(count);
        float first = FirstX(count);
        float pitch = Pitch(count);

        for (int i = 0; i < count; i++)
        {
            var rect = cards[i].Rect;
            if (rect == null) continue;

            // The row's own height, not a field of its own: a card is exactly as
            // tall as the strip it lives in, and the two being separate numbers
            // that had to be "kept in step" was a standing invitation to desync.
            rect.sizeDelta = new Vector2(slot, row.rect.height);
            if (i != held) rect.anchoredPosition = new Vector2(first + i * pitch, 0f);
        }
    }

    private int ActiveCount()
    {
        int count = 0;
        foreach (var card in cards)
            if (card != null && card.gameObject.activeSelf) count++;
        return count;
    }

    /// <summary>
    /// Sits the row so its BOTTOM edge meets the board's TOP edge — the cards
    /// stand up out of the board like bookmarks out of a book.
    ///
    /// ⚠️ THE CARDS ARE NOT ACTUALLY BEHIND THE BOARD, AND CAN'T BE. The HUD is
    /// a Screen Space - Overlay canvas, so it draws over every world sprite there
    /// is; the board could never cover a card. The illusion is that the card ENDS
    /// exactly where the board begins and has no bottom edge drawn, which is
    /// indistinguishable from one that continues behind it — as long as nothing
    /// lifts a card clear of the board while dragging.
    ///
    /// If that stops being good enough, the real fix is a second World Space
    /// canvas sorted under BoardBackground (-10), which keeps BookmarkCard's
    /// drag handling exactly as it is. Switching THIS canvas to Screen Space -
    /// Camera would not: the drag code reads pressEventCamera, which is null only
    /// in overlay mode.
    ///
    /// Worth the arithmetic because the board is camera-framed to fill its band,
    /// so its top edge moves with the screen's shape. A hardcoded Y would be
    /// right on the one aspect it was measured on.
    ///
    /// ⚠️ Assumes the row is anchored to the canvas's BOTTOM with its pivot
    /// there too, which is how the setup script places it: anchoredPosition.y is
    /// then simply the gap between the canvas's bottom edge and the row's.
    /// </summary>
    private void PinAboveBoard()
    {
        if (board == null || self == null) return;

        if (!Mathf.Approximately(self.anchorMin.y, 0f) ||
            !Mathf.Approximately(self.anchorMax.y, 0f) ||
            !Mathf.Approximately(self.pivot.y, 0f))
        {
            Debug.LogError("BookmarkRowWidget is pinned to the board, which needs it anchored " +
                           "to the canvas's bottom edge with a bottom pivot. Run " +
                           "Word Crush > Set Up Bookmark Row.", this);
            board = null;
            return;
        }

        // Through GameLayout rather than the camera directly: it owns the
        // conversion between world and canvas space, and a second copy of that
        // arithmetic is a second thing to get wrong when the framing changes.
        var layout = GameLayout.Current;
        if (layout == null || !layout.IsResolved) return;

        // The row is as tall as the band reserved for it, so the card tips can't
        // grow into the word row above on a short screen. The band is a SPACER
        // and nothing attaches to it — the row still pins against the board's
        // REAL top edge, which can sit slightly inside the board's own band when
        // the board is width-bound.
        Rect reserved = layout.CanvasRectOf(LayoutBand.Bookmarks);
        if (reserved.height > 0f)
            self.sizeDelta = new Vector2(self.sizeDelta.x, reserved.height);

        float boardTop = board.BoardCenter.y + board.BoardSize.y * 0.5f;
        float topEdge = layout.CanvasYOf(boardTop);

        self.anchoredPosition = new Vector2(
            self.anchoredPosition.x,
            Mathf.Min(maxY, topEdge + boardGap));

        // The cards take their height from the row, so they have to be re-laid
        // out after it changes — LayOut ran before this, on the old height.
        // Not mid-drag: a resize while a card is held is vanishingly rare, and
        // snapping the held card back to its slot would be worse than a card
        // that is briefly the wrong height.
        if (!Dragging) LayOut(-1);
    }

    // ------------------------------------------------------------------------
    // DRAGGING
    //
    // The run is told ONCE, on release. Reordering it live would mean the scoring
    // order flickering through every slot the finger passes over, and it would
    // write a save per frame.
    // ------------------------------------------------------------------------

    internal void BeginCardDrag(BookmarkCard card)
    {
        // One card at a time. A second finger is ignored rather than taking
        // over, so the drag that's already in flight still lands where the
        // player aimed it.
        if (Dragging || card == null || card.Rect == null) return;

        int index = cards.IndexOf(card);
        if (index < 0 || index >= ActiveCount()) return;

        dragFrom = index;
        dragTo = index;
        dragCard = card;

        // Over its neighbours while it's being held, so it reads as picked up
        // rather than sliding behind the row.
        card.Rect.SetAsLastSibling();
    }

    internal void DragCard(BookmarkCard card, PointerEventData eventData)
    {
        if (!Dragging || card != dragCard || row == null || card.Rect == null) return;

        // pressEventCamera is null on a Screen Space - Overlay canvas, which is
        // what this one is, and null is the right argument there.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                row, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        int count = ActiveCount();
        float first = FirstX(count);
        float pitch = Pitch(count);

        // X only. A card that could be flung upward would end up over the board,
        // and there is nothing up there for it to mean.
        float held = Mathf.Clamp(local.x, first, first + pitch * (count - 1));
        card.Rect.anchoredPosition = new Vector2(held, 0f);

        int target = Mathf.Clamp(Mathf.RoundToInt((held - first) / pitch), 0, count - 1);
        if (target == dragTo) return;

        // Shuffle the others along as the card passes them, so what you see
        // before you let go is what you get.
        cards.RemoveAt(dragTo);
        cards.Insert(target, card);
        dragTo = target;

        LayOut(dragTo);
    }

    internal void EndCardDrag(BookmarkCard card)
    {
        if (!Dragging || card != dragCard) return;

        int from = dragFrom;
        int to = dragTo;

        // Cleared BEFORE the run is told: MoveBookmark raises RunState.Changed,
        // which comes straight back here as a Refresh, and that must not think a
        // drag is still in progress.
        dragFrom = -1;
        dragTo = -1;
        dragCard = null;

        var run = RunState.Current;
        if (run != null && from != to && run.MoveBookmark(from, to)) return;  // Refresh came back round

        // Nothing moved, or there's no run to tell — put the card back.
        LayOut(-1);
    }
}
