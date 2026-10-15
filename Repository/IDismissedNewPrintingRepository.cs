namespace CardCollector.Repository
{
    /// <summary>
    /// Stores card set+rarity combinations the user has chosen to ignore as upgrade opportunities.
    /// </summary>
    public interface IDismissedNewPrintingRepository
    {
        /// <summary>
        /// Records the card's catalog printing as dismissed so it no longer appears as an upgrade opportunity. Does
        /// nothing when it is already dismissed.
        /// </summary>
        Task AddAsync(int printingID, int cardID);

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
