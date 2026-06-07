namespace CardCollector.Repository
{
    /// <summary>
    /// Stores card set+rarity combinations the user has chosen to ignore as upgrade opportunities.
    /// </summary>
    public interface IDismissedNewPrintingRepository
    {
        /// <summary>
        /// Records the given card set+rarity combination as dismissed. No-ops if already dismissed.
        /// </summary>
        Task AddAsync(int cardID, string setCode, string rarityName);

        /// <summary>
        /// Returns true if any dismissed records exist.
        /// </summary>
        Task<bool> AnyAsync();

        /// <summary>
        /// Returns all dismissed combinations as a set for fast lookup.
        /// </summary>
        Task<IReadOnlySet<(int CardID, string SetCode, string RarityName)>> GetAllAsync();
    }
}
