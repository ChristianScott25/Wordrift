using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything one run remembers between rounds: which round you're on, the
/// tiles you own, the bookmarks you've bought, the money you've banked, and the
/// seed every roll in the run comes from. Tile-bag abilities will live here too.
///
/// Plain C# held in a static, like ModeSelection, because scene loads wipe
/// object references. Mutable ON PURPOSE — shops and bookmarks edit this run.
/// The authored assets a run starts from (the mode config, the letter set) are
/// read-only recipes; nothing may ever write into them at runtime.
/// </summary>
public class RunState
{
    /// <summary>The run in progress, or null when there isn't one.</summary>
    public static RunState Current { get; private set; }

    /// <summary>The config this run was started from. Read it, never write it.</summary>
    public RogueDemoModeConfig Template { get; }

    /// <summary>1-based: round 1 is the first. Advanced by the shop's Continue.</summary>
    public int Round { get; private set; } = 1;

    // ---- Randomness ----------------------------------------------------
    // Every roll in a run comes from here, so a run can be replayed exactly.
    // The code is the authority, not a number: it's what the player reads off
    // the screen and — once there's a menu for it — types back in, so the code
    // that produced a run is literally the code that reproduces it.

    /// <summary>The run's seed, as the player sees it. Eight readable characters.</summary>
    public string SeedCode { get; private set; }

    /// <summary>Which tiles come out of the bag.</summary>
    public const string BagStream = "bag";

    /// <summary>What the shop offers, and which tile an upgrade lands on.</summary>
    public const string ShopStream = "shop";

    /// <summary>
    /// The run's tiles. The full bag comes back at the start of every round —
    /// playing tiles never shrinks it (TileBag drains a copy of this list).
    /// Changing THIS list is how shops and bookmarks alter what the player
    /// draws, and the change sticks for the rest of the run.
    /// </summary>
    public List<TileSpec> TileBag { get; } = new();

    /// <summary>
    /// The bookmarks this run owns, in slot order — which is the order they get
    /// to touch a word's score. No cap on how many: with three in the game a
    /// limit would be invisible, and a slot count is easy to add to the config
    /// when there's a reason for one.
    /// </summary>
    public List<BookmarkSpec> Bookmarks { get; } = new();

    /// <summary>
    /// A generator for one purpose, this round. Streams are independent by
    /// name AND by round, which buys two things worth having: a change to how
    /// often the shop rolls can never shift the tiles drawn from the bag, and
    /// round 3 deals the same bag regardless of what happened in rounds 1 and 2
    /// — so a round can be reproduced without replaying the ones before it.
    ///
    /// Take one per purpose and keep it; asking twice restarts the sequence.
    /// </summary>
    public Rng StreamFor(string streamName) => Rng.Stream(SeedCode, streamName, Round);

    /// <summary>Already owned? The shop never offers a duplicate.</summary>
    public bool Owns(Bookmark bookmark)
    {
        if (bookmark == null) return false;
        foreach (var owned in Bookmarks)
            if (owned.bookmark == bookmark) return true;
        return false;
    }

    /// <summary>Takes ownership of a bookmark, refusing a duplicate.</summary>
    public bool AddBookmark(Bookmark bookmark)
    {
        if (bookmark == null || Owns(bookmark)) return false;
        Bookmarks.Add(new BookmarkSpec(bookmark));
        return true;
    }

    /// <summary>
    /// What the run has to spend. Earned by clearing rounds, spent in the shop,
    /// and gone the moment the run is — money never outlives a run, so there is
    /// nothing to save to disk and no meta-currency to reason about yet.
    /// </summary>
    public int Money { get; private set; }

    /// <summary>What the last cleared round paid, so the shop can say "+$20 EARNED".</summary>
    public int LastPayout { get; private set; }

    public int TargetScore => Template.TargetForRound(Round);

    /// <summary>Pays the run. The only way money comes IN — see RogueDemoModeConfig.RewardFor.</summary>
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        Money += amount;
        LastPayout = amount;
    }

    public bool CanAfford(int price) => Money >= price;

    /// <summary>
    /// Takes money for a purchase, or refuses and changes nothing. The only way
    /// money goes OUT: there's no setter, so the balance can't go negative and
    /// every spend is a call site you can find.
    /// </summary>
    public bool TrySpend(int price)
    {
        if (price < 0 || !CanAfford(price)) return false;
        Money -= price;
        return true;
    }

    public static RunState StartNew(RogueDemoModeConfig template)
    {
        Current = new RunState(template);
        return Current;
    }

    /// <summary>The run is over — lost, or abandoned from the main menu.</summary>
    public static void End() => Current = null;

    public void AdvanceRound() => Round++;

    private RunState(RogueDemoModeConfig template)
    {
        Template = template;

        // A fresh seed per run. Losing and pressing PLAY AGAIN starts a NEW
        // run, so it gets a new one — replaying a seed will be something the
        // player asks for deliberately, once there's somewhere to type it.
        SeedCode = Rng.NewSeedCode();
        if (template == null || template.letterSet == null)
        {
            Debug.LogError("Run started with no letter set, so the tile bag is empty.");
            return;
        }

        // The LetterSet decides the MIX; the mode config decides how many tiles
        // that mix is shared out over. Keeping the two apart is what lets the
        // bag be resized — by a config tweak now, by an upgrade later — without
        // anyone re-authoring 26 weights.
        TileBag.AddRange(template.letterSet.BuildTileBag(template.tileBagSize));

        if (TileBag.Count == 0)
            Debug.LogError($"LetterSet '{template.letterSet.name}' has no positive weights, so the tile bag is empty.");
    }
}
