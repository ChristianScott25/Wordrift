using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🚧 Which full-screen panels are open right now — the bag view, the pause view
/// and the letter picker (2026-10-29). Each says so when it opens and closes.
///
/// It exists for ONE reader: Spoiler, which presses PLAY by itself once its
/// hint has gone in, and must not do that behind a panel the player opened in
/// the meantime. Static like Inspector, because the panels and the session
/// don't hold references to each other.
/// </summary>
public static class ModalViews
{
    private static readonly HashSet<Object> open = new();

    /// <summary>True while any of them is on screen.</summary>
    public static bool AnyOpen
    {
        get
        {
            // A destroyed panel (a scene unloaded with it open) compares equal
            // to null — drop it rather than blocking forever.
            open.RemoveWhere(view => view == null);
            return open.Count > 0;
        }
    }

    public static void Set(Object view, bool isOpen)
    {
        if (view == null) return;
        if (isOpen) open.Add(view);
        else open.Remove(view);
    }
}
