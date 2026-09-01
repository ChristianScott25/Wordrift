using System;

/// <summary>
/// The game's random number generator. Deterministic, ours, and deliberately
/// not either of the built-in options:
///
/// UnityEngine.Random is a GLOBAL. Seeding it means every system shares one
/// sequence, so an animation or a particle effect calling Random.value would
/// silently shift the tiles a player draws. Reproducibility that unrelated code
/// can break isn't reproducibility.
///
/// System.Random's algorithm is NOT guaranteed stable across .NET runtimes — it
/// changed between Framework and Core. A seed could mean one thing in the editor
/// and another on device, or change under a Unity upgrade. For a feature whose
/// entire point is that a seed means the same thing forever, that disqualifies it.
///
/// So: SplitMix64, written out here. It's small, well studied, and it can only
/// ever change if we change it — at which point every previously recorded seed
/// stops reproducing, so don't.
/// </summary>
public sealed class Rng
{
    // Characters a person can read off a screen and type back without ambiguity:
    // no 0/O, no 1/I/L. 31 of them — an odd number is fine because codes are
    // built by drawing characters, not by slicing bits.
    private const string SeedAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int SeedCodeLength = 8;

    private ulong state;

    public Rng(ulong seed) => state = seed;

    /// <summary>
    /// A stream derived from a run's seed. The stream NAME and the round number
    /// are both mixed in, which is what makes streams independent: adding a roll
    /// to the shop can't shift the tiles drawn in the bag, and round 3 deals the
    /// same bag no matter what happened in rounds 1 and 2.
    /// </summary>
    public static Rng Stream(string seedCode, string streamName, int round) =>
        new Rng(Hash($"{seedCode}:{streamName}:{round}"));

    /// <summary>
    /// One that isn't reproducible, for anything outside a run. Used when the
    /// Game scene is played on its own with no RunState to take a seed from.
    /// </summary>
    public static Rng Unseeded() =>
        new Rng((ulong)DateTime.UtcNow.Ticks ^ ((ulong)Guid.NewGuid().GetHashCode() << 32));

    /// <summary>A fresh code for a new run. What the player sees and will one day type in.</summary>
    public static string NewSeedCode()
    {
        var rng = Unseeded();
        var chars = new char[SeedCodeLength];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = SeedAlphabet[rng.Range(0, SeedAlphabet.Length)];
        return new string(chars);
    }

    /// <summary>
    /// FNV-1a. Written out rather than using string.GetHashCode, which is
    /// RANDOMISED PER PROCESS on modern .NET — the same seed code would hash
    /// differently every launch, which would quietly defeat the whole feature.
    /// </summary>
    private static ulong Hash(string text)
    {
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private ulong Next()
    {
        // SplitMix64.
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>
    /// A number in [minInclusive, maxExclusive), matching Random.Range's shape so
    /// call sites read the same. Returns min when the range is empty rather than
    /// throwing — an empty list is a normal state here, not a bug.
    /// </summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;

        // Span in long: max - min overflows int for a wide enough range, and a
        // general-purpose helper shouldn't have a domain the caller can't see.
        ulong span = (ulong)((long)maxExclusive - minInclusive);

        // Modulo, whose bias over a 64-bit draw is around 1 in 2^58 for the
        // range sizes this game uses. Rejection sampling would be more correct
        // and less readable, and nothing here is cryptographic.
        return minInclusive + (int)(Next() % span);
    }
}
