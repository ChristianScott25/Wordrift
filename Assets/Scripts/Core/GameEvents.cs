using System;

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

    public static void RaiseRoundStarted() => RoundStarted?.Invoke();
    public static void RaiseScoreChanged(int score) => ScoreChanged?.Invoke(score);
    public static void RaiseStatusChanged(ModeStatus status) => StatusChanged?.Invoke(status);
    public static void RaiseSelectionChanged(SelectionState selection) => SelectionChanged?.Invoke(selection);
    public static void RaiseWordSubmitted(WordResult result) => WordSubmitted?.Invoke(result);
    public static void RaiseRoundEnded(RoundSummary summary) => RoundEnded?.Invoke(summary);
}
