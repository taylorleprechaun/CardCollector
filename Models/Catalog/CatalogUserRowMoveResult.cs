namespace CardCollector.Models.Catalog
{
    /// <summary>What happened to the user rows of a printing merged into another.</summary>
    /// <param name="Moved">Rows repointed at the surviving printing.</param>
    /// <param name="Folded">
    /// Rows combined with the surviving printing's own row because a printing allows only one (wishlist entries keep the
    /// larger quantity, checked-out rows add up, dismissals collapse).
    /// </param>
    public sealed record CatalogUserRowMoveResult(int Moved, int Folded);
}
