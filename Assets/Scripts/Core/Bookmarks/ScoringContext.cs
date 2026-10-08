using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One word being scored, as TWO running numbers that everything gets to change:
/// Points and Mult. The final score is Points x Mult.
///
/// The split is the whole scoring model, not an implementation detail. Both
/// numbers OPEN at the word's LENGTH — so many letters, so many points, and a
/// multiplier off the same curve — and then every tile adds itself in turn,
/// then the bookmarks, then the round, then the mode. A bookmark pushes one
/// side or the other, and because it can ADD to Mult as well as multiply it,
/// the order they run in changes the answer. That ordering is a lot of where
/// the depth will come from, which is why RunState keeps them in a list.
///
/// ⚠️ TILE ORDER MATTERS TOO, as of the walk-through. A 2W/3W fires on its own
/// tile's beat and multiplies whatever has piled up so far, so the same tiles
/// dragged in a different order are a different score. See ScoreCalculator.
///
/// Every change goes through AddPoints / MultiplyPoints / AddMult / MultiplyMult
/// rather than touching the fields, because each one records a Step. The steps
/// are what the HUD plays back one beat at a time after ENTER — without them the
/// readout can say "x2" but never "BOOKEND x2", and nothing on screen would know
/// which card to shake.
///
/// ⚠️ RECORDING CAN BE SWITCHED OFF, and that is not an optimisation to skip.
/// GameSession.BestOf scores once per candidate word on a chain holding a wild —
/// which can be hundreds — on every frame of a drag, and reads nothing but the
/// final Points. Recording allocates roughly three strings per step and there is
/// now a step per TILE, so leaving it on down that path is thousands of strings
/// a frame.
///
/// Widen THIS when something needs a fact it can't see (tiles left on the board,
/// money, the round number) rather than widening every hook signature.
/// A class, not a struct, so everything down the chain mutates the same object.
/// </summary>
public class ScoringContext
{
    /// <summary>The word as submitted, lowercase.</summary>
    public string Word;

    /// <summary>
    /// The tiles it was spelled from, in selection order.
    ///
    /// ⚠️ `Tiles.Count` is NOT the word's length. A multi-letter tile ("ch") is
    /// one tile and two letters, so a bookmark that talks about length wants
    /// `Word.Length` — Marginalia and Shorthand both read this field until
    /// 2026-09-17 and both said the wrong thing the moment a CH tile was played.
    /// Use this for what the tiles themselves carry (modifiers, base scores);
    /// use Word for anything about the word.
    /// </summary>
    public IReadOnlyList<Tile> Tiles;

    /// <summary>
    /// The shortest word this mode accepts. Here so a bookmark can talk about
    /// word length in the mode's terms — "exactly the minimum" and "past the
    /// minimum" both stop meaning anything the moment minWordLength is tuned, and
    /// a bookmark carrying its own copy of 3 would quietly describe the old rule.
    /// </summary>
    public int MinWordLength;

    /// <summary>
    /// Words already accepted this round, NOT counting this one — so a bookmark
    /// can ask "have they spelled this before?" and get the honest answer.
    /// Read it, never add to it; it's ICollection only because that's the
    /// narrowest interface with an O(1) Contains on a HashSet.
    /// </summary>
    public ICollection<string> WordsThisRound;

    /// <summary>What the word is worth. Read it; change it with AddPoints / MultiplyPoints.</summary>
    public int Points;

    /// <summary>
    /// The multiplier, starting from the word's length. Read it; change it with
    /// AddMult or MultiplyMult — which of the two you pick is the difference
    /// between a bookmark that commutes with its neighbours and one that doesn't.
    /// </summary>
    public float Mult;

    /// <summary>
    /// What happened, in the order it happened. Empty whenever Recording is off,
    /// which is every speculative candidate inside GameSession.BestOf. (The live
    /// readout never gets this far — it is ScoreCalculator.Opening alone and
    /// builds no context at all.)
    /// </summary>
    public readonly List<ScoreStep> Steps = new();

    /// <summary>
    /// Are steps being kept? Set it false on any path that wants the two numbers
    /// and nothing else — see the warning on the class. A plain field so a fresh
    /// context can be built with it in one expression, same as Points and Mult.
    /// </summary>
    public bool Recording = true;

    private ScoreActor kind;
    private object actor;

    /// <summary>True when the word was played earlier this round.</summary>
    public bool IsRepeat =>
        WordsThisRound != null && Word != null && WordsThisRound.Contains(Word);

    /// <summary>
    /// Seats the opening numbers — the word's length, on both sides. Not a step:
    /// it is where the walk STARTS rather than something that happened to it, and
    /// the HUD draws it as its first frame with the length caption under it.
    /// The one legal direct write, and only from ScoreCalculator.
    /// </summary>
    public void Open(int points, float mult)
    {
        Points = ScoreLimits.Clamp((long)points);
        Mult = ScoreLimits.ClampMult(mult);
    }

    /// <summary>
    /// Who the next step belongs to. Set by ScoreCalculator before it hands the
    /// context to anything, which is why no bookmark, librarian or consumable
    /// needed a single line changed to join the walk-through — they call the same
    /// AddMult they always did and the step comes out tagged.
    ///
    /// ⚠️ `actor` is the OBJECT, not an index. See ScoreStep.Actor.
    /// </summary>
    public void Acting(ScoreActor kind, object actor)
    {
        this.kind = kind;
        this.actor = actor;
    }

