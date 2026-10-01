using System;
using UnityEngine;

/// <summary>
/// "Float this number beside this thing" — the little +3 and x2 that rise off
/// whatever just scored.
///
/// A static channel rather than a reference on each widget, for the same reason
/// Inspector is one: the things that raise it live in four unrelated places (the
/// word row, the bookmark row, the items box, the score readout) and exactly one
/// thing listens.
///
/// 🎯 IT IS RAISED BY THE PERFORMERS, NOT BY WHOEVER DRIVES THE BEAT, because
/// only the performer knows where it is on screen. Same split Inspector.Show
/// makes: the caller supplies the rectangle, the listener does the drawing. The
/// beat itself travels the other way, down GameEvents.ScoreBeat.
///
/// ⚠️ SUBSCRIBE IN OnEnable, UNSUBSCRIBE IN OnDisable, or a listener outlives its
/// scene. Same rule GameLayout.Changed, Inspector and RunState.Changed carry.
/// </summary>
public static class ScorePop
{
    /// <summary>The text, the SCREEN rectangle to float above, and which number it moved.</summary>
    public static event Action<string, Rect, ScoreSide> Requested;

    /// <summary>
    /// Floats a number above a thing. Nothing happens with no listener, which is
    /// the honest behaviour in a scene with no score readout (the shop).
    /// </summary>
    public static void Show(string text, Rect screenRect, ScoreSide side)
    {
        if (string.IsNullOrEmpty(text)) return;
        Requested?.Invoke(text, screenRect, side);
    }
}
