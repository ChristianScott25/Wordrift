using System.Collections;
using UnityEngine;

/// <summary>
/// A short shake: one scale pop with a wobble through it, then back exactly
/// where it started. What the score walk-through does to whatever just scored.
///
/// 🎯 IT IS A COMPONENT, ADDED ON DEMAND, rather than a method on each widget,
/// because four unrelated things need it — world-space tiles in the word row,
/// bookmark cards, consumable slots and the round header — and three of them
/// live on a canvas while one doesn't. A transform is a transform.
///
/// ⚠️ IT ANIMATES SCALE AND ROTATION ONLY, NEVER POSITION. Position is owned:
/// CurrentWordWidget.LayOutTiles, BookmarkRowWidget.LayOut and
/// ConsumablesAreaWidget.LayOut all write absolute values, and the consumable
/// slots are stretched rects where anchoredPosition means nothing at all. Moving
/// something here would be a tug of war with a layout pass that always wins.
///
/// ⚠️ SCALE ISN'T FREE EITHER — CANCEL BEFORE RE-LAYING ANYTHING OUT. Tile.Init
/// writes localScale absolutely (the word row re-dresses its tiles when the band
/// resizes) and GameLayout.Attach sets it back to one. If either happens while a
/// pulse is holding captured values, the restore at the end would undo it. Every
/// performer calls Cancel from its own layout callback for exactly that reason.
///
/// ⚠️ ITS OWN COMPONENT, NOT A METHOD ON Tile. Tile.Demolish calls
/// StopAllCoroutines, which would kill a pulse hosted there mid-way and leave the
/// tile at whatever size it had reached. A separate component's coroutines are
/// its own.
/// </summary>
[DisallowMultipleComponent]
public class Jolt : MonoBehaviour
{
    /// <summary>How long one pulse lasts. Short enough to fit inside the fastest beat.</summary>
    public const float Seconds = 0.22f;

    /// <summary>Peak size, as a fraction over the resting scale.</summary>
    private const float Grow = 0.18f;

    /// <summary>Peak lean, in degrees.</summary>
    private const float Tilt = 6f;

    /// <summary>How many times it swings back and forth over the pulse.</summary>
    private const float Wobbles = 2f;

    private Coroutine running;
    private Vector3 restScale;
    private Quaternion restRotation;
    private bool captured;

    /// <summary>
    /// Shakes something. Safe on null, safe to call again while one is already
    /// running, and safe on a transform that has never been shaken before.
    /// </summary>
    public static void Pulse(Transform target)
    {
        if (target == null) return;

        var jolt = target.GetComponent<Jolt>();
        if (jolt == null) jolt = target.gameObject.AddComponent<Jolt>();
        jolt.Play();
    }

    /// <summary>
    /// Puts a transform back where it started and forgets it. Call this before
    /// anything that writes scale or rotation itself — see the warning above.
    /// </summary>
    public static void Cancel(Transform target)
    {
        if (target == null) return;

        var jolt = target.GetComponent<Jolt>();
        if (jolt != null) jolt.Stop();
    }

    private void Play()
    {
        // Captured once and kept until the pulse ends, so a second Pulse landing
        // mid-pulse restarts from the ORIGINAL resting values rather than from
        // wherever this one had got to. Without that, a tile that scores twice in
        // quick succession would ratchet up a little each time and never come
        // back down.
        if (!captured)
        {
            restScale = transform.localScale;
            restRotation = transform.localRotation;
            captured = true;
        }

        if (running != null) StopCoroutine(running);

        // A disabled object can't run a coroutine, and StartCoroutine on one
        // throws. Nothing to show, so just sit at rest.
        if (!isActiveAndEnabled) { Stop(); return; }

        running = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        for (float t = 0f; t < Seconds; t += Time.deltaTime)
        {
            // One hump: zero at both ends, one in the middle. So the pulse always
            // leaves the transform where it found it even a frame early.
            float hump = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / Seconds));

            transform.localScale = restScale * (1f + Grow * hump);
            transform.localRotation = restRotation * Quaternion.Euler(
                0f, 0f, Tilt * hump * Mathf.Sin(Mathf.PI * 2f * Wobbles * t / Seconds));

            yield return null;
        }

        running = null;
        Stop();
    }

    private void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;

        if (!captured) return;
        transform.localScale = restScale;
        transform.localRotation = restRotation;
        captured = false;
    }

    // Unity kills a coroutine when the object is deactivated and never resumes
    // it, which would strand the transform mid-pulse — the word row hides its
    // surplus tiles exactly this way.
    private void OnDisable() => Stop();
}
