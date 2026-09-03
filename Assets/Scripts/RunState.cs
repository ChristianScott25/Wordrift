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

    /// <summary>Which librarian a run meets on a librarian round.</summary>
    public const string LibrarianStream = "librarian";

    /// <summary>
    /// What that librarian then chooses for itself — which letter The Censor
    /// bans. Separate from LibrarianStream so that adding a choosing librarian
    /// can't shift which librarian a seed picks, and drawn only in
    /// RogueDemoMode.Begin: keyed to the round, it re-derives the same answer
    /// every time the round is entered, which is why nothing about it is saved.
    /// </summary>
    public const string LibrarianRoundStream = "librarian-round";

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

    // ---- Librarians ----------------------------------------------------

    /// <summary>
    /// The librarian this round is played against, or null on an ordinary round.
    /// Decided when the round number changes and then FIXED — asking again would
    /// re-roll it, and a rule that changes while you're playing under it isn't a
    /// rule.
    /// </summary>
    public Librarian Librarian { get; private set; }

    /// <summary>
    /// Librarians this run hasn't met yet. Drawing removes one, and running out
    /// refills from the pool — so a run sees all of them before it sees any of
    /// them twice. This is the whole no-repeat rule, and it's why the list is
    /// run state rather than something derived from the round number.
    /// </summary>
    private readonly List<Librarian> librariansUnseen = new();

    /// <summary>
    /// Draws this round's librarian, or clears it on an ordinary round.
    ///
    /// The roll comes off the run's seed keyed to this round, so a seed always
    /// meets the same librarians in the same order — but WHICH ones are still
    /// available depends on the rounds before it, which is exactly what the
    /// no-repeat rule means and exactly why librariansUnseen is saved.
    ///
    /// Note what this deliberately allows: when the last librarian is drawn and
    /// the pool refills, the very next librarian round can draw the same one
    /// again. Every one has still been seen before any repeats, which is the
    /// rule as stated; forbidding the seam would need a memory of the last draw,
    /// and it isn't obviously the better game.
    /// </summary>
    private void PickLibrarian()
    {
        Librarian = null;

        if (Template == null) return;
        int every = Template.librarianEveryRounds;
        if (every <= 0 || Round % every != 0) return;

        if (librariansUnseen.Count == 0) RefillLibrarians();
        if (librariansUnseen.Count == 0) return;

        // One draw, one throwaway stream. Nothing else takes from this stream,
        // so there is no position to record — unlike the bag and the shop, this
        // is answered once per round and then remembered as an answer.
        int i = StreamFor(LibrarianStream).Range(0, librariansUnseen.Count);
        Librarian = librariansUnseen[i];
        librariansUnseen.RemoveAt(i);
    }

    private void RefillLibrarians()
    {
        librariansUnseen.Clear();
        if (Template?.librarians == null) return;
        foreach (var librarian in Template.librarians)
            if (librarian != null) librariansUnseen.Add(librarian);
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
        // Saturating, for the same reason the score is (see ScoreLimits): a
        // balance that wrapped would go NEGATIVE, and TrySpend would then refuse
        // everything in the shop with no explanation. Cheap insurance —
        // RogueDemoModeConfig.maxRoundPayout already bounds what one round pays.
        Money = (int)System.Math.Min((long)Money + amount, int.MaxValue);
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

    /// <summary>
    /// The run is over — lost, or abandoned from the main menu. Deleting the save
    /// is part of ending it rather than a separate call, so a dead run can never
    /// be resumed from a file somebody forgot to clean up.
    /// </summary>
    public static void End()
    {
        Current = null;
        pendingRound = null;
        pendingShop = null;
        RunSave.Delete();
    }

    /// <summary>
    /// Moves the run on a round. Also the moment the next round's librarian is
    /// decided — the round number is what says whether there is one, so the two
    /// belong in one act rather than in whoever remembers to ask.
    /// </summary>
    public void AdvanceRound()
    {
        Round++;
        PickLibrarian();
    }

    // ---- Saving and resuming ------------------------------------------------
    //
    // THE STANDING RULE: state added to this class has to be captured below and
    // restored in Resume, or it silently resets when the player continues. See
    // RunSaveData — the failure mode is quiet, because a run with a reset counter
    // still looks like a working run.

    /// <summary>
    /// The round or shop snapshot a resumed run hasn't consumed yet. Statics for
    /// the same reason ModeSelection is one: the scene load that carries the
    /// player back to where they were wipes every object reference on the way.
    /// Taken ONCE, by whichever screen loads.
    /// </summary>
    private static RoundSnapshot pendingRound;

    /// <summary>See pendingRound. Consumed by ShopScreen.</summary>
    private static ShopSnapshot pendingShop;

    public static RoundSnapshot TakePendingRound()
    {
        var pending = pendingRound;
        pendingRound = null;
        return pending;
    }

    public static ShopSnapshot TakePendingShop()
    {
        var pending = pendingShop;
        pendingShop = null;
        return pending;
    }

    /// <summary>
    /// Everything the RUN remembers, ready for a screen to add its own state to
    /// and hand to RunSave. The round and shop halves are filled in by whoever
    /// is on screen — this only knows the run.
    /// </summary>
    public RunSaveData Capture(SaveLocation location)
    {
        var data = new RunSaveData
        {
            modeConfigName = Template == null ? "" : Template.name,
            configFingerprint = fingerprint,
            seedCode = SeedCode,
            round = Round,
            money = Money,
            lastPayout = LastPayout,
            location = location,
        };

        foreach (var tile in TileBag)
        {
            var entry = new TileSpecData { letters = tile.letters, baseScore = tile.baseScore };
            if (tile.modifiers != null)
                foreach (var modifier in tile.modifiers)
                    if (modifier != null) entry.modifiers.Add(modifier.name);
            data.tileBag.Add(entry);
        }

        foreach (var owned in Bookmarks)
            if (owned?.bookmark != null) data.bookmarks.Add(owned.bookmark.name);

        // Both halves, and both are load-bearing: the current one because it's
        // the round's rule, and the unseen pool because it's the no-repeat rule.
        // Saving only the first would resume the right round into the wrong run.
        data.librarian = Librarian == null ? "" : Librarian.name;
        foreach (var librarian in librariansUnseen)
            if (librarian != null) data.librariansUnseen.Add(librarian.name);

        return data;
    }

    /// <summary>
    /// Which entry of the bag each tile is. A tile's identity IS its position in
    /// this list — that's what lets the board and the drawn-down bag be saved as
    /// indices, and what makes a shop upgrade land on the same tile after a
    /// resume. Built fresh per save; the bag is small and it can't go stale.
    /// </summary>
    public Dictionary<TileSpec, int> TileIndex()
    {
        var index = new Dictionary<TileSpec, int>(TileBag.Count);
        for (int i = 0; i < TileBag.Count; i++) index[TileBag[i]] = i;
        return index;
    }

    /// <summary>The bag entry at this index, or null when a save names one that isn't there.</summary>
    public TileSpec TileAt(int index) =>
        index >= 0 && index < TileBag.Count ? TileBag[index] : null;

    /// <summary>
    /// Could this save be picked up on this template as it is currently tuned?
    /// The menu asks before it offers CONTINUE and Resume asks again before it
    /// acts, so the button can never offer something the resume would refuse —
    /// the same reason SelectionState publishes decisions rather than facts.
    ///
    /// False is the normal answer, not an error: it covers a save for another
    /// mode and a save made before an Inspector tweak alike.
    /// </summary>
    public static bool CanResume(ModeConfig template, RunSaveData data) =>
        template != null && data != null &&
        template.name == data.modeConfigName &&
        FingerprintOf(template) == data.configFingerprint;

    /// <summary>
    /// Rebuilds a run from a save, or returns null when the save doesn't fit this
    /// template. A run that quietly ignored a retuned number would be worse than
    /// one that's gone — see CanResume.
    /// </summary>
    public static RunState Resume(RogueDemoModeConfig template, RunSaveData data)
    {
        if (!CanResume(template, data))
        {
            Debug.Log("Saved run doesn't match this mode as it's currently tuned — discarding it.");
            return null;
        }

        var run = new RunState(template, data);
        Current = run;
        pendingRound = data.location == SaveLocation.Game ? data.roundState : null;
        pendingShop = data.location == SaveLocation.Shop ? data.shopState : null;
        return run;
    }

    private static string FingerprintOf(ModeConfig template) =>
        template == null ? "" : Rng.Hash(template.Fingerprint()).ToString("x16");

    // Taken once, when the run starts. Building it walks the whole config and
    // every asset it points at, and Capture runs after every single word — a
    // couple of dozen kilobytes of throwaway strings per move is not worth
    // paying for a value that cannot change while the run is being played. In
    // the editor a mid-play tweak isn't picked up here, which is right: the
    // fingerprint only ever matters ACROSS sessions, and the next launch
    // recomputes it and refuses the save exactly as intended.
    private readonly string fingerprint;

    /// <summary>
    /// Finds an authored asset a save named, by asset file name, in the pool the
    /// mode config already lists. Resolving through the config rather than a
    /// global registry means there is nothing to keep in sync — the cost being
    /// that an asset a run somehow gained from OUTSIDE the mode's pool couldn't
    /// come back. Nothing hands one out today.
    /// </summary>
    private static T Resolve<T>(List<T> pool, string assetName) where T : Object
    {
        if (pool == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (var asset in pool)
            if (asset != null && asset.name == assetName) return asset;

        Debug.LogWarning($"Saved run refers to '{assetName}', which isn't in this mode's pool — dropping it.");
        return null;
    }

    /// <summary>Rebuilds a run from a save. See Resume, which is the way in.</summary>
    private RunState(RogueDemoModeConfig template, RunSaveData data)
    {
        Template = template;
        fingerprint = FingerprintOf(template);
        SeedCode = data.seedCode;
        Round = Mathf.Max(1, data.round);
        Money = Mathf.Max(0, data.money);
        LastPayout = Mathf.Max(0, data.lastPayout);

        // Rebuilt by value, in order: the indices the board and the bag were
        // saved as only mean anything against this list, so its order is part of
        // the save format.
        foreach (var entry in data.tileBag)
        {
            var spec = new TileSpec { letters = entry.letters, baseScore = entry.baseScore };
            if (entry.modifiers != null)
                foreach (var name in entry.modifiers)
                    spec.AddModifier(Resolve(template.tileModifiers, name));
            TileBag.Add(spec);
        }

        foreach (var name in data.bookmarks)
        {
            var bookmark = Resolve(template.bookmarks, name);
            if (bookmark != null) Bookmarks.Add(new BookmarkSpec(bookmark));
        }

        // Read back, never re-rolled. Re-deriving the draw would give the same
        // answer only while the pool and the roll order both stayed put, and the
        // pool is exactly the thing a run spends.
        foreach (var name in data.librariansUnseen)
        {
            var librarian = Resolve(template.librarians, name);
            if (librarian != null) librariansUnseen.Add(librarian);
        }
        Librarian = string.IsNullOrEmpty(data.librarian)
            ? null
            : Resolve(template.librarians, data.librarian);
    }

    private RunState(RogueDemoModeConfig template)
    {
        Template = template;
        fingerprint = FingerprintOf(template);

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

        // Round 1 normally has no librarian; asking anyway keeps the rule in one
        // place, so a config that made every round a librarian round would work.
        RefillLibrarians();
        PickLibrarian();
    }
}
