/// <summary>
/// One bookmark as the RUN owns it — the same split as TileSpec against
/// TileModifier: the asset is the recipe, this is the copy you actually have.
///
/// It looks like a pointless wrapper today because it holds one field. It isn't:
/// editions (holographic, negative, foil) belong HERE, not on the asset, because
/// two runs — or later two copies in one run — must be able to hold the same
/// bookmark at different strengths. Apply is where an edition will get to touch
/// the context after the bookmark itself has.
/// </summary>
public class BookmarkSpec
{
    public Bookmark bookmark;

    public BookmarkSpec(Bookmark bookmark) => this.bookmark = bookmark;

    /// <summary>What this owned bookmark does to a word being scored.</summary>
    public void Apply(ScoringContext ctx)
    {
        if (bookmark == null || ctx == null) return;
        bookmark.OnWordScored(ctx);
        // Editions hook in here, after the bookmark's own effect.
    }

    public string Name => bookmark == null ? "" : bookmark.displayName;
}
