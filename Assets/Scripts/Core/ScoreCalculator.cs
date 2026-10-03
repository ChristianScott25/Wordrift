using System.Collections.Generic;

/// <summary>
/// Turns a chain of tiles into a score, as TWO numbers that multiply:
///
///   5 LETTERS            5 x 2     both numbers open on the word's LENGTH
///   each tile, in turn  +3 ...     its value through its own 2L/3L
///     and its 2W/3W     x3 ...     on that tile's own beat
///   -> the run's bookmarks, in slot order, each pushing one side or the other
///   -> the round's rule (the librarian, plus anything armed)
///   -> the mode's own score multiplier
///   -> POINTS x MULT
///
/// 🎯 EVERY ONE OF THOSE IS A BEAT THE PLAYER WATCHES. That is why the tile
/// stage runs through a ScoringContext instead of summing into a local: a score
/// that arrives as one number is a score nobody can check. GameSession steps
/// through WordResult.Steps after ENTER and the HUD shakes whatever each one
/// points at.
///
/// ⚠️ LENGTH PAYS TWICE, which is the 2026-09-30 change. It always set the
/// multiplier; it now sets the opening Points as well (ModeConfig.LengthPoints).
/// It is what gives a plain word with no upgrades something to animate, and it
/// makes every word worth roughly 1.7x what it used to be — round targets have
/// NOT been retuned for that yet.
///
/// ⚠️ THE LIVE READOUT IS Opening() AND NOTHING MORE — it shows what the LENGTH
/// is worth, never what the tiles add. The whole point of the walk-through is to
/// show the tiles arriving, and a preview that had already counted them would
/// give the answer away before the show started. His call, 2026-10-01.
///
/// ⚠️ TILE ORDER CHANGES THE SCORE. A 2W/3W fires on its own tile's beat and
/// multiplies everything piled up so far, so dragging through a 3W last is worth
/// more than dragging through it first. Deliberate, and the reason this walk is
/// strictly in chain order. A 2L/3L is NOT like that — it only multiplies its own
/// tile's worth, so it folds into that tile's beat and order can't touch it.
///
/// The tile stages are fixed rules. The bookmark stage is the OPEN one: anything
/// that wants to intervene in scoring does it there, through the ScoringContext,
/// rather than by growing this class.
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
    /// The two numbers a word of this length OPENS on, before a single tile is
    /// counted. Both the live readout while you select AND the first frame of the
    /// walk-through, which is why there is one method and not two.
    ///
    /// 🎯 THE LIVE READOUT DELIBERATELY DOES NOT COUNT THE TILES. You see what
    /// the length is worth; what the tiles add is the thing you press PLAY to
    /// watch. Showing the finished number first would give away the whole
    /// walk-through before it ran — his call, 2026-10-01.
    ///
    /// ⚠️ THE ONE PLACE THE OPENING NUMBERS ARE WORKED OUT. A second copy of
    /// this expression is how the number you watched while selecting ends up
    /// disagreeing with the number the walk starts from, on screen, a frame
    /// apart.
    ///
    /// Safe to call on every frame of a drag: no context, no steps, no
    /// allocation — which is the whole of SelectionState's contract.
    /// </summary>
    public ScorePair Opening(IReadOnlyList<Tile> chain)
    {
        // LETTERS, not chain.Count: a "ch" tile is two letters out of one cell,
        // and length is what the player is told both numbers come from.
        // Null-checked here rather than inside LetterCount so WalkTiles' own
        // null guard is reachable instead of being thrown past.
        int letters = chain == null ? 0 : ChainController.LetterCount(chain);

        return new ScorePair
        {
            Points = ScoreLimits.Clamp((long)config.LengthPoints(letters)),
            Mult = ScoreLimits.ClampMult(config.LengthMultiplier(letters)),
        };
    }

    /// <summary>
    /// Opens the two numbers on the word's length and then walks the tiles into
    /// the context, one beat each. Returns the OPENING pair, before any tile.
    ///
    /// ⚠️ THE ONE PLACE THE TILE STAGE EXISTS. Only Evaluate calls it — the live
    /// readout is Opening() alone and never counts a tile — so there is exactly
    /// one answer to "what are these tiles worth", written once.
    ///
    /// ⚠️ Indexed, not foreach. `chain` arrives as an IReadOnlyList, so a foreach
    /// boxes the List's enumerator. That matters because of GameSession.BestOf,
    /// which scores once per candidate on a wild chain — hundreds of them, on
    /// every frame of a drag. Same reason CompositeScoreRule.Score is indexed.
    /// </summary>
    private ScorePair WalkTiles(ScoringContext ctx, IReadOnlyList<Tile> chain)
    {
        // The same method the live readout calls, so the number the player was
        // watching a frame ago is exactly the number this starts from.
        var opening = Opening(chain);
        ctx.Open(opening.Points, opening.Mult);

        if (chain == null) return opening;

        for (int i = 0; i < chain.Count; i++)
        {
            var tile = chain[i];
            if (tile == null) continue;

            // The spec, not the Tile: the word row draws COPIES of these tiles,
            // and the spec is the one object both the board's tile and its copy
            // hold. See ScoreStep.Actor.
            ctx.Acting(ScoreActor.Tile, tile.Spec);

            // ⚠️ The source goes in RAW — Record is what upper-cases it, and
            // Record doesn't run with recording off, so BestOf's hundreds of
            // speculative candidates cost no strings. Shown before Face so a
            // resolved wild's beat reads "E" rather than "*".
            string who = string.IsNullOrEmpty(tile.Shown) ? tile.Face : tile.Shown;

            // The tile's corner shows the BASE letter value; the badge is what
            // tells the player it gets multiplied. This is where that happens,
            // and it is the only place letter modifiers are applied. 2L/3L fold
            // into this one beat on purpose — a letter multiplier only multiplies
            // its own tile's worth, so firing it against the running total would
            // double the whole word.
            int worth = (int)TileModifier.ApplyLetterModifiers(tile.LetterPoints, tile.Modifiers);
            if (worth != 0) ctx.AddPoints(worth, who);
            else ctx.Beat(who);   // a wild is worth 0 — see ScoringContext.Beat

            // ⚠️ ON THIS TILE'S BEAT, NOT AFTER THE WORD. Not floored at 1: a
            // modifier returning 0 zeroing the word is a legitimate thing to
            // build later, and the context's clamp floors at 0 rather than
            // wrapping.
            var modifiers = tile.Modifiers;
            for (int m = 0; m < modifiers.Count; m++)
            {
                var modifier = modifiers[m];
                if (modifier != null && modifier.WordMultiplier != 1)
                    ctx.MultiplyPoints(modifier.WordMultiplier, modifier.badgeLabel);
            }
        }

        return opening;
    }

    /// <param name="wordsThisRound">
    /// What's already been played this round, so a bookmark can spot a repeat.
    /// Must NOT contain the word being scored yet.
    /// </param>
    /// <param name="bookmarks">The run's bookmarks in slot order; null for a mode without a run.</param>
    /// <param name="roundRule">
    /// The round's own rule, if it has one that touches the score — a librarian
    /// and anything armed, today. It goes AFTER the bookmarks on purpose: a round
    /// that taxes you taxes what you built, not what you started with.
    /// </param>
    /// <param name="recording">
    /// ⚠️ PASS FALSE WHEN ONLY THE NUMBER IS WANTED. GameSession.BestOf calls this
    /// once per candidate word on a chain holding a wild — which can be hundreds —
    /// and reads nothing but Points. Recording allocates about three strings per
    /// step and there is a step per tile, so leaving it on down that path is
    /// thousands of strings per frame of a drag.
    /// </param>
    public WordResult Evaluate(IReadOnlyList<Tile> chain, string word,
                               ICollection<string> wordsThisRound = null,
                               IReadOnlyList<BookmarkSpec> bookmarks = null,
                               IScoreRule roundRule = null,
                               bool recording = true)
    {
        // ⚠️ A FRESH ONE EVERY TIME, never a reused instance. WordResult.Steps is
        // handed out below as this context's own live List, and the HUD walks it
        // across several seconds of beats while new selections are raised
        // underneath — anything recycling a context would mutate the list being
        // walked and throw "Collection was modified" mid-score.
        var ctx = new ScoringContext
        {
            Word = word,
            Tiles = chain,
            MinWordLength = config.minWordLength,
            WordsThisRound = wordsThisRound,
            Recording = recording,
        };

        var opening = WalkTiles(ctx, chain);

        if (bookmarks != null)
        {
            for (int i = 0; i < bookmarks.Count; i++)
            {
                // Tagged BEFORE the call, which is the whole trick: not one
                // bookmark, librarian or consumable needed a line changed to join
                // the walk-through. They call the same AddMult they always did.
                ctx.Acting(ScoreActor.Bookmark, bookmarks[i]);
                bookmarks[i]?.Apply(ctx);
            }
        }

        // The ROUND's turn. Librarian by default — RogueDemoMode hands over the
        // bare asset when nothing is armed, so this is the common case; a
        // CompositeScoreRule re-tags each of its children on the way through.
        ctx.Acting(ScoreActor.Librarian, roundRule);
        roundRule?.Score(ctx);

        // The mode's own multiplier goes through the context like everything
        // else rather than being applied on the way out. That keeps ONE
        // invariant true — the score is always Points x Mult — so the readout
        // can't show a total that differs from what was actually awarded.
        ctx.Acting(ScoreActor.Mode, null);
        ctx.MultiplyMult(config.scoreMultiplier, config.displayName);

        // double, not float: Points can be a billion and Mult a million, and
        // float loses the low digits of that long before it overflows. The
        // clamp is what stops the cast wrapping — a float-to-int conversion out
        // of range is undefined in C#, and in practice lands on int.MinValue.
        int total = ScoreLimits.Clamp((double)ctx.Points * ctx.Mult);

        return new WordResult
        {
            Word = word,
            Accepted = true,
            Points = total,
            Opening = opening,
            FinalPoints = ctx.Points,
            FinalMult = ctx.Mult,
            Steps = ctx.Steps,
            TileCount = chain == null ? 0 : chain.Count,
        };
    }

    public static WordResult Rejected(string word, int tileCount) => new WordResult
    {
        Word = word,
        Accepted = false,
        Points = 0,
        Opening = new ScorePair { Points = 0, Mult = 1f },
        FinalMult = 1f,
        Steps = new List<ScoreStep>(),
        TileCount = tileCount,
    };
}
