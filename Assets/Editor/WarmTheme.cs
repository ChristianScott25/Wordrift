using UnityEngine;

/// <summary>
/// 🚧 The warm placeholder theme (2026-10-19/20), in ONE place so the Game, Shop
/// and Main Menu scenes can't drift apart. Every setup script that paints a
/// camera background or a panel reads these instead of keeping its own copy —
/// three copies of a cool navy is how the Shop stayed blue after the game
/// screen went warm. Stand-ins until there is background art.
/// </summary>
internal static class WarmTheme
{
    /// <summary>What every scene's camera clears to.</summary>
    internal static readonly Color Background = new Color(0.50f, 0.37f, 0.28f);

    /// <summary>
    /// A panel that has to hide what's behind it (the Shop's description panel).
    /// Darker than the background, so it reads as something in front.
    /// </summary>
    internal static readonly Color Panel = new Color(0.22f, 0.15f, 0.11f, 0.98f);
}
