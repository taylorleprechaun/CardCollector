namespace CardCollector.Repository
{
    /// <summary>
    /// Stores cards the user has chosen to exclude from Dashboard progress tracking.
    /// </summary>
    public interface IIgnoredCardRepository
    {
        /// <summary>
        /// Records the given card as ignored. No-ops if already ignored.
        /// </summary>
        Task AddAsync(int cardID);

        /// <summary>
        /// Returns all ignored cards as a dictionary of card ID to the date they were ignored.
        /// </summary>
        Task<IReadOnlyDictionary<int, DateTime>> GetAllAsync();

        /// <summary>
        /// Returns the set of all ignored card IDs.
        /// </summary>
        Task<IReadOnlySet<int>> GetIgnoredCardIDsAsync();

        /// <summary>
        /// Returns true if the given card is currently ignored.
        /// </summary>
        Task<bool> IsIgnoredAsync(int cardID);

        /// <summary>
        /// Clears the ignored status for the given card. No-ops if not currently ignored.
        /// </summary>
        Task RemoveAsync(int cardID);
    }
}
