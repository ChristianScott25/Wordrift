using System.Collections.Generic;

/// <summary>
/// One tile as it exists in the run's tile bag — the identity a tile keeps between
/// rounds. The Tile MonoBehaviour is just this spec's body on the board for
/// one round; the spec is what a shop sells, a bookmark upgrades, and a run
/// remembers.
///
/// A class, not a struct, on purpose: upgrading a specific tile ("gild THIS e")
/// needs identity, and the bag holds these directly.
/// </summary>
[System.Serializable]
public class TileSpec
{
    [UnityEngine.Tooltip("What this tile spells. Usually one letter.")]
    // A string, not a char: multi-letter tiles ("qu", "ie") are planned. Nothing
    // downstream speaks multi-letter yet — Tile.Letter and ChainController.WordOf
    // are per-character — so until they widen, only the first character plays.
    // Widen those, not this.
    public string letters = "a";

    [UnityEngine.Tooltip("What this tile is worth before any modifiers or bonuses.")]
    // The tile's OWN copy of its worth, stamped from the LetterSet catalog when
    // the spec is made (see LetterSet.CreateSpec). It lives here, not looked up
    // at scoring time, so a specific tile's value can diverge from its letter's
    // — a gilded E worth 5 is just a spec with baseScore 5. Anything that adds
    // score later stacks ON this; this stays what the corner of the tile shows.
    public int baseScore = 1;

    [UnityEngine.Tooltip("Modifiers this tile carries permanently, e.g. a bought 2L tile.")]
    // The ONLY way a tile gets a modifier: it's part of the tile, applied by an
    // upgrade, and a tile can hold several — scoring walks them in order and the
    // tile draws one badge each. How MANY it may hold is a mode's rule
    // (ModeConfig.maxModifiersPerTile), passed in rather than known here: Core
    // doesn't read configs. Wild tiles are an open design question — a special
    // letters value ("?") or a modifier — not decided here.
    public List<TileModifier> modifiers;

    /// <summary>How many modifiers this tile carries. 0 for a plain tile.</summary>
    public int ModifierCount => modifiers == null ? 0 : modifiers.Count;

    /// <summary>
    /// Is there room for another? The limit arrives as an argument because it
    /// belongs to the MODE, not to the tile — and 0 means no limit, so a caller
    /// that has no rule can pass nothing and get the old behaviour.
    /// </summary>
    public bool CanAddModifier(int limit = 0) => limit <= 0 || ModifierCount < limit;

    /// <summary>
    /// Upgrades this tile with another modifier. How the shop gilds an E.
    ///
    /// Returns false when the tile is full, and the caller must NOT have taken
    /// money — the shop only ever offers tiles that pass CanAddModifier, so a
    /// false here means something upstream is wrong, not that a purchase fell
    /// through. This is the single place a tile changes, so it's the only honest
    /// place to enforce the limit.
    /// </summary>
    public bool AddModifier(TileModifier modifier, int limit = 0)
    {
        if (modifier == null || !CanAddModifier(limit)) return false;
        modifiers ??= new List<TileModifier>();
        modifiers.Add(modifier);
        return true;
    }

    /// <summary>The single character this tile plays as, until multi-letter lands.</summary>
    public char Letter =>
        string.IsNullOrEmpty(letters) ? 'e' : char.ToLowerInvariant(letters[0]);
}
