/// <summary>
/// The rules that differ between game modes. GameSession runs the universal
/// loop (drag, validate, score, refill) and calls into here for everything
/// mode-specific: what resource you're spending, and when the round ends.
///
/// To add a mode: subclass this + subclass ModeConfig, then create the asset.
/// Nothing else in the project needs to change.
/// </summary>
public abstract class GameMode
{
    /// <summary>Called once when the round starts. Reset your resource here.</summary>
    public abstract void Begin();

    /// <summary>Called every frame while the round is running.</summary>
    public virtual void Tick(float deltaTime) { }

    /// <summary>A valid word was scored. Award bonus time / decrement moves here.</summary>
    public virtual void OnWordAccepted(WordResult result) { }

    /// <summary>An invalid chain was submitted.</summary>
    public virtual void OnWordRejected(WordResult result) { }

    /// <summary>The session ends the round as soon as this turns true.</summary>
    public abstract bool IsRoundOver { get; }

    /// <summary>What the HUD should display for this mode's resource.</summary>
    public abstract ModeStatus Status { get; }
}
