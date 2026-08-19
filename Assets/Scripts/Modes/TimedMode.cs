using UnityEngine;

/// <summary>
/// Countdown mode. Optionally awards bonus time per word, which is the
/// "extend the clock by scoring" idea — all of it lives in OnWordAccepted.
/// </summary>
public class TimedMode : GameMode
{
    private readonly TimedModeConfig config;
    private float timeLeft;
    private float maxTimeSeen;

    public TimedMode(TimedModeConfig config) => this.config = config;

    public override void Begin()
    {
        timeLeft = config.roundSeconds;
        maxTimeSeen = config.roundSeconds;
    }

    public override void Tick(float deltaTime)
    {
        timeLeft = Mathf.Max(0f, timeLeft - deltaTime);
    }

    public override void OnWordAccepted(WordResult result)
    {
        int extraLetters = Mathf.Max(0, result.TileCount - config.minWordLength);
        float bonus = config.secondsPerWord + extraLetters * config.secondsPerExtraLetter;
        if (bonus <= 0f) return;

        timeLeft += bonus;
        // Keep the progress bar sane if the clock grows past its starting value.
        maxTimeSeen = Mathf.Max(maxTimeSeen, timeLeft);
    }

    public override bool IsRoundOver => timeLeft <= 0f;

    public override ModeStatus Status
    {
        get
        {
            int seconds = Mathf.CeilToInt(timeLeft);
            return new ModeStatus
            {
                Label = "TIME",
                Value = $"{seconds / 60}:{seconds % 60:00}",
                Fraction = maxTimeSeen > 0f ? timeLeft / maxTimeSeen : 0f,
                Urgent = timeLeft <= config.urgentSeconds,
            };
        }
    }
}
