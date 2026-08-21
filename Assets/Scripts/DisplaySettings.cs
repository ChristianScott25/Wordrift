using UnityEngine;

/// <summary>
/// App-wide display settings, applied once before any scene loads.
///
/// Unity's default target frame rate on mobile is lower than the screen can
/// manage — which shows up as jitter here, because in Overflow mode something
/// is almost always in motion. Rather than rely on whatever the platform picks,
/// ask for a rate explicitly.
///
/// This is a RuntimeInitializeOnLoadMethod rather than a component because
/// Main Menu and Game share no object it could hang off, and it needs to apply
/// to both. Nothing to wire in the Inspector.
/// </summary>
public static class DisplaySettings
{
    /// <summary>
    /// Frames per second to ask the platform for.
    ///
    /// 60 rather than 120 deliberately: ProMotion iPhones would run higher, but
    /// the battery cost is real and a word game gains nothing from it. Raise it
    /// here if that changes.
    /// </summary>
    public const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // targetFrameRate is ignored while vSync is on, and the quality level
        // iOS defaults to (Medium) has vSync enabled. Clearing it hands frame
        // pacing to targetFrameRate on every platform instead of only the ones
        // where vSync happens to be off.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
