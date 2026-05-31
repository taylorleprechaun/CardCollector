using CardCollector.DTO;

namespace CardCollector.Repository
{
    /// <summary>
    /// Provides read-only access to card data parsed from the JSON source file.
    /// </summary>
    public interface ICardDataRepository
    {
        /// <summary>
        /// All distinct attribute values (e.g. DARK, LIGHT) present in the card data.
        /// </summary>
        IReadOnlyList<string> DistinctAttributes { get; }

        /// <summary>
        /// All distinct rarity name strings present in the card data.
        /// </summary>
        IReadOnlyList<string> DistinctRarityNames { get; }

        /// <summary>
        /// Returns every (card, image) artwork pair across all cards including non-browseable ones.
        /// </summary>
        IEnumerable<(Card Card, Image Image)> GetAllArtworks();

        /// <summary>
        /// Returns all cards including non-browseable ones (e.g. cards whose only sets were Speed Duel).
        /// </summary>
        IEnumerable<Card> GetAllCards();

        /// <summary>
        /// Returns (card, image) pairs for cards eligible to appear in Browse and collection views (Speed Duel-only cards excluded).
        /// </summary>
        IEnumerable<(Card Card, Image Image)> GetBrowseableArtworks();

        /// <summary>
        /// Returns cards eligible to appear in Browse and collection views (Speed Duel-only cards excluded).
        /// </summary>
        IEnumerable<Card> GetBrowseableCards();

        /// <summary>
        /// Returns the card with the given ID, or null if not found.
        /// </summary>
        Card? GetCardByID(int cardID);

        /// <summary>
        /// Returns a dictionary mapping set code (e.g. "LOB") to set name (e.g. "Legend of Blue Eyes White Dragon").
        /// </summary>
        IReadOnlyDictionary<string, string> GetSetNamesByCode();
    }
}
