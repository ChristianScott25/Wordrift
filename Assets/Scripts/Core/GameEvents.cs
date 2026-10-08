using System;
using System.Collections.Generic;

/// <summary>
/// The one channel between gameplay and the UI. GameSession raises these;
/// HUD widgets subscribe in OnEnable and unsubscribe in OnDisable.
///
/// This is what makes new UI cheap: a "moves left" display is just a prefab
/// that listens to StatusChanged — no changes to the session or the modes.
/// </summary>
public static class GameEvents
{
    public static event Action RoundStarted;
    public static event Action<int> ScoreChanged;
    public static event Action<ModeStatus> StatusChanged;
    /// <summary>
    /// The board selection changed — tiles added, removed, cleared, or submitted.
    /// Carries what's selected AND what may be done with it (see SelectionState),
    /// because the word preview and the action buttons need the same snapshot.
    /// </summary>
    public static event Action<SelectionState> SelectionChanged;
    public static event Action<WordResult> WordSubmitted;
    public static event Action<RoundSummary> RoundEnded;

    /// <summary>
    /// One beat of the score walk-through: something took its turn and here is
    /// what it did, where that left the two numbers, and WHICH thing on screen
    /// did it (ScoreStep.Kind / Actor).
    ///
    /// ⚠️ GameSession raises these on its own clock, not the HUD. The session is
    /// what waits and then clears the board, so it has to be the thing that
    /// decides when a beat happens — two timers could not stay in step. See
    /// ScoreTallyTiming.
    ///
    /// A widget listens, checks whether the beat is its business, and shakes the
    /// card or tile it already owns. Several widgets hear every beat; that's the
    /// point.
    /// </summary>
    public static event Action<ScoreStep> ScoreBeat;

    /// <summary>
    /// A played word's tiles have just left the board — released, not
    /// destroyed — and whoever draws the word row may fly them to it. Raised
    /// right after WordSubmitted.
    ///
    /// ⚠️ The list is BORROWED, valid only for the callback; copy it.
    /// ⚠️ A listener must start every flight INSIDE the callback: GameSession
    /// checks Tile.IsSettled on the very next line, and a tile not yet told to
    /// fly reads as landed, which would start the count early. With no
    /// listener at all, nothing flies and the count simply starts — the
    /// session never depends on the row.
    /// </summary>
    public static event Action<IReadOnlyList<Tile>> TilesLaunched;

    /// <summary>
    /// The walk-through is over — put everything back.
    ///
    /// ⚠️ A listener must ALSO release on RoundStarted and RoundEnded. This fires
    /// at the natural end, but a round torn down mid-walk would otherwise leave
    /// every card shaken and every floating number on screen.
    /// </summary>
    public static event Action ScoreWalkEnded;

    public static void RaiseRoundStarted() => RoundStarted?.Invoke();
    public static void RaiseScoreChanged(int score) => ScoreChanged?.Invoke(score);
    public static void RaiseStatusChanged(ModeStatus status) => StatusChanged?.Invoke(status);
    public static void RaiseSelectionChanged(SelectionState selection) => SelectionChanged?.Invoke(selection);
    public static void RaiseWordSubmitted(WordResult result) => WordSubmitted?.Invoke(result);
    public static void RaiseRoundEnded(RoundSummary summary) => RoundEnded?.Invoke(summary);
    public static void RaiseScoreBeat(ScoreStep step) => ScoreBeat?.Invoke(step);
    public static void RaiseTilesLaunched(IReadOnlyList<Tile> tiles) => TilesLaunched?.Invoke(tiles);
    public static void RaiseScoreWalkEnded() => ScoreWalkEnded?.Invoke();
}
