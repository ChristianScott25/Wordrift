/// <summary>
/// Carries the chosen mode across the scene load from the menu into the game.
/// Scene loads wipe object references, so this hands the config over instead.
/// </summary>
public static class ModeSelection
{
    private static ModeConfig pending;

    public static void Select(ModeConfig config) => pending = config;

    /// <summary>
    /// Returns the queued mode (or null) and clears it, so replaying the game
    /// scene directly falls back to whatever the scene has assigned.
    /// </summary>
    public static ModeConfig Take()
    {
        var config = pending;
        pending = null;
        return config;
    }
}
