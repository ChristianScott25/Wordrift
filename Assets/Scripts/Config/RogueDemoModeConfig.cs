using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First pass at the roguelike round: no clock, a finite bag of tiles, and a
/// score you have to reach inside a fixed number of words. Played as a RUN —
/// clear the target and the shop leads to the next round, with a higher one.
///
/// Named "Rogue Demo" on purpose — it's somewhere to start and build off, not
/// the shape the real mode will end up in.
/// </summary>
[CreateAssetMenu(fileName = "RogueDemoMode", menuName = "Word Crush/Mode/Rogue Demo")]
public class RogueDemoModeConfig : ModeConfig
{
    [Header("Round")]
    [Tooltip("Words the player gets to reach the target. The same every round for now.")]
    [Min(1)] public int moves = 20;

    [Tooltip("End the round the moment the target is reached, rather than always " +
             "playing out every move.")]
    public bool endOnTargetReached = true;

    [Tooltip("If true, submitting an invalid word also costs a move.")]
    public bool rejectedWordsCostMoves = false;

    [Tooltip("Move counter turns red at or below this many moves.")]
    public int urgentMoves = 3;

    [Tooltip("Tiles the player may throw off the board per round, in TILES not " +
             "uses — discarding three at once spends three. Refilled every round, " +
             "never carried over. Costs no move: it's a separate budget, so it " +
             "stays an escape hatch from a bad board rather than a second tax. " +
             "0 turns discarding off entirely.")]
    [Min(0)] public int discardsPerRound = 5;

    [Tooltip("Scene a cleared round continues to, between the rounds of a run.")]
    public string shopSceneName = "Shop";

    [Header("Targets")]
    [Tooltip("Score target for each round of a run, in order — round 1 is the " +
             "first entry. Tune the difficulty curve here.")]
    public int[] roundTargets = { 30, 45, 65 };

    [Tooltip("Once a run outlives the list above, each further round's target " +
             "grows by this factor.")]
    [Min(1f)] public float targetGrowth = 1.5f;

    [Tooltip("Fallback target when the list above is empty. Also the base the " +
             "growth factor compounds from in that case.")]
    [Min(1)] public int targetScore = 30;

    [Header("Tile bag")]
    [Tooltip("How many tiles a run starts with. The Letter Set's weights are " +
             "shared out across this total, with at least one of every letter — " +
             "so 98 is a full Scrabble bag and 104 is a little over one. The " +
             "board's opening fill is paid for out of this, and once it's empty " +
             "tiles stop falling for the rest of the round. The full bag returns " +
             "every round.")]
    [Min(1)] public int tileBagSize = 104;

    [Header("Librarians")]
    [Tooltip("The pool of librarians a run can meet. Which one turns up is rolled " +
             "from the run's seed, and none repeats until they've all been seen — " +
             "so the order is part of the seed, not of the list. An empty list " +
             "simply means no round is ever a librarian round.")]
    public List<Librarian> librarians = new();

    [Tooltip("How often a librarian turns up: 3 = every third round is one. " +
             "0 turns them off entirely without emptying the pool.")]
    [Min(0)] public int librarianEveryRounds = 3;

    [Tooltip("What a cleared librarian round pays, as a multiple of the usual " +
             "payout. A librarian is free to overwrite this for itself (see " +
             "RoundRules.PayoutMultiplier), so this is the default rate, not the rule.")]
    [Min(0f)] public float librarianPayoutMultiplier = 2f;

    [Tooltip("What these ARE CALLED on screen, above the individual name — " +
             "'LIBRARIAN', 'EXAM', 'CRITIC'. The only place the noun is written " +
             "down, so renaming them is this field. Blank shows just the name.")]
    public string librarianLabel = "LIBRARIAN";

    [Header("Checkouts")]
    [Tooltip("The pool of checkouts this mode's shop can offer — permanent, " +
             "run-wide perks. A run can own each at most once, and an empty list " +
             "simply means the checkout row is never stocked.")]
    public List<Checkout> checkouts = new();

    [Header("Shop")]
    [Tooltip("What the FIRST reroll of a shop visit costs. Every reroll after " +
             "it costs the growth factor below times more, and the price resets " +
             "the next time the player walks into a shop — it is a per-visit " +
             "ladder, not a run-long one. 0 removes the reroll button entirely, " +
             "the same way discardsPerRound of 0 turns discarding off.")]
    [Min(0)] public int rerollBasePrice = 5;

    [Tooltip("What each reroll multiplies the next one's price by. The ladder " +
             "compounds off the EXACT price and is only rounded up at the end, " +
             "so the rounding can't stack into a curve steeper than this says: " +
             "at 1.5 it runs $5, $8, $12, $17, $26.")]
    [Min(1f)] public float rerollPriceGrowth = 1.5f;

    [Header("Payout")]
    [Tooltip("Points needed per $1 of the round's payout. 10 = a 60-point round pays $6.")]
    [Min(1)] public int pointsPerCoin = 10;

    [Tooltip("Extra money per move left unspent when the round is cleared. Pays for " +
             "efficiency, and gives the move counter a second job. 0 turns it off.")]
    [Min(0)] public int coinsPerUnusedMove = 1;

    [Tooltip("The most one cleared round may pay, whatever it scored. A ceiling on " +
             "the payout, not on the score: it stops a runaway round buying out the " +
             "shop in one visit, and stops a broken one paying a nonsense number. " +
             "Applied LAST, after the librarian's multiplier — a librarian round " +
             "that already earned the cap is paid the cap. 0 removes the ceiling.")]
    [Min(0)] public int maxRoundPayout = 200;

