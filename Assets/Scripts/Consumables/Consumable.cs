using UnityEngine;

/// <summary>
/// What a consumable needs the player to pick before it can be used.
///
/// 🚧 ONLY None IS WIRED. A targeted consumable needs a pick-a-target flow —
/// tap the item, then tap or drag onto a tile — and that flow isn't built.
/// GameSession.UseConsumable refuses anything else and says so, rather than
/// half-using it against a target nobody chose.
/// </summary>
public enum ConsumableTarget
{
    /// <summary>Press USE and it happens. Both of today's items.</summary>
    None,

    /// <summary>🚧 One tile on the board. Arrives on ConsumableUse.Tile.</summary>
    BoardTile,
}

/// <summary>
/// Everything a consumable is handed when it's used.
///
/// ⚠️ WIDEN THIS RATHER THAN ADDING A SECOND HOOK — the same bargain RoundRules,
/// WordCheck, ScoringContext and RunPerks all take. The next lever a consumable
/// wants is a field on a bundle that already gets passed, not a new signature
/// for every existing consumable to ignore.
///
/// It's a class rather than a struct because it will keep growing, and because
/// a targeted use will want to fill Tile in after the bundle was built.
/// </summary>
public class ConsumableUse
{
    /// <summary>The run. Null only if something has gone badly wrong.</summary>
    public RunState Run;

    /// <summary>The session, for anything that has to reach the round's rules.</summary>
    public GameSession Session;

    /// <summary>The board, for anything that rearranges it.</summary>
    public Board Board;

    /// <summary>
    /// The round's consumable stream. ⚠️ Every roll in a run goes through Rng —
    /// never UnityEngine.Random — and this one is wound forward on resume like
    /// the tile bag's is, so quitting and continuing can't re-deal it.
    /// </summary>
    public Rng Rng;

    /// <summary>🚧 The tile the player picked, for a BoardTile consumable. Null today.</summary>
    public Tile Tile;

    /// <summary>🚧 The cell the player picked. Unused today.</summary>
    public Vector2Int Cell;
}

/// <summary>
/// A one-shot item you buy in the shop and spend during a round. Balatro's
/// consumables; the run carries a couple at a time and they're gone once used.
///
/// An AUTHORED RECIPE like a Bookmark, a Checkout or a Librarian: read-only at
/// runtime, with no per-run state on it. What a run owns is the asset itself
/// (RunState.Consumables) — there is no per-copy wrapper yet because nothing
/// about your copy can differ from anyone else's. The day one CAN (charges left,
/// a remembered target), that's a ConsumableSpec, exactly as BookmarkSpec exists
/// for editions. Duplicates are allowed, so the same asset may appear twice.
///
/// ONE HOOK:
///
///   Use(ConsumableUse use) -> bool
///
/// ⚠️ UNLIKE Checkout.Apply, THIS IS MEANT TO DO SOMETHING, AND IT RUNS ONCE.
/// A checkout describes a standing perk and is re-asked an unbounded number of
/// times, so it must be idempotent. This is the opposite: it fires exactly once,
/// it may change the board and the run, and nothing replays it. Don't copy a
/// checkout's habits into one.
///
/// ⚠️ RETURNING FALSE MEANS THE ITEM IS NOT SPENT. That is the whole contract
/// with GameSession.UseConsumable, which is the one place an item leaves the
/// run: it spends only on true. So "the board is mid-fall", "there's only one
/// tile left to shuffle" and, later, "the player cancelled the target" all keep
/// the item. (They do NOT keep the board selection — that is cleared before Use
/// runs either way, because an item is free to move the tiles out from under it.
/// Pressing USE is a deliberate act, so that is the honest cost.)
///
/// THE NAME IS A COSTUME, like "librarian" and "checkout" are. Subclasses are
/// named for the POWER (ShuffleConsumable, NextWordMultiplierConsumable), never
/// for the word on the asset — so renaming "Doubler" is one Inspector string.
/// </summary>
public abstract class Consumable : ScriptableObject
{
    [Tooltip("What this one is CALLED — on the shop row and on the item itself. " +
             "Free to change without touching what it does, because a save names " +
             "the asset FILE, not this.")]
    public string displayName = "";

    [TextArea]
    [Tooltip("Optional. Overrides the description this consumable writes for " +
             "itself. Leave it empty and the game shows the one derived from this " +
             "asset's own numbers — which is what stops the text going stale when " +
             "you retune.")]
    public string powerOverride = "";

    [Tooltip("What it costs in the shop, BEFORE any discount. 0 means unpriced — " +
             "Word Crush > Create Consumable Assets fills a 0 with the default and " +
             "leaves any number you've tuned alone.")]
    [Min(0)] public int price = 0;

    /// <summary>
    /// What this does, in the player's words, derived from its own settings.
    /// Derived rather than authored for the same reason a checkout's is: turning
    /// a number in the Inspector must not leave the game describing the old rule.
    /// </summary>
    public abstract string PowerText { get; }

    /// <summary>What the game shows: the author's wording if there is one, else its own.</summary>
    public string Power =>
        string.IsNullOrWhiteSpace(powerOverride) ? PowerText : powerOverride;

    /// <summary>The name to show, falling back to the asset's file name.</summary>
    public string Title => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    /// <summary>
    /// What the player has to pick first. Default None: press USE and it happens.
    /// 🚧 Anything else is refused for now — see ConsumableTarget.
    /// </summary>
    public virtual ConsumableTarget Targets => ConsumableTarget.None;

    /// <summary>
    /// Spend this item. Runs ONCE.
    ///
    /// Return false if it couldn't do anything — the item then stays in the run,
    /// which is the only reason a refusal is safe to write.
    /// </summary>
    public abstract bool Use(ConsumableUse use);
}
