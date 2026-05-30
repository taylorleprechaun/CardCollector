namespace CardCollector.Repository
{
    /// <summary>
    /// Provides TCG release dates for card sets, sourced from the YGOProDeck card sets API.
    /// </summary>
    public interface ICardSetRepository
    {
        /// <summary>
        /// Returns the TCG release date ("YYYY-MM-DD") for the set identified by the given card-level set code,
        /// or null if the set has no TCG date or the code is unrecognized.
        /// </summary>
        /// <param name="fullSetCode">Card-level set code, e.g. "BLZD-EN049".</param>
        string? GetTCGDateBySetCode(string fullSetCode);
    }
}
