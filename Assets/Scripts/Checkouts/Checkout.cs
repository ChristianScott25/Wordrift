using UnityEngine;

/// <summary>
/// Something you check out of the library and keep — a permanent, run-wide perk
/// bought in the shop. This game's version of a Balatro voucher: it applies the
/// moment you buy it and lasts until the run ends.
///
/// A checkout is an AUTHORED RECIPE, exactly like a Bookmark, a TileModifier or
/// a Librarian: read-only at runtime, with no per-run state on it. What a run
/// owns is the asset itself (RunState.Checkouts) — unlike a bookmark there is no
/// per-copy wrapper, because you can only ever own one of each and there is
/// nothing about your copy that could differ from anyone else's.
///
/// ONE HOOK, and it is deliberately not a verb:
///
///   Apply(RunPerks perks) — describe what you grant. Add to the fields.
///
/// APPLY MUST BE IDEMPOTENT AND PURELY DECLARATIVE. RunState throws the whole
/// RunPerks away and rebuilds it from scratch after every purchase and again on
/// every resume, so Apply runs an unbounded number of times for one purchase. A
/// checkout that DID something — added tiles to the bag, paid out money, gilded
/// a tile — would do it again on every rebuild. Describe a standing perk here;
/// anything that happens once belongs in the shop's Deliver, not in a checkout.
///
/// The next lever a checkout wants is a FIELD ON RunPerks, not a second hook for
/// the other checkouts to ignore — same bargain Librarian takes with RoundRules.
///
/// THE NAME IS A COSTUME, like "librarian" is. These are called checkouts today;
/// each one's own name is displayName below, and nothing user-facing is
/// hardcoded anywhere else. Subclasses are named for the POWER
/// (ShopDiscountCheckout), never for the title on the asset — so retitling
/// "Sense and Frugality" is one Inspector string.
/// </summary>
public abstract class Checkout : ScriptableObject
{
    [Tooltip("What this one is CALLED — the book title on the shop button. Free " +
             "to change without touching what it does, because a save names the " +
             "asset FILE, not this.")]
    public string displayName = "";

    [TextArea]
    [Tooltip("Optional. Overrides the description this checkout writes for itself. " +
             "Leave it empty and the shop shows the one derived from this asset's " +
             "own numbers — which is what stops the text going stale when you retune.")]
    public string powerOverride = "";

    [Tooltip("What it costs in the shop, BEFORE any discount. 0 means unpriced — " +
             "Word Crush > Create Checkout Assets fills a 0 with the default and " +
             "leaves any number you've tuned alone.")]
    [Min(0)] public int price = 0;

    /// <summary>
    /// What this checkout does, in the player's words, derived from its own
    /// settings. Derived rather than authored so that turning a number in the
    /// Inspector can't leave the shop describing the old perk — the failure a
    /// second, hand-written copy of a rule always eventually produces.
    /// </summary>
    public abstract string PowerText { get; }

    /// <summary>What the shop shows: the author's wording if there is one, else its own.</summary>
    public string Power =>
        string.IsNullOrWhiteSpace(powerOverride) ? PowerText : powerOverride;

    /// <summary>The name to show, falling back to the asset's file name.</summary>
    public string Title => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    /// <summary>
    /// Describe what owning this grants. Add to the fields — never assign, or two
    /// checkouts granting the same thing would have the second erase the first.
    ///
    /// Read the class comment before writing one: this runs many times for one
    /// purchase, so it must only ever DESCRIBE.
    /// </summary>
    public abstract void Apply(RunPerks perks);
}
