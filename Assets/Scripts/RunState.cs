using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything one run remembers between rounds: which round you're on, the
/// tiles you own, and the money you've banked. Bookmarks and sack abilities
/// will live here too.
///
/// Plain C# held in a static, like ModeSelection, because scene loads wipe
/// object references. Mutable ON PURPOSE — shops and bookmarks edit this run.
/// The authored assets a run starts from (the mode config, the letter set) are
/// read-only recipes; nothing may ever write into them at runtime.
/// </summary>
public class RunState
{
    /// <summary>The run in progress, or null when there isn't one.</summary>
    public static RunState Current { get; private set; }

    /// <summary>The config this run was started from. Read it, never write it.</summary>
    public RogueDemoModeConfig Template { get; }

    /// <summary>1-based: round 1 is the first. Advanced by the shop's Continue.</summary>
    public int Round { get; private set; } = 1;

    /// <summary>
    /// The run's tiles. The full sack comes back at the start of every round —
    /// playing tiles never shrinks it (TileSack drains a copy of this list).
    /// Changing THIS list is how shops and bookmarks alter what the player
    /// draws, and the change sticks for the rest of the run.
    /// </summary>
    public List<TileSpec> Sack { get; } = new();

    /// <summary>
    /// What the run has to spend. Earned by clearing rounds, spent in the shop,
    /// and gone the moment the run is — money never outlives a run, so there is
    /// nothing to save to disk and no meta-currency to reason about yet.
    /// </summary>
    public int Money { get; private set; }

    /// <summary>What the last cleared round paid, so the shop can say "+$20 EARNED".</summary>
    public int LastPayout { get; private set; }

    public int TargetScore => Template.TargetForRound(Round);

    /// <summary>Pays the run. The only way money comes IN — see RogueDemoModeConfig.RewardFor.</summary>
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        Money += amount;
        LastPayout = amount;
    }

    public bool CanAfford(int price) => Money >= price;

    /// <summary>
    /// Takes money for a purchase, or refuses and changes nothing. The only way
    /// money goes OUT: there's no setter, so the balance can't go negative and
    /// every spend is a call site you can find.
    /// </summary>
    public bool TrySpend(int price)
    {
        if (price < 0 || !CanAfford(price)) return false;
        Money -= price;
        return true;
    }

    public static RunState StartNew(RogueDemoModeConfig template)
    {
        Current = new RunState(template);
        return Current;
    }

    /// <summary>The run is over — lost, or abandoned from the main menu.</summary>
    public static void End() => Current = null;

    public void AdvanceRound() => Round++;

    private RunState(RogueDemoModeConfig template)
    {
        Template = template;
        if (template == null || template.letterSet == null)
        {
            Debug.LogError("Run started with no letter set, so the sack is empty.");
            return;
        }

        // The LetterSet's spawn weights read as tile counts — LetterSet_Scrabble
        // sums to Scrabble's 98 — so one asset drives both the arcade modes'
        // spawn odds and this sack's starting contents.
        foreach (var entry in template.letterSet.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.letter)) continue;
            int count = Mathf.Max(0, entry.weight) * Mathf.Max(1, template.sackCopies);
            for (int i = 0; i < count; i++)
                Sack.Add(LetterSet.CreateSpec(entry));
        }

        if (Sack.Count == 0)
            Debug.LogError($"LetterSet '{template.letterSet.name}' has no positive weights, so the sack is empty.");
    }
}
