namespace CardCollector.Repository
{
    /// <summary>
    /// Stores card set+rarity combinations the user has chosen to ignore as upgrade opportunities.
    /// </summary>
    public interface IDismissedNewPrintingRepository
    {
        /// <summary>
        /// Records the catalog printings as dismissed so they no longer appear as upgrade opportunities, in one save.
        /// Printings already dismissed are skipped.
        /// </summary>
        Task AddAsync(IReadOnlyCollection<int> printingIDs);

        /// <summary>
        /// Returns true if any dismissed records exist.
        /// </summary>
        Task<bool> AnyAsync();

        /// <summary>
        /// Returns the catalog printing IDs of every dismissed printing.
        /// </summary>
        Task<IReadOnlySet<int>> GetAllAsync();
    }
}
