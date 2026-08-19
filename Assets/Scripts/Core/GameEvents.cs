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
    public static event Action<string, bool> ChainChanged;   // live word, is it valid
    public static event Action<WordResult> WordSubmitted;
    public static event Action<RoundSummary> RoundEnded;

    public static void RaiseRoundStarted() => RoundStarted?.Invoke();
    public static void RaiseScoreChanged(int score) => ScoreChanged?.Invoke(score);
    public static void RaiseStatusChanged(ModeStatus status) => StatusChanged?.Invoke(status);
    public static void RaiseChainChanged(string word, bool valid) => ChainChanged?.Invoke(word, valid);
    public static void RaiseWordSubmitted(WordResult result) => WordSubmitted?.Invoke(result);
    public static void RaiseRoundEnded(RoundSummary summary) => RoundEnded?.Invoke(summary);
}
