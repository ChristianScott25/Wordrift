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
             "so 98 is a full Scrabble bag and 52 is about half of one. The " +
             "board's opening fill is paid for out of this, and once it's empty " +
             "tiles stop falling for the rest of the round. The full bag returns " +
             "every round.")]
    [Min(1)] public int tileBagSize = 52;

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

    [Header("Payout")]
    [Tooltip("Points needed per $1 of the round's payout. 10 = a 60-point round pays $6.")]
    [Min(1)] public int pointsPerCoin = 10;

    [Tooltip("Extra money per move left unspent when the round is cleared. Pays for " +
             "efficiency, and gives the move counter a second job. 0 turns it off.")]
    [Min(0)] public int coinsPerUnusedMove = 1;

    [Tooltip("How much an offer's price grows each time you buy it AGAIN in the same " +
             "shop visit. 1.5 = $5, then $8, then $11. Resets every visit.")]
    [Min(1f)] public float repeatPriceGrowth = 1.5f;

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
    public int RewardFor(int score, int movesLeft, float payoutMultiplier = 1f)
    {
        int earned = Mathf.Max(0, score) / Mathf.Max(1, pointsPerCoin) +
                     Mathf.Max(0, movesLeft) * Mathf.Max(0, coinsPerUnusedMove);
        return Mathf.RoundToInt(earned * Mathf.Max(0f, payoutMultiplier));
    }

    /// <summary>The score target for a given 1-based round of a run.</summary>
    public int TargetForRound(int round)
    {
        round = Mathf.Max(1, round);

        if (roundTargets != null && roundTargets.Length > 0)
        {
            if (round <= roundTargets.Length) return roundTargets[round - 1];
            int last = roundTargets[roundTargets.Length - 1];
            return Mathf.RoundToInt(last * Mathf.Pow(targetGrowth, round - roundTargets.Length));
        }

        return Mathf.RoundToInt(targetScore * Mathf.Pow(targetGrowth, round - 1));
    }

    public override GameMode CreateMode() => new RogueDemoMode(this);

    /// <summary>
    /// The librarian pool joins the stamp for the same reason the modifier and
    /// bookmark pools do: a librarian's numbers are tuning knobs, and a run
    /// resumed against a retuned one would play a round the save doesn't
    /// describe. The base stamp covers this asset's own fields, but the pool
    /// arrives there as instance IDs, which are deliberately zeroed — so the
    /// assets themselves have to be stamped by hand.
    /// </summary>
    public override string Fingerprint() => base.Fingerprint() + StampAll(librarians);
}
