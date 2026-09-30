/// <summary>
/// Something a player can read about — a tile, a bookmark, an item, a librarian.
///
/// ONE METHOD, and it FILLS a bundle it is handed rather than returning a new
/// one. That is what keeps the bundle and its list off the per-tap allocation
/// bill, and it is why the answer can't be cached anywhere: whatever implements
/// this describes itself as it is RIGHT NOW, badges and all.
///
/// It lives in Core because the things that describe themselves live in Core
/// (TileSpec, Bookmark) and Core may not reference Scripts/UI. The BOX is UI and
/// only ever reads what it is given.
/// </summary>
public interface IInspectable
{
    /// <summary>
    /// Fill in the name, the description and any qualities. The info arrives
    /// already cleared, so there is nothing to reset.
    /// </summary>
    void Describe(InspectInfo info);
}
