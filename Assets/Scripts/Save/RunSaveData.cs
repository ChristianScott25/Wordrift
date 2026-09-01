using System;
using System.Collections.Generic;

/// <summary>
/// Which screen the player was on when the run was saved. Resuming loads this
/// scene, so a run interrupted mid-round comes back mid-round rather than at the
/// start of one.
/// </summary>
public enum SaveLocation
{
    Game,
    Shop,
}

/// <summary>
/// One saved run, as it goes to disk. Plain fields only: JsonUtility can't
/// serialize dictionaries, interfaces, or subclass fields through a base-typed
/// reference, so everything here is flat on purpose and parallel arrays stand in
/// for the one map we need.
///
/// THE STANDING RULE: anything a run or a round remembers has to be captured
/// here, or it silently resets when the player resumes. Adding state to RunState
/// or to a GameMode without adding it to this file is the bug — and it's a quiet
/// one, because a resumed run with a reset counter still looks like a working run.
///
/// Assets are named by their file name (see RunState.Resume), which keeps the
/// file readable and costs nothing on the asset side; the trade is that renaming
/// a bookmark or modifier asset invalidates existing saves.
/// </summary>
[Serializable]
public class RunSaveData
{
    /// <summary>Format version. A save from another version is discarded, never migrated.</summary>
    public int version;

    /// <summary>Asset name of the ModeConfig this run is being played on.</summary>
    public string modeConfigName;

    /// <summary>
    /// A hash of every authored number this run depends on (see
    /// ModeConfig.Fingerprint). Tuning a value in the Inspector changes it, and a
    /// mismatch throws the save away rather than resuming a run that would
    /// silently ignore the change.
    /// </summary>
    public string configFingerprint;

    public string seedCode;
    public int round;
    public int money;
    public int lastPayout;

    public SaveLocation location;

    /// <summary>The run's tiles, by value and in order. Everything else indexes into this.</summary>
    public List<TileSpecData> tileBag = new();

    /// <summary>Owned bookmarks by asset name, in slot order — which is scoring order.</summary>
    public List<string> bookmarks = new();

    /// <summary>The round in progress. Only meaningful when location is Game.</summary>
    public RoundSnapshot roundState;

    /// <summary>The shelf as it stood. Only meaningful when location is Shop.</summary>
    public ShopSnapshot shopState;
}

/// <summary>One entry of the run's tile bag. The run-mutable half of a TileSpec.</summary>
[Serializable]
public class TileSpecData
{
    public string letters;
    public int baseScore;

    /// <summary>Modifier asset names, in the order the tile carries them.</summary>
    public List<string> modifiers = new();
}

/// <summary>
/// A round frozen mid-play. The board and the bag are both recorded as INDICES
/// into RunSaveData.tileBag — a tile's identity is which entry of the run's bag
/// it is, so indices are the only honest way to say "this tile" across a save.
///
/// Tiles already played or discarded this round appear in neither list, which is
/// exactly what makes them gone for the round and back next round.
/// </summary>
[Serializable]
public class RoundSnapshot
{
    /// <summary>
    /// This snapshot was actually written, rather than conjured by the reader.
    /// JsonUtility does NOT round-trip a null reference to a serializable class —
    /// it hands back a default-constructed instance instead — so without a flag
    /// an absent snapshot is indistinguishable from a real one describing an
    /// empty board, and restoring it would wipe the board on resume.
    /// </summary>
    public bool captured;

    public int score;
    public int wordsFound;
    public int bestWordPoints;
    public string bestWord = "";

    /// <summary>Words accepted so far this round. Deja Vu reads this to spot a repeat.</summary>
    public List<string> wordsThisRound = new();

    /// <summary>Bag indices still undrawn.</summary>
    public List<int> bagRemaining = new();

    // The board, as three parallel lists: cell x, cell y, and the bag index of
    // the tile sitting there. A Dictionary would be the natural type and is
    // exactly what JsonUtility refuses to serialize.
    public List<int> boardCellX = new();
    public List<int> boardCellY = new();
    public List<int> boardTile = new();

    // ---- Filled in by the mode, via GameMode.CaptureRound ----------------

    /// <summary>Words the mode will still allow. RogueDemoMode's move counter.</summary>
    public int movesLeft;

    /// <summary>Tiles the mode will still let the player throw away.</summary>
    public int discardsLeft;

    /// <summary>
    /// How far into its stream the bag had drawn. Without it a resumed round
    /// would hold the right tiles but deal them in a different order from the
    /// one it was about to — the same wind-forward ShopSnapshot.rngDraws does.
    /// </summary>
    public int bagDraws;
}

/// <summary>
/// The shop's shelf, saved so a resumed visit offers the same things at the same
/// prices rather than re-rolling. Stock is recorded rather than re-derived: the
/// roll order changes whenever the shop's code does, and a saved shelf shouldn't
/// depend on that.
/// </summary>
[Serializable]
public class ShopSnapshot
{
    /// <summary>See RoundSnapshot.captured — same JsonUtility trap, same guard.</summary>
    public bool captured;

    /// <summary>
    /// How many values the shop had already taken from its stream. A restored
    /// stream is re-derived from the seed, which starts it over, so it gets wound
    /// forward by this much (Rng.Skip) — otherwise the next re-roll would repeat
    /// one the player has already seen.
    /// </summary>
    public int rngDraws;

    public List<ShopOfferData> offers = new();
}

/// <summary>
/// One row of the shelf. A `kind` string rather than a subclass hierarchy,
/// because JsonUtility can't serialize one — the shop reads this back into its
/// own Offer types, which is where the polymorphism lives.
/// </summary>
[Serializable]
public class ShopOfferData
{
    public const string Modifier = "modifier";
    public const string Bookmark = "bookmark";

    public string kind;

    /// <summary>The TileModifier or Bookmark asset's name.</summary>
    public string assetName;

    /// <summary>Bag index the upgrade would land on. -1 for a bookmark offer.</summary>
    public int targetTile = -1;

    /// <summary>Purchases made this visit — what escalates a modifier's price.</summary>
    public int timesBought;

    // Deliberately NOT recorded: whether the row was sold out. An empty
    // assetName already says so, and the restored offer works it out through the
    // same InStock the live one uses — a second copy of that answer could only
    // ever disagree with the first.
}