    [Tooltip("The most interest one cleared round may pay, whatever the run is " +
             "holding (see Checkout_Interest). A ceiling on the rate's output, " +
             "and it lives here rather than on the checkout so that two sources " +
             "of interest are bounded together rather than one at a time. " +
             "Applied before the round payout cap. 0 removes the ceiling.")]
    [Min(0)] public int maxInterest = 25;

    [Header("🚧 Testing")]
    [Tooltip("🚧 TEMPORARY — A TEST MODE, NOT A GAME RULE.\n\n" +
             "Nothing in the shop is ever too expensive: RunState.CanAfford always " +
             "says yes and TrySpend takes nothing. Rounds still pay, so the balance " +
             "still climbs — it just never goes down, and both money readouts show " +
             "$∞ instead of a number.\n\n" +
             "It exists so the shop can be played end to end without grinding rounds " +
             "for money. Mode_RogueDemo_Unlimited.asset is the only asset that should " +
             "ever have this ticked — and it is a COPY of Mode_RogueDemo, re-synced by " +
             "Word Crush > 🚧 Create Unlimited Money Mode, so tick it there and " +
             "your tuning is the same on both.\n\n" +
             "To delete the whole test mode: the asset, Editor/UnlimitedMoneyModeSetup.cs, " +
             "this field, RunState.UnlimitedMoney, and the button block in MainMenuSetup.")]
    public bool unlimitedMoney = false;

    /// <summary>
    /// What clearing a round pays. The seam every later payout idea hangs off —
    /// interest on savings, a flat per-round purse, bookmarks that pay out — so
    /// keep the arithmetic here rather than in the mode.
    /// </summary>
    /// <param name="payoutMultiplier">
    /// The round's rate, off RoundRules — 1 for an ordinary round, more for a
    /// librarian's. It multiplies the whole payout rather than one term of it,
    /// so "this round pays double" stays true no matter which term later ideas
    /// (interest, purses) add.
    /// </param>
    /// <param name="perks">
    /// What the run's checkouts grant, or null for a run without any. Taken as
    /// the bundle rather than as loose numbers so that widening RunPerks doesn't
    /// widen this signature again.
    /// </param>
    /// <param name="moneyHeld">
    /// The balance interest is charged against — the run's money BEFORE this
    /// payout lands, so clearing a round never pays interest on its own winnings.
    /// </param>
    public int RewardFor(int score, int movesLeft, float payoutMultiplier = 1f,
                         RunPerks perks = null, int moneyHeld = 0)
    {
        // long, then double: score is already saturated at a billion (see
        // ScoreLimits), and a payout multiplier on top of that overflows an int
        // long before it overflows either of these.
        long earned = (long)Mathf.Max(0, score) / Mathf.Max(1, pointsPerCoin) +
                      (long)Mathf.Max(0, movesLeft) * Mathf.Max(0, coinsPerUnusedMove);

        double paid = earned * (double)Mathf.Max(0f, payoutMultiplier);

        // The bonus is a percentage of what the ROUND earned, so it rides the
        // librarian's multiplier — a doubled round pays a doubled bonus. Interest
        // is not part of what the round earned, so it lands after and is not
        // multiplied by either.
        if (perks != null)
        {
            paid += paid * (Mathf.Max(0, perks.PayoutBonusPercent) / 100.0);
            paid += InterestOn(moneyHeld, perks);
        }

        // Last, as ever: the cap is a cap, so a bonus or interest that would
        // carry a round past it simply doesn't.
        if (maxRoundPayout > 0 && paid > maxRoundPayout) paid = maxRoundPayout;

        return ScoreLimits.Clamp(paid);
    }

    /// <summary>
    /// What a balance earns when a round is cleared. Split out of RewardFor so
    /// the rate and its ceiling sit together and the payout above reads as the
    /// four terms it is — and so that a shop that ever wants to PREVIEW interest
    /// ("hold this and you'll earn $12") has one number to ask for rather than a
    /// second copy of the arithmetic. Nothing previews it today.
    /// </summary>
    private int InterestOn(int moneyHeld, RunPerks perks)
    {
        if (perks == null || perks.InterestPer10 <= 0 || moneyHeld <= 0) return 0;

        long interest = (long)(moneyHeld / 10) * perks.InterestPer10;
        if (maxInterest > 0 && interest > maxInterest) interest = maxInterest;

        return ScoreLimits.Clamp(interest);
    }

    /// <summary>The score target for a given 1-based round of a run.</summary>
    public int TargetForRound(int round)
    {
        round = Mathf.Max(1, round);

        // Saturated like every other score number: compounding growth passes
        // int.MaxValue somewhere around round 60, and an overflowed target
        // would come back NEGATIVE — which reads as "already cleared".
        if (roundTargets != null && roundTargets.Length > 0)
        {
            if (round <= roundTargets.Length) return roundTargets[round - 1];
            int last = roundTargets[roundTargets.Length - 1];
            return ScoreLimits.Clamp((double)last * Mathf.Pow(targetGrowth, round - roundTargets.Length));
        }

        return ScoreLimits.Clamp((double)targetScore * Mathf.Pow(targetGrowth, round - 1));
    }

    public override GameMode CreateMode() => new RogueDemoMode(this);

    /// <summary>
    /// The librarian and checkout pools join the stamp for the same reason the
    /// modifier and bookmark pools do: their numbers are tuning knobs, and a run
    /// resumed against a retuned one would play by numbers the save doesn't
    /// describe. The base stamp covers this asset's own fields, but the pools
    /// arrive there as instance IDs, which are deliberately zeroed — so the
    /// assets themselves have to be stamped by hand.
    /// </summary>
    public override string Fingerprint() =>
        base.Fingerprint() + StampAll(librarians) + StampAll(checkouts);
}
