namespace CardCollector.ViewModels
{
    /// <summary>
    /// Presentation-layer shape for one day's aggregate value snapshot (collection market value or
    /// wishlist cost-to-complete), decoupled from the EF Core snapshot entities.
    /// </summary>
    public sealed record ValueSnapshotPoint(string SnapshotDate, decimal TotalValue, int Count);
}
