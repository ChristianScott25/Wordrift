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
    // upgrade, and a tile can hold several (scoring walks them in order; the
    // badge currently only shows the last one). Wild tiles are an open design
    // question — a special letters value ("?") or a modifier — not decided here.
    public List<TileModifier> modifiers;

    /// <summary>Upgrades this tile with another modifier. How a shop will gild an E.</summary>
    public void AddModifier(TileModifier modifier)
    {
        if (modifier == null) return;
        modifiers ??= new List<TileModifier>();
        modifiers.Add(modifier);
    }

    /// <summary>The single character this tile plays as, until multi-letter lands.</summary>
    public char Letter =>
        string.IsNullOrEmpty(letters) ? 'e' : char.ToLowerInvariant(letters[0]);
}