    /// <summary>
    /// Adds flat points. "DEJA VU  +10 POINTS".
    ///
    /// Saturated rather than wrapped, and floored at 0 — so something that takes
    /// points away can zero a word but never make it worth less than nothing.
    /// See ScoreLimits.
    /// </summary>
    public void AddPoints(int amount, string source)
    {
        if (amount == 0) return;
        Points = ScoreLimits.Clamp((long)Points + amount);

        // Before the strings, not after: Signed and the interpolation below are
        // the allocation this path exists to avoid.
        if (!Recording) return;
        string shown = Signed(amount);
        Record(source, shown, $"{shown} POINTS", ScoreSide.Points);
    }

    /// <summary>
    /// Multiplies the points. What a 2W/3W does, on its own tile's beat.
    ///
    /// It lands on POINTS rather than MULT deliberately: a 2W is part of what the
    /// tiles are worth. It matters — a 3W then "+10 points" is (P*3+10)*M, where
    /// the same 3W on the mult side would be (P+10)*(M*3).
    /// </summary>
    public void MultiplyPoints(float factor, string source)
    {
        if (Mathf.Approximately(factor, 1f)) return;
        Points = ScoreLimits.Clamp((double)Points * factor);

        if (!Recording) return;
        string shown = $"x{Trim(factor)}";
        Record(source, shown, $"{shown} POINTS", ScoreSide.Points);
    }

    /// <summary>
    /// Adds to the multiplier. The additive half of the pair — a +4 before a x3
    /// is worth far more than the same +4 after it, which is what makes slot
    /// order a decision.
    /// </summary>
    public void AddMult(float amount, string source)
    {
        if (Mathf.Approximately(amount, 0f)) return;
        Mult = ScoreLimits.ClampMult(Mult + amount);

        if (!Recording) return;
        string shown = Signed(amount);
        Record(source, shown, $"{shown} MULT", ScoreSide.Mult);
    }

    /// <summary>Multiplies the multiplier. The big, order-sensitive one.</summary>
    public void MultiplyMult(float factor, string source)
    {
        if (Mathf.Approximately(factor, 1f)) return;
        Mult = ScoreLimits.ClampMult(Mult * factor);

        if (!Recording) return;
        string shown = $"x{Trim(factor)}";
        Record(source, shown, $"{shown} MULT", ScoreSide.Mult);
    }

    /// <summary>
    /// A beat that moves neither number — the tile walk's way of giving a tile
    /// its turn anyway.
    ///
    /// ⚠️ IT EXISTS BECAUSE A WILD IS WORTH 0, and so is every choice tile in the
    /// 1-point groups, by design. AddPoints returns early on zero and that guard
    /// has to stay — it is what keeps a bookmark that didn't fire out of the
    /// walk — so without this, exactly the tiles a player is most curious about
    /// would be skipped silently in the middle of the count and read as a bug.
    /// Only ScoreCalculator.WalkTiles calls it; a bookmark never should.
    /// </summary>
    public void Beat(string source)
    {
        if (!Recording) return;
        Record(source, "+0", "+0 POINTS", ScoreSide.Points);
    }

    private void Record(string source, string amount, string detail, ScoreSide side)
    {
        if (!Recording) return;

        Steps.Add(new ScoreStep
        {
            Source = string.IsNullOrEmpty(source) ? "?" : source.ToUpperInvariant(),
            Amount = amount,
            Detail = detail,
            Side = side,
            Points = Points,
            Mult = Mult,
            Kind = kind,
            Actor = actor,
        });
    }

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static string Signed(float value) =>
        value >= 0f ? $"+{Trim(value)}" : $"-{Trim(-value)}";

    /// <summary>2 rather than 2.0, but 1.5 stays 1.5.</summary>
    public static string Trim(float value) =>
        Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.##");

}

/// <summary>
/// One thing that happened to the score, and what the numbers read afterwards.
/// The HUD steps through these, so each entry has to stand alone as a beat:
/// who did it, what they did, where that left the two numbers, and WHICH THING
/// ON SCREEN to shake while it does.
/// </summary>
public struct ScoreStep
{
    public string Source;   // "BOOKEND", "E", "3W"
    public string Detail;   // "x2 MULT" — display only; nothing shows it since the caption went (2026-10-08)
    public string Amount;   // "x2" — what floats up beside the thing that did it
    public ScoreSide Side;  // which number moved; what the HUD highlights
    public int Points;      // after this step
    public float Mult;      // after this step

    /// <summary>What did it, so a widget can decide whether this beat is its business.</summary>
    public ScoreActor Kind;

    /// <summary>
    /// WHICH one — the TileSpec, the BookmarkSpec, the Consumable, the Librarian.
    ///
    /// ⚠️ A REFERENCE, NOT AN INDEX, and an index genuinely does not work here.
    /// RogueDemoMode hands over the bare librarian when nothing is armed, so most
    /// rounds never build a CompositeScoreRule at all; when one is built, its
    /// child 0 is the librarian on a librarian round and the first armed item on
    /// an ordinary one, so every index shifts by one depending on the round; and
    /// a composite index is an index into `armed`, not into RunState.Consumables,
    /// which is what the items box actually lays out. A reference costs nothing
    /// in a struct, survives a bookmark being reordered mid-walk, and lets each
    /// widget look the thing up in the list it already owns.
    /// </summary>
    public object Actor;
}

/// <summary>Which half of the score a step touched.</summary>
public enum ScoreSide
{
    Points,
    Mult,
}

/// <summary>
/// What kind of thing a step came from — so a widget can filter cheaply before
/// doing a reference lookup, and so a beat with nothing on screen behind it
/// (the opening length, the mode's own multiplier) can say so.
/// </summary>
public enum ScoreActor
{
    /// <summary>
    /// Nothing in particular. The opening length is NOT in here on purpose — it
    /// is where the two numbers start rather than something that happened to
    /// them, so it is state (WordResult.Opening) and never a step.
    /// </summary>
    None,

    Tile,
    Bookmark,
    Librarian,
    Consumable,
    Mode,
}
