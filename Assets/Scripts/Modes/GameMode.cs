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
    /// <summary>
    /// The session running this round. Gives a mode read access to the shared
    /// state the session owns — Score above all — so a mode chasing a target
    /// doesn't have to keep its own copy of it. Also the flow handle: a mode
    /// whose round continues somewhere else calls session.ContinueTo.
    /// </summary>
    protected GameSession session;

    /// <summary>
    /// The board this round is played on. Most modes never touch it — they
    /// only decide rules — but a mode that owns the board's population, or
    /// that installs a tile source it wants to keep reading (see
    /// RogueDemoMode), needs it.
    /// </summary>
    protected Board board;

    /// <summary>
    /// Called once, BEFORE the board is built. Install board policies here
    /// (gravity, refill, tile source): the opening fill happens inside
    /// Board.Build, so changing them in Begin would already be too late.
    /// </summary>
    public virtual void Attach(GameSession session, Board board)
    {
        this.session = session;
        this.board = board;
    }

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

    /// <summary>
    /// Called once when the round has ended, before the game-over panel is
    /// told anything. A mode whose round flows somewhere else — a cleared
    /// roguelike round continues in the shop — claims the ending here via
    /// session.ContinueTo, which skips the panel. Most modes do nothing.
    /// </summary>
    public virtual void End() { }

    /// <summary>What the HUD should display for this mode's resource.</summary>
    public abstract ModeStatus Status { get; }

    /// <summary>
    /// What to call the ending, once IsRoundOver is true. Null keeps the
    /// game-over panel's default wording — right for a mode you can only run
    /// out of. A mode you can pass or fail (see RogueDemoMode) says which here.
    /// </summary>
    public virtual string Outcome => null;
}
