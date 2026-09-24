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
    [UnityEngine.Tooltip("What this tile is, as the catalog authored it: \"a\", \"ch\", " +
                         "\"*\" for a wild, or \"a/e/i\" for a choice tile.")]
    // A string, not a char, and it is played WHOLE: ChainController.WordOf
    // concatenates each tile's spelling, so a "ch" tile contributes two letters
    // to the word from one board cell. There is deliberately no "first
    // character" accessor any more — one existed until 2026-09-17 and every
    // caller of it was a place the second letter went missing.
    //
    // This is the tile's whole identity and the ONLY thing saved about what it
    // is (RunSaveData.TileSpecData) — which is why a new kind of tile is a new
    // spelling here and not a new field, and why RunState.Resume can rebuild
    // one with nothing but this string and a score.
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
    // doesn't read configs. A wild or a choice tile is a special `letters` value
    // rather than a modifier — see the three views below.
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

    // ---- The three views of a tile ------------------------------------------
    //
    // A tile's authored `letters` is read three different ways, and keeping them
    // apart is what lets a choice tile exist without touching the dictionary,
    // the save format or any of the four places a chain is measured.
    //
    //   letters    Face      Spelling   Options   what it is
    //   "a"        "a"       "a"        ""        an ordinary letter
    //   "ch"       "ch"      "ch"       ""        a multi-letter tile
    //   "*"        "*"       "*"        ""        a wild
    //   "a/e/i"    "a/e/i"   "*"        "aei"     a choice tile
    //
    // The load-bearing row is the last one. ChainController.WordOf/LetterCount,
    // Board.LetterCount and GameSession.ShowResolvedLetters all walk a chain by
    // tile.Letters.Length, on the rule that one character of the pattern is one
    // character of the word. A choice tile that SPELLED "a/e/i" would count as
    // three letters toward the length multiplier, keep a dead round alive in
    // Board.LetterCount, and slide the display walk out of step for every tile
    // after it. Spelling a single "*" instead keeps all four honest with no
    // changes at all — it's the same reason a wild is "*" and not a word.

    /// <summary>
    /// What this tile DRAWS, and the string anything walking its letters should
    /// read — a choice tile weights every letter it could become in the Censor's
    /// pool, the same way a "ch" tile weights both of its own.
    /// </summary>
    public string Face =>
        string.IsNullOrEmpty(letters) ? "e" : letters.ToLowerInvariant();

    /// <summary>
    /// What this tile plays as, lowercased — the whole spelling, never a single
    /// character. "ch" is two letters out of one cell, which is the entire point
    /// of a multi-letter tile, so anything that narrows this to letters[0] is a
    /// bug rather than a shortcut.
    ///
    /// A CHOICE tile spells a wild's "*": it stands for exactly one letter that
    /// hasn't been decided yet, and GameSession.ResolveSelection narrows it to
    /// this tile's Options. See the table above for why it can't spell itself.
    /// </summary>
    public string Spelling
    {
        get
        {
            // Face once, not once per branch: it lowercases, which allocates.
            string face = Face;
            return face.IndexOf(ChoiceSeparator) >= 0 ? WildSpelling : face;
        }
    }

    /// <summary>
    /// What a WILD tile spells. A wild is a catalog row like every other kind of
    /// tile — its wildness IS its spelling — which is why nothing new has to be
    /// saved for it and why this is the only place the character is written down.
    /// </summary>
    public const string WildSpelling = "*";

    /// <summary>
    /// What separates a choice tile's options ("a/e/i"). The one place this
    /// character is written down, the same bargain WildSpelling takes — and the
    /// reason a new choice tile is a catalog row rather than any code at all.
    /// </summary>
    public const char ChoiceSeparator = '/';

    /// <summary>
    /// Becomes whichever single letter suits the word best, rather than spelling
    /// anything of its own. Worth 0, and NOT a letter: anything walking a tile's
    /// characters (the Censor's pool) has to skip it, and anything asking the
    /// dictionary has to resolve it first (GameSession.ResolveSelection).
    ///
    /// ⚠️ NOT `Spelling == WildSpelling` any more. A choice tile spells "*" too,
    /// so asking Spelling would report every choice tile as a wild — skipping
    /// them in the shop's upgrade rows and printing "*" in their corner.
    ///
    /// Reads the raw field rather than Face because this is asked on the
    /// PER-FRAME path (Tile.RefreshScoreLabel, and once per tile in the chain on
    /// every selection change) and Face lowercases, which allocates. "*" has no
    /// case for the normalising to matter to.
    /// </summary>
    public bool IsWild => letters == WildSpelling;

    /// <summary>
    /// Becomes one of a FIXED few letters — "a/e/i" is an a, an e or an i and
    /// nothing else. A wild with a fence around it, which is why it can be worth
    /// points and cost a fraction of one.
    ///
    /// Reads the raw field rather than Face, for the reason given on IsWild — and
    /// the separator has no case either.
    /// </summary>
    public bool IsChoice => letters != null && letters.IndexOf(ChoiceSeparator) >= 0;

    /// <summary>
    /// The letters a choice tile may become, with the separators taken out
    /// ("a/e/i" → "aei"), or empty for every other kind of tile.
    ///
    /// ⚠️ Comes back in the order the catalog authored it, and WordValidator.Matches
    /// needs that order ALPHABETICAL: it fills restricted slots left to right and
    /// relies on the results arriving sorted, which is how ResolveSelection's
    /// "first of equal answers" means the same word every time.
    ///
    /// ⚠️ Built off Face, so it is always LOWERCASE — Matches lowercases the
    /// pattern it is given but not the options, and an uppercase option would
    /// simply never match anything.
    ///
    /// ⚠️ ALLOCATES. Tile stamps it once in Init and the per-frame path reads it
    /// from there; don't call this one in a loop that runs while a finger moves.
    /// </summary>
    public string Options => IsChoice ? Face.Replace(SeparatorText, "") : "";

    // The separator as a string, because string.Replace has no "delete this
    // char" overload. DERIVED from the char rather than written out again — two
    // copies of one character is how they eventually stop being one character.
    private static readonly string SeparatorText = ChoiceSeparator.ToString();
}
