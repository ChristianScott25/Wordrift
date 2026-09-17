/// <summary>
/// What a run's checkouts add up to — the run-long twin of RoundRules, and it
/// works the same way: something fills it in, the perks get a turn at it, and
/// whoever needs a number reads it back. Nothing here knows which checkout put
/// a value in it, which is the point.
///
/// Mutable and deliberately small. WIDEN THIS rather than giving Checkout a
/// second hook: one bundle means the next checkout is an asset, not a change to
/// every signature. Same bargain as RoundRules and ScoringContext.
///
/// Nothing here is saved. RunState rebuilds it from the checkouts the run owns —
/// which ARE saved — every time that list changes and once on resume. See
/// Checkout.Apply for why that rebuild forces perks to be declarative.
/// </summary>
public class RunPerks
{
    /// <summary>
    /// Taken off every price in the shop. Summed across checkouts, and clamped
    /// when it's read (RunState.PriceOf) rather than here — a stack that somehow
    /// reached 100 would make the shop free, and free is not a discount.
    /// </summary>
    public int ShopDiscountPercent;

    /// <summary>Tiles added to every round's discard allowance, in TILES.</summary>
    public int ExtraDiscards;

    /// <summary>Words added to every round's move allowance.</summary>
    public int ExtraMoves;

    /// <summary>Added to what a cleared round pays, as a percentage of it.</summary>
    public int PayoutBonusPercent;

    /// <summary>
    /// Dollars paid at the end of a cleared round per $10 the run is holding.
    /// The CAP is a config number (RogueDemoModeConfig.maxInterest), not one of
    /// these — a ceiling belongs with maxRoundPayout, so two sources of interest
    /// can't compound past it.
    /// </summary>
    public int InterestPer10;
}
