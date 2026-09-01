using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a chain of tiles into a score, as TWO numbers that multiply:
///
///   POINTS  =  each tile's value through its own 2L/3L,
///              summed, then times every 2W/3W on the word
///   MULT    =  the word's LENGTH, off the mode's curve
///
///     -> the run's bookmarks, in slot order, each pushing one or the other
///     -> the mode's own score multiplier, as one more step
///     -> POINTS x MULT
///
/// The two-number split is the scoring model the player sees: Base() is what
/// the HUD shows live while tiles are being selected, and Evaluate() is what
/// happens after ENTER. They share the same first stage on purpose — a preview
/// that computed its number differently would eventually disagree with the
/// real thing.
///
/// The tile stages are fixed rules. The bookmark stage is the OPEN one: anything
/// that wants to intervene in scoring does it there, through the ScoringContext,
/// rather than by growing this class. Modes with no bookmarks pass none and the
/// stage is a no-op.
///
/// Word multipliers land on POINTS rather than MULT deliberately: a 2W is part
/// of what the tiles are worth. It matters — a 3W then "+10 points" is
/// (P*3+10)*M, where the same 3W on the mult side would be (P+10)*(M*3).
/// </summary>
public class ScoreCalculator
{
    private readonly ModeConfig config;

    public ScoreCalculator(ModeConfig config) => this.config = config;

    /// <summary>
    /// What a selection is worth before any bookmark touches it — the pair of
    /// numbers the HUD shows live. Pure: no side effects, safe to call every
    /// time the selection changes.
    /// </summary>
    public ScorePair Base(IReadOnlyList<Tile> chain)
    {
        int points = 0;
        int wordMultiplier = 1;

        foreach (var tile in chain)
        {
            if (tile == null) continue;

            // The tile's corner shows the BASE letter value; the badge is what
            // tells the player it gets multiplied. This is where that actually
            // happens, and it's the only place letter modifiers are applied.
            // The tile carries its own base worth (TileSpec.baseScore), so a
            // specific tile's value can differ from its letter's usual one.
            points += TileModifier.ApplyLetterModifiers(tile.LetterPoints, tile.Modifiers);

            foreach (var modifier in tile.Modifiers)
                if (modifier != null) wordMultiplier *= modifier.WordMultiplier;
        }

        return new ScorePair
        {
            Points = points * wordMultiplier,
            Mult = config.LengthMultiplier(chain.Count),
            WordMultiplier = wordMultiplier,
        };
    }

    /// <param name="wordsThisRound">
    /// What's already been played this round, so a bookmark can spot a repeat.
    /// Must NOT contain the word being scored yet.
    /// </param>
    /// <param name="bookmarks">The run's bookmarks in slot order; null for a mode without a run.</param>
    public WordResult Evaluate(IReadOnlyList<Tile> chain, string word,
                               ICollection<string> wordsThisRound = null,
                               IReadOnlyList<BookmarkSpec> bookmarks = null)
    {
        var start = Base(chain);

        var ctx = new ScoringContext
        {
            Word = word,
            Tiles = chain,
            WordsThisRound = wordsThisRound,
            Points = start.Points,
            Mult = start.Mult,
        };

        if (bookmarks != null)
            for (int i = 0; i < bookmarks.Count; i++)
                bookmarks[i]?.Apply(ctx);   // slot order is the call order

        // The mode's own multiplier goes through the context like everything
        // else rather than being applied on the way out. That keeps ONE
        // invariant true — the score is always Points x Mult — so the readout
        // can't show a total that differs from what was actually awarded.
        if (!Mathf.Approximately(config.scoreMultiplier, 1f))
            ctx.MultiplyMult(config.scoreMultiplier, config.displayName);

        int total = Mathf.RoundToInt(ctx.Points * ctx.Mult);

        return new WordResult
        {
            Word = word,
            Accepted = true,
            Points = total,
            Base = start,
            FinalPoints = ctx.Points,
            FinalMult = ctx.Mult,
            Steps = ctx.Steps,
            TileCount = chain.Count,
        };
    }

    public static WordResult Rejected(string word, int tileCount) => new WordResult
    {
        Word = word,
        Accepted = false,
        Points = 0,
        Base = new ScorePair { Points = 0, Mult = 1f, WordMultiplier = 1 },
        FinalMult = 1f,
        Steps = new List<ScoreStep>(),
        TileCount = tileCount,
    };
}
